using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// EP01 SH02 Voice Production Ingress V1 — exact locked graph_json sources. Do not invent.
/// Source: series_build.graph_json.lines + episode.shots.dialogueSegmentIds.
/// VoiceId is CHAR-001 from that line, not SH01 CHAR-003.
/// </summary>
public static class ProductionEp01Sh02VoiceIngressCanon
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_VOICE_V1";
    public const string SourceType = "LEGACY_INGRESS";
    public const string SourceField = "graph_json.lines|episode.shots.dialogueSegmentIds";
    public const string LineId = "line-SC01-CHAR-001-2";
    public const string LineText = "(lý nhí) Dạ… nay con làm bài không tốt.";
    public const string SpeakerCharacterId = "CHAR-001";
    public const string ListenerCharacterId = "CHAR-003";
    public const string VoiceId = "ywBZEqUhld86Jeajq94o";
    public const string ProviderId = FamixaProviderIds.ElevenLabs;
    public const string ModelId = "eleven_v3";
    public const string LineEmotion = "neutral";
    public const string Language = "vi";
    public const decimal DurationSec = 10.0m;
    public static readonly Guid VideoParentArtifactId = Guid.Parse("b44e530b-5b59-422f-9b80-e058a3a639b5");
    public const string VideoParentSha = "21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34";
    public const long VideoParentBytes = 1437239;

    public static ProductionScope Scope() => ProductionEp01Sh02IngressCanon.Scope();

    public static bool MatchesScope(ProductionScope scope) =>
        scope.BuildId == ProductionEp01Sh02IngressCanon.BuildId
        && string.Equals(scope.ShotCode, ProductionEp01Sh02IngressCanon.Shot, StringComparison.Ordinal);

    public static bool IsCanonicalVoiceId(string? voiceId) =>
        string.Equals((voiceId ?? "").Trim(), VoiceId, StringComparison.Ordinal);

    public static bool IsSh01VoiceId(string? voiceId) =>
        string.Equals((voiceId ?? "").Trim(), ProductionEp01Sh01VoiceIngressCanon.VoiceId, StringComparison.Ordinal);

    public static string ExecutionAuthority(
        Guid snapshotId,
        string snapshotHash,
        string inputHash,
        Guid videoParentArtifactId) =>
        string.Join('\n',
            "VOICE_EXECUTION_AUTHORITY",
            "BuildId=" + BuildIdText(),
            "ShotCode=" + ProductionEp01Sh02IngressCanon.Shot,
            "Provider=" + ProviderId,
            "VoiceId=" + VoiceId,
            "SnapshotId=" + snapshotId.ToString("D"),
            "SnapshotHash=" + snapshotHash,
            "InputHash=" + inputHash,
            "VideoParentArtifactId=" + videoParentArtifactId.ToString("D"));

    static string BuildIdText() => ProductionEp01Sh02IngressCanon.BuildId.ToString("D");
}
