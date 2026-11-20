namespace KitPlatform.Packs.Content.Production;

/// <summary>Deterministic Fal-shaped transport. Never opens a socket.</summary>
public class FakeFalLipsyncTransport : IFalLipsyncTransport
{
    public const string JobPrefix = "fal-fake-";
    protected virtual string ProviderJobPrefix => JobPrefix;

    public virtual bool RequiresNetwork => false;
    public int StartCount { get; protected set; }
    public string? LastProviderJobId { get; protected set; }
    public FalLipsyncTransportRequest? LastRequest { get; protected set; }
    protected byte[]? LastBytes { get; set; }

    public virtual Task<FalLipsyncTransportResult> ExecuteOnceAsync(
        FalLipsyncTransportRequest request, CancellationToken ct = default)
    {
        _ = ct;
        LastRequest = request;
        if (request.VideoBytes is not { Length: >= 32 } || request.VoiceBytes is not { Length: >= 32 })
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!ProductionLipsyncSpeakerTarget.TryAcceptTransportPin(request.Scope, request.Pin, out _))
            throw new InvalidOperationException(ProductionFailureReasons.LipsyncTargetUnresolved);

        StartCount++;
        var jobId = ProviderJobPrefix + request.TaskId.ToString("N");
        LastProviderJobId = jobId;
        var duration = request.VideoDurationSec > 0 ? request.VideoDurationSec : 5;
        var bytes = ProductionTestMp4.Encode(
            request.InputSnapshotHash + request.Pin.CharacterId + request.TaskId.ToString("N"), duration);
        LastBytes = bytes;
        return Task.FromResult(new FalLipsyncTransportResult(jobId, bytes, ProductionTestMp4.Mime));
    }

    public virtual Task<FalLipsyncTransportResult> RecoverOnceAsync(
        string providerJobId, CancellationToken ct = default)
    {
        _ = ct;
        var job = string.IsNullOrWhiteSpace(providerJobId) ? LastProviderJobId : providerJobId.Trim();
        if (string.IsNullOrWhiteSpace(job))
            throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
        if (LastBytes is { Length: >= 32 })
            return Task.FromResult(new FalLipsyncTransportResult(job, LastBytes, ProductionTestMp4.Mime));
        if (LastRequest is not null)
        {
            var duration = LastRequest.VideoDurationSec > 0 ? LastRequest.VideoDurationSec : 5;
            var bytes = ProductionTestMp4.Encode(
                LastRequest.InputSnapshotHash + LastRequest.Pin.CharacterId + LastRequest.TaskId.ToString("N"), duration);
            LastBytes = bytes;
            return Task.FromResult(new FalLipsyncTransportResult(job, bytes, ProductionTestMp4.Mime));
        }
        throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
    }
}

public sealed class NetworkFakeFalLipsyncTransport : FakeFalLipsyncTransport
{
    public override bool RequiresNetwork => true;
    protected override string ProviderJobPrefix => "fal-net-";
}
