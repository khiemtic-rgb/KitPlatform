using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyInsightService : IFamilyInsightService
{
    private const int MinDays = 1;
    private const int MaxDays = 31;

    private readonly FamilyInsightRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyChallengeService _challenges;

    public FamilyInsightService(
        FamilyInsightRepository repo,
        FamilyGraphRepository families,
        IFamilyChallengeService challenges)
    {
        _repo = repo;
        _families = families;
        _challenges = challenges;
    }

    public async Task<FamilyWeeklyReportDto> GetWeeklyReportAsync(
        Guid familyId,
        DateOnly? asOf = null,
        int days = 7,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        days = Math.Clamp(days, MinDays, MaxDays);
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var periodEnd = asOf ?? today;
        var periodStart = periodEnd.AddDays(-(days - 1));
        var prevEnd = periodStart.AddDays(-1);
        var prevStart = prevEnd.AddDays(-(days - 1));

        var rows = await _repo.ListCommitmentsAsync(
            familyId, prevStart, periodEnd, family.Timezone, cancellationToken);
        var dataDays = await _repo.ListDataDaysAsync(
            familyId, periodStart, periodEnd, cancellationToken);
        var reminderRows = await _repo.ListReminderCountsAsync(
            familyId, prevStart, periodEnd, cancellationToken);
        var remindersTracked = await _repo.HasAnyReminderHistoryAsync(familyId, cancellationToken);

        var current = rows.Where(r => r.FlowDate >= periodStart && r.FlowDate <= periodEnd).ToList();
        var previous = rows.Where(r => r.FlowDate >= prevStart && r.FlowDate <= prevEnd).ToList();

        var total = current.Count;
        var done = current.Count(r => IsDone(r));
        var lateDone = current.Count(r => IsDone(r) && r.IsLateDone);
        var onTimeDone = done - lateDone;
        var skipped = current.Count(r => IsSkipped(r));
        var pending = total - done - skipped;
        var stars = current.Where(r => r.StarDelta > 0).Sum(r => r.StarDelta);

        var reminderCurrent = reminderRows
            .Where(r => r.FlowDate >= periodStart && r.FlowDate <= periodEnd)
            .Sum(r => r.Count);
        var reminderPrev = reminderRows
            .Where(r => r.FlowDate >= prevStart && r.FlowDate <= prevEnd)
            .Sum(r => r.Count);
        double? reminderDeltaPct = reminderPrev > 0
            ? Math.Round((reminderCurrent - reminderPrev) * 100.0 / reminderPrev, 0)
            : null;

        var members = BuildMembers(current, dataDays, periodEnd);
        var habits = BuildHabits(current, previous);

        var parentGoalRows = await _repo.ListSharedParentGoalStatsAsync(
            familyId, periodStart, periodEnd, today, cancellationToken);
        var teamUnlocks = await _repo.CountTeamUnlocksConfirmedAsync(
            familyId, periodStart, periodEnd, cancellationToken);

        FamilyMirrorChallengeDto? challengeMirror = null;
        try
        {
            var challenge = await _challenges.GetCurrentAsync(familyId, cancellationToken);
            if (challenge is not null)
            {
                challengeMirror = new FamilyMirrorChallengeDto(
                    challenge.Id,
                    challenge.Title,
                    challenge.Status,
                    challenge.RewardLabel,
                    challenge.LegsComplete,
                    challenge.LegsTotal);
            }
        }
        catch
        {
            // Mirror stays available even if challenge table is not migrated yet.
        }

        var mirror = BuildMirror(
            current, members, dataDays, periodEnd,
            parentGoalRows, teamUnlocks,
            stars, remindersTracked, reminderCurrent, days,
            challengeMirror);

        var health = BuildHealthScore(
            total, done, onTimeDone,
            remindersTracked, reminderCurrent, days,
            members,
            mirror.Parent.CheckinRate);

        var highlights = BuildHighlights(
            total, done, CompletionRate(done, total), stars,
            remindersTracked, reminderCurrent, reminderPrev, reminderDeltaPct,
            members, habits, health, mirror);

        var isPartial = dataDays.Count < days;
        var note = isPartial
            ? $"Dữ liệu {dataDays.Count}/{days} ngày — báo cáo đầy đủ hơn khi cả nhà dùng liên tục."
            : null;

        return new FamilyWeeklyReportDto(
            familyId,
            family.Timezone,
            periodStart,
            periodEnd,
            days,
            dataDays.Count,
            isPartial,
            note,
            DateTimeOffset.UtcNow,
            total,
            done,
            onTimeDone,
            lateDone,
            skipped,
            pending,
            CompletionRate(done, total),
            done > 0 ? Math.Round(onTimeDone * 1.0 / done, 3) : (double?)null,
            stars,
            health,
            new FamilyWeeklyReminderDto(remindersTracked, reminderCurrent, reminderPrev, reminderDeltaPct),
            members,
            habits,
            highlights,
            mirror);
    }

    private static bool IsDone(FamilyInsightRepository.InsightRow r) =>
        r.Status == FamilyCommitmentStatuses.Done;

    private static bool IsSkipped(FamilyInsightRepository.InsightRow r) =>
        r.Status == FamilyCommitmentStatuses.Skipped;

    private static double? CompletionRate(int done, int total) =>
        total > 0 ? Math.Round(done * 1.0 / total, 3) : (double?)null;

    private static List<FamilyWeeklyMemberDto> BuildMembers(
        List<FamilyInsightRepository.InsightRow> current,
        IReadOnlyList<DateOnly> dataDays,
        DateOnly periodEnd)
    {
        var dataDaySet = dataDays.ToHashSet();
        return current
            .Where(r => r.MemberId is not null)
            .GroupBy(r => r.MemberId!.Value)
            .Select(g =>
            {
                var total = g.Count();
                var done = g.Count(IsDone);
                var lateDone = g.Count(r => IsDone(r) && r.IsLateDone);
                var skipped = g.Count(IsSkipped);
                var stars = g.Where(r => r.StarDelta > 0).Sum(r => r.StarDelta);
                var name = g.Select(r => r.MemberName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                           ?? "Thành viên";
                var streak = ComputeStreak(g, dataDaySet, periodEnd);
                return new FamilyWeeklyMemberDto(
                    g.Key,
                    name,
                    total,
                    done,
                    done - lateDone,
                    skipped,
                    CompletionRate(done, total),
                    stars,
                    streak);
            })
            .OrderByDescending(m => m.CompletionRate ?? 0)
            .ThenByDescending(m => m.DoneCount)
            .ToList();
    }

    /// <summary>
    /// Consecutive days (walking back from period end over dates that actually have
    /// data) where the member completed at least one commitment. Breaks on the first
    /// day with data but no completion, or the first missing-data day.
    /// </summary>
    private static int ComputeStreak(
        IEnumerable<FamilyInsightRepository.InsightRow> memberRows,
        HashSet<DateOnly> dataDaySet,
        DateOnly periodEnd)
    {
        var doneByDay = memberRows
            .Where(IsDone)
            .Select(r => r.FlowDate)
            .ToHashSet();

        var streak = 0;
        for (var d = periodEnd; ; d = d.AddDays(-1))
        {
            if (!dataDaySet.Contains(d)) break;
            if (!doneByDay.Contains(d)) break;
            streak++;
        }
        return streak;
    }

    private static List<FamilyWeeklyHabitDto> BuildHabits(
        List<FamilyInsightRepository.InsightRow> current,
        List<FamilyInsightRepository.InsightRow> previous)
    {
        static string KeyOf(FamilyInsightRepository.InsightRow r) =>
            r.TemplateId?.ToString() ?? ("title:" + r.Title.Trim().ToLowerInvariant());

        var prevByKey = previous
            .GroupBy(KeyOf)
            .ToDictionary(g => g.Key, g => (Occ: g.Count(), Done: g.Count(IsDone)));

        return current
            .GroupBy(KeyOf)
            .Select(g =>
            {
                var sample = g.First();
                var occ = g.Count();
                var done = g.Count(IsDone);
                var forgot = g.Count(r =>
                    IsSkipped(r)
                    && string.Equals(r.SkipReason, FamilySkipReasons.Forgot, StringComparison.OrdinalIgnoreCase));
                double? rate = occ > 0 ? Math.Round(done * 1.0 / occ, 3) : (double?)null;

                double? prevRate = null;
                var trend = FamilyHabitTrends.New;
                if (prevByKey.TryGetValue(g.Key, out var p) && p.Occ > 0)
                {
                    prevRate = Math.Round(p.Done * 1.0 / p.Occ, 3);
                    var delta = (rate ?? 0) - prevRate.Value;
                    trend = delta > 0.1 ? FamilyHabitTrends.Up
                          : delta < -0.1 ? FamilyHabitTrends.Down
                          : FamilyHabitTrends.Flat;
                }

                return new FamilyWeeklyHabitDto(
                    sample.TemplateId,
                    sample.Title,
                    sample.MemberName,
                    occ,
                    done,
                    forgot,
                    rate,
                    prevRate,
                    trend);
            })
            .OrderByDescending(h => h.Occurrences)
            .ThenByDescending(h => h.DoneCount)
            .ToList();
    }

    private static FamilyMirrorDto BuildMirror(
        List<FamilyInsightRepository.InsightRow> current,
        List<FamilyWeeklyMemberDto> members,
        IReadOnlyList<DateOnly> dataDays,
        DateOnly periodEnd,
        IReadOnlyList<FamilyInsightRepository.ParentGoalPeriodRow> parentGoals,
        int teamUnlocksConfirmed,
        int stars,
        bool remindersTracked,
        int reminderCurrent,
        int days,
        FamilyMirrorChallengeDto? challenge)
    {
        var childIds = current
            .Where(r => r.MemberId is Guid id && IsChildRole(r.RoleCode))
            .Select(r => r.MemberId!.Value)
            .ToHashSet();

        // Prefer role=child; if no role data, fall back to all members with commitments.
        var childMembers = members
            .Where(m => m.MemberId is Guid mid && childIds.Contains(mid))
            .ToList();
        if (childMembers.Count == 0 && members.Count > 0 && childIds.Count == 0)
            childMembers = members;

        var childRows = current.Where(r =>
            r.MemberId is Guid mid && (childIds.Count == 0 || childIds.Contains(mid))).ToList();
        var childTotal = childRows.Count;
        var childDone = childRows.Count(IsDone);
        var childStars = childRows.Where(r => r.StarDelta > 0).Sum(r => r.StarDelta);
        var bestStreak = childMembers.Count > 0 ? childMembers.Max(m => m.CurrentStreakDays) : 0;

        var child = new FamilyMirrorChildDto(
            childMembers.Count,
            childTotal,
            childDone,
            CompletionRate(childDone, childTotal),
            childStars,
            bestStreak,
            childMembers);

        var goalDtos = parentGoals
            .Select(g => new FamilyMirrorParentGoalDto(
                g.GoalId, g.MemberId, g.MemberName, g.Title, g.Emoji,
                g.TargetDaysPerWeek, g.DoneDays, g.TodayDone))
            .ToList();
        var expected = goalDtos.Sum(g => Math.Min(days, g.TargetDaysPerWeek));
        var doneCheckins = goalDtos.Sum(g => g.DoneDays);
        double? parentRate = expected > 0
            ? Math.Round(doneCheckins * 1.0 / expected, 3)
            : null;

        var parent = new FamilyMirrorParentDto(
            goalDtos.Count > 0,
            goalDtos.Count,
            doneCheckins,
            expected,
            parentRate,
            goalDtos);

        var household = new FamilyMirrorHouseholdDto(
            teamUnlocksConfirmed,
            stars,
            reminderCurrent,
            remindersTracked);

        var reflections = BuildReflections(child, parent, household, dataDays.Count, days, challenge);
        return new FamilyMirrorDto(child, parent, household, reflections, challenge);
    }

    private static bool IsChildRole(string? roleCode) =>
        string.Equals(roleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(roleCode);

    private static List<string> BuildReflections(
        FamilyMirrorChildDto child,
        FamilyMirrorParentDto parent,
        FamilyMirrorHouseholdDto household,
        int dataDays,
        int days,
        FamilyMirrorChallengeDto? challenge)
    {
        var lines = new List<string>();

        if (child.TotalCommitments > 0 && child.CompletionRate is double cr)
            lines.Add($"Tuần này con hoàn thành {cr * 100:0}% routine ({child.DoneCount}/{child.TotalCommitments}).");
        else if (dataDays == 0)
            lines.Add("Tuần này chưa có ngày nào có dữ liệu — mở Daily Flow vài buổi để gương rõ hơn.");

        if (parent.AnyShared)
        {
            foreach (var g in parent.Goals.Take(3))
            {
                lines.Add(
                    $"{g.MemberName} đã check-in “{g.Title}” {g.DoneDays}/{Math.Min(days, g.TargetDaysPerWeek)} ngày.");
            }
        }
        else
        {
            lines.Add("Bố/mẹ chưa chia sẻ mục tiêu — bật “Chia sẻ” ở Mục tiêu của tôi nếu muốn cả nhà cùng thấy.");
        }

        if (challenge is not null)
        {
            lines.Add(
                challenge.Status == FamilyChallengeStatuses.Completed
                    ? $"Challenge tuần: đủ {challenge.LegsComplete}/{challenge.LegsTotal} chân → {challenge.RewardLabel}."
                    : $"Challenge tuần: {challenge.LegsComplete}/{challenge.LegsTotal} chân hướng tới {challenge.RewardLabel}.");
        }

        if (household.TeamUnlocksConfirmed > 0)
            lines.Add($"Cả nhà mở thưởng chung {household.TeamUnlocksConfirmed} lần trong kỳ.");
        else if (household.StarsEarned > 0)
            lines.Add($"Cả nhà tích được {household.StarsEarned} sao — giữ nhịp nhẹ nhàng.");

        if (household.RemindersTracked && household.ReminderCount == 0 && child.TotalCommitments > 0)
            lines.Add("Tuần này nhà gần như không cần nhắc — môi trường đang hỗ trợ tốt.");

        return lines;
    }

    private static FamilyHealthScoreDto BuildHealthScore(
        int total,
        int done,
        int onTimeDone,
        bool remindersTracked,
        int reminderCurrent,
        int days,
        List<FamilyWeeklyMemberDto> members,
        double? parentCheckinRate)
    {
        if (total <= 0)
        {
            return new FamilyHealthScoreDto(
                null, null, null, null, null,
                null,
                "Chưa có dữ liệu cam kết trong kỳ — Health Score sẽ hiện khi nhà bắt đầu dùng.");
        }

        var completion = ClampScore((done * 100.0) / total);
        int? reminderCalm = null;
        if (remindersTracked)
        {
            // 0 reminders/day → 100; ~8 reminders/day → 0
            var perDay = reminderCurrent / Math.Max(1.0, days);
            reminderCalm = ClampScore(100 - perDay * 12.5);
        }

        var bestStreak = members.Count > 0 ? members.Max(m => m.CurrentStreakDays) : 0;
        var streak = ClampScore(Math.Min(100, bestStreak * 12.5));
        var onTime = done > 0 ? ClampScore((onTimeDone * 100.0) / done) : 55;
        int? parentProgress = parentCheckinRate is double pr
            ? ClampScore(pr * 100.0)
            : null;

        // When opt-in parent data exists, blend a light 10% parent leg.
        // Otherwise keep the classic weights — never punish missing parent data.
        double score;
        if (reminderCalm is int calm && parentProgress is int pp)
        {
            score = completion * 0.25 + calm * 0.25 + streak * 0.2 + onTime * 0.2 + pp * 0.1;
        }
        else if (reminderCalm is int calmOnly)
        {
            score = completion * 0.3 + calmOnly * 0.3 + streak * 0.2 + onTime * 0.2;
        }
        else if (parentProgress is int ppOnly)
        {
            score = completion * 0.4 + streak * 0.2 + onTime * 0.3 + ppOnly * 0.1;
        }
        else
        {
            score = completion * 0.45 + streak * 0.25 + onTime * 0.3;
        }

        var rounded = ClampScore(score);
        return new FamilyHealthScoreDto(
            rounded,
            completion,
            reminderCalm,
            streak,
            onTime,
            HealthLabel(rounded),
            reminderCalm is int rc && rc >= 70
                ? "Nhà đang ít cần nhắc — đây là giá trị Famixa đo được từ dữ liệu thật."
                : "Giảm số lần nhắc + tăng tự giác = lý do tiếp tục dùng Famixa.",
            parentProgress);
    }

    private static int ClampScore(double n) =>
        Math.Max(0, Math.Min(100, (int)Math.Round(n)));

    private static string HealthLabel(int score) =>
        score >= 85 ? "Gia đình đang rất khỏe"
        : score >= 70 ? "Nhịp nhà đang tốt"
        : score >= 55 ? "Đang tiến bộ — giữ nhẹ nhàng"
        : score >= 40 ? "Cần sát cánh thêm"
        : "Ưu tiên 1–2 thói quen quan trọng";

    private static List<string> BuildHighlights(
        int total,
        int done,
        double? completionRate,
        int stars,
        bool remindersTracked,
        int reminderCurrent,
        int reminderPrev,
        double? reminderDeltaPct,
        List<FamilyWeeklyMemberDto> members,
        List<FamilyWeeklyHabitDto> habits,
        FamilyHealthScoreDto health,
        FamilyMirrorDto mirror)
    {
        _ = total;
        _ = done;
        _ = completionRate;
        _ = reminderCurrent;

        // Prefer Mirror reflections (non-judgmental). Keep a short evidence appendix.
        var highlights = new List<string>(mirror.Reflections);

        if (health.Score is int hs)
            highlights.Insert(0, $"Family Health Score: {hs}/100 — {health.Label}.");

        if (remindersTracked && reminderPrev > 0 && reminderDeltaPct is double dp && dp < 0)
            highlights.Add($"Số lần nhắc giảm {Math.Abs(dp):0}% so với kỳ trước — nhịp nhà đang êm hơn.");

        if (stars > 0 && mirror.Reflections.All(r => !r.Contains("sao", StringComparison.Ordinal)))
            highlights.Add($"Cả nhà tích được {stars} sao trong kỳ.");

        var steadyMember = members.FirstOrDefault(m => m.TotalCommitments >= 3 && (m.CompletionRate ?? 0) >= 0.7);
        if (steadyMember is not null && steadyMember.CompletionRate is double mr)
            highlights.Add($"{steadyMember.Name} giữ nhịp ổn với {mr * 100:0}% hoàn thành.");

        var bestStreak = members.OrderByDescending(m => m.CurrentStreakDays).FirstOrDefault();
        if (bestStreak is not null && bestStreak.CurrentStreakDays >= 3)
            highlights.Add($"{bestStreak.Name} giữ chuỗi {bestStreak.CurrentStreakDays} ngày liên tiếp.");

        var risingHabit = habits.FirstOrDefault(h => h.Trend == FamilyHabitTrends.Up && h.Occurrences >= 3);
        if (risingHabit is not null && risingHabit.DoneRate is double hr)
            highlights.Add($"“{risingHabit.Title}” đang tiến bộ — {hr * 100:0}% so với kỳ trước.");

        // Soften former "forgot" judgment into a gentle anchor tip.
        var softSpot = habits
            .Where(h => h.ForgotCount >= 3)
            .OrderByDescending(h => h.ForgotCount)
            .FirstOrDefault();
        if (softSpot is not null)
            highlights.Add($"“{softSpot.Title}” còn bỏ lỡ vài buổi — tuần sau thử neo giờ cố định nhé.");

        return highlights;
    }
}
