using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Catalog + capability gate only. Does not call Gemini, Runway, or Veo.</summary>
internal sealed class ProductionOsService : IProductionOsService, IProductionProviderSelector
{
    private static readonly ProductionOsRules.ProviderCapabilityProfile[] Profiles =
        [.. ProductionOsRules.DefaultCatalog];

    public IReadOnlyList<string> RunRegression() => ProductionOsArchitectureV1Regression.Run();

    public IReadOnlyList<ProductionOsRules.ProviderCapabilityProfile> Catalog() => Profiles;

    public ProductionOsRules.CanonicalProductionDescription Describe(ProductionOsRules.ProductionIntent intent) =>
        ProductionOsRules.Canonical(intent);

    public IReadOnlyList<ProductionOsRules.ProductionProviderCandidate> ListCandidates(
        ProductionOsRules.ProductionIntent intent, string generationType) =>
        ProductionOsRules.ListCandidates(intent, generationType, Profiles);

    public ProductionOsRules.ProductionProviderGate Evaluate(
        ProductionOsRules.ProductionIntent intent, string providerId, string generationType)
    {
        var profile = Profiles.FirstOrDefault(p => p.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("PRODUCTION_OS_INVALID: provider chưa đăng ký.");
        return ProductionOsRules.EvaluateCapability(intent, profile, generationType);
    }

    public ProductionOsRules.ProductionProviderGate DecideSelection(
        ProductionOsRules.ProductionIntent intent, string generationType, string? directorProviderId) =>
        ProductionOsRules.DecideSelection(ListCandidates(intent, generationType), directorProviderId);
}
