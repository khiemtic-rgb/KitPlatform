namespace KitPlatform.Packs.Content.Production;

public static class MotionPollStatuses
{
    public const string Processing = "PROCESSING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

public sealed record MotionStartResult(string ProviderJobId);

public sealed record MotionStartContext(
    byte[]? KeyframeBytes = null,
    string? KeyframeMime = null,
    Guid AttemptId = default,
    ProductionScope? Scope = null,
    string? ShotCode = null);

public sealed record MotionPollResult(string Status, string ProviderJobId, string? FailureReason = null);

/// <summary>
/// HOW only. Cannot choose story, bind CURRENT, or remap CREATE_VIDEO.
/// Start and Poll are separate. OS persists providerJobId before Poll.
/// </summary>
public interface IMotionExecutionAdapter
{
    string ProviderId { get; }
    string ModelId { get; }
    bool IsPaid { get; }
    bool RequiresNetwork { get; }
    Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default);
    Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, MotionStartContext? context, CancellationToken ct)
        => StartFrozenAsync(snapshot, ct);
    Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default);
    Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default);
    Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default);
    Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default);
}

public sealed record MotionAdapterResult(
    string ProviderJobId,
    byte[] Bytes,
    string MimeType,
    decimal DurationSec,
    string StorageKey);

public static class MotionExecutionLifecycle
{
    public static async Task<MotionAdapterResult> ExecuteViaLifecycleAsync(
        IMotionExecutionAdapter adapter,
        ProductionTaskInputSnapshot snapshot,
        CancellationToken ct = default)
    {
        var start = await adapter.StartFrozenAsync(snapshot, ct);
        var poll = await adapter.PollFrozenAsync(snapshot, start.ProviderJobId, ct);
        if (!string.Equals(poll.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal))
            poll = await adapter.RecoverFrozenAsync(snapshot, start.ProviderJobId, ct);
        if (!string.Equals(poll.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal))
            throw new InvalidOperationException(poll.FailureReason ?? ProductionFailureReasons.ProviderUnavailable);
        return await adapter.FetchOutputAsync(snapshot, start.ProviderJobId, ct);
    }
}

/// <summary>Deterministic fixture. Not a live Veo/Runway call.</summary>
public sealed class FixtureMotionAdapter : IMotionExecutionAdapter
{
    public const string JobPrefix = "fixture-motion-";
    public string ProviderId => ProductionMotionCatalog.FixtureProviderId;
    public string ModelId => ProductionMotionCatalog.FixtureModelId;
    public bool IsPaid => false;
    public bool RequiresNetwork => false;

    public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => Task.FromResult(new MotionStartResult(JobPrefix + snapshot.TaskId.ToString("N")[..12]));

    public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        => Task.FromResult(new MotionPollResult(MotionPollStatuses.Succeeded, providerJobId));

    public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        => PollFrozenAsync(snapshot, providerJobId, ct);

    public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
    {
        var seed = snapshot.SnapshotHash + snapshot.TaskId.ToString("N");
        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 5;
        var bytes = ProductionTestMp4.Encode(seed, duration);
        return Task.FromResult(new MotionAdapterResult(
            providerJobId,
            bytes,
            ProductionTestMp4.Mime,
            duration,
            string.Empty));
    }

    public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => MotionExecutionLifecycle.ExecuteViaLifecycleAsync(this, snapshot, ct);
}
