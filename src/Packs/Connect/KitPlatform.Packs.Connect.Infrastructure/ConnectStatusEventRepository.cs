using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectStatusEventRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectStatusEventRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;
    public Guid CurrentUserId => _tenant.UserId;

    private const string SelectSql = """
        SELECT
            e.id AS Id,
            e.pharmacy_tenant_id AS PharmacyTenantId,
            pt.tenant_code AS PharmacyTenantCode,
            pt.tenant_name AS PharmacyTenantName,
            e.clinic_tenant_id AS ClinicTenantId,
            ct.tenant_code AS ClinicTenantCode,
            ct.tenant_name AS ClinicTenantName,
            e.event_type AS EventType,
            e.source_type AS SourceType,
            e.source_id AS SourceId,
            e.patient_display_name AS PatientDisplayName,
            e.patient_phone AS PatientPhone,
            e.summary AS Summary,
            e.event_status AS EventStatus,
            e.created_at AS CreatedAt,
            e.consumed_at AS ConsumedAt
        FROM pack_connect.status_events e
        INNER JOIN public.tenants pt ON pt.id = e.pharmacy_tenant_id
        INNER JOIN public.tenants ct ON ct.id = e.clinic_tenant_id
        """;

    public async Task<IReadOnlyList<ConnectStatusEventDto>> ListForTenantAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "(e.pharmacy_tenant_id = @TenantId OR e.clinic_tenant_id = @TenantId)",
        };
        var parameters = new DynamicParameters(new { TenantId = CurrentTenantId });
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("e.event_status = @Status");
            parameters.Add("Status", status.Trim());
        }

        var sql = $"""
            {SelectSql}
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY e.created_at DESC
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectStatusEventDto>(sql, parameters)).ToList();
    }

    public async Task<IReadOnlyList<ConnectStatusEventDto>> ListPendingForPharmacyAsync(
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE e.pharmacy_tenant_id = @TenantId
              AND e.event_status = @Pending
            ORDER BY e.created_at ASC
            LIMIT 100
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectStatusEventDto>(sql, new
        {
            TenantId = CurrentTenantId,
            Pending = ConnectStatusEventStatuses.PendingPharmacy,
        })).ToList();
    }

    public async Task<ConnectStatusEventDto?> GetViewAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE e.id = @Id
              AND (e.pharmacy_tenant_id = @TenantId OR e.clinic_tenant_id = @TenantId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ConnectStatusEventDto>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<StatusEventRow?> GetRawAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                pharmacy_tenant_id AS PharmacyTenantId,
                clinic_tenant_id AS ClinicTenantId,
                event_status AS EventStatus,
                source_type AS SourceType,
                source_id AS SourceId
            FROM pack_connect.status_events
            WHERE id = @Id
              AND (pharmacy_tenant_id = @TenantId OR clinic_tenant_id = @TenantId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<StatusEventRow>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    /// <summary>Returns new id, or null if duplicate ready event for same source.</summary>
    public async Task<Guid?> TryInsertReadyAsync(
        Guid pharmacyTenantId,
        Guid clinicTenantId,
        string sourceType,
        Guid? sourceId,
        string? patientDisplayName,
        string? patientPhone,
        string? summary,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.status_events (
                pharmacy_tenant_id, clinic_tenant_id, event_type, source_type, source_id,
                patient_display_name, patient_phone, summary, event_status, created_by
            )
            VALUES (
                @PharmacyId, @ClinicId, @EventType, @SourceType, @SourceId,
                @PatientName, @PatientPhone, @Summary, @Status, @Actor
            )
            ON CONFLICT (source_type, source_id, event_type)
                WHERE source_id IS NOT NULL AND event_type = 'ready_to_dispense'
            DO NOTHING
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            PharmacyId = pharmacyTenantId,
            ClinicId = clinicTenantId,
            EventType = ConnectStatusEventTypes.ReadyToDispense,
            SourceType = sourceType,
            SourceId = sourceId,
            PatientName = patientDisplayName,
            PatientPhone = patientPhone,
            Summary = summary,
            Status = ConnectStatusEventStatuses.PendingPharmacy,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
    }

    public async Task<bool> UpdateStatusAsync(
        Guid id,
        string fromStatus,
        string toStatus,
        CancellationToken cancellationToken)
    {
        var setConsumed = toStatus == ConnectStatusEventStatuses.Consumed
            ? ", consumed_at = NOW(), consumed_by = @Actor"
            : "";
        var setDismissed = toStatus == ConnectStatusEventStatuses.Dismissed
            ? ", dismissed_at = NOW(), dismissed_by = @Actor"
            : "";

        var sql = $"""
            UPDATE pack_connect.status_events
            SET event_status = @ToStatus,
                updated_at = NOW()
                {setConsumed}
                {setDismissed}
            WHERE id = @Id
              AND pharmacy_tenant_id = @TenantId
              AND event_status = @FromStatus
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
        return n > 0;
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

    internal sealed class StatusEventRow
    {
        public Guid Id { get; init; }
        public Guid PharmacyTenantId { get; init; }
        public Guid ClinicTenantId { get; init; }
        public string EventStatus { get; init; } = "";
        public string SourceType { get; init; } = "";
        public Guid? SourceId { get; init; }
    }
}
