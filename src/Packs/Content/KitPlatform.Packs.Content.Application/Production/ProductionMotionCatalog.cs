using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionMotionModel(
    string ProviderId,
    string ModelId,
    FamixaProviderCapability Capability,
    string DisplayName,
    string Action,
    bool Enabled,
    bool IsPaid);

/// <summary>Server catalog only. Client cost/endpoint/vendor IDs are not authority.</summary>
public static class ProductionMotionCatalog
{
    public const string FixtureProviderId = "fixture";
    public const string FixtureModelId = "fixture-motion-v1";
    public const string RunwayProviderId = FamixaProviderIds.Runway;
    public const string RunwayGen4Turbo = "gen4_turbo";
    public const string GoogleProviderId = FamixaProviderIds.Google;
    public const string Veo31ModelId = "veo-3.1";

    public static readonly ProductionMotionModel Fixture = new(
        FixtureProviderId, FixtureModelId, FamixaProviderCapability.Motion,
        "Fixture Motion", ProductionActions.CreateVideo, Enabled: true, IsPaid: false);

    public static readonly ProductionMotionModel RunwayGen4 = new(
        RunwayProviderId, RunwayGen4Turbo, FamixaProviderCapability.Motion,
        "Runway Gen-4 Turbo", ProductionActions.CreateVideo, Enabled: true, IsPaid: true);

    /// <summary>Binding/resolver identity only. Cost is <see cref="FamixaVeo31OfficialPricing"/> for veo-3.1 VIDEO_ONLY 720p.</summary>
    public static readonly ProductionMotionModel GoogleVeo31 = new(
        GoogleProviderId, Veo31ModelId, FamixaProviderCapability.Motion,
        "Google Veo 3.1", ProductionActions.CreateVideo, Enabled: true, IsPaid: true);

    public static IReadOnlyList<ProductionMotionModel> All { get; } = [Fixture, RunwayGen4, GoogleVeo31];

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

    public static bool ProviderExists(string? providerId)
    {
        var provider = (providerId ?? "").Trim();
        return All.Any(m => string.Equals(m.ProviderId, provider, StringComparison.OrdinalIgnoreCase));
    }
}

public static class ProductionMotionSelection
{
    public static (string ProviderId, string ModelId) Normalize(
        string? providerId,
        string? modelId,
        IMotionExecutionAdapter? fallback = null)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length > 0 && model.Length > 0)
            return (provider, model);
        if (provider.Length > 0 || model.Length > 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProviderModel);
        var fallbackProvider = (fallback?.ProviderId ?? "").Trim();
        var fallbackModel = (fallback?.ModelId ?? "").Trim();
        if (fallbackProvider.Length > 0 && fallbackModel.Length > 0)
            return (fallbackProvider, fallbackModel);
        throw new InvalidOperationException(ProductionFailureReasons.MissingProviderModel);
    }
}
