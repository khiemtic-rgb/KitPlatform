namespace KitPlatform.Packs.Content.Production;

public enum FakeRunwayScenario
{
    Success,
    Failed,
    Recover,
    DuplicateStart,
    InvalidMp4,
    WrongDuration,
    Late,
    DownloadFail,
    UnknownCost,
    OverBudget,
}

/// <summary>
/// Deterministic Runway-shaped transport. Never opens a socket, never uses credentials.
/// Scenarios A–J are selected explicitly by tests.
/// </summary>
public class FakeRunwayTransport : IRunwayTransport
{
    public const string JobPrefix = "runway-fake-";

    readonly Dictionary<string, int> _polls = new(StringComparer.Ordinal);
    readonly HashSet<Guid> _submittedTasks = [];

    public virtual bool RequiresNetwork => false;
    public int HttpCallCount => 0;
    public int CreateCalls { get; private set; }
    public int GetTaskCalls { get; private set; }
    public int DownloadCalls { get; private set; }
    public string? LastCreatedJobId { get; private set; }
    public string? LastGetTaskJobId { get; private set; }
    public RunwayTransportCreateRequest? LastCreateRequest { get; private set; }
    public Action<string>? OnBeforeGetTask { get; set; }
    public bool BlockGetTask { get; set; }
    public byte[]? ForcedDownloadBytes { get; set; }
    public FakeRunwayScenario Scenario { get; set; } = FakeRunwayScenario.Success;

    public Task<RunwayTransportCreateResult> CreateImageToVideoAsync(RunwayTransportCreateRequest request, CancellationToken ct = default)
    {
        LastCreateRequest = request;
        if (Scenario == FakeRunwayScenario.DuplicateStart && _submittedTasks.Contains(request.TaskId))
            return Task.FromResult(new RunwayTransportCreateResult(JobPrefix + request.TaskId.ToString("N")));

        CreateCalls++;
        _submittedTasks.Add(request.TaskId);
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
            throw new InvalidOperationException("FAKE_RUNWAY_POLL_BLOCKED");
        if (string.IsNullOrWhiteSpace(providerJobId)
            || !providerJobId.StartsWith(JobPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(new RunwayTransportTaskResult(
                providerJobId, MotionPollStatuses.Failed, ProductionFailureReasons.UnknownProvider));
        }

        if (Scenario == FakeRunwayScenario.Failed)
            return Task.FromResult(new RunwayTransportTaskResult(
                providerJobId, MotionPollStatuses.Failed, ProductionFailureReasons.ProviderUnavailable));

        _polls.TryGetValue(providerJobId, out var n);
        _polls[providerJobId] = n + 1;
        if (Scenario == FakeRunwayScenario.Recover && n == 0)
        {
            return Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Processing));
        }

        return Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Succeeded));
    }

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default)
    {
        DownloadCalls++;
        if (Scenario == FakeRunwayScenario.DownloadFail)
            throw new InvalidOperationException(ProductionFailureReasons.OutputDownloadFailed);
        if (ForcedDownloadBytes is not null)
            return Task.FromResult(ForcedDownloadBytes);
        if (Scenario == FakeRunwayScenario.InvalidMp4)
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes("FAMIXA-NOT-AN-MP4-" + new string('Z', 48)));
        if (Scenario == FakeRunwayScenario.WrongDuration)
            return Task.FromResult(ProductionTestMp4.EncodeUnreadableDuration(providerJobId));
        return Task.FromResult(ProductionTestMp4.Encode(providerJobId, 5));
    }
}
