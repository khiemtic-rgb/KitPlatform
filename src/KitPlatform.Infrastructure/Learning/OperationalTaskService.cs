using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Learning;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Learning;

public sealed class OperationalTaskService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IBranchAccessService _branchAccess;
    private readonly IAuditLogService? _audit;

    public OperationalTaskService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IBranchAccessService branchAccess,
        IAuditLogService? audit = null)
    {
        _db = db;
        _tenant = tenant;
        _branchAccess = branchAccess;
        _audit = audit;
    }

    public async Task<IReadOnlyList<OperationalTaskDto>> ListAsync(
        bool canManage,
        Guid? branchId,
        Guid? salesShiftId,
        CancellationToken cancellationToken,
        bool overdueOnly = false)
    {
        if (branchId is Guid branch)
            await _branchAccess.EnsureBranchAccessAsync(branch, cancellationToken);

        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<OperationalTaskDto>(
            """
            SELECT
                t.id AS Id,
                t.branch_id AS BranchId,
                t.sales_shift_id AS SalesShiftId,
                t.assignee_employee_id AS AssigneeEmployeeId,
                e.full_name AS AssigneeName,
                t.title AS Title,
                t.due_at AS DueAt,
                t.status AS Status,
                t.evidence_note AS EvidenceNote,
                t.created_by_user_id AS CreatedByUserId,
                t.completed_at AS CompletedAt,
                t.created_at AS CreatedAt,
                FALSE AS IsOverdue
            FROM pack_learning.operational_task t
            INNER JOIN employees e ON e.id = t.assignee_employee_id
            WHERE t.tenant_id = @TenantId
              AND (NOT @OverdueOnly OR (t.due_at IS NOT NULL AND t.status IN ('open', 'in_progress')))
              AND (@BranchId IS NULL OR t.branch_id = @BranchId)
              AND (@SalesShiftId IS NULL OR t.sales_shift_id = @SalesShiftId)
              AND (
                    @Unrestricted
                    OR t.branch_id IS NULL
                    OR t.branch_id = ANY(@AllowedBranches)
                  )
              AND (
                    @CanManage
                    OR EXISTS (
                        SELECT 1
                        FROM users u
                        WHERE u.tenant_id = t.tenant_id
                          AND u.id = @UserId
                          AND u.employee_id = t.assignee_employee_id
                    )
                  )
            ORDER BY t.due_at NULLS LAST, t.created_at DESC
            """,
            new
            {
                TenantId = _tenant.TenantId,
                UserId = _tenant.UserId,
                CanManage = canManage,
                OverdueOnly = overdueOnly,
                BranchId = branchId,
                SalesShiftId = salesShiftId,
                Unrestricted = scope.Unrestricted,
                AllowedBranches = scope.BranchIds.ToArray(),
            });
        return OperationalTaskRules.ApplyOverdue(rows, DateTime.UtcNow, overdueOnly);
    }

    public async Task<OperationalTaskDto> CreateAsync(
        CreateOperationalTaskRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (title.Length is 0 or > 200)
            throw new InvalidOperationException("Tiêu đề việc cần từ 1 đến 200 ký tự.");

        var branchId = request.BranchId;
        if (request.SalesShiftId is Guid shiftId)
        {
            await using var lookup = await _db.CreateOpenConnectionAsync(cancellationToken);
            var shiftBranch = await lookup.QuerySingleOrDefaultAsync<Guid?>(
                """
                SELECT w.branch_id
                FROM sales_shifts s
                INNER JOIN warehouses w ON w.id = s.warehouse_id
                WHERE s.id = @ShiftId AND s.tenant_id = @TenantId
                """,
                new { ShiftId = shiftId, TenantId = _tenant.TenantId });
            if (shiftBranch is null)
                throw new InvalidOperationException("Ca bán không thuộc cửa hàng này.");
            if (branchId is Guid requested && requested != shiftBranch)
                throw new InvalidOperationException("Chi nhánh không khớp ca bán.");
            branchId = shiftBranch;
        }

        if (branchId is Guid scoped)
            await _branchAccess.EnsureBranchAccessAsync(scoped, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var id = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            INSERT INTO pack_learning.operational_task (
                tenant_id, branch_id, sales_shift_id, assignee_employee_id,
                title, due_at, status, created_by_user_id
            )
            SELECT @TenantId, @BranchId, @SalesShiftId, e.id, @Title, @DueAt, 'open', @UserId
            FROM employees e
            WHERE e.id = @AssigneeId AND e.tenant_id = @TenantId
            RETURNING id
            """,
            new
            {
                TenantId = _tenant.TenantId,
                BranchId = branchId,
                SalesShiftId = request.SalesShiftId,
                AssigneeId = request.AssigneeEmployeeId,
                Title = title,
                DueAt = request.DueAt,
                UserId = _tenant.UserId,
            });
        if (id is null)
            throw new InvalidOperationException("Không tìm thấy nhân viên được giao.");

        var created = (await ReadAsync(conn, id.Value, cancellationToken))
            ?? throw new InvalidOperationException("Không đọc lại được việc vừa giao.");
        if (_audit is not null)
        {
            await _audit.WriteAsync(
                "learning.operational_task",
                created.Id,
                "assign",
                new { created.AssigneeEmployeeId, created.SalesShiftId, created.BranchId, created.Title },
                cancellationToken);
        }
        return created;
    }

    public async Task<IReadOnlyList<OperationalShiftOptionDto>> ListOpenShiftsAsync(
        CancellationToken cancellationToken)
    {
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<OperationalShiftOptionDto>(
            """
            SELECT s.id AS Id,
                   s.shift_number AS ShiftNumber,
                   w.warehouse_name AS WarehouseName,
                   w.branch_id AS BranchId
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id
            WHERE s.tenant_id = @TenantId
              AND s.status = 1
              AND (@Unrestricted OR w.branch_id = ANY(@AllowedBranches))
            ORDER BY s.opened_at
            """,
            new
            {
                TenantId = _tenant.TenantId,
                Unrestricted = scope.Unrestricted,
                AllowedBranches = scope.BranchIds.ToArray(),
            });
        return rows.AsList();
    }

    public async Task<OperationalTaskDto> UpdateStatusAsync(
        Guid taskId,
        bool canManage,
        UpdateOperationalTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = string.IsNullOrWhiteSpace(request.EvidenceNote) ? null : request.EvidenceNote.Trim();
        if (evidence is { Length: > 2000 })
            throw new InvalidOperationException("Bằng chứng vượt quá 2000 ký tự.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var current = await conn.QuerySingleOrDefaultAsync<TaskHead>(
            """
            SELECT t.status AS Status, t.branch_id AS BranchId
            FROM pack_learning.operational_task t
            WHERE t.id = @Id
              AND t.tenant_id = @TenantId
              AND (
                    @CanManage
                    OR EXISTS (
                        SELECT 1 FROM users u
                        WHERE u.tenant_id = t.tenant_id
                          AND u.id = @UserId
                          AND u.employee_id = t.assignee_employee_id
                    )
                  )
            """,
            new { Id = taskId, TenantId = _tenant.TenantId, UserId = _tenant.UserId, CanManage = canManage });
        if (current is null)
            throw new KeyNotFoundException("Không tìm thấy việc.");

        if (current.BranchId is Guid branch)
            await _branchAccess.EnsureBranchAccessAsync(branch, cancellationToken);

        var target = request.Status.Trim().ToLowerInvariant();
        if (!OperationalTaskRules.CanTransition(current.Status, target, canManage))
            throw new InvalidOperationException("Không chuyển được trạng thái việc này.");

        var done = target == OperationalTaskRules.Done;
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_learning.operational_task
            SET status = @Status,
                evidence_note = COALESCE(@Evidence, evidence_note),
                completed_at = CASE WHEN @Done THEN NOW() ELSE NULL END,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Current
            """,
            new
            {
                Id = taskId,
                TenantId = _tenant.TenantId,
                Status = target,
                Evidence = evidence,
                Done = done,
                Current = current.Status,
            });
        if (n == 0)
            throw new InvalidOperationException("Việc vừa đổi trạng thái. Tải lại.");

        var updated = (await ReadAsync(conn, taskId, cancellationToken))
            ?? throw new InvalidOperationException("Không đọc lại được việc.");
        if (_audit is not null)
        {
            await _audit.WriteAsync(
                "learning.operational_task",
                updated.Id,
                "status",
                new { From = current.Status, To = target, HasEvidence = evidence is not null },
                cancellationToken);
        }
        return updated;
    }

    private async Task<OperationalTaskDto?> ReadAsync(
        System.Data.IDbConnection conn,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        return await conn.QuerySingleOrDefaultAsync<OperationalTaskDto>(
            new CommandDefinition(
                """
                SELECT
                    t.id AS Id,
                    t.branch_id AS BranchId,
                    t.sales_shift_id AS SalesShiftId,
                    t.assignee_employee_id AS AssigneeEmployeeId,
                    e.full_name AS AssigneeName,
                    t.title AS Title,
                    t.due_at AS DueAt,
                    t.status AS Status,
                    t.evidence_note AS EvidenceNote,
                    t.created_by_user_id AS CreatedByUserId,
                    t.completed_at AS CompletedAt,
                    t.created_at AS CreatedAt,
                    FALSE AS IsOverdue
                FROM pack_learning.operational_task t
                INNER JOIN employees e ON e.id = t.assignee_employee_id
                WHERE t.id = @Id AND t.tenant_id = @TenantId
                """,
                new { Id = taskId, TenantId = _tenant.TenantId },
                cancellationToken: cancellationToken)) is { } task
            ? task with { IsOverdue = OperationalTaskRules.IsOverdue(task.DueAt, task.Status, DateTime.UtcNow) }
            : null;
    }

    private sealed class TaskHead
    {
        public string Status { get; set; } = "";
        public Guid? BranchId { get; set; }
    }
}
