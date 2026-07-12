namespace KitPlatform.Packs.Connect;

public static class ConnectOrgRoles
{
    public const string Pharmacy = "pharmacy";
    public const string Clinic = "clinic";
}

public static class ConnectOrgLinkStatuses
{
    /// <summary>Current tenant invited partner; waiting for partner to accept.</summary>
    public const string PendingPartnerAccept = "pending_partner_accept";

    /// <summary>Partner requested link; waiting for current tenant to approve.</summary>
    public const string PendingOurApproval = "pending_our_approval";

    public const string Active = "active";
    public const string Rejected = "rejected";
    public const string Revoked = "revoked";
}

public sealed record ConnectOrgLinkDto(
    Guid Id,
    Guid PartnerTenantId,
    string PartnerTenantCode,
    string PartnerTenantName,
    string OurOrgRole,
    string PartnerOrgRole,
    string LinkStatus,
    bool WeAreInitiator,
    string? Notes,
    DateTime InvitedAt,
    DateTime? RespondedAt,
    DateTime CreatedAt);

public sealed record ConnectDirectoryEntryDto(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string? OrgKind,
    string? Address,
    string? Phone);

public sealed record InviteConnectOrgLinkRequest(
    string PartnerTenantCode,
    string OurOrgRole,
    string PartnerOrgRole,
    string? Notes = null);

public sealed record RequestConnectOrgLinkRequest(
    string PartnerTenantCode,
    string OurOrgRole,
    string PartnerOrgRole,
    string? Notes = null);

public interface IConnectOrgLinkService
{
    Task<IReadOnlyList<ConnectOrgLinkDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectOrgLinkDto>> ListPendingIncomingAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectDirectoryEntryDto>> SearchDirectoryAsync(
        string? query,
        CancellationToken cancellationToken = default);

    Task<ConnectOrgLinkDto> InviteAsync(
        InviteConnectOrgLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectOrgLinkDto> RequestAsync(
        RequestConnectOrgLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectOrgLinkDto?> AcceptAsync(Guid linkId, CancellationToken cancellationToken = default);

    Task<ConnectOrgLinkDto?> ApproveAsync(Guid linkId, CancellationToken cancellationToken = default);

    Task<ConnectOrgLinkDto?> RejectAsync(Guid linkId, CancellationToken cancellationToken = default);

    Task<ConnectOrgLinkDto?> RevokeAsync(Guid linkId, CancellationToken cancellationToken = default);
}
