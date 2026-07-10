namespace KitPlatform.Packs.Pharmacy.Rx;

public static class PrescriberLinkStatuses
{
    public const string PendingNtInvite = "pending_nt_invite";
    public const string PendingNtApproval = "pending_nt_approval";
    public const string Active = "active";
    public const string Rejected = "rejected";
    public const string Revoked = "revoked";
}

public sealed record PrescriberOtpRequest(string Phone);

public sealed record PrescriberOtpVerifyRequest(string Phone, string Code);

public sealed record PrescriberOtpSentResponse(
    int ExpiresInSeconds,
    int CooldownSeconds,
    string Message,
    string? PilotCode = null);

public sealed record PrescriberLoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    PrescriberProfileDto Profile);

public sealed record PrescriberProfileDto(
    Guid Id,
    string FullName,
    string? LicenseNumber,
    string Phone,
    string? Specialty,
    string Status);

public sealed record PrescriberTenantLinkDto(
    Guid Id,
    Guid PrescriberId,
    Guid TenantId,
    string TenantCode,
    string TenantName,
    Guid? LinkedPrescriberId,
    string LinkStatus,
    string InitiatedBy,
    DateTime InvitedAt,
    DateTime? RespondedAt,
    string? PrescriberName,
    string? PrescriberPhone,
    string? PrescriberLicenseNumber);

public sealed record PharmacyDirectoryItemDto(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string? Address,
    string? Phone);

public sealed record InvitePrescriberLinkRequest(
    string Phone,
    string FullName,
    string LicenseNumber,
    string? Specialty = null,
    string? Notes = null);

public sealed record RequestPharmacyLinkRequest(string TenantCode);

public sealed record RejectPrescriberLinkRequest(string? Reason = null);

public sealed record PortalCustomerSearchItemDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string? Phone);

public sealed record PortalProductUnitDto(
    Guid Id,
    string UnitName,
    bool IsBaseUnit,
    decimal ConversionFactor = 1);

public sealed record PortalProductSearchItemDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string DispensingClass,
    Guid? DefaultUnitId,
    string? DefaultUnitName,
    IReadOnlyList<PortalProductUnitDto> Units,
    decimal StockAvailableQty = 0);

public sealed record PortalCreatePrescriptionLineRequest(
    Guid ProductId,
    Guid? ProductUnitId,
    decimal QtyPrescribed,
    string? DosageInstruction = null,
    int SortOrder = 0);

public sealed record PortalCreatePrescriptionRequest(
    Guid TenantId,
    Guid CustomerId,
    string? PatientName,
    string? PatientPhone,
    string? Notes,
    IReadOnlyList<PortalCreatePrescriptionLineRequest> Lines);

public sealed record PortalPrescriptionSummaryDto(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string PrescriptionCode,
    string Status,
    string Source,
    string? PatientName,
    string? PatientPhone,
    DateTime? SignedAt,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    int LineCount);

public sealed record PortalPrescriptionDetailDto(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string PrescriptionCode,
    string Status,
    string Source,
    Guid? CustomerId,
    string? PatientName,
    string? PatientPhone,
    DateTime? SignedAt,
    DateTime? ExpiresAt,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<PrescriptionLineDto> Lines,
    string? PosDeepLink = null);

public sealed record PortalPrescriberDashboardDto(
    int SignedThisMonth,
    int SignedTotal,
    int ActivePharmacyCount,
    IReadOnlyList<PortalPrescriberDashboardTenantRow> ByTenant);

public sealed record PortalPrescriberDashboardTenantRow(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    int SignedThisMonth,
    int SignedTotal);

public sealed record PortalPrescriptionShareDto(
    Guid PrescriptionId,
    string PrescriptionCode,
    string PosDeepLink);
