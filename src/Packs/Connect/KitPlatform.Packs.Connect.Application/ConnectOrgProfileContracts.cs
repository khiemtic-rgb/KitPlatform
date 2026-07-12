namespace KitPlatform.Packs.Connect;

public static class ConnectOrgKinds
{
    public const string Pharmacy = "pharmacy";
    public const string Clinic = "clinic";
}

public sealed record ConnectOrgProfileDto(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string OrgKind,
    string? DisplayName);

public interface IConnectOrgProfileService
{
    Task<ConnectOrgProfileDto?> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<string?> GetOrgKindAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
