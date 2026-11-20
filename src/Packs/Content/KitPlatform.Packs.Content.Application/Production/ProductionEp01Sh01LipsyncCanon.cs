namespace KitPlatform.Packs.Content.Production;

/// <summary>Locked EP01 SH01 LipSync parents from REAL FILM RUN V2 VIDEO + VOICE.</summary>
public static class ProductionEp01Sh01LipsyncCanon
{
    public static readonly Guid VideoArtifactId = Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae");
    public const string VideoSha256 = "8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296";
    public const decimal VideoDurationSec = 5.042m;
    public static readonly Guid VoiceArtifactId = Guid.Parse("bf8d0827-21ff-4daa-83b6-116bc3324ab0");
    public const string VoiceSha256 = "948bb91adb6d10edd1fe0024b91090537699ddec79905cae9e8f956f1b4dbfd2";
    public const decimal VoiceDurationSec = 4.232m;
    public const string LineId = ProductionEp01Sh01VoiceIngressCanon.LineId;
    public const string SpeakerCharacterId = ProductionEp01Sh01VoiceIngressCanon.SpeakerCharacterId;
    public const string VoiceId = ProductionEp01Sh01VoiceIngressCanon.VoiceId;

    public static bool MatchesScope(ProductionScope scope) =>
        scope.BuildId == ProductionScopeAnchorRules.OfficialEp01
        && string.Equals(scope.ShotCode, ProductionEp01Sh01IngressCanon.Shot, StringComparison.Ordinal);
}
