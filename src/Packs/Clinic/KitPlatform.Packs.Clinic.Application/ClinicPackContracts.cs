namespace KitPlatform.Packs.Clinic;

public static class ClinicAppointmentStatuses
{
    public const string Scheduled = "scheduled";
    public const string CheckedIn = "checked_in";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string NoShow = "no_show";
}

/// <summary>CL3 encounter channel. remote_video reserved for WebRTC (B).</summary>
public static class ClinicEncounterModalities
{
    public const string InPerson = "in_person";
    public const string RemoteAsync = "remote_async";
    public const string RemoteVideo = "remote_video";

    public static bool IsKnown(string? value) =>
        value is InPerson or RemoteAsync or RemoteVideo;

    public static bool IsRemote(string? value) =>
        value is RemoteAsync or RemoteVideo;

    public static string Normalize(string? value) =>
        IsKnown(value) ? value! : InPerson;
}

public static class EncounterSessionStatuses
{
    public const string None = "none";
    public const string Waiting = "waiting";
    public const string Live = "live";
    public const string Ended = "ended";
    public const string Failed = "failed";
}

public sealed record ClinicAppointmentDto(
    Guid Id,
    Guid CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    Guid? ProviderId,
    string? ProviderDisplayName,
    Guid? BranchId,
    DateTime AppointmentAt,
    int DurationMinutes,
    string AppointmentStatus,
    string EncounterModality,
    string? Reason,
    string? Notes,
    DateTime CreatedAt);

public sealed record CreateClinicAppointmentRequest(
    Guid CustomerId,
    Guid? ProviderId,
    Guid? BranchId,
    DateTimeOffset AppointmentAt,
    int DurationMinutes = 30,
    string? Reason = null,
    string? Notes = null,
    string EncounterModality = ClinicEncounterModalities.InPerson);

public sealed record UpdateClinicAppointmentStatusRequest(string AppointmentStatus);

public sealed record CrmLeadDto(
    Guid Id,
    string LeadCode,
    string FullName,
    string? Phone,
    string? Email,
    string LeadStatus,
    string Source,
    DateTimeOffset CreatedAt);

public sealed record CreateCrmLeadRequest(
    string LeadCode,
    string FullName,
    string? Phone = null,
    string? Email = null,
    string Source = "walk_in",
    Guid? CustomerId = null,
    string? Notes = null);

public interface IClinicAppointmentService
{
    Task<IReadOnlyList<ClinicAppointmentDto>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<ClinicAppointmentDto?> GetAsync(Guid appointmentId, CancellationToken cancellationToken = default);

    Task<ClinicAppointmentDto> CreateAsync(
        CreateClinicAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicAppointmentDto?> UpdateStatusAsync(
        Guid appointmentId,
        string appointmentStatus,
        CancellationToken cancellationToken = default);

    /// <summary>scheduled → checked_in and open a visit (idempotent if visit already open).</summary>
    Task<ClinicVisitDto> CheckInAsync(Guid appointmentId, CancellationToken cancellationToken = default);
}

public interface ICrmLeadService
{
    Task<IReadOnlyList<CrmLeadDto>> ListAsync(
        string? status,
        CancellationToken cancellationToken = default);

