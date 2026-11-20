namespace KitPlatform.Packs.Content.Production;

/// <summary>Locked EP01 SH01 MIX parent from REAL LIPSYNC recover.</summary>
public static class ProductionEp01Sh01MixCanon
{
    public static readonly Guid LipsyncArtifactId = Guid.Parse("837ed989-5e1f-45ad-b77d-cb26f5e808a2");
    public const string LipsyncSha256 = "8e1bb610070ede431f2b0d0caac9194169145ee468496bf8769155f85b379c3e";
    public const decimal LipsyncDurationSec = 5.042m;

    public static bool MatchesScope(ProductionScope scope) =>
        ProductionEp01Sh01LipsyncCanon.MatchesScope(scope);
}
