using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyDigitalMirrorRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyDigitalMirrorRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task UpsertHeartbeatAsync(
        Guid familyId,
        Guid memberId,
        string deviceId,
        string? deviceLabel,
        string? agentVersion,
        string? lastForegroundApp,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.mirror_agent_device (
                tenant_id, family_id, member_id, device_id,
                device_label, agent_version, last_heartbeat_at,
                last_foreground_app, status
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @DeviceId,
                @DeviceLabel, @AgentVersion, NOW(),
                @LastForegroundApp, 'online'
            )
            ON CONFLICT (tenant_id, family_id, device_id)
            WHERE deleted_at IS NULL
            DO UPDATE SET
                member_id = EXCLUDED.member_id,
                device_label = COALESCE(EXCLUDED.device_label, pack_family.mirror_agent_device.device_label),
                agent_version = COALESCE(EXCLUDED.agent_version, pack_family.mirror_agent_device.agent_version),
                last_heartbeat_at = NOW(),
                last_foreground_app = COALESCE(EXCLUDED.last_foreground_app, pack_family.mirror_agent_device.last_foreground_app),
                status = 'online',
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                DeviceId = deviceId,
                DeviceLabel = deviceLabel,
                AgentVersion = agentVersion,
                LastForegroundApp = lastForegroundApp,
            });
    }

    public async Task<DeviceRow?> GetLatestDeviceAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DeviceRow>(
            """
            SELECT
                id AS Id,
                member_id AS MemberId,
                device_id AS DeviceId,
                device_label AS DeviceLabel,
                agent_version AS AgentVersion,
                last_heartbeat_at AS LastHeartbeatAt,
                last_foreground_app AS LastForegroundApp,
                status AS Status
            FROM pack_family.mirror_agent_device
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND deleted_at IS NULL
            ORDER BY last_heartbeat_at DESC NULLS LAST, updated_at DESC
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
    }

    public async Task IngestUsageAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        IReadOnlyList<UsageIngestItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        foreach (var item in items)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.mirror_usage_day (
                    tenant_id, family_id, member_id, flow_date,
                    app_key, app_label, kind, seconds
                )
                VALUES (
                    @TenantId, @FamilyId, @MemberId, @FlowDate,
                    @AppKey, @AppLabel, @Kind, @Seconds
                )
                ON CONFLICT (tenant_id, family_id, member_id, flow_date, app_key, kind)
                DO UPDATE SET
                    app_label = COALESCE(EXCLUDED.app_label, pack_family.mirror_usage_day.app_label),
                    seconds = pack_family.mirror_usage_day.seconds + GREATEST(0, EXCLUDED.seconds),
                    updated_at = NOW(),
                    deleted_at = NULL
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    MemberId = memberId,
                    FlowDate = flowDate,
                    AppKey = item.AppKey,
                    AppLabel = item.AppLabel,
                    Kind = item.Kind,
                    Seconds = item.Seconds,
                },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UsageRow>> ListUsageDayAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<UsageRow>(
            """
            SELECT
                app_key AS AppKey,
                COALESCE(app_label, app_key) AS AppLabel,
                kind AS Kind,
                seconds AS Seconds
            FROM pack_family.mirror_usage_day
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND flow_date = @FlowDate
              AND deleted_at IS NULL
              AND seconds > 0
            ORDER BY seconds DESC, app_key
            LIMIT @Limit
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
                Limit = limit,
            });
        return rows.AsList();
    }

    public async Task<int> SumUsageSecondsAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(SUM(seconds), 0)
            FROM pack_family.mirror_usage_day
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND flow_date = @FlowDate
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, FlowDate = flowDate });
    }

    public async Task<IReadOnlyList<ParentNoteRow>> ListParentNotesAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ParentNoteRow>(
            """
            SELECT
                n.id AS Id,
                n.member_id AS MemberId,
                n.flow_date AS FlowDate,
                n.from_membership_id AS FromMembershipId,
                fm.display_name AS FromMemberName,
                n.tone AS Tone,
                n.body_vi AS BodyVi,
                n.created_at AS CreatedAt
            FROM pack_family.mirror_parent_note n
            INNER JOIN pack_family.membership fm
                ON fm.tenant_id = n.tenant_id
               AND fm.id = n.from_membership_id
               AND fm.deleted_at IS NULL
            WHERE n.tenant_id = @TenantId
              AND n.family_id = @FamilyId
              AND n.member_id = @MemberId
              AND n.flow_date = @FlowDate
              AND n.deleted_at IS NULL
            ORDER BY n.created_at DESC
            LIMIT 20
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, FlowDate = flowDate });
        return rows.AsList();
    }

    public async Task<int> CountParentNotesAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        Guid fromMembershipId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.mirror_parent_note
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND flow_date = @FlowDate
              AND from_membership_id = @FromMembershipId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
                FromMembershipId = fromMembershipId,
            });
    }

    public async Task<ParentNoteRow?> InsertParentNoteAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        Guid fromMembershipId,
        string tone,
        string bodyVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var id = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.mirror_parent_note (
                tenant_id, family_id, member_id, flow_date,
                from_membership_id, tone, body_vi
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @FlowDate,
                @FromMembershipId, @Tone, @BodyVi
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
                FromMembershipId = fromMembershipId,
                Tone = tone,
                BodyVi = bodyVi,
            });

        var rows = await ListParentNotesAsync(familyId, memberId, flowDate, cancellationToken);
        return rows.FirstOrDefault(n => n.Id == id);
    }

    internal sealed class DeviceRow
    {
        public Guid Id { get; init; }
        public Guid MemberId { get; init; }
        public string DeviceId { get; init; } = "";
        public string? DeviceLabel { get; init; }
        public string? AgentVersion { get; init; }
        public DateTimeOffset? LastHeartbeatAt { get; init; }
        public string? LastForegroundApp { get; init; }
        public string Status { get; init; } = "";
    }

    internal sealed record UsageIngestItem(
        string AppKey,
        string? AppLabel,
        string Kind,
        int Seconds);

    internal sealed class UsageRow
    {
        public string AppKey { get; init; } = "";
        public string AppLabel { get; init; } = "";
        public string Kind { get; init; } = "";
        public int Seconds { get; init; }
    }

    internal sealed class ParentNoteRow
    {
        public Guid Id { get; init; }
        public Guid MemberId { get; init; }
        public DateOnly FlowDate { get; init; }
        public Guid FromMembershipId { get; init; }
        public string FromMemberName { get; init; } = "";
        public string Tone { get; init; } = "";
        public string BodyVi { get; init; } = "";
        public DateTimeOffset CreatedAt { get; init; }
    }
}
