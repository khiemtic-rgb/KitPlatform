using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectDoctorMembershipRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectDoctorMembershipRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<ConnectDoctorMembershipDto>> ListForClinicAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "m.clinic_tenant_id = @TenantId" };
        var parameters = new DynamicParameters(new { TenantId = CurrentTenantId });

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (string.Equals(normalized, ConnectMembershipStatuses.PendingOurApproval, StringComparison.Ordinal))
            {
                conditions.Add("m.membership_status = @PendingClinicApproval");
            }
            else
            {
                conditions.Add("m.membership_status = @Status");
                parameters.Add("Status", normalized);
            }
        }

        var sql = $"""
            SELECT
                m.id AS Id,
                m.doctor_id AS DoctorId,
                d.full_name AS DoctorFullName,
                d.phone AS DoctorPhone,
                d.license_number AS DoctorLicenseNumber,
                d.specialty AS DoctorSpecialty,
                m.clinic_tenant_id AS ClinicTenantId,
                t.tenant_code AS ClinicTenantCode,
                t.tenant_name AS ClinicTenantName,
                m.membership_role AS MembershipRole,
                CASE
                    WHEN m.membership_status = @PendingClinicApproval THEN @PendingOurApproval
                    ELSE m.membership_status
                END AS MembershipStatus,
                m.initiated_by AS InitiatedBy,
                m.notes AS Notes,
                m.invited_at AS InvitedAt,
                m.responded_at AS RespondedAt,
                m.created_at AS CreatedAt
            FROM pack_connect.doctor_memberships m
            INNER JOIN pack_connect.doctors d ON d.id = m.doctor_id AND d.deleted_at IS NULL
            INNER JOIN public.tenants t ON t.id = m.clinic_tenant_id
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY m.updated_at DESC
            LIMIT 200
            """;

        parameters.Add("PendingClinicApproval", ConnectMembershipStatuses.PendingClinicApproval);
        parameters.Add("PendingOurApproval", ConnectMembershipStatuses.PendingOurApproval);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectDoctorMembershipDto>(sql, parameters)).ToList();
    }

    public Task<IReadOnlyList<ConnectDoctorMembershipDto>> ListPendingForClinicAsync(
        CancellationToken cancellationToken) =>
        ListForClinicAsync(ConnectMembershipStatuses.PendingOurApproval, cancellationToken);

    public async Task<ConnectDoctorMembershipDto?> GetViewAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        var all = await ListForClinicAsync(null, cancellationToken);
        return all.FirstOrDefault(x => x.Id == membershipId);
    }

    public async Task<MembershipRow?> GetRawAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                doctor_id AS DoctorId,
                clinic_tenant_id AS ClinicTenantId,
                membership_status AS MembershipStatus,
                initiated_by AS InitiatedBy
            FROM pack_connect.doctor_memberships
            WHERE id = @Id
              AND clinic_tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MembershipRow>(
            sql,
            new { Id = membershipId, TenantId = CurrentTenantId });
    }

    public async Task<Guid> UpsertDoctorAsync(
        string fullName,
        string phone,
        string? licenseNumber,
        string? specialty,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT id FROM pack_connect.doctors
            WHERE phone = @Phone AND deleted_at IS NULL
            LIMIT 1
            """,
            new { Phone = phone });

        if (existing is Guid id)
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_connect.doctors
                SET full_name = @FullName,
                    license_number = COALESCE(@LicenseNumber, license_number),
                    specialty = COALESCE(@Specialty, specialty),
                    updated_at = NOW()
                WHERE id = @Id
                """,
                new
                {
                    Id = id,
                    FullName = fullName,
                    LicenseNumber = licenseNumber,
                    Specialty = specialty,
                });
            return id;
        }

        return await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_connect.doctors (
                full_name, phone, license_number, specialty, status
            )
            VALUES (
                @FullName, @Phone, @LicenseNumber, @Specialty, @Status
            )
            RETURNING id
            """,
            new
            {
                FullName = fullName,
                Phone = phone,
                LicenseNumber = licenseNumber,
                Specialty = specialty,
                Status = ConnectDoctorStatuses.Active,
            });
    }

    public async Task<Guid> UpsertClinicInviteAsync(
        Guid doctorId,
        string membershipRole,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.doctor_memberships (
                doctor_id, clinic_tenant_id, membership_role,
                membership_status, initiated_by, notes, invited_at
            )
            VALUES (
                @DoctorId, @ClinicId, @Role,
                @Status, @InitiatedBy, @Notes, NOW()
            )
            ON CONFLICT (doctor_id, clinic_tenant_id) DO UPDATE SET
                membership_role = EXCLUDED.membership_role,
                membership_status = EXCLUDED.membership_status,
                initiated_by = EXCLUDED.initiated_by,
                notes = COALESCE(EXCLUDED.notes, pack_connect.doctor_memberships.notes),
                invited_at = NOW(),
                responded_at = NULL,
                responded_by = NULL,
                revoked_at = NULL,
                revoked_by = NULL,
                updated_at = NOW()
            WHERE pack_connect.doctor_memberships.membership_status IN (@Rejected, @Revoked, @PendingDoctorAccept)
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var id = await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            DoctorId = doctorId,
            ClinicId = CurrentTenantId,
            Role = membershipRole,
            Status = ConnectMembershipStatuses.PendingDoctorAccept,
            InitiatedBy = ConnectMembershipInitiators.Clinic,
            Notes = notes?.Trim(),
            Rejected = ConnectMembershipStatuses.Rejected,
            Revoked = ConnectMembershipStatuses.Revoked,
            PendingDoctorAccept = ConnectMembershipStatuses.PendingDoctorAccept,
        });

        if (id is null)
        {
            var existing = await conn.QuerySingleOrDefaultAsync<Guid?>(
                """
                SELECT id FROM pack_connect.doctor_memberships
                WHERE doctor_id = @DoctorId AND clinic_tenant_id = @ClinicId
                """,
                new { DoctorId = doctorId, ClinicId = CurrentTenantId });
            if (existing is null)
                throw new InvalidOperationException("Không tạo được membership bác sĩ.");
            throw new InvalidOperationException("Bác sĩ đã có membership với phòng khám này.");
        }

        return id.Value;
    }

    public async Task<bool> UpdateStatusAsync(
        Guid membershipId,
        string expectedStatus,
        string nextStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.doctor_memberships
            SET membership_status = @NextStatus,
                responded_at = CASE WHEN @NextStatus IN (@Active, @Rejected) THEN NOW() ELSE responded_at END,
                responded_by = CASE WHEN @NextStatus IN (@Active, @Rejected) THEN @Actor ELSE responded_by END,
                revoked_at = CASE WHEN @NextStatus = @Revoked THEN NOW() ELSE revoked_at END,
                revoked_by = CASE WHEN @NextStatus = @Revoked THEN @Actor ELSE revoked_by END,
                updated_at = NOW()
            WHERE id = @Id
              AND membership_status = @ExpectedStatus
              AND clinic_tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = membershipId,
            ExpectedStatus = expectedStatus,
            NextStatus = nextStatus,
            TenantId = CurrentTenantId,
            Actor = _tenant.UserId,
            Active = ConnectMembershipStatuses.Active,
            Rejected = ConnectMembershipStatuses.Rejected,
            Revoked = ConnectMembershipStatuses.Revoked,
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

    public async Task<IReadOnlyList<ConnectDoctorDto>> ListActiveDoctorsForClinicAsync(
        Guid clinicTenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.id AS Id,
                d.full_name AS FullName,
                d.phone AS Phone,
                d.license_number AS LicenseNumber,
                d.specialty AS Specialty,
                d.status AS Status,
                d.created_at AS CreatedAt
            FROM pack_connect.doctor_memberships m
            INNER JOIN pack_connect.doctors d ON d.id = m.doctor_id AND d.deleted_at IS NULL
            WHERE m.clinic_tenant_id = @ClinicId
              AND m.membership_status = @Active
            ORDER BY d.full_name
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectDoctorDto>(sql, new
        {
            ClinicId = clinicTenantId,
            Active = ConnectMembershipStatuses.Active,
        })).ToList();
    }

    internal sealed class MembershipRow
    {
        public Guid Id { get; init; }
        public Guid DoctorId { get; init; }
        public Guid ClinicTenantId { get; init; }
        public string MembershipStatus { get; init; } = "";
        public string InitiatedBy { get; init; } = "";
    }
}
