using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectBookingRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectBookingRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;
    public Guid CurrentUserId => _tenant.UserId;

    private const string SelectSql = """
        SELECT
            b.id AS Id,
            b.clinic_tenant_id AS ClinicTenantId,
            ct.tenant_code AS ClinicTenantCode,
            ct.tenant_name AS ClinicTenantName,
            b.pharmacy_tenant_id AS PharmacyTenantId,
            pt.tenant_code AS PharmacyTenantCode,
            pt.tenant_name AS PharmacyTenantName,
            b.referral_id AS ReferralId,
            b.doctor_id AS DoctorId,
            d.full_name AS DoctorFullName,
            b.patient_display_name AS PatientDisplayName,
            b.patient_phone AS PatientPhone,
            b.pharmacy_customer_id AS PharmacyCustomerId,
            b.scheduled_at AS ScheduledAt,
            b.duration_minutes AS DurationMinutes,
            b.booking_status AS BookingStatus,
            b.encounter_modality AS EncounterModality,
            b.notes AS Notes,
            b.notified_at AS NotifiedAt,
            b.created_at AS CreatedAt
        FROM pack_connect.bookings b
        INNER JOIN public.tenants ct ON ct.id = b.clinic_tenant_id
        LEFT JOIN public.tenants pt ON pt.id = b.pharmacy_tenant_id
        LEFT JOIN pack_connect.doctors d ON d.id = b.doctor_id
        """;

    public async Task<IReadOnlyList<ConnectBookingDto>> ListForTenantAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "(b.clinic_tenant_id = @TenantId OR b.pharmacy_tenant_id = @TenantId)",
        };
        var parameters = new DynamicParameters(new { TenantId = CurrentTenantId });
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("b.booking_status = @Status");
            parameters.Add("Status", status.Trim());
        }

        var sql = $"""
            {SelectSql}
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY b.scheduled_at DESC
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectBookingDto>(sql, parameters)).ToList();
    }

    public async Task<ConnectBookingDto?> GetViewAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE b.id = @Id
              AND (b.clinic_tenant_id = @TenantId OR b.pharmacy_tenant_id = @TenantId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ConnectBookingDto>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<BookingRow?> GetRawAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                clinic_tenant_id AS ClinicTenantId,
                pharmacy_tenant_id AS PharmacyTenantId,
                referral_id AS ReferralId,
                booking_status AS BookingStatus
            FROM pack_connect.bookings
            WHERE id = @Id
              AND (clinic_tenant_id = @TenantId OR pharmacy_tenant_id = @TenantId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<BookingRow>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<ReferralSnap?> GetReferralAsync(Guid referralId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                pharmacy_tenant_id AS PharmacyTenantId,
                clinic_tenant_id AS ClinicTenantId,
                doctor_id AS DoctorId,
                patient_display_name AS PatientDisplayName,
                patient_phone AS PatientPhone,
                pharmacy_customer_id AS PharmacyCustomerId,
                clinic_customer_id AS ClinicCustomerId,
                referral_status AS ReferralStatus
            FROM pack_connect.referrals
            WHERE id = @Id
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ReferralSnap>(sql, new { Id = referralId });
    }

    public async Task<Guid> InsertAsync(
        Guid? pharmacyTenantId,
        Guid? referralId,
        Guid? doctorId,
        Guid? pharmacyCustomerId,
        string patientName,
        string? patientPhone,
        DateTime scheduledAt,
        int durationMinutes,
        string? notes,
        string encounterModality,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.bookings (
                clinic_tenant_id, pharmacy_tenant_id, referral_id, doctor_id,
                pharmacy_customer_id, patient_display_name, patient_phone, scheduled_at, duration_minutes,
                booking_status, notes, encounter_modality, created_by
            )
            VALUES (
                @ClinicId, @PharmacyId, @ReferralId, @DoctorId,
                @CustomerId, @PatientName, @PatientPhone, @ScheduledAt, @Duration,
                @Status, @Notes, @EncounterModality, @Actor
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            ClinicId = CurrentTenantId,
            PharmacyId = pharmacyTenantId,
            ReferralId = referralId,
            DoctorId = doctorId,
            CustomerId = pharmacyCustomerId,
            PatientName = patientName,
            PatientPhone = patientPhone,
            ScheduledAt = scheduledAt,
            Duration = durationMinutes,
            Status = ConnectBookingStatuses.Proposed,
            Notes = notes,
            EncounterModality = encounterModality,
            Actor = CurrentUserId,
        });
    }

    public async Task<bool> UpdateStatusAsync(
        Guid id,
        string expectedStatus,
        string nextStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.bookings
            SET booking_status = @NextStatus,
                updated_by = @Actor,
                updated_at = NOW()
            WHERE id = @Id
              AND booking_status = @ExpectedStatus
              AND clinic_tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            ExpectedStatus = expectedStatus,
            NextStatus = nextStatus,
            TenantId = CurrentTenantId,
            Actor = CurrentUserId,
        });
        return rows > 0;
    }

    public async Task MarkNotifiedAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.bookings
            SET notified_at = NOW(), updated_at = NOW()
            WHERE id = @Id AND clinic_tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id, TenantId = CurrentTenantId });
    }

    public async Task<bool> HasActiveOrgLinkWithAsync(
        Guid partnerTenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM pack_connect.org_links
            WHERE link_status = @Active
              AND (
                    (initiator_tenant_id = @Us AND partner_tenant_id = @Them)
                 OR (initiator_tenant_id = @Them AND partner_tenant_id = @Us)
              )
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var found = await conn.QuerySingleOrDefaultAsync<int?>(sql, new
        {
            Us = CurrentTenantId,
            Them = partnerTenantId,
            Active = ConnectOrgLinkStatuses.Active,
        });
        return found is not null;
    }

    public async Task<bool> DoctorActiveAtClinicAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM pack_connect.doctor_memberships
            WHERE doctor_id = @DoctorId
              AND clinic_tenant_id = @ClinicId
              AND membership_status = @Active
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var found = await conn.QuerySingleOrDefaultAsync<int?>(sql, new
        {
            DoctorId = doctorId,
            ClinicId = CurrentTenantId,
            Active = ConnectMembershipStatuses.Active,
        });
        return found is not null;
    }

    internal sealed class BookingRow
    {
        public Guid Id { get; init; }
        public Guid ClinicTenantId { get; init; }
        public Guid? PharmacyTenantId { get; init; }
        public Guid? ReferralId { get; init; }
        public string BookingStatus { get; init; } = "";
    }

    internal sealed class ReferralSnap
    {
        public Guid Id { get; init; }
        public Guid PharmacyTenantId { get; init; }
        public Guid ClinicTenantId { get; init; }
        public Guid? DoctorId { get; init; }
        public string PatientDisplayName { get; init; } = "";
        public string? PatientPhone { get; init; }
        public Guid? PharmacyCustomerId { get; init; }
        public Guid? ClinicCustomerId { get; init; }
        public string ReferralStatus { get; init; } = "";
    }
}
