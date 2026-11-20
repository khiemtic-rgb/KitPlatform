using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionLipsyncCatalog
{
    public const string FixtureProviderId = "fixture";
    public const string FixtureModelId = "fixture-lipsync-v1";
    public const string FalProviderId = FamixaProviderIds.Fal;
    public const string SyncLipsyncV3 = "sync-lipsync-v3";

    public static readonly ProductionMotionModel Fixture = new(
        FixtureProviderId, FixtureModelId, FamixaProviderCapability.LipSync,
        "Fixture Lipsync", ProductionActions.CreateLipsync, Enabled: true, IsPaid: false);

    public static readonly ProductionMotionModel FalSyncV3 = new(
        FalProviderId, SyncLipsyncV3, FamixaProviderCapability.LipSync,
        "Fal Sync Lipsync V3", ProductionActions.CreateLipsync, Enabled: true, IsPaid: true);

    public static IReadOnlyList<ProductionMotionModel> All { get; } = [Fixture, FalSyncV3];

    public static bool IsFixtureProvider(string? providerId) =>
        string.Equals((providerId ?? "").Trim(), FixtureProviderId, StringComparison.OrdinalIgnoreCase);

    public static bool TryGet(string? providerId, string? modelId, out ProductionMotionModel model)
    {
        var provider = (providerId ?? "").Trim();
        var mid = (modelId ?? "").Trim();
        model = All.FirstOrDefault(m =>
            string.Equals(m.ProviderId, provider, StringComparison.OrdinalIgnoreCase)
            && string.Equals(m.ModelId, mid, StringComparison.OrdinalIgnoreCase))
            ?? default!;
        return model is not null;
    }
}
