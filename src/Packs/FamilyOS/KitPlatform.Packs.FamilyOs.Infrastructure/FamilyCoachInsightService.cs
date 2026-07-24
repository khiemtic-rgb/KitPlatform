using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCoachInsightService : IFamilyCoachInsightService
{
    public const int PatternWindowDays = 7;
    public const int PatternForgotThreshold = 3;

    private readonly FamilyCoachInsightRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyDayFlowService _dayFlows;

    public FamilyCoachInsightService(
        FamilyCoachInsightRepository repo,
        FamilyGraphRepository families,
        IFamilyDayFlowService dayFlows)
    {
        _repo = repo;
        _families = families;
        _dayFlows = dayFlows;
    }

    public async Task<FamilyCoachInsightDto> GetInsightAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;

        var flow = await _dayFlows.GetDayFlowAsync(familyId, date, cancellationToken)
            ?? await _dayFlows.EnsureDayFlowAsync(
                familyId, new EnsureDayFlowRequest(date, null), cancellationToken);

        var historyFrom = date.AddDays(-(PatternWindowDays - 1));
        var history = await _repo.ListHistoryAsync(
            familyId, historyFrom, date, family.Timezone, cancellationToken);

        return Compose(flow, history, date);
    }

    internal static FamilyCoachInsightDto Compose(
        DayFlowDto flow,
        IReadOnlyList<FamilyCoachInsightRepository.HistoryRow> history,
        DateOnly date)
    {
        var total = flow.TotalCommitments;
        var done = flow.DoneCount;
        var open = flow.PendingCount;
        var skipped = flow.Commitments.Count(c => c.Status == FamilyCommitmentStatuses.Skipped);

        var headline = total <= 0
            ? "Chưa có cam kết trong ngày"
            : skipped > 0
                ? $"Hôm nay {done}/{total} hoàn thành · {skipped} bỏ qua"
                : $"Hôm nay {done}/{total} cam kết hoàn thành";

        string? strength = null;
        string? attention = null;
        string? patternText = null;
        string? proposal = null;
        string? proposalCode = null;
        string? ctaPath = "/family-os/routines";
        string? ctaLabel = "Mở Routine";
        Guid? focusMemberId = null;
        string? focusMemberName = null;
        Guid? focusTemplateId = null;
        string? focusTitle = null;
        var patternForgotCount = 0;

        var patterns = BuildPatterns(history);
        var worstForgot = patterns
            .Where(p => p.ForgotCount >= PatternForgotThreshold)
            .OrderByDescending(p => p.ForgotCount)
            .ThenByDescending(p => p.SkipCount)
            .FirstOrDefault();

        var todaySkipped = flow.Commitments
            .Where(c => c.Status == FamilyCommitmentStatuses.Skipped)
            .OrderBy(c => c.SortOrder)
            .ToList();

        var overdueOpen = flow.Commitments
            .Where(c =>
                c.Status is FamilyCommitmentStatuses.Pending or FamilyCommitmentStatuses.InProgress
                && c.ReminderState == FamilyReminderStates.Overdue)
            .OrderBy(c => c.SortOrder)
            .ToList();

        if (worstForgot is not null)
        {
            patternForgotCount = worstForgot.ForgotCount;
            focusTemplateId = worstForgot.TemplateId;
            focusTitle = worstForgot.Title;
            focusMemberId = worstForgot.MemberId;
            focusMemberName = worstForgot.MemberName;
            patternText =
                $"Trong {PatternWindowDays} ngày gần đây, “{worstForgot.Title}” bị quên {worstForgot.ForgotCount} lần"
                + (worstForgot.SkipCount > worstForgot.ForgotCount
                    ? $" (bỏ qua tổng {worstForgot.SkipCount} lần)."
                    : ".");

            var todayMatch = todaySkipped.FirstOrDefault(c => MatchesKey(c, worstForgot));
            if (todayMatch is not null)
            {
                var reason = FamilySkipReasons.LabelVi(todayMatch.SkipReason) ?? todayMatch.SkipReason;
                attention = reason is not null
                    ? $"Hôm nay “{todayMatch.Title}” bị bỏ qua — lý do: {reason}."
                    : $"Hôm nay “{todayMatch.Title}” bị bỏ qua.";
                focusMemberId = todayMatch.MemberId ?? focusMemberId;
                focusMemberName = todayMatch.MemberName ?? focusMemberName;
                focusTemplateId = todayMatch.TemplateId ?? focusTemplateId;
                focusTitle = todayMatch.Title;
            }
            else
            {
                attention = $"Việc “{worstForgot.Title}” đang lặp lại pattern quên.";
            }

            var moveToDinner = LooksLikeMorningPrep(worstForgot.Title, worstForgot.ContextAnchor);
            proposalCode = moveToDinner
                ? FamilyCoachProposalCodes.SuggestMoveAfterDinner
                : FamilyCoachProposalCodes.SuggestMoveAfterSchool;
            proposal = moveToDinner
                ? $"Gia đình có muốn chuyển “{worstForgot.Title}” sang ngay sau bữa tối không? Buổi sáng thường dễ quên hơn khi bị dồn giờ."
                : $"Gia đình có muốn neo “{worstForgot.Title}” sang sau giờ học / khung ổn định hơn không?";
            ctaLabel = "Chỉnh trong Routine";
        }
        else if (todaySkipped.Count > 0)
        {
            var s = todaySkipped[0];
            var reason = FamilySkipReasons.LabelVi(s.SkipReason) ?? s.SkipReason;
            attention = reason is not null
                ? $"Chỉ còn “{s.Title}” bị bỏ qua — lý do: {reason}."
                : $"Chỉ còn “{s.Title}” bị bỏ qua.";
            if (todaySkipped.Count > 1)
                attention += $" (và {todaySkipped.Count - 1} việc khác.)";

            focusMemberId = s.MemberId;
            focusMemberName = s.MemberName;
            focusTemplateId = s.TemplateId;
            focusTitle = s.Title;

            if (string.Equals(s.SkipReason, FamilySkipReasons.Forgot, StringComparison.OrdinalIgnoreCase)
                && LooksLikeMorningPrep(s.Title, s.ContextAnchor))
            {
                proposalCode = FamilyCoachProposalCodes.SuggestMoveAfterDinner;
                proposal =
                    $"Nếu tối nay chuẩn bị luôn “{s.Title}” thì sáng mai sẽ thoải mái hơn. Có muốn chuyển neo sang sau bữa tối trong Routine không?";
                ctaLabel = "Chỉnh trong Routine";
            }
            else
            {
                proposalCode = FamilyCoachProposalCodes.OpenToday;
                proposal = "Mở Hôm nay để hỗ trợ hoặc mở lại cam kết nếu đã làm xong.";
                ctaPath = "/family-os/day-flow";
                ctaLabel = "Mở hôm nay";
            }
        }
        else if (overdueOpen.Count > 0)
        {
            var o = overdueOpen[0];
            attention = overdueOpen.Count == 1
                ? $"“{o.Title}” đang quá giờ — nhắc nhẹ giúp con hoàn thành."
                : $"Có {overdueOpen.Count} việc quá giờ; ưu tiên “{o.Title}”.";
            focusMemberId = o.MemberId;
            focusMemberName = o.MemberName;
            focusTemplateId = o.TemplateId;
            focusTitle = o.Title;
            proposalCode = FamilyCoachProposalCodes.SupportOverdue;
            proposal = "Không đánh giá con — mở Hôm nay và hỗ trợ đúng việc đang kẹt.";
            ctaPath = "/family-os/day-flow";
            ctaLabel = "Mở hôm nay";
        }
        else if (open > 0)
        {
            attention = $"Còn {open} việc trong ngày — giữ nhịp, chưa cần siết.";
            proposalCode = FamilyCoachProposalCodes.OpenToday;
            proposal = "Theo dõi Hôm nay khi đến khung giờ tiếp theo.";
            ctaPath = "/family-os/day-flow";
            ctaLabel = "Mở hôm nay";
        }

        var (onTimeDoneCount, lateDoneCount) = CountDoneTiming(flow);
        var doneTotal = onTimeDoneCount + lateDoneCount;

        strength = PickStrength(flow);

        if (strength is null && total > 0 && open == 0 && skipped == 0 && lateDoneCount == 0)
            strength = "Hôm nay gia đình đã hoàn thành đủ cam kết trong ngày.";

        if (strength is null && total > 0 && open == 0 && skipped == 0
            && onTimeDoneCount > 0 && lateDoneCount > 0 && onTimeDoneCount > lateDoneCount)
            strength = $"Hôm nay hoàn thành đủ cam kết — {onTimeDoneCount} việc đúng giờ.";

        var lateAttention = BuildLateAttention(doneTotal, lateDoneCount, onTimeDoneCount);
        if (lateAttention is not null)
        {
            if (strength is not null && IsCompletionCountPraise(strength))
                strength = null;

            attention = attention is null
                ? lateAttention
                : attention.Contains("sau giờ", StringComparison.Ordinal)
                    ? attention
                    : $"{attention} {lateAttention}";
        }

        if (total > 0 && open == 0 && skipped == 0 && proposal is null)
        {
            ctaPath = "/family-os/day-flow";
            ctaLabel = "Xem hôm nay";
        }

        return new FamilyCoachInsightDto(
            date,
            headline,
            strength,
            attention,
            patternText,
            proposal,
            proposalCode,
            ctaPath,
            ctaLabel,
            focusMemberId,
            focusMemberName,
            focusTemplateId,
            focusTitle,
            done,
            skipped,
            open,
            total,
            patternForgotCount,
            PatternWindowDays);
    }

    private static bool MatchesKey(CommitmentDto c, PatternAgg p) =>
        (p.TemplateId is Guid tid && c.TemplateId == tid)
        || string.Equals(c.Title, p.Title, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMorningPrep(string title, string? contextAnchor)
    {
        if (string.Equals(contextAnchor, FamilyContextAnchors.BeforeSchool, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contextAnchor, FamilyContextAnchors.AfterWake, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contextAnchor, FamilyContextAnchors.AfterBreakfast, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contextAnchor, FamilyContextAnchors.BeforeBreakfast, StringComparison.OrdinalIgnoreCase))
            return true;

        var t = title.ToLowerInvariant();
        return t.Contains("cặp", StringComparison.Ordinal)
            || t.Contains("balo", StringComparison.Ordinal)
            || t.Contains("đồng phục", StringComparison.Ordinal)
            || t.Contains("dong phuc", StringComparison.Ordinal)
            || t.Contains("chuẩn bị", StringComparison.Ordinal)
            || t.Contains("chuan bi", StringComparison.Ordinal);
    }

    private static (int OnTime, int Late) CountDoneTiming(DayFlowDto flow)
    {
        var done = flow.Commitments.Where(c => c.Status == FamilyCommitmentStatuses.Done);
        var late = done.Count(c => c.IsLateDone);
        return (done.Count() - late, late);
    }

    private static string? BuildLateAttention(int doneTotal, int lateCount, int onTimeCount)
    {
        if (lateCount <= 0 || lateCount < onTimeCount)
            return null;

        return lateCount == doneTotal
            ? $"Xong {doneTotal} việc nhưng đều sau giờ — mai tranh thủ đúng giờ hơn."
            : $"Xong {doneTotal} việc nhưng {lateCount} việc sau giờ — mai tranh thủ đúng giờ hơn.";
    }

    private static bool IsCompletionCountPraise(string strength) =>
        strength.Contains("giữ nhịp ổn định", StringComparison.Ordinal)
        || strength.Contains("hoàn thành đủ cam kết", StringComparison.Ordinal)
        || strength.Contains("cam kết — giữ", StringComparison.Ordinal);

    private static string? PickStrength(DayFlowDto flow)
    {
        var done = flow.Commitments
            .Where(c => c.Status == FamilyCommitmentStatuses.Done)
            .ToList();
        if (done.Count == 0) return null;

        var onTimeDone = done.Where(c => !c.IsLateDone).ToList();
        var lateDone = done.Where(c => c.IsLateDone).ToList();
        var hasSkipped = flow.Commitments.Any(c => c.Status == FamilyCommitmentStatuses.Skipped);
        var allClosed = flow.PendingCount == 0 && !hasSkipped;

        // Mostly or all late — completion count is not a “win” under Điều nhà làm tốt.
        if (lateDone.Count >= onTimeDone.Count && lateDone.Count > 0)
            return null;

        var optionalOnTime = onTimeDone.FirstOrDefault(c =>
            string.Equals(c.Priority, FamilyCommitmentPriorities.Optional, StringComparison.OrdinalIgnoreCase));
        if (optionalOnTime is not null)
        {
            return $"Đáng khen: hoàn thành “{optionalOnTime.Title}” (việc tuỳ chọn) — chủ động giữ nhịp.";
        }

        var eveningOnTime = onTimeDone.Where(c =>
                string.Equals(c.ContextAnchor, FamilyContextAnchors.AfterDinner, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.ContextAnchor, FamilyContextAnchors.BeforeSleep, StringComparison.OrdinalIgnoreCase)
                || (c.WindowStart is TimeOnly ws && ws.Hour >= 18))
            .ToList();
        if (eveningOnTime.Count >= 2)
            return "Đáng khen: các việc buổi tối hôm nay hoàn thành đúng giờ.";

        var lateButDone = lateDone.FirstOrDefault();
        if (lateButDone is not null && hasSkipped)
        {
            return $"Dù có việc bị bỏ qua, con vẫn hoàn thành “{lateButDone.Title}” — không bỏ hết ngày.";
        }

        var reading = onTimeDone.FirstOrDefault(c =>
            c.Title.Contains("đọc", StringComparison.OrdinalIgnoreCase)
            || c.Title.Contains("doc", StringComparison.OrdinalIgnoreCase));
        if (reading is not null)
            return $"Đáng khen: “{reading.Title}” đã xong đúng nhịp.";

        if (onTimeDone.Count >= 3 && allClosed)
            return null;

        if (onTimeDone.Count >= 5)
            return $"Đã hoàn thành {onTimeDone.Count} cam kết đúng giờ — giữ nhịp ổn định.";

        if (onTimeDone.Count >= 2)
            return $"Đáng khen: {onTimeDone.Count} việc xong đúng giờ hôm nay.";

        return null;
    }

    private static List<PatternAgg> BuildPatterns(
        IReadOnlyList<FamilyCoachInsightRepository.HistoryRow> history)
    {
        return history
            .GroupBy(r => r.TemplateId?.ToString() ?? ("title:" + r.Title.Trim().ToLowerInvariant()))
            .Select(g =>
            {
                var sample = g.OrderByDescending(x => x.FlowDate).First();
                return new PatternAgg(
                    sample.TemplateId,
                    sample.Title,
                    sample.MemberId,
                    sample.MemberName,
                    sample.ContextAnchor,
                    g.Count(x => x.Status == FamilyCommitmentStatuses.Skipped),
                    g.Count(x =>
                        x.Status == FamilyCommitmentStatuses.Skipped
                        && string.Equals(x.SkipReason, FamilySkipReasons.Forgot, StringComparison.OrdinalIgnoreCase)),
                    g.Count(x => x.Status == FamilyCommitmentStatuses.Done),
                    g.Select(x => x.FlowDate).Distinct().Count());
            })
            .ToList();
    }

    private sealed record PatternAgg(
        Guid? TemplateId,
        string Title,
        Guid? MemberId,
        string? MemberName,
        string? ContextAnchor,
        int SkipCount,
        int ForgotCount,
        int DoneCount,
        int DayCount);
}
