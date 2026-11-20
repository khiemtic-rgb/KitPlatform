namespace KitPlatform.Packs.Content.Production;

/// <summary>Locked EP01 SH01 ASSEMBLE parent from REAL MIX one-shot.</summary>
public static class ProductionEp01Sh01AssemblyCanon
{
    public static readonly Guid MixArtifactId = Guid.Parse("2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2");
    public const string MixSha256 = "3818672c08d716b7d51c3fb12aa3f96ed204e78ef05800f27bec17e87fc28f10";
    public const string MixStorageKey = "os/58796f8cc6b24578894bda828cc7e2c0";
    public const decimal MixDurationSec = 5.042m;

    public static bool MatchesScope(ProductionScope scope) =>
        ProductionEp01Sh01LipsyncCanon.MatchesScope(scope);
}
