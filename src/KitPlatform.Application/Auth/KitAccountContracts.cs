namespace KitPlatform.Application.Auth;

public sealed record AuthWorkspaceDto(
    Guid UserId,
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string ProductCode,
    string Username,
    bool IsDefault);

public sealed record AuthLoginResult(
    bool RequiresWorkspaceChoice,
    LoginResponse? Session,
    string? SelectionToken,
    IReadOnlyList<AuthWorkspaceDto>? Workspaces)
{
    public static AuthLoginResult Success(LoginResponse session) =>
        new(false, session, null, null);

    public static AuthLoginResult Choice(string selectionToken, IReadOnlyList<AuthWorkspaceDto> workspaces) =>
        new(true, null, selectionToken, workspaces);
}

public sealed record LoginWorkspaceChoiceResponse(
    bool RequiresWorkspaceChoice,
    string SelectionToken,
    IReadOnlyList<AuthWorkspaceDto> Workspaces);

public sealed record SelectWorkspaceRequest(string SelectionToken, Guid UserId);

public interface IKitAccountService
{
    Task<Guid> EnsureAccountForUserAsync(
        Guid userId,
        Guid tenantId,
        string email,
        string plaintextPassword,
        string passwordHash,
        string? displayName,
        System.Data.IDbConnection? connection = null,
        System.Data.IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task AssertEmailPasswordCompatibleAsync(
        string email,
        string plaintextPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// When a linked user changes password, update kit_accounts + all membership users.
    /// </summary>
    Task SyncPasswordForUserAsync(
        Guid userId,
        string plaintextPassword,
        string passwordHash,
        CancellationToken cancellationToken = default);
}