    Task<CrmLeadDto> CreateAsync(
        CreateCrmLeadRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ClinicVisitDto(
    Guid Id,
    Guid? AppointmentId,
    Guid CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    Guid? ProviderId,
    string? ProviderDisplayName,
    string VisitStatus,
    string EncounterModality,
    string? ChiefComplaint,
    string? DiagnosisSummary,
    DateTime StartedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    /// <summary>Pharmacy locked from Connect referral/booking trail (null = walk-in / free choice).</summary>
    Guid? PreferredPharmacyTenantId = null,
    string? PreferredPharmacyName = null,
    string? PreferredPharmacyCode = null,
    string? ConnectSource = null);

public sealed record CreateClinicVisitRequest(
    Guid CustomerId,
    Guid? AppointmentId = null,
    Guid? ProviderId = null,
    string? ChiefComplaint = null,
    string EncounterModality = ClinicEncounterModalities.InPerson);

public sealed record UpdateClinicVisitRequest(
    string? ChiefComplaint = null,
    string? DiagnosisSummary = null,
    string? VisitStatus = null,
    Guid? ProviderId = null);

public sealed record ClinicVisitNoteDto(
    Guid Id,
    Guid VisitId,
    string NoteType,
    string NoteBody,
    Guid? AuthorUserId,
    DateTime CreatedAt);

public sealed record CreateClinicVisitNoteRequest(
    string NoteBody,
    string NoteType = "clinical");

public interface IClinicVisitService
{
    Task<IReadOnlyList<ClinicVisitDto>> ListAsync(
        Guid? customerId,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<ClinicVisitDto?> GetAsync(Guid visitId, CancellationToken cancellationToken = default);

    Task<ClinicVisitDto> CreateAsync(
        CreateClinicVisitRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicVisitDto?> UpdateAsync(
        Guid visitId,
        UpdateClinicVisitRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClinicVisitNoteDto>> ListNotesAsync(
        Guid visitId,
        CancellationToken cancellationToken = default);

    Task<ClinicVisitNoteDto> AddNoteAsync(
        Guid visitId,
        CreateClinicVisitNoteRequest request,
        CancellationToken cancellationToken = default);
}

// --- Providers (CL1.0) ---

public sealed record ClinicProviderDto(
    Guid Id,
    string ProviderCode,
    string DisplayName,
    string? Specialty,
    string? LicenseNo,
    short Status,
    Guid? ConnectDoctorId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Phone = null,
    string? Email = null,
    string? Title = null,
    string? Notes = null);

public sealed record CreateClinicProviderRequest(
    string ProviderCode,
    string DisplayName,
    string? Specialty = null,
    string? LicenseNo = null,
    short Status = 1,
    Guid? ConnectDoctorId = null,
    string? Phone = null,
    string? Email = null,
    string? Title = null,
    string? Notes = null);

public sealed record UpdateClinicProviderRequest(
    string? DisplayName = null,
    string? Specialty = null,
    string? LicenseNo = null,
    short? Status = null,
    Guid? ConnectDoctorId = null,
    bool ClearConnectDoctorId = false,
    string? Phone = null,
    string? Email = null,
    string? Title = null,
    string? Notes = null);

public sealed record UpsertClinicProviderFromConnectRequest(
    Guid ConnectDoctorId,
    string? ProviderCode = null,
    string? DisplayName = null,
    string? Specialty = null,
    string? LicenseNo = null);

public interface IClinicProviderService
{
    Task<IReadOnlyList<ClinicProviderDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ClinicProviderDto?> GetAsync(Guid providerId, CancellationToken cancellationToken = default);

    Task<ClinicProviderDto> CreateAsync(
        CreateClinicProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicProviderDto?> UpdateAsync(
        Guid providerId,
        UpdateClinicProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicProviderDto> UpsertFromConnectAsync(
        UpsertClinicProviderFromConnectRequest request,
        CancellationToken cancellationToken = default);
}

// --- Prescriptions (CL1.2) ---

public static class ClinicPrescriptionStatuses
{
    public const string Draft = "draft";
    public const string Finalized = "finalized";
    public const string Signed = "signed";
    public const string Cancelled = "cancelled";
}

public static class ClinicSignatureProviders
{
    public const string Mock = "mock";
    public const string UsbCa = "usb_ca";
}

public sealed record ClinicPrescriptionLineDto(
    Guid Id,
    string DrugName,
    string? Strength,
    decimal Quantity,
    string? Unit,
    string? DosageInstruction,
    int SortOrder);

public sealed record ClinicPrescriptionDto(
    Guid Id,
    Guid VisitId,
    Guid CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    Guid? ProviderId,
    string? ProviderDisplayName,
    string PrescriptionCode,
    string PrescriptionStatus,
    string? DiagnosisText,
    string? Notes,
    DateTime? FinalizedAt,
    string? PdfSha256,
    Guid? PharmacyTenantId,
    DateTime? SentAt,
    Guid? ConnectHandoffId,
    DateTime? SignedAt,
    string? SignatureProvider,
    DateTime CreatedAt,
    IReadOnlyList<ClinicPrescriptionLineDto> Lines);

public sealed record ClinicPrescriptionLineInput(
    string DrugName,
    string? Strength = null,
    decimal Quantity = 1,
    string? Unit = null,
    string? DosageInstruction = null);

public sealed record CreateClinicPrescriptionRequest(
    Guid VisitId,
    Guid? ProviderId = null,
    string? DiagnosisText = null,
    string? Notes = null,
    IReadOnlyList<ClinicPrescriptionLineInput>? Lines = null);

public sealed record UpdateClinicPrescriptionRequest(
    Guid? ProviderId = null,
    string? DiagnosisText = null,
    string? Notes = null,
    IReadOnlyList<ClinicPrescriptionLineInput>? Lines = null);

public sealed record SendClinicPrescriptionToPharmacyRequest(Guid PharmacyTenantId);

public sealed record ClinicSignRequest(
    Guid PrescriptionId,
    string PrescriptionCode,
    string PdfSha256,
    Guid? SignerUserId,
    string? ProviderDisplayName);

public sealed record ClinicSignatureResult(
    string SignatureAlg,
    string SignatureValue,
    string SignatureProvider,
    string? SignerCertThumbprint = null);

public interface IClinicPrescriptionSigner
{
    string ProviderCode { get; }

    Task<ClinicSignatureResult> SignAsync(
        ClinicSignRequest request,
        CancellationToken cancellationToken = default);
}

public interface IClinicPrescriptionService
{
    Task<IReadOnlyList<ClinicPrescriptionDto>> ListByVisitAsync(
        Guid visitId,
        CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto?> GetAsync(Guid prescriptionId, CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto> CreateAsync(
        CreateClinicPrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto?> UpdateAsync(
        Guid prescriptionId,
        UpdateClinicPrescriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto?> FinalizeAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto?> SignAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto?> CancelAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default);

    Task<ClinicPrescriptionDto?> SendToPharmacyAsync(
        Guid prescriptionId,
        SendClinicPrescriptionToPharmacyRequest request,
        CancellationToken cancellationToken = default);

    Task<(byte[] Pdf, string FileName, string Sha256)?> GetPdfAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default);
}

// --- Day summary (CL1.4) ---

public sealed record ClinicDaySummaryDto(
    DateOnly Date,
    int AppointmentsToday,
    int AppointmentsRemoteToday,
    int AppointmentsCheckedIn,
    int AppointmentsNoShow,
    int VisitsOpen,
    int VisitsClosed,
    int PrescriptionsDraft,
    int PrescriptionsFinalized,
    int PrescriptionsSentToPharmacy);

public interface IClinicDaySummaryService
{
    Task<ClinicDaySummaryDto> GetAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default);
}

// --- Clinic print / hours settings (S12) — tenants.settings.clinic ---

public sealed record ClinicTenantSettingsDto(
    string Name,
    string? Address,
    string? Phone,
    string? WorkingHours);

public sealed record UpdateClinicTenantSettingsRequest(
    string Name,
    string? Address = null,
    string? Phone = null,
    string? WorkingHours = null);

public interface IClinicTenantSettingsService
{
    Task<ClinicTenantSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<ClinicTenantSettingsDto> UpdateAsync(
        UpdateClinicTenantSettingsRequest request,
        CancellationToken cancellationToken = default);
}
