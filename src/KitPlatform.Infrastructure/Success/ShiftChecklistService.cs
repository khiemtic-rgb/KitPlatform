using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;

namespace KitPlatform.Infrastructure.Success;

internal sealed class ShiftChecklistService : IShiftChecklistService
{
    private readonly ShiftChecklistRepository _repo;
    private readonly IBranchAccessService _branchAccess;
    private readonly ITenantContext _tenant;

    public ShiftChecklistService(
        ShiftChecklistRepository repo,
        IBranchAccessService branchAccess,
        ITenantContext tenant)
    {
        _repo = repo;
        _branchAccess = branchAccess;
        _tenant = tenant;
    }

    public async Task<ShiftChecklistTodayDto> GetTodayAsync(
        Guid? branchId,
        Guid? salesShiftId = null,
        CancellationToken cancellationToken = default)
    {
        await _repo.EnsureDefaultTemplatesAsync(cancellationToken);
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        var allowed = scope.Unrestricted ? null : scope.BranchIds.ToArray();
        var branches = await _repo.ListBranchesAsync(allowed, cancellationToken);
        if (branches.Count == 0)
        {
            var empty = new ShiftChecklistKindStatusDto("open", "missing", null, 0, 0, 0, null);
            var emptyClose = empty with { Kind = "close" };
            return new ShiftChecklistTodayDto(VietnamBusinessCalendar.Today(DateTime.UtcNow), null, branches, empty, emptyClose);
        }

        var selected = ResolveBranch(branchId, branches, scope);
        await _branchAccess.EnsureBranchAccessAsync(selected, cancellationToken);

        var businessDate = VietnamBusinessCalendar.Today(DateTime.UtcNow);
        var openShifts = await _repo.ListOpenShiftsForBranchAsync(selected, cancellationToken);
        var chosen = ShiftChecklistShiftSelection.Resolve(
            openShifts.Select(s => s.Id).ToList(),
            salesShiftId,
            required: false);
        ShiftChecklistKindStatusDto open;
        ShiftChecklistKindStatusDto close;
        if (chosen is Guid shiftId)
        {
            open = await _repo.GetKindStatusAsync(selected, "open", shiftId, cancellationToken);
            close = await _repo.GetKindStatusAsync(selected, "close", shiftId, cancellationToken);
        }
        else
        {
            open = new ShiftChecklistKindStatusDto("open", "missing", null, 0, 0, 0, null);
            close = open with { Kind = "close" };
        }

        return new ShiftChecklistTodayDto(businessDate, selected, branches, open, close, openShifts);
    }

    public async Task<ShiftChecklistRunDto> StartOrGetAsync(
        StartShiftChecklistRequest request,
        CancellationToken cancellationToken = default)
    {
        var kind = NormalizeKind(request.Kind);
        await _branchAccess.EnsureBranchAccessAsync(request.BranchId, cancellationToken);
        await _repo.EnsureDefaultTemplatesAsync(cancellationToken);

        var businessDate = VietnamBusinessCalendar.Today(DateTime.UtcNow);
        var salesShiftId = await ResolveOpenShiftAsync(request, cancellationToken);
        var existing = await _repo.FindRunIdByShiftAsync(salesShiftId, kind, cancellationToken);
        if (existing is Guid runId)
            return await RequireRunAsync(runId, cancellationToken);

        var template = await _repo.GetActiveTemplateAsync(kind, cancellationToken);
        if (template is null)
            throw new InvalidOperationException($"Không có template checklist '{kind}'.");
        var templateId = template.Value.Id;
        var templateItems = await _repo.GetTemplateItemsAsync(templateId, cancellationToken);
        if (templateItems.Count == 0)
            throw new InvalidOperationException("Template checklist trống.");

        try
        {
            runId = await _repo.CreateRunAsync(
                request.BranchId,
                salesShiftId,
                templateId,
                kind,
                businessDate,
                _tenant.UserId,
                templateItems.Select(i => (i.Id, i.SortOrder, i.Label, i.IsRequired)).ToList(),
                cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("ux_success_shift_checklist_run_shift_kind", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
        {
            var raced = await _repo.FindRunIdByShiftAsync(salesShiftId, kind, cancellationToken);
            if (raced is null) throw;
            runId = raced.Value;
        }

        return await RequireRunAsync(runId, cancellationToken);
    }

    public async Task<ShiftChecklistRunDto> SetItemCheckedAsync(
        Guid runId,
        Guid itemId,
        bool checkedValue,
        CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        await _branchAccess.EnsureBranchAccessAsync(run.BranchId, cancellationToken);
        if (!string.Equals(run.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Checklist đã hoàn thành, không thể sửa.");

        var ok = await _repo.SetItemCheckedAsync(runId, itemId, checkedValue, _tenant.UserId, cancellationToken);
        if (!ok)
            throw new KeyNotFoundException("Không tìm thấy mục checklist hoặc đã đóng.");

        return await RequireRunAsync(runId, cancellationToken);
    }

    public async Task<ShiftChecklistRunDto> CompleteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        await _branchAccess.EnsureBranchAccessAsync(run.BranchId, cancellationToken);
        if (string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase))
            return run;

        var (requiredTotal, requiredChecked) = await _repo.CountRequiredAsync(runId, cancellationToken);
        if (requiredChecked < requiredTotal)
            throw new InvalidOperationException($"Còn {requiredTotal - requiredChecked} mục bắt buộc chưa tick.");

        var ok = await _repo.CompleteRunAsync(runId, _tenant.UserId, cancellationToken);
        if (!ok)
        {
            var current = await RequireRunAsync(runId, cancellationToken);
            if (string.Equals(current.Status, "completed", StringComparison.OrdinalIgnoreCase))
                return current;
            throw new InvalidOperationException("Không hoàn thành được checklist.");
        }

        return await RequireRunAsync(runId, cancellationToken);
    }

    private async Task<Guid> ResolveOpenShiftAsync(
        StartShiftChecklistRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SalesShiftId is Guid requested)
        {
            var belongs = await _repo.OpenShiftBelongsToBranchAsync(requested, request.BranchId, cancellationToken);
            if (!belongs)
                throw new InvalidOperationException("Ca bán không đang mở tại chi nhánh này.");
            return requested;
        }

        var open = await _repo.ListOpenShiftsForBranchAsync(request.BranchId, cancellationToken);
        return ShiftChecklistShiftSelection.Resolve(
            open.Select(s => s.Id).ToList(),
            request.SalesShiftId,
            required: true)
            ?? throw new InvalidOperationException("Chi nhánh có nhiều ca đang mở. Chọn ca cụ thể.");
    }

    private async Task<ShiftChecklistRunDto> RequireRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repo.GetRunAsync(runId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy checklist ca.");
        return run;
    }

    private static Guid ResolveBranch(
        Guid? branchId,
        IReadOnlyList<ShiftChecklistBranchOptionDto> branches,
        BranchAccessScope scope)
    {
        if (branchId is Guid requested)
        {
            if (branches.Any(b => b.Id == requested))
                return requested;
            throw new UnauthorizedAccessException("Không có quyền chi nhánh đã chọn.");
        }

        if (scope.PrimaryBranchId is Guid primary && branches.Any(b => b.Id == primary))
            return primary;

        return branches[0].Id;
    }

    private static string NormalizeKind(string? kind)
    {
        var k = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (k is not ("open" or "close"))
            throw new ArgumentException("Kind phải là 'open' hoặc 'close'.");
        return k;
    }
}
