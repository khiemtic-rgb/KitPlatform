using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Production OS Veo adapter. Vertex HTTP lives behind IVeoMotionTransport.
/// Checkpoint 7 registers FakeVeoMotionTransport only — no Vertex/Veo HTTP.
/// </summary>
public sealed class VeoMotionExecutionAdapter : IMotionExecutionAdapter
{
    readonly IVeoMotionTransport _transport;

    public VeoMotionExecutionAdapter(IVeoMotionTransport transport) => _transport = transport;

    public string ProviderId => ProductionMotionCatalog.GoogleProviderId;
    public string ModelId => ProductionMotionCatalog.Veo31ModelId;
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

        var bytes = context?.KeyframeBytes;
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        if (snapshot.KeyframeArtifactId is null || snapshot.KeyframeArtifactId == Guid.Empty)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);

        var liveHash = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (CreateVideoActionContractGate.IsPresent(snapshot.KeyframeHash)
            && !string.Equals(snapshot.KeyframeHash, liveHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);

        var mime = (context?.KeyframeMime ?? "").Trim();
        var dataUri = VeoMotionImageDataUri.FromBytes(bytes, mime);
        var created = await _transport.CreateImageToVideoAsync(new VeoMotionTransportCreateRequest(
            snapshot.TaskId,
            context?.AttemptId ?? Guid.Empty,
            ProviderId,
            ModelId,
            VeoMotionPrompt.FromFrozen(snapshot),
            dataUri,
            mime.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : mime.Trim().ToLowerInvariant(),
            VeoMotionDurationMapping.ToProviderDurationSec(snapshot.DurationSec),
            snapshot.DurationSec,
            VeoMotionImageDataUri.ToProviderAspect(snapshot.AspectRatio),
            SampleCount: 1,
            Resolution: "720p",
            GenerateAudio: false,
            snapshot.SnapshotHash,
            snapshot.KeyframeHash,
            snapshot.KeyframeArtifactId,
            ProductionFrozenCredential.FromSnapshot(snapshot)), ct);
        if (string.IsNullOrWhiteSpace(created.ProviderJobId))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        return new MotionStartResult(created.ProviderJobId);
    }

    public async Task<MotionPollResult> PollFrozenAsync(
        ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerJobId))
            throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);
        var op = await _transport.GetOperationAsync(
            providerJobId, ProductionFrozenCredential.FromSnapshot(snapshot), ct);
        return new MotionPollResult(MapPoll(op.Status), op.ProviderJobId, op.FailureReason);
    }

    public Task<MotionPollResult> RecoverFrozenAsync(
        ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        => PollFrozenAsync(snapshot, providerJobId, ct);

    public async Task<MotionAdapterResult> FetchOutputAsync(
        ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerJobId))
            throw new InvalidOperationException(ProductionFailureReasons.UnknownProvider);
        byte[] bytes;
        try
        {
            bytes = await _transport.DownloadOutputAsync(providerJobId, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.OutputDownloadFailed)
        {
            throw;
        }
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 0;
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

    static string MapPoll(string? status)
    {
        var raw = (status ?? "").Trim().ToUpperInvariant();
        if (raw is "RUNNING" or "PROCESSING") return MotionPollStatuses.Processing;
        if (raw == "SUCCEEDED") return MotionPollStatuses.Succeeded;
        if (raw == "FAILED") return MotionPollStatuses.Failed;
        return MotionPollStatuses.Failed;
    }
}
