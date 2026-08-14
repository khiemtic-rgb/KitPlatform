using Microsoft.Extensions.Options;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class CsdlDuocCredentialResolver : ICsdlDuocCredentialResolver
{
    private readonly TenantCsdlDuocLinkRepository _links;
    private readonly IOptionsMonitor<NationalDrugCatalogSettings> _platform;

    public CsdlDuocCredentialResolver(
        TenantCsdlDuocLinkRepository links,
        IOptionsMonitor<NationalDrugCatalogSettings> platform)
    {
        _links = links;
        _platform = platform;
    }

    public async Task<CsdlDuocEffectiveCredentials> ResolveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var platform = _platform.CurrentValue;
        var link = await _links.GetAsync(tenantId, cancellationToken);

        if (link is not null
            && link.Enabled
            && string.Equals(link.Status, "Connected", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(link.Username)
            && !string.IsNullOrWhiteSpace(link.Password))
        {
            var mode = NationalDrugCatalogSettings.NormalizeMode(link.Environment);
            var baseUrl = mode == "live"
                ? "https://api.csdlduoc.com.vn/v2"
                : "https://api-sandbox.csdlduoc.com.vn/v2";

            return new CsdlDuocEffectiveCredentials(
                tenantId,
                Source: "tenant",
                Mode: mode,
                BaseUrl: baseUrl,
                Username: link.Username.Trim(),
                Password: link.Password,
                PasswordIsBase64: false,
                PracticeLicenseCode: link.PracticeLicenseCode,
                EnableStockOutSync: link.EnableStockOutSync,
                EnableStockInSync: link.EnableStockInSync,
                LinkStatus: link.Status);
        }

        return new CsdlDuocEffectiveCredentials(
            tenantId,
            Source: "platform",
            Mode: NationalDrugCatalogSettings.NormalizeMode(platform.Mode),
            BaseUrl: platform.ResolveBaseUrl(),
            Username: platform.Username?.Trim() ?? "",
            Password: platform.Password ?? "",
            PasswordIsBase64: platform.PasswordIsBase64,
            PracticeLicenseCode: platform.PracticeLicenseCode,
            EnableStockOutSync: platform.EnableStockOutSync,
            EnableStockInSync: false,
            LinkStatus: link?.Status ?? "NotConfigured");
    }
}
