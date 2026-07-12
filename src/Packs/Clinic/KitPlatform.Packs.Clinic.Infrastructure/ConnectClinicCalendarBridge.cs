using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Customers;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.Clinic;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Clinic.Infrastructure;

/// <summary>CL1.4 — Connect booking confirm → clinic_appointment (idempotent via metadata).</summary>
internal sealed class ConnectClinicCalendarBridge : IConnectClinicCalendarBridge
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IWorkspaceResolver _workspace;
    private readonly ICustomerAdminService _customers;

    public ConnectClinicCalendarBridge(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IWorkspaceResolver workspace,
        ICustomerAdminService customers)
    {
        _db = db;
        _tenant = tenant;
        _workspace = workspace;
        _customers = customers;
    }

    public async Task OnBookingConfirmedAsync(
        Guid bookingId,
        string patientDisplayName,
        string? patientPhone,
        DateTime scheduledAt,
        int durationMinutes,
        string? notes,
        Guid? pharmacyCustomerId = null,
        Guid? clinicCustomerId = null,
        string encounterModality = "in_person",
        Guid? pharmacyTenantId = null,
        Guid? referralId = null,
        CancellationToken cancellationToken = default)
    {
        if (bookingId == Guid.Empty) return;

        var workspaceId = await _workspace.ResolveWorkspaceIdAsync(
            _tenant.TenantId,
            _tenant.WorkspaceId,
            ClinicPackDefinition.PackCode,
            cancellationToken);
        if (workspaceId is null) return; // Clinic pack workspace not provisioned

        var existing = await FindByBookingIdAsync(bookingId, cancellationToken);
        if (existing is not null) return;

        // Prefer BN already provisioned on Accept (clinic_customer_id)
        var customerId = clinicCustomerId is Guid cid && cid != Guid.Empty
            ? cid
            : await ResolveCustomerAsync(patientDisplayName, patientPhone, cancellationToken);
        if (customerId is null) return;

        var duration = durationMinutes is < 5 or > 480 ? 30 : durationMinutes;
        var reason = ExtractReason(notes) ?? "Connect booking";
        var modality = ClinicEncounterModalities.Normalize(encounterModality);
        await CreateWithMetadataAsync(
            workspaceId.Value,
            customerId.Value,
            scheduledAt,
            duration,
            reason,
            notes,
            bookingId,
            pharmacyCustomerId,
            clinicCustomerId ?? customerId,
            modality,
            pharmacyTenantId,
            referralId,
            cancellationToken);
    }

    public Task OnBookingCancelledAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        SyncStatusAsync(bookingId, ClinicAppointmentStatuses.Cancelled, cancellationToken);

    public Task OnBookingNoShowAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        SyncStatusAsync(bookingId, ClinicAppointmentStatuses.NoShow, cancellationToken);

    public Task OnBookingCompletedAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        SyncStatusAsync(bookingId, ClinicAppointmentStatuses.Completed, cancellationToken);

    private async Task SyncStatusAsync(
        Guid bookingId,
        string toStatus,
        CancellationToken cancellationToken)
    {
        var id = await FindByBookingIdAsync(bookingId, cancellationToken);
        if (id is null) return;

        const string sql = """
            UPDATE pack_clinic.clinic_appointment
            SET appointment_status = @ToStatus,
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
              AND appointment_status IN ('scheduled', 'checked_in')
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            Id = id.Value,
            TenantId = _tenant.TenantId,
            ToStatus = toStatus,
        });
    }

    private async Task<Guid?> FindByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM pack_clinic.clinic_appointment
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND metadata->>'connect_booking_id' = @BookingId
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            TenantId = _tenant.TenantId,
            BookingId = bookingId.ToString(),
        });
    }

    private async Task CreateWithMetadataAsync(
        Guid workspaceId,
        Guid customerId,
        DateTime scheduledAt,
        int durationMinutes,
        string? reason,
        string? notes,
        Guid bookingId,
        Guid? pharmacyCustomerId,
        Guid? clinicCustomerId,
        string encounterModality,
        Guid? pharmacyTenantId,
        Guid? referralId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_clinic.clinic_appointment (
                tenant_id, workspace_id, customer_id, provider_id, branch_id,
                appointment_at, duration_minutes, reason, notes, encounter_modality, metadata
            )
            VALUES (
                @TenantId, @WorkspaceId, @CustomerId, NULL, NULL,
                @AppointmentAt, @DurationMinutes, @Reason, @Notes, @EncounterModality,
                jsonb_strip_nulls(jsonb_build_object(
                    'connect_booking_id', @BookingId,
                    'pharmacy_customer_id', @PharmacyCustomerId,
                    'clinic_customer_id', @ClinicCustomerId,
                    'pharmacy_tenant_id', @PharmacyTenantId,
                    'connect_referral_id', @ReferralId
                ))
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            TenantId = _tenant.TenantId,
            WorkspaceId = workspaceId,
            CustomerId = customerId,
            AppointmentAt = scheduledAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc)
                : scheduledAt.ToUniversalTime(),
            DurationMinutes = durationMinutes,
            Reason = reason,
            Notes = notes,
            EncounterModality = encounterModality,
            BookingId = bookingId.ToString(),
            PharmacyCustomerId = pharmacyCustomerId?.ToString(),
            ClinicCustomerId = clinicCustomerId?.ToString(),
            PharmacyTenantId = pharmacyTenantId?.ToString(),
            ReferralId = referralId?.ToString(),
        });
    }

    private static string? ExtractReason(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        const string prefix = "Referral:";
        var trimmed = notes.Trim();
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var body = trimmed[prefix.Length..].Trim();
            return string.IsNullOrEmpty(body) ? null : body;
        }
        return trimmed.Length <= 120 ? trimmed : trimmed[..120];
    }

    private async Task<Guid?> ResolveCustomerAsync(
        string patientDisplayName,
        string? patientPhone,
        CancellationToken cancellationToken)
    {
        var phone = NormalizePhone(patientPhone);
        if (!string.IsNullOrEmpty(phone))
        {
            var found = await _customers.ListAsync(phone, 1, 5, cancellationToken);
            var hit = found.Items.FirstOrDefault(c =>
                NormalizePhone(c.Phone) == phone);
            if (hit is not null) return hit.Id;
        }

        var name = patientDisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2) return null;

        // Prefer matching existing by name if no phone
        if (string.IsNullOrEmpty(phone))
        {
            var byName = await _customers.ListAsync(name, 1, 5, cancellationToken);
            var hit = byName.Items.FirstOrDefault(c =>
                string.Equals(c.FullName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit.Id;
            return null; // avoid creating customer without phone (party_identifier constraints)
        }

        try
        {
            var created = await _customers.CreateAsync(
                new CreateCustomerRequest(name, phone),
                cancellationToken);
            return created.Id;
        }
        catch (InvalidOperationException)
        {
            var again = await _customers.ListAsync(phone, 1, 5, cancellationToken);
            return again.Items.FirstOrDefault(c => NormalizePhone(c.Phone) == phone)?.Id;
        }
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 9 ? digits : null;
    }
}
