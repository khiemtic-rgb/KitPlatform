using KitPlatform.Application.Configuration;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal sealed class PharmacyConsultationAiConfigProvider
{
    private readonly ITenantSettingsService _tenantSettings;
    private readonly IConfiguration _configuration;
    private readonly IDbConnectionFactory _db;

    public PharmacyConsultationAiConfigProvider(
        ITenantSettingsService tenantSettings,
        IConfiguration configuration,
        IDbConnectionFactory db)
    {
        _tenantSettings = tenantSettings;
        _configuration = configuration;
        _db = db;
    }

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

        var pharmacyKey = ResolvePharmacyKey(row.GeminiApiKey, row.GeminiApiKeySecretRef);
        if (!string.IsNullOrWhiteSpace(pharmacyKey))
            return new ResolvedConfig(pharmacyKey, pharmacyModel, true, false);

        var content = await ContentParkGeminiKeyResolver.ResolveSettingsAsync(_db, _configuration, ct);
        var contentModel = string.IsNullOrWhiteSpace(content.TextModel)
            ? "gemini-3.6-flash"
            : content.TextModel.Trim();
        return new ResolvedConfig(
            content.ApiKey,
            contentModel,
            !string.IsNullOrWhiteSpace(content.ApiKey),
            !string.IsNullOrWhiteSpace(content.ApiKey));
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
