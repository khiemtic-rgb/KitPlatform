using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Production still models that have a tenant-credential execution adapter.
/// Only the model TenantGeminiImageExecutionAdapter actually runs.
/// </summary>
public static class ProductionImageCatalog
{
    public const string GeminiProviderId = ImageGenerationExecutionRules.Provider;
    /// <summary>Approved still model. gemini-2.5-flash-image is retired and is not a fallback.</summary>
    public const string GeminiFlashImage = "gemini-3.1-flash-image";

    public static readonly ProductionMotionModel GeminiFlash = new(
        GeminiProviderId, GeminiFlashImage, FamixaProviderCapability.Picture,
        "Gemini 3.1 Flash Image", ProductionActions.CreateImage, Enabled: true, IsPaid: true);

    public static IReadOnlyList<ProductionMotionModel> All { get; } = [GeminiFlash];
}
