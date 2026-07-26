using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyChildRequestService : IFamilyChildRequestService
{
    public const int MaxRequestsPerWeek = 3;

    private readonly FamilyChildRequestRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyDayFlowService _dayFlows;
    private readonly IFamilyCalendarPeriodService _periods;
    private readonly IFamilyScreenWalletService _wallet;
    private readonly IFamilyScoreService _score;
    private readonly IFamilyOsParentPushService _parentPush;
    private readonly ITenantContext _tenant;

    public FamilyChildRequestService(
        FamilyChildRequestRepository repo,
        FamilyGraphRepository families,
        IFamilyDayFlowService dayFlows,
        IFamilyCalendarPeriodService periods,
        IFamilyScreenWalletService wallet,
        IFamilyScoreService score,
        IFamilyOsParentPushService parentPush,
        ITenantContext tenant)
    {
        _repo = repo;
        _families = families;
        _dayFlows = dayFlows;
        _periods = periods;
        _wallet = wallet;
        _score = score;
        _parentPush = parentPush;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FamilyChildRequestDto>> ListAsync(
        Guid familyId,
        string? status = null,
        Guid? memberId = null,
        int limit = 40,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _repo.ListAsync(familyId, status, memberId, limit, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyChildRequestDto> CreateAsync(
        Guid familyId,
        FamilyChildRequestCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var child = members.FirstOrDefault(m => m.Id == request.MemberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");
        if (!string.Equals(child.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ con mới gửi đề xuất.");

        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? FamilyChildRequestKinds.ScreenMinutes
            : request.Kind.Trim().ToLowerInvariant();
        if (!FamilyChildRequestKinds.All.Contains(kind))
            throw new InvalidOperationException("Loại đề xuất không hợp lệ.");

        var isMission = kind == FamilyChildRequestKinds.DayMission;
        var titleVi = Trim(request.TitleVi, 200);
        int? amountMinutes = request.AmountMinutes;

        if (isMission)
        {
            if (string.IsNullOrWhiteSpace(titleVi))
                throw new InvalidOperationException("Nhập tên việc muốn đề xuất hôm nay.");
            if (amountMinutes is < 5 or > 240)
                amountMinutes = null;
        }
        else
        {
            if (amountMinutes is < 5 or > 120)
                throw new InvalidOperationException("Số phút xin phải từ 5 đến 120.");
        }

        var reasons = (request.ReasonCodes ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!isMission && reasons.Length == 0)
            throw new InvalidOperationException("Chọn ít nhất một lý do.");
        if (isMission && reasons.Length == 0)
            reasons = ["other"];

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;

        if (!isMission)
        {
            var pendingToday = await _repo.GetPendingTodayAsync(
                familyId, request.MemberId, flowDate, cancellationToken);
            if (pendingToday is not null
                && string.Equals(pendingToday.Kind, FamilyChildRequestKinds.ScreenMinutes, StringComparison.OrdinalIgnoreCase))
                return Map(pendingToday);
        }

        var weekStart = flowDate.AddDays(-(int)(flowDate.DayOfWeek == DayOfWeek.Sunday
            ? 6
            : flowDate.DayOfWeek - DayOfWeek.Monday));
        var weekEnd = weekStart.AddDays(6);
        var weekCount = await _repo.CountPendingOrRecentWeekAsync(
            familyId, request.MemberId, weekStart, weekEnd, cancellationToken);
        var maxWeek = isMission ? 8 : MaxRequestsPerWeek;
        if (weekCount >= maxWeek)
            throw new InvalidOperationException(
                $"Tuần này đã gửi {maxWeek} đề xuất. Hãy chờ bố mẹ duyệt hoặc tuần sau nhé.");

        var (summary, recommend) = isMission
            ? await BuildMissionSummaryAsync(
                familyId, child.Id, child.DisplayName, flowDate, titleVi!, reasons, weekCount,
                cancellationToken)
            : await BuildAiSummaryAsync(
                familyId, child.Id, child.DisplayName, flowDate, amountMinutes ?? 30, reasons,
                weekCount, cancellationToken);

        var id = await _repo.InsertAsync(
            familyId,
            request.MemberId,
            flowDate,
            kind,
            amountMinutes,
            titleVi,
            request.WindowStart,
            request.WindowEnd,
            reasons,
            Trim(request.ReasonNote, 400),
            summary,
            recommend,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được đề xuất.");

        try
        {
            var shortName = ShortName(child.DisplayName);
            var pushTitle = isMission
                ? $"{shortName} đề xuất việc: {titleVi}"
                : $"{shortName} xin thêm {amountMinutes} phút";
            await _parentPush.TryNotifyFamilyAsync(
                _tenant.TenantId,
                familyId,
                flowDate,
                kind: "child_request",
                title: pushTitle,
                body: summary.Length > 160 ? summary[..157] + "…" : summary,
                url: "/today",
                dataType: "familyos_child_request",
                payloadSummary: id.ToString("D"),
                cancellationToken: cancellationToken);
        }
        catch
        {
            // saved — push optional
        }

        return Map(row);
    }

    public async Task<FamilyChildRequestDto> DecideAsync(
        Guid familyId,
        Guid requestId,
        FamilyChildRequestDecideRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var decider = members.FirstOrDefault(m => m.Id == request.DecidedByMemberId)
            ?? throw new InvalidOperationException("Người quyết định không thuộc gia đình.");
        if (decider.RoleCode is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
            throw new InvalidOperationException("Chỉ bố mẹ / người chăm sóc mới duyệt.");

        var row = await _repo.GetAsync(familyId, requestId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy đề xuất.");
        if (!string.Equals(row.Status, FamilyChildRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Map(row);

        var isMission = string.Equals(
            row.Kind, FamilyChildRequestKinds.DayMission, StringComparison.OrdinalIgnoreCase);

        var decision = (request.Decision ?? "").Trim().ToLowerInvariant();
        string status;
        int? granted = null;
        switch (decision)
        {
            case "approve":
            case "approved":
                status = FamilyChildRequestStatuses.Approved;
                granted = isMission ? null : row.AmountMinutes;
                break;
            case "partial":
                if (isMission)
                {
                    status = FamilyChildRequestStatuses.Approved;
                    granted = null;
                }
                else
                {
                    status = FamilyChildRequestStatuses.Partial;
                    var baseAmt = row.AmountMinutes ?? 30;
                    granted = request.GrantedMinutes is > 0
                        ? Math.Min(request.GrantedMinutes.Value, baseAmt)
                        : Math.Max(5, baseAmt / 2);
                }
                break;
            case "reject":
            case "rejected":
                status = FamilyChildRequestStatuses.Rejected;
                granted = isMission ? null : 0;
                break;
            default:
                throw new InvalidOperationException("Decision phải là approve | reject | partial.");
        }

        await _repo.DecideAsync(
            familyId, requestId, status, request.DecidedByMemberId, granted,
            Trim(request.Note, 400), cancellationToken);

        if (status == FamilyChildRequestStatuses.Approved && isMission
            && !string.IsNullOrWhiteSpace(row.TitleVi))
        {
            try
            {
                await _dayFlows.AddAdHocCommitmentAsync(
                    familyId,
                    new AddAdHocCommitmentRequest(
                        row.FlowDate,
                        row.MemberId,
                        row.TitleVi!,
                        Description: row.ReasonNote ?? row.AiSummaryVi,
                        WindowStart: row.WindowStart,
                        WindowEnd: row.WindowEnd,
                        ExpectedDurationMinutes: row.AmountMinutes,
                        Priority: "normal"),
                    cancellationToken);
            }
            catch
            {
                // mission apply best-effort after decide is recorded
            }
        }
        else if (granted is > 0 && !isMission)
        {
            try
            {
                await _wallet.ApplyGrantAsync(
                    familyId,
                    row.MemberId,
                    granted.Value,
                    sourceRef: $"child_request:{requestId:D}",
                    noteVi: $"Duyệt xin +{granted} phút",
                    cancellationToken);
            }
            catch
            {
                // grant is best-effort if wallet week not active yet
            }
        }

        return Map((await _repo.GetAsync(familyId, requestId, cancellationToken))!);
    }

    private async Task<(string Summary, string Recommend)> BuildMissionSummaryAsync(
        Guid familyId,
        Guid childId,
        string childDisplayName,
        DateOnly flowDate,
        string titleVi,
        string[] reasons,
        int weekCountBefore,
        CancellationToken cancellationToken)
    {
        var shortName = ShortName(childDisplayName);
        var bullets = new List<string> { $"✓ Muốn thêm việc «{titleVi}» vào hôm nay." };
        foreach (var code in reasons)
        {
            if (FamilyChildRequestReasons.LabelsVi.TryGetValue(code, out var label))
                bullets.Add($"✓ {label}.");
        }

        try
        {
            var flow = await _dayFlows.GetDayFlowAsync(familyId, flowDate, cancellationToken);
            if (flow is not null)
            {
                var mine = flow.Commitments.Where(c => c.MemberId == childId).ToList();
                var done = mine.Count(c =>
                    string.Equals(c.Status, "done", StringComparison.OrdinalIgnoreCase));
                if (mine.Count > 0)
                    bullets.Add($"○ Routine hôm nay: {done}/{mine.Count} việc.");
            }
        }
        catch
        {
            // optional
        }

        if (weekCountBefore == 0)
            bullets.Add("✓ Tuần này chưa gửi đề xuất việc lần nào.");
        else
            bullets.Add($"○ Tuần này đã gửi {weekCountBefore} đề xuất.");

        var score = await _score.GetWeekScoreAsync(familyId, cancellationToken);
        var recommend = score.AllowBonusMinutes || score.RoutinePct >= 40 ? "approve" : "partial";
        var summary = $"{shortName} đề xuất việc hôm nay.\n" + string.Join("\n", bullets);
        return (summary, recommend);
    }

    private async Task<(string Summary, string Recommend)> BuildAiSummaryAsync(
        Guid familyId,
        Guid childId,
        string childDisplayName,
        DateOnly flowDate,
        int amountMinutes,
        string[] reasons,
        int weekCountBefore,
        CancellationToken cancellationToken)
    {
        var bullets = new List<string>();
        var shortName = ShortName(childDisplayName);

        try
        {
            var flow = await _dayFlows.GetDayFlowAsync(familyId, flowDate, cancellationToken);
            if (flow is not null)
            {
                var mine = flow.Commitments
                    .Where(c => c.MemberId == childId)
                    .ToList();
                var total = mine.Count;
                var done = mine.Count(c =>
                    string.Equals(c.Status, "done", StringComparison.OrdinalIgnoreCase));
                if (total > 0 && done == total)
                    bullets.Add("✓ Đã hoàn thành toàn bộ Routine hôm nay.");
                else if (total > 0)
                    bullets.Add($"○ Routine hôm nay: {done}/{total} việc.");
            }
        }
        catch
        {
            // optional
        }

        foreach (var code in reasons)
        {
            if (FamilyChildRequestReasons.LabelsVi.TryGetValue(code, out var label))
                bullets.Add($"✓ {label}.");
        }

        if (weekCountBefore == 0)
            bullets.Add("✓ Tuần này chưa xin thêm lần nào.");
        else
            bullets.Add($"○ Tuần này đã xin {weekCountBefore} lần.");

        try
        {
            var resolved = await _periods.ResolveAsync(familyId, flowDate, cancellationToken);
            if (!string.IsNullOrWhiteSpace(resolved.PeriodKind)
                && !string.Equals(resolved.PeriodKind, "school_year", StringComparison.OrdinalIgnoreCase))
            {
                bullets.Add($"○ Đang ở chế độ {FamilyCalendarPeriodKinds.LabelVi(resolved.PeriodKind)}.");
            }
        }
        catch
        {
            // optional
        }

        var score = await _score.GetWeekScoreAsync(familyId, cancellationToken);
        string recommend;
        if (!score.AllowBonusMinutes)
        {
            recommend = "reject";
            bullets.Add("○ Family Score tuần này chưa đủ điều kiện thưởng phút.");
        }
        else if (weekCountBefore >= MaxRequestsPerWeek - 1)
        {
            recommend = "partial";
        }
        else if (bullets.Any(b => b.StartsWith('✓') && b.Contains("toàn bộ Routine", StringComparison.Ordinal)))
        {
            recommend = "approve";
        }
        else if (bullets.Count(b => b.StartsWith('✓')) >= 2)
        {
            recommend = "approve";
        }
        else
        {
            recommend = "partial";
        }

        var summary =
            $"{shortName} xin +{amountMinutes} phút.\n" +
            string.Join("\n", bullets);

        return (summary, recommend);
    }

    private static FamilyChildRequestDto Map(FamilyChildRequestRepository.ChildRequestRow r) =>
        new(
            r.Id,
            r.FamilyId,
            r.MemberId,
            r.MemberName,
            r.FlowDate,
            r.Kind,
            r.AmountMinutes,
            r.TitleVi,
            r.WindowStart,
            r.WindowEnd,
            r.ReasonCodes ?? [],
            r.ReasonNote,
            r.Status,
            r.AiSummaryVi,
            r.AiRecommend,
            r.GrantedMinutes,
            r.CreatedAt,
            r.DecidedAt);

    private static string ShortName(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? name : parts[^1];
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Trim().Length <= max ? value.Trim()
        : value.Trim()[..max];
}
