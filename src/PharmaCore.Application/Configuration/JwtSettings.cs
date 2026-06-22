namespace PharmaCore.Application.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PharmaCore";
    public string Audience { get; set; } = "PharmaCore.Client";
    public int AccessTokenExpireMinutes { get; set; } = 60;
    public int RefreshTokenExpireDays { get; set; } = 30;
}
