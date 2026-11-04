using KitPlatform.Application.Configuration;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal sealed class PharmacyConsultationAiConfigProvider
{
    private readonly ITenantSettingsService _tenantSettings;

    public PharmacyConsultationAiConfigProvider(ITenantSettingsService tenantSettings) =>
        _tenantSettings = tenantSettings;

    public sealed record ResolvedConfig(
        string? ApiKey,
        string TextModel,
        bool IsConfigured,
        bool UsesContentFallback);

    public async Task<ResolvedConfig> ResolveAsync(CancellationToken ct)
    {
        var row = await _tenantSettings.ResolvePharmacyConsultationAiAsync(ct);
        var pharmacyModel = string.IsNullOrWhiteSpace(row.TextModel)
            ? "gemini-2.5-flash-lite"
            : row.TextModel.Trim();

        // Customer health text never goes through the Content Park (marketing) key.
        var pharmacyKey = ResolvePharmacyKey(row.GeminiApiKey, row.GeminiApiKeySecretRef);
        return string.IsNullOrWhiteSpace(pharmacyKey)
            ? new ResolvedConfig(null, pharmacyModel, false, false)
            : new ResolvedConfig(pharmacyKey, pharmacyModel, true, false);
    }

    private static string? ResolvePharmacyKey(string? dbKey, string? secretRef)
    {
        if (!string.IsNullOrWhiteSpace(dbKey))
            return dbKey.Trim();

        var refName = string.IsNullOrWhiteSpace(secretRef) ? "GEMINI_API_KEY" : secretRef.Trim();
        var fromRef = Environment.GetEnvironmentVariable(refName);
        if (!string.IsNullOrWhiteSpace(fromRef))
            return fromRef.Trim();

        var fromEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }
}
