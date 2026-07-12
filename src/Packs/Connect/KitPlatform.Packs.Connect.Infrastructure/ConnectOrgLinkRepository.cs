using System.Data;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectOrgLinkRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectOrgLinkRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public Guid CurrentTenantId => TenantId;

    public async Task<IReadOnlyList<ConnectOrgLinkDto>> ListForTenantAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "(l.initiator_tenant_id = @TenantId OR l.partner_tenant_id = @TenantId)",
        };
        var parameters = new DynamicParameters(new { TenantId });
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            // pending_our_approval is POV-only (DB stores pending_partner_accept for invitee).
            if (string.Equals(normalized, ConnectOrgLinkStatuses.PendingOurApproval, StringComparison.Ordinal))
            {
                conditions.Add("l.partner_tenant_id = @TenantId");
                conditions.Add("l.link_status = @PendingPartnerAccept");
            }
            else if (string.Equals(normalized, ConnectOrgLinkStatuses.PendingPartnerAccept, StringComparison.Ordinal))
            {
                conditions.Add("l.initiator_tenant_id = @TenantId");
                conditions.Add("l.link_status = @PendingPartnerAccept");
            }
            else
            {
                conditions.Add("l.link_status = @Status");
                parameters.Add("Status", normalized);
            }
        }

        var sql = $"""
            SELECT
                l.id AS Id,
                CASE WHEN l.initiator_tenant_id = @TenantId THEN l.partner_tenant_id ELSE l.initiator_tenant_id END AS PartnerTenantId,
                CASE WHEN l.initiator_tenant_id = @TenantId THEN pt.tenant_code ELSE it.tenant_code END AS PartnerTenantCode,
                CASE WHEN l.initiator_tenant_id = @TenantId THEN pt.tenant_name ELSE it.tenant_name END AS PartnerTenantName,
                CASE WHEN l.initiator_tenant_id = @TenantId THEN l.initiator_org_role ELSE l.partner_org_role END AS OurOrgRole,
                CASE WHEN l.initiator_tenant_id = @TenantId THEN l.partner_org_role ELSE l.initiator_org_role END AS PartnerOrgRole,
                CASE
                    WHEN l.link_status = @PendingPartnerAccept AND l.partner_tenant_id = @TenantId THEN @PendingOurApproval
                    WHEN l.link_status = @PendingPartnerAccept AND l.initiator_tenant_id = @TenantId THEN @PendingPartnerAccept
                    ELSE l.link_status
                END AS LinkStatus,
                (l.initiator_tenant_id = @TenantId) AS WeAreInitiator,
                l.notes AS Notes,
                l.invited_at AS InvitedAt,
                l.responded_at AS RespondedAt,
                l.created_at AS CreatedAt
            FROM pack_connect.org_links l
            INNER JOIN public.tenants it ON it.id = l.initiator_tenant_id
            INNER JOIN public.tenants pt ON pt.id = l.partner_tenant_id
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY l.updated_at DESC
            LIMIT 200
            """;

        parameters.Add("PendingPartnerAccept", ConnectOrgLinkStatuses.PendingPartnerAccept);
        parameters.Add("PendingOurApproval", ConnectOrgLinkStatuses.PendingOurApproval);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectOrgLinkDto>(sql, parameters)).ToList();
    }

    public async Task<IReadOnlyList<ConnectOrgLinkDto>> ListPendingIncomingAsync(
        CancellationToken cancellationToken)
    {
        // Incoming = partner invited us (we are partner_tenant, status pending_partner_accept)
        // Presented as pending_our_approval from our POV.
        const string sql = """
            SELECT
                l.id AS Id,
                l.initiator_tenant_id AS PartnerTenantId,
                it.tenant_code AS PartnerTenantCode,
                it.tenant_name AS PartnerTenantName,
                l.partner_org_role AS OurOrgRole,
                l.initiator_org_role AS PartnerOrgRole,
                @PendingOurApproval AS LinkStatus,
                FALSE AS WeAreInitiator,
                l.notes AS Notes,
                l.invited_at AS InvitedAt,
                l.responded_at AS RespondedAt,
                l.created_at AS CreatedAt
            FROM pack_connect.org_links l
            INNER JOIN public.tenants it ON it.id = l.initiator_tenant_id
            WHERE l.partner_tenant_id = @TenantId
              AND l.link_status = @PendingPartnerAccept
            ORDER BY l.invited_at ASC
            LIMIT 100
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectOrgLinkDto>(sql, new
        {
            TenantId,
            PendingPartnerAccept = ConnectOrgLinkStatuses.PendingPartnerAccept,
            PendingOurApproval = ConnectOrgLinkStatuses.PendingOurApproval,
        })).ToList();
    }

    public async Task<IReadOnlyList<ConnectDirectoryEntryDto>> SearchDirectoryAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "t.deleted_at IS NULL",
            "t.status = 1",
            "d.discoverable = TRUE",
            "t.id <> @TenantId",
        };
        var parameters = new DynamicParameters(new { TenantId });

        if (!string.IsNullOrWhiteSpace(query))
        {
            conditions.Add("(t.tenant_code ILIKE @Search OR t.tenant_name ILIKE @Search)");
            parameters.Add("Search", $"%{query.Trim()}%");
        }

        var sql = $"""
            SELECT
                t.id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                p.org_kind AS OrgKind,
                b.address AS Address,
                b.phone AS Phone
            FROM pack_connect.directory_opt_in d
            INNER JOIN public.tenants t ON t.id = d.tenant_id
            LEFT JOIN pack_connect.org_profiles p ON p.tenant_id = t.id
            LEFT JOIN LATERAL (
                SELECT address, phone
                FROM public.branches br
                WHERE br.tenant_id = t.id
                  AND br.deleted_at IS NULL
                ORDER BY br.is_head_office DESC NULLS LAST, br.created_at ASC
                LIMIT 1
            ) b ON TRUE
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY t.tenant_name
            LIMIT 30
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ConnectDirectoryEntryDto>(sql, parameters)).ToList();
    }

    public async Task<(Guid Id, string TenantCode, string TenantName)?> ResolveTenantByCodeAsync(
        string tenantCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, tenant_code AS TenantCode, tenant_name AS TenantName
            FROM public.tenants
            WHERE tenant_code = @TenantCode
              AND deleted_at IS NULL
              AND status = 1
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<(Guid Id, string TenantCode, string TenantName)>(
            sql,
            new { TenantCode = tenantCode.Trim() });
        return row.Id == Guid.Empty ? null : row;
    }

    public async Task<ConnectOrgLinkDto?> GetViewAsync(Guid linkId, CancellationToken cancellationToken)
    {
        var all = await ListForTenantAsync(null, cancellationToken);
        return all.FirstOrDefault(x => x.Id == linkId);
    }

    public async Task<Guid> UpsertInviteAsync(
        Guid partnerTenantId,
        string initiatorOrgRole,
        string partnerOrgRole,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.org_links (
                initiator_tenant_id, partner_tenant_id,
                initiator_org_role, partner_org_role,
                link_status, notes, invited_at
            )
            VALUES (
                @InitiatorId, @PartnerId,
                @InitiatorRole, @PartnerRole,
                @Status, @Notes, NOW()
            )
            ON CONFLICT (initiator_tenant_id, partner_tenant_id) DO UPDATE SET
                initiator_org_role = EXCLUDED.initiator_org_role,
                partner_org_role = EXCLUDED.partner_org_role,
                link_status = EXCLUDED.link_status,
                notes = COALESCE(EXCLUDED.notes, pack_connect.org_links.notes),
                invited_at = NOW(),
                responded_at = NULL,
                responded_by = NULL,
                revoked_at = NULL,
                revoked_by = NULL,
                updated_at = NOW()
            WHERE pack_connect.org_links.link_status IN (@Rejected, @Revoked, @PendingPartnerAccept)
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var id = await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            InitiatorId = TenantId,
            PartnerId = partnerTenantId,
            InitiatorRole = initiatorOrgRole,
            PartnerRole = partnerOrgRole,
            Status = ConnectOrgLinkStatuses.PendingPartnerAccept,
            Notes = notes?.Trim(),
            Rejected = ConnectOrgLinkStatuses.Rejected,
            Revoked = ConnectOrgLinkStatuses.Revoked,
            PendingPartnerAccept = ConnectOrgLinkStatuses.PendingPartnerAccept,
        });

        if (id is null)
        {
            // Conflict with active/pending that cannot be overwritten — fetch existing
            var existing = await conn.QuerySingleOrDefaultAsync<Guid?>(
                """
                SELECT id FROM pack_connect.org_links
                WHERE initiator_tenant_id = @InitiatorId AND partner_tenant_id = @PartnerId
                """,
                new { InitiatorId = TenantId, PartnerId = partnerTenantId });
            if (existing is null)
                throw new InvalidOperationException("Không tạo được liên kết tổ chức.");
            throw new InvalidOperationException("Liên kết với đối tác này đã tồn tại.");
        }

        return id.Value;
    }

    public async Task<bool> UpdateStatusAsync(
        Guid linkId,
        string expectedStatus,
        string nextStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.org_links
            SET link_status = @NextStatus,
                responded_at = CASE WHEN @NextStatus IN (@Active, @Rejected) THEN NOW() ELSE responded_at END,
                responded_by = CASE WHEN @NextStatus IN (@Active, @Rejected) THEN @Actor ELSE responded_by END,
                revoked_at = CASE WHEN @NextStatus = @Revoked THEN NOW() ELSE revoked_at END,
                revoked_by = CASE WHEN @NextStatus = @Revoked THEN @Actor ELSE revoked_by END,
                updated_at = NOW()
            WHERE id = @LinkId
              AND link_status = @ExpectedStatus
              AND (initiator_tenant_id = @TenantId OR partner_tenant_id = @TenantId)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            LinkId = linkId,
            ExpectedStatus = expectedStatus,
            NextStatus = nextStatus,
            TenantId,
            Actor = _tenant.UserId,
            Active = ConnectOrgLinkStatuses.Active,
            Rejected = ConnectOrgLinkStatuses.Rejected,
            Revoked = ConnectOrgLinkStatuses.Revoked,
        });
        return rows > 0;
    }

    public async Task<OrgLinkRow?> GetRawAsync(Guid linkId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                initiator_tenant_id AS InitiatorTenantId,
                partner_tenant_id AS PartnerTenantId,
                link_status AS LinkStatus
            FROM pack_connect.org_links
            WHERE id = @LinkId
              AND (initiator_tenant_id = @TenantId OR partner_tenant_id = @TenantId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OrgLinkRow>(sql, new { LinkId = linkId, TenantId });
    }

    internal sealed class OrgLinkRow
    {
        public Guid Id { get; init; }
        public Guid InitiatorTenantId { get; init; }
        public Guid PartnerTenantId { get; init; }
        public string LinkStatus { get; init; } = "";
    }
}
