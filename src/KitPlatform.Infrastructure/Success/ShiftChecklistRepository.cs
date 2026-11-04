using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Success;

internal sealed class ShiftChecklistRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ShiftChecklistRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task EnsureDefaultTemplatesAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var existing = (await conn.QueryAsync<string>(
            """
            SELECT kind FROM success_shift_checklist_template
            WHERE tenant_id = @TenantId AND is_active
            """,
            new { TenantId },
            tx)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains("open"))
            await InsertTemplateAsync(conn, tx, "open", "Checklist mở ca", DefaultOpenItems());
        if (!existing.Contains("close"))
            await InsertTemplateAsync(conn, tx, "close", "Checklist đóng ca", DefaultCloseItems());

        await tx.CommitAsync(cancellationToken);
    }

    private async Task InsertTemplateAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        string kind,
        string title,
        IReadOnlyList<(string Label, bool Required)> items)
    {
        var templateId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO success_shift_checklist_template (tenant_id, kind, title)
            VALUES (@TenantId, @Kind, @Title)
            RETURNING id
            """,
            new { TenantId, Kind = kind, Title = title },
            tx);

        var order = 0;
        foreach (var (label, required) in items)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO success_shift_checklist_template_item (template_id, sort_order, label, is_required)
                VALUES (@TemplateId, @SortOrder, @Label, @IsRequired)
                """,
                new
                {
                    TemplateId = templateId,
                    SortOrder = order++,
                    Label = label,
                    IsRequired = required,
                },
                tx);
        }
    }

    private static IReadOnlyList<(string Label, bool Required)> DefaultOpenItems() =>
    [
        ("Kiểm tra két / quỹ đầu ca", true),
        ("Kiểm tra máy POS / in bill", true),
        ("Kiểm tra tủ lạnh / bảo quản", true),
        ("Rà tồn cận HSD cần xử lý trong ca", true),
        ("Dọn quầy / vệ sinh khu vực bán", true),
        ("Nhận bàn giao ca trước (nếu có)", false),
    ];

    private static IReadOnlyList<(string Label, bool Required)> DefaultCloseItems() =>
    [
        ("Đối chiếu két / quỹ cuối ca", true),
        ("Kiểm tra đơn mở / nháp còn treo", true),
        ("Ghi nhận sự cố / khiếu nại trong ca", false),
        ("Tắt thiết bị / khóa két", true),
        ("Dọn bàn quầy cuối ca", true),
        ("Bàn giao ca sau hoặc khóa cửa", true),
    ];

    public async Task<IReadOnlyList<ShiftChecklistBranchOptionDto>> ListBranchesAsync(
        Guid[]? allowedBranchIds,
        CancellationToken cancellationToken)
    {
        var filter = allowedBranchIds is { Length: > 0 }
            ? "AND b.id = ANY(@AllowedBranchIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ShiftChecklistBranchOptionDto>(
            $"""
            SELECT b.id AS Id, b.branch_name AS Name, b.branch_code AS Code
            FROM branches b
            WHERE b.tenant_id = @TenantId
              AND b.deleted_at IS NULL
              {filter}
            ORDER BY b.branch_name
            """,
            new { TenantId, AllowedBranchIds = allowedBranchIds });
        return rows.AsList();
    }

    public async Task<(Guid Id, string Title)?> GetActiveTemplateAsync(string kind, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Title)>(
            """
            SELECT id AS Id, title AS Title
            FROM success_shift_checklist_template
            WHERE tenant_id = @TenantId AND kind = @Kind AND is_active
            LIMIT 1
            """,
            new { TenantId, Kind = kind });
        return row.Id == Guid.Empty ? null : row;
    }

    public async Task<IReadOnlyList<(Guid Id, int SortOrder, string Label, bool IsRequired)>> GetTemplateItemsAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid Id, int SortOrder, string Label, bool IsRequired)>(
            """
            SELECT id AS Id, sort_order AS SortOrder, label AS Label, is_required AS IsRequired
            FROM success_shift_checklist_template_item
            WHERE template_id = @TemplateId
            ORDER BY sort_order, label
            """,
            new { TemplateId = templateId });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<ShiftChecklistOpenShiftDto>> ListOpenShiftsForBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ShiftChecklistOpenShiftDto>(
            """
            SELECT s.id AS Id, s.shift_number AS ShiftNumber, w.warehouse_name AS WarehouseName
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id
            WHERE s.tenant_id = @TenantId
              AND w.branch_id = @BranchId
              AND s.status = 1
            ORDER BY s.opened_at
            """,
            new { TenantId, BranchId = branchId });
        return rows.AsList();
    }

    public async Task<bool> OpenShiftBelongsToBranchAsync(
        Guid salesShiftId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM sales_shifts s
                INNER JOIN warehouses w ON w.id = s.warehouse_id
                WHERE s.id = @ShiftId
                  AND s.tenant_id = @TenantId
                  AND w.branch_id = @BranchId
                  AND s.status = 1
            )
            """,
            new { TenantId, ShiftId = salesShiftId, BranchId = branchId });
    }

    public async Task<Guid?> FindRunIdByShiftAsync(
        Guid salesShiftId,
        string kind,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id
            FROM success_shift_checklist_run
            WHERE tenant_id = @TenantId
              AND sales_shift_id = @SalesShiftId
              AND kind = @Kind
            LIMIT 1
            """,
            new { TenantId, SalesShiftId = salesShiftId, Kind = kind });
    }

    public async Task<Guid> CreateRunAsync(
        Guid branchId,
        Guid salesShiftId,
        Guid templateId,
        string kind,
        DateOnly businessDate,
        Guid userId,
        IReadOnlyList<(Guid TemplateItemId, int SortOrder, string Label, bool IsRequired)> items,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var runId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO success_shift_checklist_run (
                tenant_id, branch_id, sales_shift_id, template_id, kind, business_date, status, started_by_user_id
            )
            VALUES (
                @TenantId, @BranchId, @SalesShiftId, @TemplateId, @Kind, @BusinessDate, 'in_progress', @UserId
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                BranchId = branchId,
                SalesShiftId = salesShiftId,
                TemplateId = templateId,
                Kind = kind,
                BusinessDate = businessDate,
                UserId = userId,
            },
            tx);

        foreach (var item in items)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO success_shift_checklist_run_item (
                    run_id, template_item_id, sort_order, label, is_required
                )
                VALUES (
                    @RunId, @TemplateItemId, @SortOrder, @Label, @IsRequired
                )
                """,
                new
                {
                    RunId = runId,
                    TemplateItemId = item.TemplateItemId,
                    SortOrder = item.SortOrder,
                    Label = item.Label,
                    IsRequired = item.IsRequired,
                },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
        return runId;
    }

    public async Task<ShiftChecklistRunDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<(
            Guid Id,
            Guid BranchId,
            string BranchName,
            string Kind,
            DateOnly BusinessDate,
            string Status,
            DateTime StartedAt,
            DateTime? CompletedAt)>(
            """
            SELECT
                r.id AS Id,
                r.branch_id AS BranchId,
                b.branch_name AS BranchName,
                r.kind AS Kind,
                r.business_date AS BusinessDate,
                r.status AS Status,
                r.started_at AS StartedAt,
                r.completed_at AS CompletedAt
            FROM success_shift_checklist_run r
            INNER JOIN branches b ON b.id = r.branch_id
            WHERE r.id = @RunId AND r.tenant_id = @TenantId
            """,
            new { RunId = runId, TenantId });

        if (header.Id == Guid.Empty)
            return null;

        var items = (await conn.QueryAsync<ShiftChecklistRunItemDto>(
            """
            SELECT
                id AS Id,
                label AS Label,
                is_required AS IsRequired,
                is_checked AS IsChecked,
                checked_at AS CheckedAt,
                checked_by_user_id AS CheckedByUserId
            FROM success_shift_checklist_run_item
            WHERE run_id = @RunId
            ORDER BY sort_order, label
            """,
            new { RunId = runId })).AsList();

        return new ShiftChecklistRunDto(
            header.Id,
            header.BranchId,
            header.BranchName,
            header.Kind,
            header.BusinessDate,
            header.Status,
            header.StartedAt,
            header.CompletedAt,
            items);
    }

    public async Task<bool> SetItemCheckedAsync(
        Guid runId,
        Guid itemId,
        bool checkedValue,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE success_shift_checklist_run_item i
            SET
                is_checked = @Checked,
                checked_by_user_id = CASE WHEN @Checked THEN @UserId ELSE NULL END,
                checked_at = CASE WHEN @Checked THEN NOW() ELSE NULL END
            FROM success_shift_checklist_run r
            WHERE i.id = @ItemId
              AND i.run_id = @RunId
              AND r.id = i.run_id
              AND r.tenant_id = @TenantId
              AND r.status = 'in_progress'
            """,
            new { ItemId = itemId, RunId = runId, Checked = checkedValue, UserId = userId, TenantId });
        return n > 0;
    }

    public async Task<(int RequiredTotal, int RequiredChecked)> CountRequiredAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<(int RequiredTotal, int RequiredChecked)>(
            """
            SELECT
                COUNT(*) FILTER (WHERE is_required)::int AS RequiredTotal,
                COUNT(*) FILTER (WHERE is_required AND is_checked)::int AS RequiredChecked
            FROM success_shift_checklist_run_item
            WHERE run_id = @RunId
            """,
            new { RunId = runId });
    }

    public async Task<bool> CompleteRunAsync(Guid runId, Guid userId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE success_shift_checklist_run
            SET status = 'completed',
                completed_by_user_id = @UserId,
                completed_at = NOW()
            WHERE id = @RunId
              AND tenant_id = @TenantId
              AND status = 'in_progress'
            """,
            new { RunId = runId, UserId = userId, TenantId });
        return n > 0;
    }

    public async Task<ShiftChecklistKindStatusDto> GetKindStatusAsync(
        Guid branchId,
        string kind,
        Guid salesShiftId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<(
            Guid? RunId,
            string? Status,
            DateTime? CompletedAt,
            int CheckedCount,
            int TotalCount,
            int RequiredMissing)>(
            """
            SELECT
                r.id AS RunId,
                r.status AS Status,
                r.completed_at AS CompletedAt,
                COALESCE((
                    SELECT COUNT(*)::int FROM success_shift_checklist_run_item i
                    WHERE i.run_id = r.id AND i.is_checked
                ), 0) AS CheckedCount,
                COALESCE((
                    SELECT COUNT(*)::int FROM success_shift_checklist_run_item i
                    WHERE i.run_id = r.id
                ), 0) AS TotalCount,
                COALESCE((
                    SELECT COUNT(*)::int FROM success_shift_checklist_run_item i
                    WHERE i.run_id = r.id AND i.is_required AND NOT i.is_checked
                ), 0) AS RequiredMissing
            FROM success_shift_checklist_run r
            WHERE r.tenant_id = @TenantId
              AND r.branch_id = @BranchId
              AND r.kind = @Kind
              AND r.sales_shift_id = @SalesShiftId
            """,
            new { TenantId, BranchId = branchId, Kind = kind, SalesShiftId = salesShiftId });

        if (row.RunId is null)
        {
            return new ShiftChecklistKindStatusDto(kind, "missing", null, 0, 0, 0, null, salesShiftId);
        }

        return new ShiftChecklistKindStatusDto(
            kind,
            row.Status ?? "missing",
            row.RunId,
            row.CheckedCount,
            row.TotalCount,
            row.RequiredMissing,
            row.CompletedAt,
            salesShiftId);
    }
}
