namespace KitPlatform.Packs.TestPack;

/// <summary>KIT Platform Pack Onboarding Canary. Not a business product.</summary>
public static class TestPackDefinition
{
    public const string PackCode = "test_pack";
    public const string DisplayName = "KIT Test Pack";
    public const string PrimaryModuleCode = "test_pack";
    public const string ReadPermission = "test_pack.read";

    public static IReadOnlyList<string> PackModuleCodes { get; } = [PrimaryModuleCode];
}
