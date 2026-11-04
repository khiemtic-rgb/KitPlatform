namespace KitPlatform.Api.Authorization;

/// <summary>Per-client-IP limits for anonymous auth endpoints. Client IP comes from forwarded headers behind nginx.</summary>
public static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
    public const string PublicOtp = "auth-public-otp";
}
