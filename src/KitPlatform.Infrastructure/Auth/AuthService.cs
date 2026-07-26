using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using KitPlatform.Application.Auth;

namespace KitPlatform.Infrastructure.Auth;

internal sealed class AuthService : IAuthService
{
    private const short ActiveStatus = 1;
    private const string DevDefaultTenantCode = "DEMO_PHARMACY";
    private static readonly TimeSpan WorkspaceSelectionTtl = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, WorkspaceSelectionEntry> WorkspaceSelections = new();

    private readonly AuthRepository _repository;
    private readonly KitAccountRepository _kitAccounts;
    private readonly JwtTokenService _jwt;
    private readonly IHostEnvironment _environment;
    private readonly string? _configuredDefaultTenantCode;

    public AuthService(
        AuthRepository repository,
        KitAccountRepository kitAccounts,
        JwtTokenService jwt,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _repository = repository;
        _kitAccounts = kitAccounts;
        _jwt = jwt;
        _environment = environment;
        _configuredDefaultTenantCode = configuration["Auth:DefaultTenantCode"]?.Trim();
        if (string.IsNullOrWhiteSpace(_configuredDefaultTenantCode))
            _configuredDefaultTenantCode = configuration["Assessment:EventTenantCode"]?.Trim();
    }

    public async Task<AuthLoginResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? "";
        var password = request.Password ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        if (string.IsNullOrWhiteSpace(request.TenantCode) && username.Contains('@'))
            return await LoginByKitEmailAsync(username, password, cancellationToken);

        var tenantCode = ResolveTenantCode(request.TenantCode);
        if (string.IsNullOrWhiteSpace(tenantCode))
            return null;

        var user = await _repository.FindByCredentialsAsync(tenantCode, username, cancellationToken);
        if (user is null || user.Status != ActiveStatus)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return AuthLoginResult.Success(await IssueTokensAsync(user, cancellationToken));
    }

    public async Task<LoginResponse?> SelectWorkspaceAsync(
        SelectWorkspaceRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var token = request.SelectionToken?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(token) || request.UserId == Guid.Empty)
            return null;

        if (!WorkspaceSelections.TryGetValue(token, out var entry) || entry.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            WorkspaceSelections.TryRemove(token, out _);
            return null;
        }

        if (!entry.AllowedUserIds.Contains(request.UserId))
            return null;

        var user = await _repository.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.Status != ActiveStatus)
            return null;

        if (user.KitAccountId != entry.AccountId)
            return null;

        WorkspaceSelections.TryRemove(token, out _);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<IReadOnlyList<AuthWorkspaceDto>> ListWorkspacesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var accountId = await _kitAccounts.FindAccountIdByUserIdAsync(userId, cancellationToken);
        if (accountId is null)
        {
            var user = await _repository.FindByIdAsync(userId, cancellationToken);
            if (user is null)
                return [];

            return
            [
                new AuthWorkspaceDto(
                    user.Id,
                    user.TenantId,
                    user.TenantCode,
                    user.TenantCode,
                    "hybrid",
                    user.Username,
                    true),
            ];
        }

        return await _kitAccounts.ListWorkspacesByAccountIdAsync(accountId.Value, cancellationToken);
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var userId = await _repository.FindUserIdByRefreshTokenHashAsync(hash, cancellationToken);
        if (userId is null)
            return null;

        var user = await _repository.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null || user.Status != ActiveStatus)
            return null;

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

    private async Task<AuthLoginResult?> LoginByKitEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var account = await _kitAccounts.FindByEmailAsync(normalized, null, null, cancellationToken);
        if (account is null || account.Status != ActiveStatus)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
            return null;

        var workspaces = await _kitAccounts.ListWorkspacesByAccountIdAsync(account.Id, cancellationToken);
        if (workspaces.Count == 0)
            return null;

        if (workspaces.Count == 1)
        {
            var user = await _repository.FindByIdAsync(workspaces[0].UserId, cancellationToken);
            if (user is null || user.Status != ActiveStatus)
                return null;

            return AuthLoginResult.Success(await IssueTokensAsync(user, cancellationToken));
        }

        var selectionToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        WorkspaceSelections[selectionToken] = new WorkspaceSelectionEntry(
            account.Id,
            workspaces.Select(w => w.UserId).ToHashSet(),
            DateTimeOffset.UtcNow.Add(WorkspaceSelectionTtl));

        return AuthLoginResult.Choice(selectionToken, workspaces);
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

    private string? ResolveTenantCode(string? requestTenantCode)
    {
        if (!string.IsNullOrWhiteSpace(requestTenantCode))
            return requestTenantCode.Trim();

        if (!string.IsNullOrWhiteSpace(_configuredDefaultTenantCode))
            return _configuredDefaultTenantCode;

        return _environment.IsDevelopment() ? DevDefaultTenantCode : null;
    }

    private sealed record WorkspaceSelectionEntry(
        Guid AccountId,
        HashSet<Guid> AllowedUserIds,
        DateTimeOffset ExpiresAtUtc);
}
