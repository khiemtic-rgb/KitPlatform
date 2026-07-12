using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectReferralRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectReferralRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;
    public Guid CurrentUserId => _tenant.UserId;

    private const string SelectSql = """
        SELECT
            r.id AS Id,
            r.pharmacy_tenant_id AS PharmacyTenantId,
            pt.tenant_code AS PharmacyTenantCode,
            pt.tenant_name AS PharmacyTenantName,
            r.clinic_tenant_id AS ClinicTenantId,
            ct.tenant_code AS ClinicTenantCode,
            ct.tenant_name AS ClinicTenantName,
            r.doctor_id AS DoctorId,
            d.full_name AS DoctorFullName,
            r.patient_display_name AS PatientDisplayName,
            r.patient_phone AS PatientPhone,
            r.pharmacy_customer_id AS PharmacyCustomerId,
            r.clinic_customer_id AS ClinicCustomerId,
            r.reason AS Reason,
            r.notes AS Notes,
            r.referral_status AS ReferralStatus,
            r.created_at AS CreatedAt,
            r.responded_at AS RespondedAt,
            r.completed_at AS CompletedAt
        FROM pack_connect.referrals r
        INNER JOIN public.tenants pt ON pt.id = r.pharmacy_tenant_id
        INNER JOIN public.tenants ct ON ct.id = r.clinic_tenant_id
        LEFT JOIN pack_connect.doctors d ON d.id = r.doctor_id
        """;

    public async Task<IReadOnlyList<ConnectReferralDto>> ListForTenantAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "(r.pharmacy_tenant_id = @TenantId OR r.clinic_tenant_id = @TenantId)",
        };
        var parameters = new DynamicParameters(new { TenantId = CurrentTenantId });
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("r.referral_status = @Status");
            parameters.Add("Status", status.Trim());
        }

        var sql = $"""
            {SelectSql}
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY r.updated_at DESC
            LIMIT 200
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectReferralDto>(sql, parameters)).ToList();
    }

    public async Task<IReadOnlyList<ConnectReferralDto>> ListInboxAsync(
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE r.clinic_tenant_id = @TenantId
              AND r.referral_status = @Pending
            ORDER BY r.created_at ASC
            LIMIT 100
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectReferralDto>(sql, new
        {
            TenantId = CurrentTenantId,
            Pending = ConnectReferralStatuses.PendingClinicAccept,
        })).ToList();
    }

    public async Task<ConnectReferralDto?> GetViewAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE r.id = @Id
              AND (r.pharmacy_tenant_id = @TenantId OR r.clinic_tenant_id = @TenantId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ConnectReferralDto>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<ReferralRow?> GetRawAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                pharmacy_tenant_id AS PharmacyTenantId,
                clinic_tenant_id AS ClinicTenantId,
                doctor_id AS DoctorId,
                referral_status AS ReferralStatus
            FROM pack_connect.referrals
            WHERE id = @Id
              AND (pharmacy_tenant_id = @TenantId OR clinic_tenant_id = @TenantId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ReferralRow>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<Guid> InsertAsync(
        Guid clinicTenantId,
        Guid? doctorId,
        Guid pharmacyCustomerId,
        string patientName,
        string? patientPhone,
        string? reason,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.referrals (
                pharmacy_tenant_id, clinic_tenant_id, doctor_id,
                pharmacy_customer_id, patient_display_name, patient_phone, reason, notes,
                referral_status, created_by
            )
            VALUES (
                @PharmacyId, @ClinicId, @DoctorId,
                @CustomerId, @PatientName, @PatientPhone, @Reason, @Notes,
                @Status, @Actor
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            PharmacyId = CurrentTenantId,
            ClinicId = clinicTenantId,
            DoctorId = doctorId,
            CustomerId = pharmacyCustomerId,
            PatientName = patientName,
            PatientPhone = patientPhone,
            Reason = reason,
            Notes = notes,
            Status = ConnectReferralStatuses.PendingClinicAccept,
            Actor = CurrentUserId,
        });
    }

    public async Task<CustomerSnap?> GetOwnCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, full_name AS FullName, phone AS Phone
            FROM public.customers
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerSnap>(sql, new
        {
            Id = customerId,
            TenantId = CurrentTenantId,
        });
    }

    public async Task SetClinicCustomerIdAsync(
        Guid referralId,
        Guid clinicCustomerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.referrals
            SET clinic_customer_id = @CustomerId,
                updated_at = NOW()
            WHERE id = @Id
              AND clinic_tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            Id = referralId,
            CustomerId = clinicCustomerId,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<bool> UpdateStatusAsync(
        Guid id,
        string expectedStatus,
        string nextStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.referrals
            SET referral_status = @NextStatus,
                responded_at = CASE
                    WHEN @NextStatus IN (@Accepted, @Rejected) THEN NOW()
                    ELSE responded_at END,
                responded_by = CASE
                    WHEN @NextStatus IN (@Accepted, @Rejected) THEN @Actor
                    ELSE responded_by END,
                completed_at = CASE WHEN @NextStatus = @Completed THEN NOW() ELSE completed_at END,
                completed_by = CASE WHEN @NextStatus = @Completed THEN @Actor ELSE completed_by END,
                cancelled_at = CASE WHEN @NextStatus = @Cancelled THEN NOW() ELSE cancelled_at END,
                cancelled_by = CASE WHEN @NextStatus = @Cancelled THEN @Actor ELSE cancelled_by END,
                updated_at = NOW()
            WHERE id = @Id
              AND referral_status = @ExpectedStatus
              AND (pharmacy_tenant_id = @TenantId OR clinic_tenant_id = @TenantId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            ExpectedStatus = expectedStatus,
            NextStatus = nextStatus,
            TenantId = CurrentTenantId,
            Actor = CurrentUserId,
            Accepted = ConnectReferralStatuses.Accepted,
            Rejected = ConnectReferralStatuses.Rejected,
            Completed = ConnectReferralStatuses.Completed,
            Cancelled = ConnectReferralStatuses.Cancelled,
        });
        return rows > 0;
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
        Guid clinicTenantId,
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
            ClinicId = clinicTenantId,
            Active = ConnectMembershipStatuses.Active,
        });
        return found is not null;
    }

    internal sealed class CustomerSnap
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = "";
        public string? Phone { get; init; }
    }

    internal sealed class ReferralRow
    {
        public Guid Id { get; init; }
        public Guid PharmacyTenantId { get; init; }
        public Guid ClinicTenantId { get; init; }
        public Guid? DoctorId { get; init; }
        public string ReferralStatus { get; init; } = "";
    }
}
