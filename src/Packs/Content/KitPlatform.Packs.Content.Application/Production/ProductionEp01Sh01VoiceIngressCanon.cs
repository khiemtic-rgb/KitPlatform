using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// EP01 SH01 Voice Production Ingress V1 — exact locked official-film sources. Do not invent.
/// </summary>
public static class ProductionEp01Sh01VoiceIngressCanon
{
    public const string SuiteId = "FAMIXA_SH01_VOICE_PRODUCTION_INGRESS_V1";
    public const string SourceType = "LEGACY_INGRESS";
    public const string SourceField = "graph_json.lines|episode.shots.dialogueSegmentIds";
    public const string LineId = "line-SC01-CHAR-003-1";
    public const string LineText = "Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?";
    public const string SpeakerCharacterId = "CHAR-003";
    public const string VoiceId = "kOIUKJ9E0DPndqTtvJm5";
    public const string ProviderId = FamixaProviderIds.ElevenLabs;
    public const string ModelId = "eleven_v3";
    public const string LineEmotion = "neutral";
    public const string HistoricalVoiceArtifactId = "vpa-20260915T161911Z-e0b3093f";

    public static ProductionScope Scope() => ProductionEp01Sh01IngressCanon.Scope();
}
