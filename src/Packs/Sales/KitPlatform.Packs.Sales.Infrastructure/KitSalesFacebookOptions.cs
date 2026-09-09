namespace KitPlatform.Packs.Sales.Infrastructure;

public sealed class KitSalesFacebookOptions
{
    public const string Section = "KitSales:Facebook";

    public bool Enabled { get; set; }

    public string? PageId { get; set; }

    public string? PageAccessToken { get; set; }

    public string? VerifyToken { get; set; }

    public string? AppSecret { get; set; }

    public string MeLink { get; set; } = "https://m.me/novixa68";

    public string PageUrl { get; set; } = "https://www.facebook.com/novixa68";

    public string PhcUrl { get; set; } = "https://novixa.vn/vi/health-check/";
}
