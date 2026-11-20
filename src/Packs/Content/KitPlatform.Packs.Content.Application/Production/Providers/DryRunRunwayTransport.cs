namespace KitPlatform.Packs.Content.Production;

/// <summary>Never performs HTTP. Deterministic Runway lifecycle for Production OS dry-run.</summary>
public sealed class DryRunRunwayTransport : IRunwayTransport
{
    public const string JobPrefix = "runway-dryrun-";

    public bool RequiresNetwork => false;
    public int HttpCallCount => 0;
    public int CreateCalls { get; private set; }
    public int GetTaskCalls { get; private set; }
    public int DownloadCalls { get; private set; }
    public string? LastCreatedJobId { get; private set; }
    public string? LastGetTaskJobId { get; private set; }
    public Action<string>? OnBeforeGetTask { get; set; }
    public bool BlockGetTask { get; set; }
    public byte[]? ForcedDownloadBytes { get; set; }

    public Task<RunwayTransportCreateResult> CreateImageToVideoAsync(RunwayTransportCreateRequest request, CancellationToken ct = default)
    {
        CreateCalls++;
        var jobId = JobPrefix + request.TaskId.ToString("N");
        LastCreatedJobId = jobId;
        return Task.FromResult(new RunwayTransportCreateResult(jobId));
    }

    public Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default)
    {
        GetTaskCalls++;
        LastGetTaskJobId = providerJobId;
        OnBeforeGetTask?.Invoke(providerJobId);
        if (BlockGetTask)
            throw new InvalidOperationException("DRY_RUN_POLL_BLOCKED");
        if (string.IsNullOrWhiteSpace(providerJobId)
            || !providerJobId.StartsWith(JobPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(new RunwayTransportTaskResult(
                providerJobId, MotionPollStatuses.Failed, ProductionFailureReasons.UnknownProvider));
        }

        return Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Succeeded));
    }

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default)
    {
        DownloadCalls++;
        if (ForcedDownloadBytes is not null)
            return Task.FromResult(ForcedDownloadBytes);
        return Task.FromResult(ProductionTestMp4.Encode(providerJobId, 5));
    }
}
