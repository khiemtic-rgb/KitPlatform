namespace PharmaCore.Infrastructure.Auth;

internal sealed class UserRecord
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantCode { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public short Status { get; init; }
    public string[] Roles { get; init; } = [];
    public string[] Permissions { get; init; } = [];
}
