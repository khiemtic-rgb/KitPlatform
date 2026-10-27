using System.Data.Common;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class PaperTaskStore : IPaperTaskStore
{
    readonly IDbConnectionFactory _db;
    readonly ITenantContext _tenant;

    public PaperTaskStore(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PaperTaskSnapshot> ContinueProposedAsync(
        BusinessCaller caller,
        Guid decisionId,
        string operationKey,
        IReadOnlyDictionary<string, string> slots,
        string evidenceJson,
        string evidenceSha256,
        CancellationToken cancellationToken)
    {
        if (caller.TenantId != _tenant.TenantId)
            throw new InvalidOperationException("Phiên đăng nhập không khớp tenant của task.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var operation = await conn.QuerySingleOrDefaultAsync<(string PayloadHash, Guid? DecisionId)>(
            """
            SELECT payload_hash AS PayloadHash, decision_id AS DecisionId
            FROM pack_personal_intelligence.pi_operation
            WHERE tenant_id = @TenantId AND operation_key = @OperationKey
            """,
            new { TenantId = _tenant.TenantId, OperationKey = operationKey });
        if (operation.PayloadHash is null || operation.DecisionId != decisionId)
            throw new InvalidOperationException("Operation và decision không cùng tenant hoặc không khớp.");

        var goal = await conn.QuerySingleOrDefaultAsync<string>(
            """
            SELECT thesis FROM pack_personal_intelligence.pi_decision
            WHERE tenant_id = @TenantId AND decision_id = @DecisionId
            """,
            new { TenantId = _tenant.TenantId, DecisionId = decisionId });
        if (string.IsNullOrWhiteSpace(goal))
            throw new InvalidOperationException("Không tìm thấy decision trong tenant hiện tại.");

        var existing = await conn.QuerySingleOrDefaultAsync<(Guid TaskId, string PayloadHash)>(
            """
            SELECT task_id AS TaskId, payload_hash AS PayloadHash
            FROM pack_personal_intelligence.pi_task
            WHERE tenant_id = @TenantId AND task_key = @TaskKey
            """,
            new { TenantId = _tenant.TenantId, TaskKey = operationKey });
        if (existing.TaskId != Guid.Empty && !string.Equals(existing.PayloadHash, operation.PayloadHash, StringComparison.Ordinal))
            throw new TradingOperationConflictException();

        if (existing.TaskId == Guid.Empty)
        {
            var plan = JsonSerializer.Serialize(new
            {
                capabilityId = PlatformCapabilityCatalog.TradingPaperEvaluate,
                disposition = IntentDisposition.Evaluate,
                slots,
            });
            var conditions = JsonSerializer.Serialize(new[]
            {
                DemoPaperExecution.SeparateApprovalRequired,
                "Accepted only when the linked decision is accepted and exactly one PAPER execution is re-read.",
            });
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_personal_intelligence.pi_task (
                    tenant_id, task_key, payload_hash, status, goal, completion_conditions,
                    plan_snapshot, evidence, evidence_sha256, decision_id, operation_key)
                VALUES (
                    @TenantId, @TaskKey, @PayloadHash, 'waiting_approval', @Goal, @Conditions::jsonb,
                    @Plan::jsonb, @Evidence::jsonb, @EvidenceSha256, @DecisionId, @OperationKey)
                ON CONFLICT (tenant_id, task_key) DO NOTHING
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    TaskKey = operationKey,
                    PayloadHash = operation.PayloadHash,
                    Goal = goal,
                    Conditions = conditions,
                    Plan = plan,
                    Evidence = evidenceJson,
                    EvidenceSha256 = evidenceSha256,
                    DecisionId = decisionId,
                    OperationKey = operationKey,
                });
        }

        var taskId = existing.TaskId != Guid.Empty
            ? existing.TaskId
            : await conn.QuerySingleAsync<Guid>(
                """
                SELECT task_id FROM pack_personal_intelligence.pi_task
                WHERE tenant_id = @TenantId AND task_key = @TaskKey AND payload_hash = @PayloadHash
                """,
                new { TenantId = _tenant.TenantId, TaskKey = operationKey, PayloadHash = operation.PayloadHash });
        return await VerifyAsync(taskId, cancellationToken) ?? throw new InvalidOperationException("Không đọc lại được task vừa lưu.");
    }

    public async Task<PaperTaskSnapshot?> VerifyAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await ReadAsync(conn, taskId);
        if (row is null)
            return null;
        if (await TryPersistAsync(conn, row))
            return Snapshot(row);
        var newer = await ReadAsync(conn, taskId);
        if (newer is null)
            return null;
        await TryPersistAsync(conn, newer);
        return Snapshot(newer);
    }

    async Task<TaskReadRow?> ReadAsync(System.Data.Common.DbConnection conn, Guid taskId)
    {
        return await conn.QuerySingleOrDefaultAsync<TaskReadRow>(
            """
            SELECT t.task_id AS TaskId, t.task_key AS TaskKey, t.goal AS Goal,
                   t.decision_id AS DecisionId, t.operation_key AS OperationKey, t.payload_hash AS PayloadHash,
                   t.updated_at AS UpdatedAt,
                   d.status AS DecisionStatus,
                   op.decision_id AS OperationDecisionId, op.payload_hash AS OperationPayloadHash,
                   (SELECT count(*)
                      FROM pack_personal_intelligence.trading_execution e
                      JOIN pack_personal_intelligence.trading_order o
                        ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
                     WHERE o.tenant_id = t.tenant_id AND o.decision_id = t.decision_id AND e.mode = 'PAPER') AS PaperCount
            FROM pack_personal_intelligence.pi_task t
            LEFT JOIN pack_personal_intelligence.pi_decision d
              ON d.decision_id = t.decision_id AND d.tenant_id = t.tenant_id
            LEFT JOIN pack_personal_intelligence.pi_operation op
              ON op.tenant_id = t.tenant_id AND op.operation_key = t.operation_key
            WHERE t.tenant_id = @TenantId AND t.task_id = @TaskId
            """,
            new { TenantId = _tenant.TenantId, TaskId = taskId });
    }

    async Task<bool> TryPersistAsync(System.Data.Common.DbConnection conn, TaskReadRow row)
    {
        var assessment = Assess(row);
        var verification = JsonSerializer.Serialize(new
        {
            workflowId = PaperProposalWorkflow.Id,
            decisionStatus = row.DecisionStatus,
            paperExecutionCount = row.PaperCount,
            verdict = assessment.TaskStatus,
            note = assessment.Note,
            steps = assessment.Steps.Select(step => new
            {
                key = step.Key,
                kind = step.Kind,
                dependsOn = step.DependsOn,
                status = step.Status,
                note = step.Note,
            }),
        });
        var written = await conn.ExecuteAsync(
            """
            UPDATE pack_personal_intelligence.pi_task
            SET status = @Status, verification_note = @Note, last_verification = @Verification::jsonb, updated_at = NOW()
            WHERE tenant_id = @TenantId AND task_id = @TaskId AND payload_hash = @PayloadHash AND updated_at = @SeenUpdatedAt
            """,
            new
            {
                Status = assessment.TaskStatus,
                Note = assessment.Note,
                Verification = verification,
                TenantId = _tenant.TenantId,
                TaskId = row.TaskId,
                row.PayloadHash,
                SeenUpdatedAt = row.UpdatedAt,
            });
        return written == 1;
    }

    static PaperProposalWorkflow.Assessment Assess(TaskReadRow row)
    {
        var linkMatches = row.OperationDecisionId == row.DecisionId
            && string.Equals(row.OperationPayloadHash, row.PayloadHash, StringComparison.Ordinal);
        return PaperProposalWorkflow.Assess(row.DecisionStatus, row.PaperCount, linkMatches, readable: true);
    }

    static PaperTaskSnapshot Snapshot(TaskReadRow row)
    {
        var assessment = Assess(row);
        return new PaperTaskSnapshot(
            row.TaskId,
            row.TaskKey,
            assessment.TaskStatus,
            row.Goal,
            row.DecisionId,
            row.OperationKey,
            row.DecisionStatus ?? "",
            row.PaperCount,
            assessment.Note,
            assessment.Steps);
    }

    public async Task<IReadOnlyList<PaperTaskSnapshot>> ListAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var ids = (await conn.QueryAsync<Guid>(
            """
            SELECT task_id FROM pack_personal_intelligence.pi_task
            WHERE tenant_id = @TenantId
            ORDER BY updated_at DESC
            LIMIT 20
            """,
            new { TenantId = _tenant.TenantId })).ToArray();
        var list = new List<PaperTaskSnapshot>(ids.Length);
        foreach (var id in ids)
        {
            var snapshot = await VerifyAsync(id, cancellationToken);
            if (snapshot is not null)
                list.Add(snapshot);
        }

        return list;
    }

    sealed class TaskReadRow
    {
        public Guid TaskId { get; set; }
        public string TaskKey { get; set; } = "";
        public string Goal { get; set; } = "";
        public Guid DecisionId { get; set; }
        public string OperationKey { get; set; } = "";
        public string PayloadHash { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
        public string? DecisionStatus { get; set; }
        public Guid? OperationDecisionId { get; set; }
        public string? OperationPayloadHash { get; set; }
        public int PaperCount { get; set; }
    }
}
