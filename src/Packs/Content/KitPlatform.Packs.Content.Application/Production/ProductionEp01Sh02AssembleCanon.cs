namespace KitPlatform.Packs.Content.Production;

/// <summary>Locked official EP01 SH02 ASSEMBLE parent from CK24D ACCEPTED:MIX.</summary>
public static class ProductionEp01Sh02AssembleCanon
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_ASSEMBLE_V1";
    public static readonly Guid MixArtifactId = Guid.Parse("58297402-e252-45e8-8d33-c7414749a297");
    public const string MixSha256 = "e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b";
    public const decimal MixDurationSec = 8;
    public static readonly Guid VideoArtifactId = ProductionEp01Sh02MixCanon.VideoArtifactId;
    public static readonly Guid VoiceArtifactId = ProductionEp01Sh02MixCanon.VoiceArtifactId;
    public static readonly Guid LipsyncArtifactId = ProductionEp01Sh02MixCanon.LipsyncArtifactId;
    public static readonly Guid HistoricalSh01AssemblyId = Guid.Parse("d56a3c63-fbe7-41d6-8355-0af246c092b7");
    public static readonly Guid HistoricalSh01MixArtifactId = ProductionEp01Sh01AssemblyCanon.MixArtifactId;

    public static ProductionScope Scope() => ProductionEp01Sh02IngressCanon.Scope();

    public static bool MatchesScope(ProductionScope scope) =>
        ProductionEp01Sh02LipsyncCanon.MatchesScope(scope);

    public static string? ParentLockError(ProductionArtifact mix)
    {
        if (mix.Role != ProductionArtifactRoles.Mix)
            return ProductionFailureReasons.FailedIdentity;
        if (mix.ArtifactId != MixArtifactId)
            return ProductionFailureReasons.BindingChanged;
        if (!string.Equals(ProductionMixContract.NormSha(mix.ByteSha256), MixSha256, StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.HashChanged;
        if (mix.VerificationStatus != ProductionArtifactStatuses.Verified)
            return ProductionFailureReasons.FailedVerification;
        if (mix.DurationSec != MixDurationSec)
            return ProductionFailureReasons.DurationMismatch;
        return null;
    }
}
