using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareCohortRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CareCohortRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<CareCohortDefinitionDto>> ListDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CohortDefRow>(
            """
            SELECT id AS Id, cohort_code AS CohortCode, display_name AS DisplayName,
                   description AS Description, tier_target AS TierTarget, status AS Status,
                   criteria::text AS CriteriaJson
            FROM pack_care.care_cohort_definition
            WHERE status <> 'retired'
            ORDER BY tier_target, cohort_code
            """);
        return rows.Select(r => new CareCohortDefinitionDto(
            r.Id, r.CohortCode, r.DisplayName, r.Description, r.TierTarget, r.Status, r.CriteriaJson)).ToList();
    }

    public async Task<IReadOnlyList<CareCohortMembershipDto>> ListMembershipsAsync(
        Guid? cohortId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MembershipRow>(
            """
            SELECT m.id AS Id, m.cohort_id AS CohortId, d.cohort_code AS CohortCode,
                   m.customer_id AS CustomerId, m.status AS Status, m.source AS Source,
                   m.assigned_at AS AssignedAt, m.notes AS Notes
            FROM pack_care.care_cohort_membership m
            INNER JOIN pack_care.care_cohort_definition d ON d.id = m.cohort_id
            WHERE m.tenant_id = @TenantId
              AND m.status = 'active'
              AND (@CohortId IS NULL OR m.cohort_id = @CohortId)
            ORDER BY m.assigned_at DESC
            LIMIT 500
            """,
            new { TenantId, CohortId = cohortId });
        return rows.Select(Map).ToList();
    }

    public async Task<CareCohortMembershipDto> AssignAsync(
        AssignCareCohortRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var cohortOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_care.care_cohort_definition
            WHERE id = @CohortId AND status = 'active'
            """,
            new { request.CohortId });
        if (cohortOk == 0)
            throw new InvalidOperationException("Không tìm thấy cohort active.");

        var customerOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM customers
            WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { request.CustomerId, TenantId });
        if (customerOk == 0)
            throw new InvalidOperationException("Không tìm thấy khách hàng.");

        var source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim();
        var row = await conn.QuerySingleAsync<MembershipRow>(
            """
            INSERT INTO pack_care.care_cohort_membership (
                tenant_id, cohort_id, customer_id, status, assigned_by, source, notes
            )
            VALUES (@TenantId, @CohortId, @CustomerId, 'active', @ActorUserId, @Source, @Notes)
            ON CONFLICT (tenant_id, cohort_id, customer_id) DO UPDATE SET
                status = 'active',
                ended_at = NULL,
                assigned_at = NOW(),
                assigned_by = COALESCE(EXCLUDED.assigned_by, pack_care.care_cohort_membership.assigned_by),
                source = EXCLUDED.source,
                notes = COALESCE(EXCLUDED.notes, pack_care.care_cohort_membership.notes),
                updated_at = NOW()
            RETURNING id AS Id, cohort_id AS CohortId,
                      (SELECT cohort_code FROM pack_care.care_cohort_definition WHERE id = cohort_id) AS CohortCode,
                      customer_id AS CustomerId, status AS Status, source AS Source,
                      assigned_at AS AssignedAt, notes AS Notes
            """,
            new
            {
                TenantId,
                request.CohortId,
                request.CustomerId,
                ActorUserId = actorUserId,
                Source = source,
                request.Notes,
            });
        return Map(row);
    }

    private static CareCohortMembershipDto Map(MembershipRow r) => new(
        r.Id,
        r.CohortId,
        r.CohortCode,
        r.CustomerId,
        r.Status,
        r.Source,
        new DateTimeOffset(DateTime.SpecifyKind(r.AssignedAt, DateTimeKind.Utc)),
        r.Notes);

    private sealed class CohortDefRow
    {
        public Guid Id { get; init; }
        public string CohortCode { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? Description { get; init; }
        public short TierTarget { get; init; }
        public string Status { get; init; } = "";
        public string CriteriaJson { get; init; } = "{}";
    }

    private sealed class MembershipRow
    {
        public Guid Id { get; init; }
        public Guid CohortId { get; init; }
        public string CohortCode { get; init; } = "";
        public Guid CustomerId { get; init; }
        public string Status { get; init; } = "";
        public string Source { get; init; } = "";
        public DateTime AssignedAt { get; init; }
        public string? Notes { get; init; }
    }
}
