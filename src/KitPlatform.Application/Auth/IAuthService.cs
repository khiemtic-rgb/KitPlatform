namespace KitPlatform.Application.Auth;

public interface IAuthService
{
    Task<AuthLoginResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<LoginResponse?> SelectWorkspaceAsync(SelectWorkspaceRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthWorkspaceDto>> ListWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<AuthUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
