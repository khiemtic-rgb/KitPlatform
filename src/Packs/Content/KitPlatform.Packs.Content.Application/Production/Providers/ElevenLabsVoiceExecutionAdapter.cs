namespace KitPlatform.Packs.Content.Production;

/// <summary>Production OS ElevenLabs voice adapter. Vendor HTTP lives behind IElevenLabsVoiceTransport.</summary>
public sealed class ElevenLabsVoiceExecutionAdapter : IVoiceExecutionAdapter
{
    readonly IElevenLabsVoiceTransport _transport;

    public ElevenLabsVoiceExecutionAdapter(IElevenLabsVoiceTransport transport) => _transport = transport;

    public string ProviderId => ProductionVoiceCatalog.ElevenLabsProviderId;
    public string ModelId => ProductionVoiceCatalog.ElevenV3;
    public bool IsPaid => true;
    public bool RequiresNetwork => _transport.RequiresNetwork;

    public Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        => ExecuteFrozenAsync(snapshot, null, ct);

    public async Task<ProductionMediaResult> ExecuteFrozenAsync(
        ProductionTaskInputSnapshot snapshot, VoiceStartContext? context, CancellationToken ct)
    {
        if (!string.Equals(snapshot.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.ModelId, ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
        if (!ProductionVoiceFreeze.TryParse(snapshot.ShotContract, out _, out _, out var voiceId, out var text)
            || ProductionVoiceFreeze.IsTestIdentity(voiceId)
            || !string.Equals(text, snapshot.Story, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.CanonicalVoiceInputNotBound);

        var synthesized = await _transport.SynthesizeOnceAsync(new ElevenLabsVoiceTransportRequest(
            snapshot.TaskId,
            context?.AttemptId ?? Guid.Empty,
            voiceId,
            ModelId,
            text,
            snapshot.SnapshotHash,
            context?.Scope,
            context?.ShotCode ?? snapshot.TaskId.ToString("N"),
            ProductionFrozenCredential.FromSnapshot(snapshot)), ct);
        if (string.IsNullOrWhiteSpace(synthesized.ProviderJobId)
            || synthesized.ProviderJobId.StartsWith(FakeElevenLabsVoiceTransport.JobPrefix, StringComparison.Ordinal)
                && _transport.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        if (synthesized.Bytes is null || synthesized.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var duration = snapshot.DurationSec > 0 ? snapshot.DurationSec : 1;
        if (ProductionTestWav.TryReadDuration(synthesized.Bytes, out var wav) && wav > 0)
            duration = wav;
        else if (ProductionMpegAudio.TryReadDuration(synthesized.Bytes, out var mpeg) && mpeg > 0)
            duration = mpeg;

        return new ProductionMediaResult(
            synthesized.ProviderJobId,
            synthesized.Bytes,
            string.IsNullOrWhiteSpace(synthesized.MimeType) ? "audio/mpeg" : synthesized.MimeType,
            duration);
    }
}
