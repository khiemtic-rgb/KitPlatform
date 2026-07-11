namespace KitPlatform.Application.Configuration;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// SPA frontends that call the API from the browser.
    /// Always unioned in Production so a partial Cors__AllowedOrigins__* env array
    /// cannot drop pos/survey/partner and break live apps.
    /// </summary>
    public static readonly string[] RequiredNovixaSpaOrigins =
    [
        "https://admin.novixa.vn",
        "https://app.novixa.vn",
        "https://pos.novixa.vn",
        "https://survey.novixa.vn",
        "https://prescriber.novixa.vn",
        "https://partner.novixa.vn",
    ];

    public string[] AllowedOrigins { get; set; } = [];
}
