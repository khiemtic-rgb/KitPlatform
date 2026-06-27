using Microsoft.Extensions.Hosting;
using PharmaCore.Application.Auth;
using PharmaCore.Application.Configuration;

namespace PharmaCore.Infrastructure.Auth;

internal sealed class AuthService : IAuthService
{
    private const short ActiveStatus = 1;
    private const string DefaultTenantCode = "DEMO_PHARMACY";

    private readonly AuthRepository _repository;
    private readonly JwtTokenService _jwt;
    private readonly IHostEnvironment _environment;

    public AuthService(AuthRepository repository, JwtTokenService jwt, IHostEnvironment environment)
    {
        _repository = repository;
        _jwt = jwt;
        _environment = environment;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var tenantCode = string.IsNullOrWhiteSpace(request.TenantCode)
            ? (_environment.IsDevelopment() ? DefaultTenantCode : null)
            : request.TenantCode.Trim();

        if (string.IsNullOrWhiteSpace(tenantCode))
            return null;

        var user = await _repository.FindByCredentialsAsync(tenantCode, request.Username.Trim(), cancellationToken);
        if (user is null || user.Status != ActiveStatus)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var userId = await _repository.FindUserIdByRefreshTokenHashAsync(hash, cancellationToken);
        if (userId is null)
        {
            return null;
        }

        var user = await _repository.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null || user.Status != ActiveStatus)
        {
            return null;
        }

        await _repository.RevokeRefreshTokenAsync(hash, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var hash = JwtTokenService.HashToken(refreshToken);
        await _repository.RevokeRefreshTokenAsync(hash, cancellationToken);
        return true;
    }

    public async Task<AuthUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _repository.FindByIdAsync(userId, cancellationToken);
        return user is null ? null : MapUser(user);
    }

    private async Task<LoginResponse> IssueTokensAsync(UserRecord user, CancellationToken cancellationToken)
    {
        var (accessToken, expiresAt) = _jwt.CreateAccessToken(user);
        var refreshToken = JwtTokenService.GenerateRefreshToken();
        var refreshHash = JwtTokenService.HashToken(refreshToken);

        await _repository.StoreRefreshTokenAsync(user.Id, refreshHash, _jwt.GetRefreshTokenExpiry(), cancellationToken);
        await _repository.UpdateLastLoginAsync(user.Id, cancellationToken);

        return new LoginResponse(accessToken, refreshToken, expiresAt, MapUser(user));
    }

    private static AuthUserDto MapUser(UserRecord user) =>
        new(user.Id, user.TenantId, user.TenantCode, user.Username, user.Email, user.Roles, user.Permissions);
}
