namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Locked official EP01 SH02 MIX parents. VIDEO is ACCEPTED. LIPSYNC is CURRENT/ACCEPTED/VISIBLE.
/// VOICE is not a MIX parent. Historical CK23 LIPSYNC is forbidden.
/// </summary>
public static class ProductionEp01Sh02MixCanon
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_MIX_V1";
    public static readonly Guid VideoArtifactId = Guid.Parse("b44e530b-5b59-422f-9b80-e058a3a639b5");
    public const string VideoSha256 = "21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34";
    public const decimal VideoDurationSec = 8;
    public static readonly Guid LipsyncArtifactId = Guid.Parse("b0b4f0f2-95b5-4839-b9bc-22c48028bb9e");
    public const string LipsyncSha256 = "725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e";
    public const decimal LipsyncDurationSec = 2.5m;
    public static readonly Guid VoiceArtifactId = Guid.Parse("0fde86c2-250f-4303-bda0-4913a6dbd1e4");
    public static readonly Guid HistoricalLipsyncArtifactId =
        ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId;

    public static ProductionScope Scope() => ProductionEp01Sh02IngressCanon.Scope();

    public static bool MatchesScope(ProductionScope scope) =>
        ProductionEp01Sh02LipsyncCanon.MatchesScope(scope);

    public static bool IsHistoricalLipsync(Guid artifactId) =>
        artifactId == HistoricalLipsyncArtifactId;

    public static string? ParentLockError(ProductionArtifact video, ProductionArtifact lipsync)
    {
        if (IsHistoricalLipsync(lipsync.ArtifactId))
            return ProductionFailureReasons.BindingChanged;
        if (video.ArtifactId != VideoArtifactId || lipsync.ArtifactId != LipsyncArtifactId)
            return ProductionFailureReasons.BindingChanged;
        if (!string.Equals(ProductionMixContract.NormSha(video.ByteSha256), VideoSha256, StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.HashChanged;
        if (!string.Equals(ProductionMixContract.NormSha(lipsync.ByteSha256), LipsyncSha256, StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.HashChanged;
        if (video.DurationSec != VideoDurationSec)
            return ProductionFailureReasons.DurationMismatch;
        if (lipsync.DurationSec <= 0)
            return ProductionFailureReasons.MissingTimeline;
        return null;
    }

    public static string PermitReason(string contractHash) =>
        string.Join('\n',
            "MIX_EXECUTION_AUTHORITY",
            "BuildId=" + ProductionEp01Sh02IngressCanon.BuildId.ToString("D"),
            "ShotCode=" + ProductionEp01Sh02IngressCanon.Shot,
            "ContractVersion=" + ProductionMixContract.Version,
            "CompositionMode=" + ProductionMixContract.ComposeSegments,
            "OutputClock=" + ProductionMixContract.OutputClockVideoTake,
            "VideoParentArtifactId=" + VideoArtifactId.ToString("D"),
            "VideoParentSHA=" + VideoSha256,
            "LipsyncParentArtifactId=" + LipsyncArtifactId.ToString("D"),
            "LipsyncParentSHA=" + LipsyncSha256,
            "ContractHash=" + contractHash);
}
