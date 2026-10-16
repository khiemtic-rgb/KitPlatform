namespace KitPlatform.Application.Auth;

public sealed record LoginRequest(string Username, string Password, string? TenantCode = null);

/// <summary>
/// Login omitted a tenant and <c>Auth:DefaultTenantCode</c> is empty.
/// Assessment event tenant is not a substitute.
/// </summary>
public sealed class AuthTenantRequiredException : InvalidOperationException
{
    public AuthTenantRequiredException()
        : base("Mã nhà thuốc là bắt buộc khi chưa cấu hình tenant mặc định.")
    {
    }
}

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    AuthUserDto User);

public sealed record AuthUserDto(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
