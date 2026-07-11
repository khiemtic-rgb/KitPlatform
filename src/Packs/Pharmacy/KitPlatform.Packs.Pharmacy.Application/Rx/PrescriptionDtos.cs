namespace KitPlatform.Packs.Pharmacy.Rx;

public static class PrescriptionStatuses
{
    public const string Draft = "draft";
    public const string PendingVerification = "pending_verification";
    public const string Verified = "verified";
    public const string Signed = "signed";
    public const string PartiallyDispensed = "partially_dispensed";
    public const string Dispensed = "dispensed";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
}

public sealed record LinkedPrescriberDto(
    Guid Id,
    string FullName,
    string? LicenseNumber,
    string? Phone,
    string? Specialty,
    short Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateLinkedPrescriberRequest(
    string FullName,
    string LicenseNumber,
    string? Phone = null,
    string? Specialty = null,
    string? Notes = null);

public sealed record UpdateLinkedPrescriberRequest(
    string FullName,
    string LicenseNumber,
    string? Phone = null,
    string? Specialty = null,
    short Status = 1,
    string? Notes = null);

public sealed record PrescriptionLineDto(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid? ProductUnitId,
    string? UnitName,
    string LineDispensingClass,
    decimal QtyPrescribed,
    decimal QtyDispensed,
    decimal QtyRemaining,
    string? DosageInstruction,
    int SortOrder);

public sealed record PrescriptionAttachmentDto(
    Guid Id,
    string FileUrl,
    string? FileName,
    Guid? UploadedBy,
    DateTime CreatedAt);

public sealed record PrescriptionListItemDto(
    Guid Id,
    string PrescriptionCode,
    Guid? BranchId,
    Guid LinkedPrescriberId,
    string PrescriberName,
    Guid? CustomerId,
    string? PatientName,
    string? PatientPhone,
    string Status,
    string Source,
    DateTime? VerifiedAt,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    int LineCount,
    decimal QtyRemaining);

public sealed record PrescriptionDetailDto(
    Guid Id,
    string PrescriptionCode,
    Guid? BranchId,
    Guid LinkedPrescriberId,
    string PrescriberName,
    Guid? CustomerId,
    string? PatientName,
    string? PatientPhone,
    string Status,
    string Source,
    string? VerificationMethod,
    Guid? VerifiedBy,
    DateTime? VerifiedAt,
    DateTime? SignedAt,
    DateTime? ExpiresAt,
    DateTime? DispensedAt,
    string? Notes,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CancelledAt,
    IReadOnlyList<PrescriptionLineDto> Lines,
    IReadOnlyList<PrescriptionAttachmentDto> Attachments);

public sealed record CreatePrescriptionLineRequest(
    Guid ProductId,
    Guid? ProductUnitId,
    decimal QtyPrescribed,
    string? DosageInstruction = null,
    int SortOrder = 0);

public sealed record CreatePrescriptionRequest(
    Guid? BranchId,
    Guid LinkedPrescriberId,
    Guid? CustomerId,
    string? PatientName,
    string? PatientPhone,
    string Source = "staff_entry",
    string? Notes = null,
    IReadOnlyList<CreatePrescriptionLineRequest>? Lines = null);

public sealed record UpdatePrescriptionRequest(
    Guid? BranchId,
    Guid LinkedPrescriberId,
    Guid? CustomerId,
    string? PatientName,
    string? PatientPhone,
    string Source = "staff_entry",
    string? Notes = null,
    IReadOnlyList<CreatePrescriptionLineRequest>? Lines = null);

public sealed record VerifyPrescriptionRequest(
    string VerificationMethod = "manual_check",
    DateTime? SignedAt = null);

public sealed record CancelPrescriptionRequest(string? Reason = null);

public sealed record AddPrescriptionAttachmentRequest(
    string FileUrl,
    string? FileName = null);

public sealed record PrescriptionListFilter(
    string? Status = null,
    string? PhoneSearch = null,
    int Page = 1,
    int PageSize = 50);

public sealed record PrescriptionPagedListResult(
    IReadOnlyList<PrescriptionListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record PrescriptionPosLoadLineDto(
    Guid PrescriptionLineId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid? ProductUnitId,
    string? UnitName,
    decimal UnitPrice,
    decimal QtyPrescribed,
    decimal QtyDispensed,
    decimal QtyRemaining,
    decimal StockAvailable,
    string LineDispensingClass,
    string? DosageInstruction);

public sealed record PrescriptionPosLoadDto(
    Guid Id,
    string PrescriptionCode,
    string Status,
    Guid? BranchId,
    Guid LinkedPrescriberId,
    string PrescriberName,
    Guid? CustomerId,
    string? PatientName,
    string? PatientPhone,
    DateTime? VerifiedAt,
    DateTime? ExpiresAt,
    IReadOnlyList<PrescriptionPosLoadLineDto> Lines);

public sealed record PrescriptionDispenseSaleItem(
    Guid SalesOrderItemId,
    Guid PrescriptionLineId,
    decimal Quantity);

/// <summary>B5 — Dashboard NT (top BS, SLA, chờ cấp phát).</summary>
public sealed record TenantRxDashboardDto(
    int SignedThisMonth,
    int SignedTotal,
    int PendingDispense,
    int OverduePendingDispense,
    int PendingVerification,
    int PendingLinkApprovals,
    int ActivePrescriberLinks,
    int PortalSignedThisMonth,
    int StaffSignedThisMonth,
    double? AvgHoursToDispense,
    IReadOnlyList<TenantRxTopPrescriberDto> TopPrescribers,
    IReadOnlyList<TenantRxPendingItemDto> RecentPendingDispense);

public sealed record TenantRxTopPrescriberDto(
    Guid? PrescriberId,
    string PrescriberName,
    string? Phone,
    int SignedThisMonth,
    int SignedTotal);

public sealed record TenantRxPendingItemDto(
    Guid Id,
    string PrescriptionCode,
    string? PrescriberName,
    string? PatientName,
    string Status,
    DateTime? SignedAt,
    double? HoursWaiting);
