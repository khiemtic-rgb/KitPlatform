using System.Data;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Survey;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal sealed class AssessmentPartnerRepository
{
    private readonly IDbConnectionFactory _db;

    public AssessmentPartnerRepository(IDbConnectionFactory db) => _db = db;

    public sealed class PartnerRow
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string PartnerType { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string PasswordHash { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal? CommissionRatePct { get; set; }
        public string? Notes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
    }

    public sealed class PartnerStatsRow
    {
        public Guid Id { get; set; }
        public int SubmissionCount { get; set; }
        public int LeadCount { get; set; }
        public int CompletedCount { get; set; }
        public int DemoScheduledCount { get; set; }
        public int WonCount { get; set; }
        public int PendingCommissionCount { get; set; }
    }

    public async Task<IReadOnlyList<PartnerRow>> ListAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id, code AS Code, name AS Name, partner_type AS PartnerType,
                phone AS Phone, email AS Email, password_hash AS PasswordHash,
                status AS Status, commission_rate_pct AS CommissionRatePct, notes AS Notes,
                created_at AS CreatedAt, updated_at AS UpdatedAt, last_login_at AS LastLoginAt
            FROM assessment_partner
            WHERE status <> 'archived'
            ORDER BY created_at DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<PartnerRow>(sql)).ToList();
    }

    public async Task<PartnerRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id, code AS Code, name AS Name, partner_type AS PartnerType,
                phone AS Phone, email AS Email, password_hash AS PasswordHash,
                status AS Status, commission_rate_pct AS CommissionRatePct, notes AS Notes,
                created_at AS CreatedAt, updated_at AS UpdatedAt, last_login_at AS LastLoginAt
            FROM assessment_partner
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PartnerRow>(sql, new { Id = id });
    }

    public async Task<PartnerRow?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id, code AS Code, name AS Name, partner_type AS PartnerType,
                phone AS Phone, email AS Email, password_hash AS PasswordHash,
                status AS Status, commission_rate_pct AS CommissionRatePct, notes AS Notes,
                created_at AS CreatedAt, updated_at AS UpdatedAt, last_login_at AS LastLoginAt
            FROM assessment_partner
            WHERE LOWER(code) = LOWER(@Code)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PartnerRow>(sql, new { Code = code.Trim() });
    }

    public async Task<PartnerRow?> GetByLoginAsync(string login, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id, code AS Code, name AS Name, partner_type AS PartnerType,
                phone AS Phone, email AS Email, password_hash AS PasswordHash,
                status AS Status, commission_rate_pct AS CommissionRatePct, notes AS Notes,
                created_at AS CreatedAt, updated_at AS UpdatedAt, last_login_at AS LastLoginAt
            FROM assessment_partner
            WHERE status = 'active'
              AND (
                LOWER(code) = LOWER(@Login)
                OR LOWER(COALESCE(email, '')) = LOWER(@Login)
                OR COALESCE(phone, '') = @Login
              )
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PartnerRow>(sql, new { Login = login.Trim() });
    }

    public async Task<Guid> InsertAsync(
        string code,
        string name,
        string partnerType,
        string? phone,
        string? email,
        string passwordHash,
        decimal? commissionRatePct,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO assessment_partner (
                code, name, partner_type, phone, email, password_hash,
                commission_rate_pct, notes
            )
            VALUES (
                @Code, @Name, @PartnerType, @Phone, @Email, @PasswordHash,
                @CommissionRatePct, @Notes
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            PartnerType = partnerType,
            Phone = NullIfEmpty(phone),
            Email = NullIfEmpty(email),
            PasswordHash = passwordHash,
            CommissionRatePct = commissionRatePct,
            Notes = NullIfEmpty(notes),
        });
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        string partnerType,
        string? phone,
        string? email,
        string status,
        decimal? commissionRatePct,
        string? notes,
        string? passwordHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE assessment_partner SET
                name = @Name,
                partner_type = @PartnerType,
                phone = @Phone,
                email = @Email,
                status = @Status,
                commission_rate_pct = @CommissionRatePct,
                notes = @Notes,
                password_hash = COALESCE(@PasswordHash, password_hash),
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Name = name.Trim(),
            PartnerType = partnerType,
            Phone = NullIfEmpty(phone),
            Email = NullIfEmpty(email),
            Status = status,
            CommissionRatePct = commissionRatePct,
            Notes = NullIfEmpty(notes),
            PasswordHash = passwordHash,
        });
        return n > 0;
    }

    public async Task TouchLoginAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE assessment_partner
            SET last_login_at = NOW(), updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<IReadOnlyDictionary<Guid, PartnerStatsRow>> GetStatsByPartnerIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, PartnerStatsRow>();

        const string sql = """
            SELECT
                partner_id AS Id,
                COUNT(*)::int AS SubmissionCount,
                COUNT(*) FILTER (WHERE respondent_phone IS NOT NULL)::int AS LeadCount,
                COUNT(*) FILTER (WHERE status IN ('completed', 'lead_captured', 'report_ready'))::int AS CompletedCount,
                COUNT(*) FILTER (WHERE lead_pipeline_status = 'demo_scheduled')::int AS DemoScheduledCount,
                COUNT(*) FILTER (WHERE lead_pipeline_status = 'won')::int AS WonCount,
                COUNT(*) FILTER (WHERE commission_status = 'pending')::int AS PendingCommissionCount
            FROM assessment_submission
            WHERE partner_id = ANY(@Ids::uuid[])
              AND archived_at IS NULL
            GROUP BY partner_id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PartnerStatsRow>(sql, new { Ids = ids.ToArray() });
        return rows.ToDictionary(r => r.Id);
    }

    public async Task<IReadOnlyList<PartnerPortalLeadItemDto>> ListLeadsForPartnerAsync(
        Guid partnerId,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                status AS Status,
                respondent_org_name AS OrgName,
                respondent_name AS ContactName,
                respondent_phone AS Phone,
                overall_pct AS OverallPct,
                lead_pipeline_status AS LeadPipelineStatus,
                commission_status AS CommissionStatus,
                started_at AS StartedAt,
                lead_captured_at AS LeadCapturedAt
            FROM assessment_submission
            WHERE partner_id = @PartnerId
              AND archived_at IS NULL
            ORDER BY started_at DESC
            LIMIT @Limit
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PartnerLeadRow>(sql, new { PartnerId = partnerId, Limit = limit });
        return rows.Select(r => new PartnerPortalLeadItemDto(
            r.Id,
            r.Status,
            r.OrgName,
            r.ContactName,
            r.Phone,
            r.OverallPct,
            r.LeadPipelineStatus,
            r.CommissionStatus,
            r.StartedAt,
            r.LeadCapturedAt)).ToList();
    }

    private sealed class PartnerLeadRow
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "";
        public string? OrgName { get; set; }
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public decimal? OverallPct { get; set; }
        public string LeadPipelineStatus { get; set; } = "new";
        public string CommissionStatus { get; set; } = "none";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? LeadCapturedAt { get; set; }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
