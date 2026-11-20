namespace KitPlatform.Packs.Content.Production;

/// <summary>Deterministic ElevenLabs-shaped transport. Never opens a socket.</summary>
public class FakeElevenLabsVoiceTransport : IElevenLabsVoiceTransport
{
    public const string JobPrefix = "elevenlabs-fake-";
    protected virtual string ProviderJobPrefix => JobPrefix;

    public virtual bool RequiresNetwork => false;
    public int SynthesizeCount { get; private set; }
    public string? LastProviderJobId { get; private set; }
    public ElevenLabsVoiceTransportRequest? LastRequest { get; private set; }

    public Task<ElevenLabsVoiceTransportResult> SynthesizeOnceAsync(
        ElevenLabsVoiceTransportRequest request, CancellationToken ct = default)
    {
        _ = ct;
        LastRequest = request;
        if (string.IsNullOrWhiteSpace(request.VoiceId)
            || ProductionVoiceFreeze.IsTestIdentity(request.VoiceId)
            || string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!string.Equals(request.ModelId, ProductionVoiceCatalog.ElevenV3, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);

        SynthesizeCount++;
        var jobId = ProviderJobPrefix + request.TaskId.ToString("N");
        LastProviderJobId = jobId;
        var duration = 1.2m;
        var bytes = ProductionTestWav.Encode(request.InputSnapshotHash + request.VoiceId + request.Text, duration);
        return Task.FromResult(new ElevenLabsVoiceTransportResult(jobId, bytes, ProductionTestWav.Mime));
    }
}
