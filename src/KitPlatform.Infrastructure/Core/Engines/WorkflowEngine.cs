using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Core.Engines;

internal sealed class WorkflowEngine : IWorkflowEngine
{
    private const string PosDiscountOverrideCode = "pos_discount_override";
    private const string PurchaseOrderApproveCode = "purchase_order_approve";

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditEngine _audit;

    public WorkflowEngine(IDbConnectionFactory db, ITenantContext tenant, IAuditEngine audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task EnforcePosDiscountApprovalAsync(
        SaleOrderPricingResult pricing,
        SalesDiscountPolicy policy,
        Guid? approvedWorkflowTaskId = null,
        Guid? salesOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var violation = SalesPricing.TryGetDiscountViolation(pricing, policy);
        if (violation is null)
            return;

        if (approvedWorkflowTaskId is Guid taskId
            && await IsApprovedTaskAsync(taskId, pricing, salesOrderId, cancellationToken))
        {
            return;
        }

        var workflowTaskId = await CreateOverrideRequestAsync(
            pricing,
            policy,
            violation,
            salesOrderId,
            cancellationToken);

        throw new DiscountApprovalRequiredException(
            workflowTaskId,
            $"{violation} Yêu cầu phê duyệt quản lý (task: {workflowTaskId}).");
    }

    private async Task<bool> IsApprovedTaskAsync(
        Guid taskId,
        SaleOrderPricingResult pricing,
        Guid? salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                wt.task_status AS TaskStatus,
                wt.decision AS Decision,
                wi.metadata::text AS MetadataJson
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            WHERE wt.id = @TaskId
              AND wt.tenant_id = @TenantId
              AND wt.deleted_at IS NULL
              AND wi.deleted_at IS NULL
            """;

        var row = await conn.QuerySingleOrDefaultAsync<(string TaskStatus, string? Decision, string? MetadataJson)>(
            sql,
            new { TaskId = taskId, TenantId = _tenant.TenantId });

        if (row.TaskStatus != "completed" || row.Decision != "approved")
            return false;

        if (string.IsNullOrWhiteSpace(row.MetadataJson))
            return false;

        using var doc = JsonDocument.Parse(row.MetadataJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("salesOrderId", out var orderEl)
            && orderEl.ValueKind != JsonValueKind.Null
            && salesOrderId.HasValue
            && orderEl.GetGuid() != salesOrderId.Value)
        {
            return false;
        }

        if (root.TryGetProperty("totalAmount", out var totalEl)
            && totalEl.TryGetDecimal(out var approvedTotal)
            && approvedTotal != pricing.TotalAmount)
        {
            return false;
        }

        return true;
    }

    private async Task<Guid> CreateOverrideRequestAsync(
        SaleOrderPricingResult pricing,
        SalesDiscountPolicy policy,
        string violation,
        Guid? salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string definitionSql = """
            SELECT w.id AS WorkflowId, ws.id AS ApprovalStepId
            FROM kit_workflow.workflow_definition w
            INNER JOIN kit_workflow.workflow_step ws
                ON ws.workflow_id = w.id AND ws.step_code = 'manager_approve'
            WHERE w.workflow_code = @Code
              AND w.tenant_id IS NULL
              AND w.deleted_at IS NULL
              AND ws.deleted_at IS NULL
            LIMIT 1
            """;

        var definition = await conn.QuerySingleOrDefaultAsync<(Guid WorkflowId, Guid ApprovalStepId)>(
            definitionSql,
            new { Code = PosDiscountOverrideCode },
            tx);

        if (definition.WorkflowId == Guid.Empty)
            throw new InvalidOperationException("Workflow pos_discount_override chưa được cấu hình.");

        var entityId = salesOrderId ?? Guid.NewGuid();
        var metadata = JsonSerializer.Serialize(new
        {
            violation,
            maxPercent = policy.MaxPercent,
            lineDiscountTotal = pricing.LineDiscountTotal,
            orderDiscountAmount = pricing.OrderDiscountAmount,
            totalAmount = pricing.TotalAmount,
            salesOrderId,
            requestedBy = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
        });

        const string startStepSql = """
            SELECT id FROM kit_workflow.workflow_step
            WHERE workflow_id = @WorkflowId AND step_code = 'start' AND deleted_at IS NULL
            LIMIT 1
            """;

        var startStepId = await conn.QuerySingleAsync<Guid>(
            startStepSql,
            new { definition.WorkflowId },
            tx);

        var instanceId = Guid.NewGuid();
        const string instanceSql = """
            INSERT INTO kit_workflow.workflow_instance (
                id, tenant_id, workflow_id, entity_type, entity_id,
                current_step_id, started_by, instance_status, metadata, created_by
            )
            VALUES (
                @Id, @TenantId, @WorkflowId, 'sales_order', @EntityId,
                @CurrentStepId, @StartedBy, 'running', @Metadata::jsonb, @StartedBy
            )
            """;

        await conn.ExecuteAsync(instanceSql, new
        {
            Id = instanceId,
            TenantId = _tenant.TenantId,
            WorkflowId = definition.WorkflowId,
            EntityId = entityId,
            CurrentStepId = definition.ApprovalStepId,
            StartedBy = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            Metadata = metadata,
        }, tx);

        var taskId = Guid.NewGuid();
        const string taskSql = """
            INSERT INTO kit_workflow.workflow_task (
                id, tenant_id, instance_id, step_id, task_status, created_by
            )
            VALUES (
                @Id, @TenantId, @InstanceId, @StepId, 'pending', @CreatedBy
            )
            """;

        await conn.ExecuteAsync(taskSql, new
        {
            Id = taskId,
            TenantId = _tenant.TenantId,
            InstanceId = instanceId,
            StepId = definition.ApprovalStepId,
            CreatedBy = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
        }, tx);

        const string historySql = """
            INSERT INTO kit_workflow.workflow_history (
                tenant_id, instance_id, task_id, step_id, actor_user_id,
                action, from_status, to_status, metadata
            )
            VALUES (
                @TenantId, @InstanceId, @TaskId, @StepId, @ActorUserId,
                'submit_override', 'start', 'pending', @Metadata::jsonb
            )
            """;

        await conn.ExecuteAsync(historySql, new
        {
            TenantId = _tenant.TenantId,
            InstanceId = instanceId,
            TaskId = taskId,
            StepId = startStepId,
            ActorUserId = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            Metadata = metadata,
        }, tx);

        await tx.CommitAsync(cancellationToken);
        return taskId;
    }

    public async Task<WorkflowTaskDecisionDto> ApprovePosDiscountTaskAsync(
        Guid taskId,
        bool approved,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string lockSql = """
            SELECT
                wt.id AS TaskId,
                wt.instance_id AS InstanceId,
                wt.step_id AS StepId,
                wt.task_status AS TaskStatus,
                wi.workflow_id AS WorkflowId
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            INNER JOIN kit_workflow.workflow_definition wd ON wd.id = wi.workflow_id
            WHERE wt.id = @TaskId
              AND wt.tenant_id = @TenantId
              AND wt.deleted_at IS NULL
              AND wi.deleted_at IS NULL
              AND wd.workflow_code = @WorkflowCode
            FOR UPDATE OF wt
            """;

        var row = await conn.QuerySingleOrDefaultAsync<WorkflowTaskLockRow>(
            lockSql,
            new { TaskId = taskId, TenantId = _tenant.TenantId, WorkflowCode = PosDiscountOverrideCode },
            tx);

        if (row is null)
            throw new InvalidOperationException("Workflow task không tồn tại.");

        if (row.TaskStatus != "pending")
            throw new InvalidOperationException($"Task đã xử lý (status: {row.TaskStatus}).");

        var decision = approved ? "approved" : "rejected";
        var newTaskStatus = "completed";
        var instanceStatus = approved ? "completed" : "cancelled";

        await conn.ExecuteAsync("""
            UPDATE kit_workflow.workflow_task
            SET task_status = @TaskStatus,
                decision = @Decision,
                decision_notes = @Notes,
                completed_at = NOW(),
                updated_at = NOW()
            WHERE id = @TaskId
            """, new
        {
            TaskId = taskId,
            TaskStatus = newTaskStatus,
            Decision = decision,
            Notes = notes,
        }, tx);

        await conn.ExecuteAsync("""
            UPDATE kit_workflow.workflow_instance
            SET instance_status = @InstanceStatus,
                completed_at = NOW(),
                updated_at = NOW()
            WHERE id = @InstanceId
            """, new { InstanceId = row.InstanceId, InstanceStatus = instanceStatus }, tx);

        await conn.ExecuteAsync("""
            INSERT INTO kit_workflow.workflow_history (
                tenant_id, instance_id, task_id, step_id, actor_user_id,
                action, from_status, to_status, notes
            )
            VALUES (
                @TenantId, @InstanceId, @TaskId, @StepId, @ActorUserId,
                @Action, 'pending', @ToStatus, @Notes
            )
            """, new
        {
            TenantId = _tenant.TenantId,
            InstanceId = row.InstanceId,
            TaskId = taskId,
            StepId = row.StepId,
            ActorUserId = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            Action = approved ? "approve" : "reject",
            ToStatus = newTaskStatus,
            Notes = notes,
        }, tx);

        await tx.CommitAsync(cancellationToken);

        await _audit.WriteAsync(
            "workflow_task",
            taskId,
            approved ? "discount_override_approved" : "discount_override_rejected",
            new { workflowCode = PosDiscountOverrideCode, decision, notes },
            cancellationToken);

        return new WorkflowTaskDecisionDto(taskId, newTaskStatus, decision, DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<WorkflowTaskListItemDto>> ListPendingPosDiscountTasksAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                wt.id AS TaskId,
                wt.instance_id AS InstanceId,
                wt.task_status AS TaskStatus,
                wt.decision AS Decision,
                wt.created_at AS CreatedAt,
                wi.metadata::text AS MetadataJson
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            INNER JOIN kit_workflow.workflow_definition wd ON wd.id = wi.workflow_id
            WHERE wt.tenant_id = @TenantId
              AND wt.task_status = 'pending'
              AND wt.deleted_at IS NULL
              AND wd.workflow_code = @WorkflowCode
            ORDER BY wt.created_at DESC
            LIMIT 50
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<WorkflowTaskListRow>(sql, new
        {
            TenantId = _tenant.TenantId,
            WorkflowCode = PosDiscountOverrideCode,
        });

        return rows.Select(r =>
        {
            string? summary = null;
            if (!string.IsNullOrWhiteSpace(r.MetadataJson))
            {
                using var doc = JsonDocument.Parse(r.MetadataJson);
                summary = doc.RootElement.TryGetProperty("violation", out var v)
                    ? v.GetString()
                    : null;
            }

            return new WorkflowTaskListItemDto(
                r.TaskId, r.InstanceId, r.TaskStatus, r.Decision, r.CreatedAt, summary);
        }).ToList();
    }

    public async Task<bool> HasPendingPurchaseOrderTaskAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 1
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            INNER JOIN kit_workflow.workflow_definition wd ON wd.id = wi.workflow_id
            WHERE wt.tenant_id = @TenantId
              AND wt.task_status = 'pending'
              AND wt.deleted_at IS NULL
              AND wi.deleted_at IS NULL
              AND wd.workflow_code = @WorkflowCode
              AND wi.entity_id = @PurchaseOrderId
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int?>(sql, new
        {
            TenantId = _tenant.TenantId,
            WorkflowCode = PurchaseOrderApproveCode,
            PurchaseOrderId = purchaseOrderId,
        }) is not null;
    }

    public async Task<Guid> SubmitPurchaseOrderApprovalAsync(
        Guid purchaseOrderId,
        string poNumber,
        string supplierName,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        if (await HasPendingPurchaseOrderTaskAsync(purchaseOrderId, cancellationToken))
            throw new InvalidOperationException("PO đang chờ phê duyệt.");

        return await CreateApprovalTaskAsync(
            PurchaseOrderApproveCode,
            "purchase_order",
            purchaseOrderId,
            JsonSerializer.Serialize(new
            {
                purchaseOrderId,
                poNumber,
                supplierName,
                totalAmount,
                requestedBy = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            }),
            cancellationToken);
    }

    public async Task<Guid?> GetPurchaseOrderIdForPendingTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT wi.entity_id
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            INNER JOIN kit_workflow.workflow_definition wd ON wd.id = wi.workflow_id
            WHERE wt.id = @TaskId
              AND wt.tenant_id = @TenantId
              AND wt.deleted_at IS NULL
              AND wi.deleted_at IS NULL
              AND wd.workflow_code = @WorkflowCode
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid?>(sql, new
        {
            TaskId = taskId,
            TenantId = _tenant.TenantId,
            WorkflowCode = PurchaseOrderApproveCode,
        });
    }

    public Task<WorkflowTaskDecisionDto> DecidePurchaseOrderTaskAsync(
        Guid taskId,
        bool approved,
        string? notes = null,
        CancellationToken cancellationToken = default) =>
        DecideWorkflowTaskAsync(PurchaseOrderApproveCode, taskId, approved, notes, cancellationToken);

    public Task<IReadOnlyList<WorkflowTaskListItemDto>> ListPendingPurchaseOrderTasksAsync(
        CancellationToken cancellationToken = default) =>
        ListPendingTasksAsync(PurchaseOrderApproveCode, "poNumber", cancellationToken);

    private async Task<Guid> CreateApprovalTaskAsync(
        string workflowCode,
        string entityType,
        Guid entityId,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string definitionSql = """
            SELECT w.id AS WorkflowId, ws.id AS ApprovalStepId
            FROM kit_workflow.workflow_definition w
            INNER JOIN kit_workflow.workflow_step ws
                ON ws.workflow_id = w.id AND ws.step_code = 'manager_approve'
            WHERE w.workflow_code = @Code
              AND w.tenant_id IS NULL
              AND w.deleted_at IS NULL
              AND ws.deleted_at IS NULL
            LIMIT 1
            """;

        var definition = await conn.QuerySingleOrDefaultAsync<(Guid WorkflowId, Guid ApprovalStepId)>(
            definitionSql,
            new { Code = workflowCode },
            tx);

        if (definition.WorkflowId == Guid.Empty)
            throw new InvalidOperationException($"Workflow {workflowCode} chưa được cấu hình.");

        const string startStepSql = """
            SELECT id FROM kit_workflow.workflow_step
            WHERE workflow_id = @WorkflowId AND step_code = 'start' AND deleted_at IS NULL
            LIMIT 1
            """;

        var startStepId = await conn.QuerySingleAsync<Guid>(
            startStepSql,
            new { definition.WorkflowId },
            tx);

        var instanceId = Guid.NewGuid();
        const string instanceSql = """
            INSERT INTO kit_workflow.workflow_instance (
                id, tenant_id, workflow_id, entity_type, entity_id,
                current_step_id, started_by, instance_status, metadata, created_by
            )
            VALUES (
                @Id, @TenantId, @WorkflowId, @EntityType, @EntityId,
                @CurrentStepId, @StartedBy, 'running', @Metadata::jsonb, @StartedBy
            )
            """;

        await conn.ExecuteAsync(instanceSql, new
        {
            Id = instanceId,
            TenantId = _tenant.TenantId,
            WorkflowId = definition.WorkflowId,
            EntityType = entityType,
            EntityId = entityId,
            CurrentStepId = definition.ApprovalStepId,
            StartedBy = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            Metadata = metadataJson,
        }, tx);

        var taskId = Guid.NewGuid();
        const string taskSql = """
            INSERT INTO kit_workflow.workflow_task (
                id, tenant_id, instance_id, step_id, task_status, created_by
            )
            VALUES (
                @Id, @TenantId, @InstanceId, @StepId, 'pending', @CreatedBy
            )
            """;

        await conn.ExecuteAsync(taskSql, new
        {
            Id = taskId,
            TenantId = _tenant.TenantId,
            InstanceId = instanceId,
            StepId = definition.ApprovalStepId,
            CreatedBy = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
        }, tx);

        const string historySql = """
            INSERT INTO kit_workflow.workflow_history (
                tenant_id, instance_id, task_id, step_id, actor_user_id,
                action, from_status, to_status, metadata
            )
            VALUES (
                @TenantId, @InstanceId, @TaskId, @StepId, @ActorUserId,
                'submit', 'start', 'pending', @Metadata::jsonb
            )
            """;

        await conn.ExecuteAsync(historySql, new
        {
            TenantId = _tenant.TenantId,
            InstanceId = instanceId,
            TaskId = taskId,
            StepId = startStepId,
            ActorUserId = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            Metadata = metadataJson,
        }, tx);

        await tx.CommitAsync(cancellationToken);
        return taskId;
    }

    private async Task<WorkflowTaskDecisionDto> DecideWorkflowTaskAsync(
        string workflowCode,
        Guid taskId,
        bool approved,
        string? notes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string lockSql = """
            SELECT
                wt.id AS TaskId,
                wt.instance_id AS InstanceId,
                wt.step_id AS StepId,
                wt.task_status AS TaskStatus,
                wi.workflow_id AS WorkflowId
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            INNER JOIN kit_workflow.workflow_definition wd ON wd.id = wi.workflow_id
            WHERE wt.id = @TaskId
              AND wt.tenant_id = @TenantId
              AND wt.deleted_at IS NULL
              AND wi.deleted_at IS NULL
              AND wd.workflow_code = @WorkflowCode
            FOR UPDATE OF wt
            """;

        var row = await conn.QuerySingleOrDefaultAsync<WorkflowTaskLockRow>(
            lockSql,
            new { TaskId = taskId, TenantId = _tenant.TenantId, WorkflowCode = workflowCode },
            tx);

        if (row is null)
            throw new InvalidOperationException("Workflow task không tồn tại.");

        if (row.TaskStatus != "pending")
            throw new InvalidOperationException($"Task đã xử lý (status: {row.TaskStatus}).");

        var decision = approved ? "approved" : "rejected";
        var newTaskStatus = "completed";
        var instanceStatus = approved ? "completed" : "cancelled";

        await conn.ExecuteAsync("""
            UPDATE kit_workflow.workflow_task
            SET task_status = @TaskStatus,
                decision = @Decision,
                decision_notes = @Notes,
                completed_at = NOW(),
                updated_at = NOW()
            WHERE id = @TaskId
            """, new
        {
            TaskId = taskId,
            TaskStatus = newTaskStatus,
            Decision = decision,
            Notes = notes,
        }, tx);

        await conn.ExecuteAsync("""
            UPDATE kit_workflow.workflow_instance
            SET instance_status = @InstanceStatus,
                completed_at = NOW(),
                updated_at = NOW()
            WHERE id = @InstanceId
            """, new { InstanceId = row.InstanceId, InstanceStatus = instanceStatus }, tx);

        await conn.ExecuteAsync("""
            INSERT INTO kit_workflow.workflow_history (
                tenant_id, instance_id, task_id, step_id, actor_user_id,
                action, from_status, to_status, notes
            )
            VALUES (
                @TenantId, @InstanceId, @TaskId, @StepId, @ActorUserId,
                @Action, 'pending', @ToStatus, @Notes
            )
            """, new
        {
            TenantId = _tenant.TenantId,
            InstanceId = row.InstanceId,
            TaskId = taskId,
            StepId = row.StepId,
            ActorUserId = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            Action = approved ? "approve" : "reject",
            ToStatus = newTaskStatus,
            Notes = notes,
        }, tx);

        await tx.CommitAsync(cancellationToken);

        return new WorkflowTaskDecisionDto(taskId, newTaskStatus, decision, DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<WorkflowTaskListItemDto>> ListPendingTasksAsync(
        string workflowCode,
        string summaryField,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                wt.id AS TaskId,
                wt.instance_id AS InstanceId,
                wt.task_status AS TaskStatus,
                wt.decision AS Decision,
                wt.created_at AS CreatedAt,
                wi.metadata::text AS MetadataJson
            FROM kit_workflow.workflow_task wt
            INNER JOIN kit_workflow.workflow_instance wi ON wi.id = wt.instance_id
            INNER JOIN kit_workflow.workflow_definition wd ON wd.id = wi.workflow_id
            WHERE wt.tenant_id = @TenantId
              AND wt.task_status = 'pending'
              AND wt.deleted_at IS NULL
              AND wd.workflow_code = @WorkflowCode
            ORDER BY wt.created_at DESC
            LIMIT 50
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<WorkflowTaskListRow>(sql, new
        {
            TenantId = _tenant.TenantId,
            WorkflowCode = workflowCode,
        });

        return rows.Select(r =>
        {
            string? summary = null;
            if (!string.IsNullOrWhiteSpace(r.MetadataJson))
            {
                using var doc = JsonDocument.Parse(r.MetadataJson);
                summary = doc.RootElement.TryGetProperty(summaryField, out var v)
                    ? v.GetString()
                    : doc.RootElement.TryGetProperty("violation", out var violation)
                        ? violation.GetString()
                        : null;
            }

            return new WorkflowTaskListItemDto(
                r.TaskId, r.InstanceId, r.TaskStatus, r.Decision, r.CreatedAt, summary);
        }).ToList();
    }

    private sealed class WorkflowTaskLockRow
    {
        public Guid TaskId { get; set; }
        public Guid InstanceId { get; set; }
        public Guid StepId { get; set; }
        public string TaskStatus { get; set; } = "";
        public Guid WorkflowId { get; set; }
    }

    private sealed class WorkflowTaskListRow
    {
        public Guid TaskId { get; set; }
        public Guid InstanceId { get; set; }
        public string TaskStatus { get; set; } = "";
        public string? Decision { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? MetadataJson { get; set; }
    }
}
