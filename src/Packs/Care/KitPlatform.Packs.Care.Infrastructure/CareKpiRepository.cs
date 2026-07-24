using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareKpiRepository
{
    private readonly IDbConnectionFactory _db;

    public CareKpiRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<CareKpiDefinitionDto>> ListDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<KpiRow>(
            """
            SELECT id AS Id, kpi_code AS KpiCode, display_name AS DisplayName,
                   description AS Description, tier_target AS TierTarget,
                   problem_index AS ProblemIndex, unit AS Unit, status AS Status,
                   compute_hints::text AS ComputeHintsJson, runnable_when AS RunnableWhen
            FROM pack_care.care_kpi_definition
            WHERE status <> 'retired'
            ORDER BY problem_index NULLS LAST, kpi_code
            """);
        return rows.Select(r => new CareKpiDefinitionDto(
            r.Id,
            r.KpiCode,
            r.DisplayName,
            r.Description,
            r.TierTarget,
            r.ProblemIndex,
            r.Unit,
            r.Status,
            r.ComputeHintsJson,
            r.RunnableWhen)).ToList();
    }

    private sealed class KpiRow
    {
        public Guid Id { get; init; }
        public string KpiCode { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string? Description { get; init; }
        public short TierTarget { get; init; }
        public short? ProblemIndex { get; init; }
        public string Unit { get; init; } = "";
        public string Status { get; init; } = "";
        public string ComputeHintsJson { get; init; } = "{}";
        public string RunnableWhen { get; init; } = "";
    }
}
