using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Data;
using Npgsql;

namespace KitPlatform.Infrastructure.Success;

internal sealed class OwnerStaffTodayRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public OwnerStaffTodayRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<(Guid BranchId, string BranchName)>> ListActiveBranchesAsync(
        BranchAccessScope scope,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid BranchId, string BranchName)>(
            """
            SELECT b.id AS BranchId, b.branch_name AS BranchName
            FROM branches b
            WHERE b.tenant_id = @TenantId
              AND b.deleted_at IS NULL
              AND b.status = 1
              AND (@Unrestricted OR b.id = ANY(@AllowedBranches))
            ORDER BY b.branch_name
            """,
            new { TenantId, scope.Unrestricted, AllowedBranches = scope.BranchIds.ToArray() });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<StaffChecklistItemSample>> ListChecklistItemsAsync(
        DateOnly businessDate,
        BranchAccessScope scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var rows = await conn.QueryAsync<StaffChecklistItemSample>(
                """
                SELECT
                    r.branch_id AS BranchId,
                    r.id AS RunId,
                    r.kind AS Kind,
                    r.status AS RunStatus,
                    i.is_required AS IsRequired,
                    i.is_checked AS IsChecked,
                    COALESCE(e.full_name, u.username) AS CheckedByName
                FROM success_shift_checklist_run r
                INNER JOIN success_shift_checklist_run_item i ON i.run_id = r.id
                LEFT JOIN users u ON u.id = i.checked_by_user_id AND u.tenant_id = r.tenant_id
                LEFT JOIN employees e ON e.id = u.employee_id AND e.tenant_id = r.tenant_id
                WHERE r.tenant_id = @TenantId
                  AND r.business_date = @BusinessDate
                  AND (@Unrestricted OR r.branch_id = ANY(@AllowedBranches))
                """,
                new
                {
                    TenantId,
                    BusinessDate = businessDate,
                    scope.Unrestricted,
                    AllowedBranches = scope.BranchIds.ToArray(),
                });
            return rows.AsList();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<StaffOpenTaskSample>> ListOpenTasksAsync(
        BranchAccessScope scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var rows = await conn.QueryAsync<StaffOpenTaskSample>(
                """
                SELECT
                    t.assignee_employee_id AS EmployeeId,
                    e.full_name AS AssigneeName,
                    t.due_at AS DueAt,
                    t.status AS Status
                FROM pack_learning.operational_task t
                INNER JOIN employees e ON e.id = t.assignee_employee_id AND e.tenant_id = t.tenant_id
                WHERE t.tenant_id = @TenantId
                  AND t.status IN ('open', 'in_progress')
                  AND (@Unrestricted OR t.branch_id IS NULL OR t.branch_id = ANY(@AllowedBranches))
                """,
                new { TenantId, scope.Unrestricted, AllowedBranches = scope.BranchIds.ToArray() });
            return rows.AsList();
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            return [];
        }
    }

    public async Task<int> CountActiveEmployeesAsync(BranchAccessScope scope, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(*)::int
            FROM employees e
            WHERE {ActiveEmployeeFilter}
            """,
            new { TenantId, scope.Unrestricted, AllowedBranches = scope.BranchIds.ToArray() });
    }

    /// <summary>Null when Learning tables are not installed on this database.</summary>
    public async Task<int?> CountEmployeesWithoutCredentialAsync(
        BranchAccessScope scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            return await conn.ExecuteScalarAsync<int>(
                $"""
                SELECT COUNT(*)::int
                FROM employees e
                WHERE {ActiveEmployeeFilter}
                  AND NOT EXISTS (
                      SELECT 1 FROM pack_learning.credential c
                      WHERE c.tenant_id = e.tenant_id AND c.employee_id = e.id
                  )
                """,
                new { TenantId, scope.Unrestricted, AllowedBranches = scope.BranchIds.ToArray() });
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            return null;
        }
    }

    private const string ActiveEmployeeFilter = """
        e.tenant_id = @TenantId
          AND e.deleted_at IS NULL
          AND e.status = 1
          AND (
                @Unrestricted
                OR EXISTS (
                    SELECT 1 FROM employee_branches eb
                    WHERE eb.employee_id = e.id AND eb.branch_id = ANY(@AllowedBranches)
                )
              )
        """;
}
