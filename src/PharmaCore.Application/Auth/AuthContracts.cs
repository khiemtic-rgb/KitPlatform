namespace PharmaCore.Application.Auth;

public sealed record LoginRequest(string Username, string Password, string? TenantCode = null);

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
