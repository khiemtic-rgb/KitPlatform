namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Production OS Runway adapter. Vendor HTTP lives behind IRunwayTransport.
/// The composition root registers HttpRunwayTransport. A start still requires a consumed permit and a frozen credential.
/// </summary>
public sealed class RunwayMotionExecutionAdapter : IMotionExecutionAdapter
{
    readonly IRunwayTransport _transport;

    public RunwayMotionExecutionAdapter(IRunwayTransport transport) => _transport = transport;

    public string ProviderId => ProductionMotionCatalog.RunwayProviderId;
    public string ModelId => ProductionMotionCatalog.RunwayGen4Turbo;
    public bool IsPaid => true;
    public bool RequiresNetwork => _transport.RequiresNetwork;

    public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => StartFrozenAsync(snapshot, null, ct);

    public async Task<MotionStartResult> StartFrozenAsync(
        ProductionTaskInputSnapshot snapshot, MotionStartContext? context, CancellationToken ct)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);

        var decision = RunwayPromptBudget.Decide(
            snapshot.Story, snapshot.ActingBeat, snapshot.VisualContract, snapshot.ShotContract);
        if (!decision.Fits)
            throw new InvalidOperationException(decision.Reason);
        var prompt = decision.PromptText;
        var created = await _transport.CreateImageToVideoAsync(new RunwayTransportCreateRequest(
            snapshot.TaskId,
            context?.AttemptId ?? Guid.Empty,
            ModelId,
            MapDuration(snapshot.DurationSec),
            string.IsNullOrWhiteSpace(snapshot.AspectRatio) ? "16:9" : snapshot.AspectRatio,
            snapshot.SnapshotHash,
            snapshot.KeyframeHash,
            context?.KeyframeBytes,
            context?.KeyframeMime,
            prompt,
            context?.Scope,
            context?.ShotCode ?? snapshot.TaskId.ToString("N"),
            ProductionFrozenCredential.FromSnapshot(snapshot)), ct);
        if (string.IsNullOrWhiteSpace(created.ProviderJobId))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        if (created.ProviderJobId.StartsWith(DryRunRunwayTransport.JobPrefix, StringComparison.Ordinal)
            && _transport.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return new MotionStartResult(created.ProviderJobId);
    }

    public async Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerJobId))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        var reference = ProductionFrozenCredential.FromSnapshot(snapshot);
        if (reference is null && _transport.RequiresNetwork)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var task = reference is null
            ? await _transport.GetTaskAsync(providerJobId, ct)
            : await _transport.GetTaskAsync(providerJobId, reference, snapshot.ModelId, ct);
        return new MotionPollResult(task.Status, task.ProviderJobId, task.FailureReason);
    }

    public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        => PollFrozenAsync(snapshot, providerJobId, ct);

    public async Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
    {
        var reference = ProductionFrozenCredential.FromSnapshot(snapshot);
        if (reference is null && _transport.RequiresNetwork)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        byte[] bytes;
        try
        {
            bytes = reference is null || !_transport.RequiresNetwork
                ? await _transport.DownloadOutputAsync(providerJobId, ct)
                : await _transport.DownloadOutputAsync(providerJobId, reference, snapshot.ModelId, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.OutputDownloadFailed)
        {
            throw;
        }
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
        if (ProductionTestMp4.TryReadDuration(bytes, out var measured) && measured > 0)
            duration = measured;
        return new MotionAdapterResult(
            providerJobId,
            bytes,
            ProductionTestMp4.Mime,
            duration,
            string.Empty);
    }

    public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => MotionExecutionLifecycle.ExecuteViaLifecycleAsync(this, snapshot, ct);

    static decimal MapDuration(decimal durationSec) => durationSec >= 8 ? 10 : 5;
}
