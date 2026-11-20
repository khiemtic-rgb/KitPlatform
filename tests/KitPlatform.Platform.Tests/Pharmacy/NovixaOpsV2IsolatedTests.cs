using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Learning;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Learning;
using KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// Runs only when POS_IDEMPOTENCY_TEST_CONNECTION points at an empty isolated database.
/// Refuses kitplatform, kitplatform_test, and production names before any write.
/// </summary>
[Collection("kitplatform_test-sales-idempotency")]
public sealed class NovixaOpsV2IsolatedTests
{
    private const string ConnectionEnvironmentVariable = "POS_IDEMPOTENCY_TEST_CONNECTION";

    private static readonly HashSet<string> DeniedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitplatform",
        "kitplatform_test",
        "postgres",
        "novixa_prod",
        "pharmacore",
    };

    [Fact]
    public async Task Knowledge_lifecycle_preserves_reference_version_and_append_only_history()
    {
        var cs = await RequireIsolatedAsync();
        var code = "opsv2-" + Guid.NewGuid().ToString("N")[..16];
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO tenants (id, tenant_code, tenant_name, status)
                VALUES
                    (@TenantId, @TenantCode, @TenantCode, 1),
                    (@OtherTenantId, @OtherCode, @OtherCode, 1);
                INSERT INTO pharmacy_knowledge_rule (rule_code, reason_vi, approval_status, version)
                VALUES (@Code, 'synthetic', 'PENDING_REVIEW', '1');
                INSERT INTO pharmacy_knowledge_rule_version (
                    rule_code, version, reason_vi, priority, lifecycle_status,
                    reference_source, source_identity, source_url, issuing_organization, evidence_note,
                    source_verification_status, verified_by, verified_at)
                VALUES (
                    @Code, '1', 'synthetic', 100, 'PUBLISHED',
                    'synthetic-citation', 'SYNTH-TEST', 'https://example.test/k1-fixture',
                    'synthetic-org', 'fixture section', 'ACCEPTED', 'fixture', NOW());
                """,
                new
                {
                    Code = code,
                    TenantId = tenantId,
                    OtherTenantId = otherTenantId,
                    TenantCode = "w5" + tenantId.ToString("N")[..10],
                    OtherCode = "w5" + otherTenantId.ToString("N")[..10],
                });

            var approval = new PharmacyKnowledgeApprovalService(new NpgsqlConnectionFactory(cs));
            var before = await CountVisibleAsync(conn, tenantId, code);
            var approved = await approval.TransitionAsync(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "synthetic-source", "1"),
                tenantId,
                userId);
            var other = await CountVisibleAsync(conn, otherTenantId, code);
            var retired = await approval.TransitionAsync(
                code,
                new PharmacyKnowledgeApprovalRequest("RETIRED", "later-source", "1"),
                tenantId,
                userId);
            var history = await approval.ListHistoryAsync(code, tenantId);

            Assert.Equal(0, before);
            Assert.Equal(0, other);
            Assert.Equal(PharmacyKnowledgeApproval.Approved, approved!.ApprovalStatus);
            Assert.Equal("synthetic-source", approved.ReferenceSource);
            Assert.Equal("1", approved.Version);
            Assert.Equal("synthetic-source", retired!.ReferenceSource);
            Assert.Equal("1", retired.Version);
            Assert.Equal(0, await CountVisibleAsync(conn, tenantId, code));
            Assert.Equal(2, history.Count);
            Assert.Contains(history, item => item.ToStatus == "RETIRED" && item.ReferenceSource == "synthetic-source" && item.Version == "1");

            var mutate = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                "UPDATE pharmacy_knowledge_approval_event SET reference_source = 'changed' WHERE rule_code = @Code",
                new { Code = code }));
            var deleted = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                "DELETE FROM pharmacy_knowledge_approval_event WHERE rule_code = @Code",
                new { Code = code }));
            Assert.NotNull(mutate);
            Assert.NotNull(deleted);
        }
        finally
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM pharmacy_knowledge_adoption WHERE rule_code = @Code;
                DELETE FROM pharmacy_knowledge_rule_version
                WHERE rule_code = @Code AND lifecycle_status IN ('DRAFT', 'IN_REVIEW');
                DELETE FROM pharmacy_knowledge_rule
                WHERE rule_code = @Code
                  AND NOT EXISTS (
                      SELECT 1 FROM pharmacy_knowledge_rule_version WHERE rule_code = @Code
                  );
                DELETE FROM tenants WHERE id IN (@TenantId, @OtherTenantId);
                """,
                new { Code = code, TenantId = tenantId, OtherTenantId = otherTenantId });
        }
    }

    [Fact]
    public async Task Checklist_requires_an_explicit_shift_when_two_are_open()
    {
        var cs = await RequireIsolatedAsync();
        var ids = NewIds();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
        await SeedBranchAsync(conn, ids);
        await conn.ExecuteAsync(
            """
            INSERT INTO success_shift_checklist_template (id, tenant_id, kind, title)
            VALUES (@TemplateId, @TenantId, 'open', 'synthetic');
            INSERT INTO sales_shifts (id, tenant_id, warehouse_id, opened_by, shift_number, status)
            VALUES (@ShiftA, @TenantId, @WarehouseA, @UserId, @ShiftNoA, 1);
            INSERT INTO sales_shifts (id, tenant_id, warehouse_id, opened_by, shift_number, status)
            VALUES (@ShiftB, @TenantId, @WarehouseB, @UserId, @ShiftNoB, 1);
            INSERT INTO success_shift_checklist_run (
                tenant_id, branch_id, sales_shift_id, template_id, kind, business_date, started_by_user_id)
            VALUES (@TenantId, @BranchId, @ShiftA, @TemplateId, 'open', CURRENT_DATE, @UserId);
            """,
            ids);

        await conn.ExecuteAsync(
            """
            INSERT INTO success_shift_checklist_run (
                tenant_id, branch_id, sales_shift_id, template_id, kind, business_date, started_by_user_id)
            VALUES (@TenantId, @BranchId, @ShiftB, @TemplateId, 'open', CURRENT_DATE, @UserId)
            """,
            ids);
        var duplicate = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            INSERT INTO success_shift_checklist_run (
                tenant_id, branch_id, sales_shift_id, template_id, kind, business_date, started_by_user_id)
            VALUES (@TenantId, @BranchId, @ShiftA, @TemplateId, 'open', CURRENT_DATE, @UserId)
            """,
            ids));
        var legacy = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            INSERT INTO success_shift_checklist_run (
                tenant_id, branch_id, template_id, kind, business_date, started_by_user_id)
            VALUES (@TenantId, @BranchId, @TemplateId, 'open', CURRENT_DATE, @UserId),
                   (@TenantId, @BranchId, @TemplateId, 'open', CURRENT_DATE, @UserId)
            """,
            ids));

        Assert.NotNull(duplicate);
        Assert.NotNull(legacy);
        }
        finally
        {
            await CleanupBranchAsync(conn, ids);
        }
    }

    [Fact]
    public async Task Staff_enablement_journeys_stay_inside_the_assignee_and_the_shift()
    {
        var cs = await RequireIsolatedAsync();
        var ids = NewIds();
        var employeeB = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            var learning = await conn.QuerySingleAsync<(int Programs, int Modules, int Questions)>(
                """
                SELECT
                    (SELECT COUNT(*)::int FROM pack_learning.program) AS Programs,
                    (SELECT COUNT(*)::int FROM pack_learning.module) AS Modules,
                    (SELECT COUNT(*)::int FROM pack_learning.quiz_question) AS Questions
                """);
            Assert.True(learning.Programs > 0, "Learning programs are missing.");
            Assert.True(learning.Modules > 0, "Learning modules are missing.");
            Assert.True(learning.Questions > 0, "Quiz questions are missing.");
            foreach (var relation in new[]
            {
                "pack_learning.enrollment",
                "pack_learning.module_progress",
                "pack_learning.evaluation",
                "pack_learning.recognition",
                "pack_learning.credential",
                "pack_learning.employee_career",
            })
            {
                var exists = await conn.ExecuteScalarAsync<bool>(
                    "SELECT to_regclass(@Relation) IS NOT NULL",
                    new { Relation = relation });
                Assert.True(exists, relation);
            }

            await SeedPeopleAsync(conn, ids);
            await conn.ExecuteAsync(
                """
                INSERT INTO employees (id, tenant_id, employee_code, full_name, status)
                VALUES (@EmployeeB, @TenantId, @CodeB, @CodeB, 1);
                INSERT INTO users (id, tenant_id, employee_id, username, email, password_hash, status)
                VALUES (@UserB, @TenantId, @EmployeeB, @CodeB, @CodeB || '@example.test', 'not-a-login', 1);
                INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
                VALUES
                    (@WarehouseA, @TenantId, @BranchId, @ShiftNoA, @ShiftNoA, 1, 1),
                    (@WarehouseB, @TenantId, @BranchId, @ShiftNoB, @ShiftNoB, 1, 1);
                INSERT INTO sales_shifts (id, tenant_id, warehouse_id, opened_by, shift_number, status)
                VALUES
                    (@ShiftA, @TenantId, @WarehouseA, @UserId, @ShiftNoA, 1),
                    (@ShiftB, @TenantId, @WarehouseB, @UserId, @ShiftNoB, 1);
                INSERT INTO success_shift_checklist_template (id, tenant_id, kind, title)
                VALUES (@TemplateId, @TenantId, 'open', 'synthetic open');
                """,
                new
                {
                    ids.TenantId,
                    ids.BranchId,
                    ids.UserId,
                    ids.WarehouseA,
                    ids.WarehouseB,
                    ids.ShiftA,
                    ids.ShiftB,
                    ids.ShiftNoA,
                    ids.ShiftNoB,
                    ids.TemplateId,
                    EmployeeB = employeeB,
                    UserB = userB,
                    CodeB = ids.Code + "b",
                });

            var programId = await conn.ExecuteScalarAsync<Guid>(
                "SELECT id FROM pack_learning.program ORDER BY sort_order LIMIT 1");
            var moduleId = await conn.ExecuteScalarAsync<Guid>(
                "SELECT id FROM pack_learning.module ORDER BY sort_order LIMIT 1");
            var levelId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_learning.career_level (id, tenant_id, code, title, sort_order)
                VALUES (@LevelId, @TenantId, 'synthetic', 'synthetic', 1);
                """,
                new { LevelId = levelId, ids.TenantId });
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_learning.enrollment (id, tenant_id, employee_id, program_id, status, assigned_by_user_id)
                VALUES (@EnrollmentId, @TenantId, @EmployeeId, @ProgramId, 'in_progress', @UserId);
                INSERT INTO pack_learning.module_progress (
                    enrollment_id, module_id, status, score_pct, quiz_attempts, acknowledged_at, acknowledged_by_user_id)
                VALUES (@EnrollmentId, @ModuleId, 'passed', 80, 1, NOW(), @UserId);
                INSERT INTO pack_learning.evaluation (
                    tenant_id, employee_id, period_year, period_month,
                    score_knowledge, score_attitude, score_care, score_stock, score_discipline,
                    comment, reviewed_by_user_id)
                VALUES (@TenantId, @EmployeeId, 2026, 10, 70, 70, 70, 70, 70, 'synthetic review', @UserId);
                INSERT INTO pack_learning.recognition (tenant_id, employee_id, kind, title, created_by_user_id)
                VALUES (@TenantId, @EmployeeId, 'custom', 'synthetic recognition', @UserId);
                INSERT INTO pack_learning.credential (tenant_id, employee_id, competency_code, level_code)
                VALUES (@TenantId, @EmployeeId, 'pos_basic', 'L1');
                INSERT INTO pack_learning.employee_career (tenant_id, employee_id, level_id, assigned_by_user_id)
                VALUES (@TenantId, @EmployeeId, @LevelId, @UserId);
                """,
                new
                {
                    EnrollmentId = enrollmentId,
                    ids.TenantId,
                    ids.EmployeeId,
                    ProgramId = programId,
                    ids.UserId,
                    ModuleId = moduleId,
                    LevelId = levelId,
                });

            Assert.Equal(2, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM sales_shifts WHERE tenant_id = @TenantId AND status = 1",
                ids));
            Assert.Throws<InvalidOperationException>(() =>
                ShiftChecklistShiftSelection.Resolve([ids.ShiftA, ids.ShiftB], null, required: true));
            Assert.Equal(ids.ShiftA, ShiftChecklistShiftSelection.Resolve([ids.ShiftA, ids.ShiftB], ids.ShiftA, required: true));

            await conn.ExecuteAsync(
                """
                INSERT INTO success_shift_checklist_run (
                    tenant_id, branch_id, sales_shift_id, template_id, kind, business_date, started_by_user_id)
                VALUES
                    (@TenantId, @BranchId, @ShiftA, @TemplateId, 'open', CURRENT_DATE, @UserId),
                    (@TenantId, @BranchId, @ShiftB, @TemplateId, 'open', CURRENT_DATE, @UserId);
                """,
                ids);
            var duplicateShift = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                """
                INSERT INTO success_shift_checklist_run (
                    tenant_id, branch_id, sales_shift_id, template_id, kind, business_date, started_by_user_id)
                VALUES (@TenantId, @BranchId, @ShiftA, @TemplateId, 'open', CURRENT_DATE, @UserId)
                """,
                ids));
            Assert.NotNull(duplicateShift);
            Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM success_shift_checklist_run WHERE sales_shift_id = @ShiftA",
                ids));
            Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM success_shift_checklist_run WHERE sales_shift_id = @ShiftB",
                ids));

            var audit = new RecordingAudit();
            var tenant = new IsolatedTenant(ids.TenantId, ids.UserId);
            var tasks = new OperationalTaskService(new NpgsqlConnectionFactory(cs), tenant, new AllowBranches(), audit);
            var onShiftA = await tasks.CreateAsync(
                new CreateOperationalTaskRequest(ids.EmployeeId, "synthetic shift A", ids.BranchId, ids.ShiftA),
                CancellationToken.None);
            var onShiftB = await tasks.CreateAsync(
                new CreateOperationalTaskRequest(employeeB, "synthetic shift B", ids.BranchId, ids.ShiftB),
                CancellationToken.None);
            Assert.Equal(ids.ShiftA, onShiftA.SalesShiftId);
            Assert.Equal(ids.ShiftB, onShiftB.SalesShiftId);
            Assert.Contains("assign", audit.Actions);

            var shiftAOnly = await tasks.ListAsync(true, null, ids.ShiftA, CancellationToken.None);
            Assert.Contains(shiftAOnly, item => item.Id == onShiftA.Id);
            Assert.DoesNotContain(shiftAOnly, item => item.Id == onShiftB.Id);

            tenant.UserId = ids.UserId;
            var cancel = await Record.ExceptionAsync(() => tasks.UpdateStatusAsync(
                onShiftA.Id,
                canManage: false,
                new UpdateOperationalTaskStatusRequest("cancelled", null),
                CancellationToken.None));
            Assert.IsType<InvalidOperationException>(cancel);

            await tasks.UpdateStatusAsync(
                onShiftA.Id,
                canManage: false,
                new UpdateOperationalTaskStatusRequest("in_progress", null),
                CancellationToken.None);
            var done = await tasks.UpdateStatusAsync(
                onShiftA.Id,
                canManage: false,
                new UpdateOperationalTaskStatusRequest("done", "synthetic evidence"),
                CancellationToken.None);
            Assert.Equal("done", done.Status);
            Assert.Equal("synthetic evidence", done.EvidenceNote);
            var reopen = await Record.ExceptionAsync(() => tasks.UpdateStatusAsync(
                onShiftA.Id,
                canManage: true,
                new UpdateOperationalTaskStatusRequest("open", null),
                CancellationToken.None));
            Assert.IsType<InvalidOperationException>(reopen);

            tenant.UserId = userB;
            var hidden = await tasks.ListAsync(false, null, null, CancellationToken.None);
            Assert.DoesNotContain(hidden, item => item.Id == onShiftA.Id);
            Assert.Contains(hidden, item => item.Id == onShiftB.Id);
            var cross = await Record.ExceptionAsync(() => tasks.UpdateStatusAsync(
                onShiftA.Id,
                canManage: false,
                new UpdateOperationalTaskStatusRequest("cancelled", null),
                CancellationToken.None));
            Assert.IsType<KeyNotFoundException>(cross);
            Assert.Contains("status", audit.Actions);
        }
        finally
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM pack_learning.operational_task WHERE tenant_id = @TenantId;
                DELETE FROM pack_learning.module_progress
                WHERE enrollment_id IN (SELECT id FROM pack_learning.enrollment WHERE tenant_id = @TenantId);
                DELETE FROM pack_learning.enrollment WHERE tenant_id = @TenantId;
                DELETE FROM pack_learning.evaluation WHERE tenant_id = @TenantId;
                DELETE FROM pack_learning.recognition WHERE tenant_id = @TenantId;
                DELETE FROM pack_learning.credential WHERE tenant_id = @TenantId;
                DELETE FROM pack_learning.employee_career WHERE tenant_id = @TenantId;
                DELETE FROM pack_learning.career_level WHERE tenant_id = @TenantId;
                DELETE FROM success_shift_checklist_run WHERE tenant_id = @TenantId;
                DELETE FROM success_shift_checklist_template WHERE tenant_id = @TenantId;
                DELETE FROM sales_shifts WHERE tenant_id = @TenantId;
                DELETE FROM warehouses WHERE tenant_id = @TenantId;
                DELETE FROM users WHERE tenant_id = @TenantId;
                DELETE FROM employees WHERE tenant_id = @TenantId;
                DELETE FROM branches WHERE tenant_id = @TenantId;
                DELETE FROM tenants WHERE id = @TenantId;
                """,
                ids);
        }
    }

    [Fact]
    public async Task Operational_task_is_visible_only_to_the_assignee_or_a_manager()
    {
        var cs = await RequireIsolatedAsync();
        var ids = NewIds();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
        await SeedPeopleAsync(conn, ids);
        var tenant = new IsolatedTenant(ids.TenantId, ids.UserId);
        var tasks = new OperationalTaskService(new NpgsqlConnectionFactory(cs), tenant, new AllowBranches());
        var created = await tasks.CreateAsync(
            new CreateOperationalTaskRequest(ids.EmployeeId, "synthetic task", ids.BranchId),
            CancellationToken.None);
        await tasks.UpdateStatusAsync(
            created.Id,
            canManage: false,
            new UpdateOperationalTaskStatusRequest("done", "synthetic evidence"),
            CancellationToken.None);

        tenant.UserId = ids.OtherUserId;
        var hidden = await tasks.ListAsync(false, null, null, CancellationToken.None);
        var error = await Record.ExceptionAsync(() => tasks.UpdateStatusAsync(
            created.Id,
            false,
            new UpdateOperationalTaskStatusRequest("cancelled", null),
            CancellationToken.None));

        tenant.UserId = ids.UserId;
        var visible = await tasks.ListAsync(false, null, null, CancellationToken.None);

        Assert.Empty(hidden);
        Assert.IsType<KeyNotFoundException>(error);
        Assert.Contains(visible, item => item.Id == created.Id && item.EvidenceNote == "synthetic evidence");
        }
        finally
        {
            await CleanupPeopleAsync(conn, ids);
        }
    }

    [Fact]
    public async Task Repurchase_suggestions_do_not_cross_tenants()
    {
        var cs = await RequireIsolatedAsync();
        var ids = NewIds();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
        await SeedSaleCustomerAsync(conn, ids);
        await conn.ExecuteAsync(
            """
            INSERT INTO repurchase_suggestions (
                tenant_id, customer_id, customer_account_id, sales_order_id, order_label)
            VALUES (@TenantId, @CustomerId, @AccountId, @OrderId, 'synthetic')
            """,
            ids);
        var own = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM repurchase_suggestions rs WHERE " + StaffRepurchaseQuery.ByCustomer,
            new { ids.TenantId, ids.CustomerId });
        var other = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM repurchase_suggestions rs WHERE " + StaffRepurchaseQuery.ByCustomer,
            new { TenantId = Guid.NewGuid(), ids.CustomerId });

        Assert.Equal(1, own);
        Assert.Equal(0, other);
        }
        finally
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM repurchase_suggestions WHERE tenant_id = @TenantId;
                DELETE FROM sales_orders WHERE tenant_id = @TenantId;
                DELETE FROM customer_accounts WHERE tenant_id = @TenantId;
                DELETE FROM customers WHERE tenant_id = @TenantId;
                """,
                ids);
            await CleanupBranchAsync(conn, ids);
        }
    }

    private static async Task<int> CountVisibleAsync(NpgsqlConnection conn, Guid tenantId, string code) =>
        await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pharmacy_knowledge_adoption a
            INNER JOIN pharmacy_knowledge_rule_version v
                ON v.rule_code = a.rule_code AND v.version = a.rule_version
            WHERE a.rule_code = @Code AND 
            """ + PharmacyKnowledgeCopilotQuery.TenantPublishedPin,
            new { Code = code, TenantId = tenantId });

    private static async Task<string> RequireIsolatedAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail(
                $"Stopped before any connection. Set {ConnectionEnvironmentVariable} to an empty isolated database. "
                + "kitplatform and kitplatform_test are refused.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (DeniedDatabases.Contains(builder.Database ?? "")
            || (builder.Database ?? "").Contains("prod", StringComparison.OrdinalIgnoreCase))
            Assert.Fail($"Refusing database '{builder.Database}'. No connection was opened for writes.");

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()") ?? "";
        if (!string.Equals(database, builder.Database, StringComparison.Ordinal) || DeniedDatabases.Contains(database))
            Assert.Fail($"Refusing to run against database '{database}'. No writes were attempted.");

        await NovixaValidationSession.EnsureIsolatedRoleAsync(conn);

        var realTenants = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM tenants
            WHERE tenant_code IN ('DEMO_PHARMACY', 'NT_XUANHOA', 'KIT_MKT')
            """);
        if (realTenants != 0)
            Assert.Fail("Refusing database that already contains a real tenant code. No test rows were inserted.");

        foreach (var relation in new[]
        {
            "public.pharmacy_knowledge_approval_event",
            "public.pharmacy_safety_flag_review",
            "pack_learning.operational_task",
        })
        {
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT to_regclass(@Relation) IS NOT NULL",
                new { Relation = relation });
            if (!exists)
                Assert.Fail($"Apply migrations 411-415 on the isolated database. Missing {relation}. No test rows were inserted.");
        }

        var missingColumns = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
            FROM (VALUES
                ('public', 'pharmacy_knowledge_rule', 'approval_status'),
                ('public', 'pharmacy_knowledge_rule', 'reference_source'),
                ('public', 'success_shift_checklist_run', 'sales_shift_id'),
                ('public', 'repurchase_suggestions', 'customer_id'),
                ('public', 'sales_returns', 'client_return_id')
            ) AS need(schema_name, table_name, column_name)
            WHERE NOT EXISTS (
                SELECT 1
                FROM information_schema.columns c
                WHERE c.table_schema = need.schema_name
                  AND c.table_name = need.table_name
                  AND c.column_name = need.column_name
            )
            """);
        if (missingColumns != 0)
            Assert.Fail("Apply migrations 411-415 on the isolated database. Required columns are missing. No test rows were inserted.");

        var sourceColumn = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pharmacy_knowledge_rule_version'
              AND column_name = 'source_verification_status'
            """);
        if (sourceColumn != 1)
            Assert.Fail("Apply migration 423 on the isolated database before this test. No test rows were inserted.");

        return builder.ConnectionString;
    }

    private static IsolatedIds NewIds()
    {
        var code = "z" + Guid.NewGuid().ToString("N")[..10];
        return new IsolatedIds(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            code,
            code + "-a",
            code + "-b");
    }

    private static Task SeedBranchAsync(NpgsqlConnection conn, IsolatedIds ids) =>
        conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @Code, @Code, 1);
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @Code, @Code, 1);
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseA, @TenantId, @BranchId, @ShiftNoA, @ShiftNoA, 1, 1),
                   (@WarehouseB, @TenantId, @BranchId, @ShiftNoB, @ShiftNoB, 1, 1);
            INSERT INTO users (id, tenant_id, username, email, password_hash, status)
            VALUES (@UserId, @TenantId, @Code, @Code || '@example.test', 'not-a-login', 1);
            """,
            ids);

    private static Task SeedPeopleAsync(NpgsqlConnection conn, IsolatedIds ids) =>
        conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @Code, @Code, 1);
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @Code, @Code, 1);
            INSERT INTO employees (id, tenant_id, employee_code, full_name, status)
            VALUES (@EmployeeId, @TenantId, @Code, @Code, 1);
            INSERT INTO users (id, tenant_id, employee_id, username, email, password_hash, status)
            VALUES
                (@UserId, @TenantId, @EmployeeId, @Code, @Code || '@example.test', 'not-a-login', 1),
                (@OtherUserId, @TenantId, NULL, @ShiftNoA, @ShiftNoA || '@example.test', 'not-a-login', 1);
            """,
            ids);

    private static Task SeedSaleCustomerAsync(NpgsqlConnection conn, IsolatedIds ids) =>
        conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @Code, @Code, 1);
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @Code, @Code, 1);
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseA, @TenantId, @BranchId, @Code, @Code, 1, 1);
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
            VALUES (@CustomerId, @TenantId, @Code, @Code, @Code, 1);
            INSERT INTO customer_accounts (id, tenant_id, customer_id, phone)
            VALUES (@AccountId, @TenantId, @CustomerId, @Code);
            INSERT INTO sales_orders (
                id, tenant_id, order_number, branch_id, warehouse_id, customer_id,
                status, price_type, subtotal, discount_amount, total_amount, amount_paid, outstanding)
            VALUES (
                @OrderId, @TenantId, @Code, @BranchId, @WarehouseA, @CustomerId,
                2, 1, 1, 0, 1, 1, 0);
            """,
            ids);

    private static Task CleanupBranchAsync(NpgsqlConnection conn, IsolatedIds ids) =>
        conn.ExecuteAsync(
            """
            DELETE FROM success_shift_checklist_run WHERE tenant_id = @TenantId;
            DELETE FROM success_shift_checklist_template WHERE tenant_id = @TenantId;
            DELETE FROM sales_shifts WHERE tenant_id = @TenantId;
            DELETE FROM warehouses WHERE tenant_id = @TenantId;
            DELETE FROM users WHERE tenant_id = @TenantId;
            DELETE FROM branches WHERE tenant_id = @TenantId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            ids);

    private static Task CleanupPeopleAsync(NpgsqlConnection conn, IsolatedIds ids) =>
        conn.ExecuteAsync(
            """
            DELETE FROM pack_learning.operational_task WHERE tenant_id = @TenantId;
            DELETE FROM users WHERE tenant_id = @TenantId;
            DELETE FROM employees WHERE tenant_id = @TenantId;
            DELETE FROM branches WHERE tenant_id = @TenantId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            ids);

    private sealed class RecordingAudit : IAuditLogService
    {
        public List<string> Actions { get; } = [];

        public Task WriteAsync(
            string entityType,
            Guid? entityId,
            string action,
            object? payload = null,
            CancellationToken cancellationToken = default)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class AllowBranches : IBranchAccessService
    {
        public Task<BranchAccessScope> GetScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(BranchAccessScope.UnrestrictedScope());

        public Task EnsureWarehouseAccessAsync(Guid warehouseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task EnsureBranchAccessAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(Guid? WarehouseId, Guid[]? AllowedWarehouseIds)> ResolveWarehouseQueryAsync(
            Guid? requestedWarehouseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Guid?, Guid[]?)>((requestedWarehouseId, null));
    }

    private sealed class IsolatedTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; set; } = tenantId;
        public Guid UserId { get; set; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    private sealed record IsolatedIds(
        Guid TenantId,
        Guid BranchId,
        Guid WarehouseA,
        Guid WarehouseB,
        Guid UserId,
        Guid OtherUserId,
        Guid EmployeeId,
        Guid TemplateId,
        Guid ShiftA,
        Guid ShiftB,
        Guid CustomerId,
        Guid AccountId,
        string Code,
        string ShiftNoA,
        string ShiftNoB)
    {
        public Guid OrderId { get; } = Guid.NewGuid();
    }
}
