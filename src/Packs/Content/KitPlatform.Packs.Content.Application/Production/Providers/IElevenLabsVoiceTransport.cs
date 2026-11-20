namespace KitPlatform.Packs.Content.Production;

public interface IElevenLabsVoiceTransport
{
    bool RequiresNetwork { get; }
    int SynthesizeCount { get; }
    Task<ElevenLabsVoiceTransportResult> SynthesizeOnceAsync(ElevenLabsVoiceTransportRequest request, CancellationToken ct = default);
}

public sealed record ElevenLabsVoiceTransportRequest(
    Guid TaskId,
    Guid AttemptId,
    string VoiceId,
    string ModelId,
    string Text,
    string InputSnapshotHash,
    ProductionScope? Scope = null,
    string? ShotCode = null,
    ProductionCredentialReference? Credential = null);

public sealed record VoiceStartContext(
    Guid AttemptId,
    ProductionScope? Scope = null,
    string? ShotCode = null);

public sealed record ElevenLabsVoiceTransportResult(
    string ProviderJobId,
    byte[] Bytes,
    string MimeType);
