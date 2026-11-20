using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public enum FakeVeoScenario
{
    Success,
    Failed,
    InvalidOutput,
}

/// <summary>Deterministic in-memory Veo transport. Never opens a socket. Never uses credentials.</summary>
public sealed class FakeVeoMotionTransport : IVeoMotionTransport
{
    public const string JobPrefix = "veo-fake-";
    public const string WireRunning = "RUNNING";
    public const string WireSucceeded = "SUCCEEDED";
    public const string WireFailed = "FAILED";

    readonly Dictionary<string, int> _polls = new(StringComparer.Ordinal);
    readonly bool _requiresNetwork;

    public FakeVeoMotionTransport(bool requiresNetwork = false) => _requiresNetwork = requiresNetwork;

    public bool RequiresNetwork => _requiresNetwork;
    public bool SucceedImmediately { get; set; }
    public int HttpCallCount => 0;
    public int CreateCalls { get; private set; }
    public int GetOperationCalls { get; private set; }
    public int DownloadCalls { get; private set; }
    public string? LastCreatedJobId { get; private set; }
    public VeoMotionTransportCreateRequest? LastCreateRequest { get; private set; }
    public byte[]? ForcedDownloadBytes { get; set; }
    public FakeVeoScenario Scenario { get; set; } = FakeVeoScenario.Success;

    public Task<VeoMotionTransportCreateResult> CreateImageToVideoAsync(
        VeoMotionTransportCreateRequest request, CancellationToken ct = default)
    {
        LastCreateRequest = request;
        CreateCalls++;
        var jobId = JobPrefix + request.TaskId.ToString("N");
        LastCreatedJobId = jobId;
        return Task.FromResult(new VeoMotionTransportCreateResult(jobId));
    }

    public Task<VeoMotionTransportPollResult> GetOperationAsync(string providerJobId, CancellationToken ct = default)
    {
        GetOperationCalls++;
        if (string.IsNullOrWhiteSpace(providerJobId)
            || !providerJobId.StartsWith(JobPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(new VeoMotionTransportPollResult(
                providerJobId, WireFailed, ProductionFailureReasons.UnknownProvider));
        }

        if (Scenario == FakeVeoScenario.Failed)
        {
            return Task.FromResult(new VeoMotionTransportPollResult(
                providerJobId, WireFailed, ProductionFailureReasons.ProviderUnavailable));
        }

        if (SucceedImmediately)
            return Task.FromResult(new VeoMotionTransportPollResult(providerJobId, WireSucceeded));

        _polls.TryGetValue(providerJobId, out var n);
        _polls[providerJobId] = n + 1;
        if (n < 2)
            return Task.FromResult(new VeoMotionTransportPollResult(providerJobId, WireRunning));
        return Task.FromResult(new VeoMotionTransportPollResult(providerJobId, WireSucceeded));
    }

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default)
    {
        DownloadCalls++;
        if (ForcedDownloadBytes is not null)
            return Task.FromResult(ForcedDownloadBytes);
        if (Scenario == FakeVeoScenario.InvalidOutput)
            return Task.FromResult(Array.Empty<byte>());
        if (string.IsNullOrWhiteSpace(providerJobId)
            || !providerJobId.StartsWith(JobPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.OutputDownloadFailed);
        var osDuration = LastCreateRequest is { OsDurationSec: > 0 } req ? req.OsDurationSec : 10;
        return Task.FromResult(ProductionTestMp4.Encode(providerJobId, osDuration));
    }
}
