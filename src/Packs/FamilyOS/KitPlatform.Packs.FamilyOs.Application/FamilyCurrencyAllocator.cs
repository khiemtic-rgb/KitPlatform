namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// C1–C4: allocate daily star budget across today's tasks by contribution category,
/// duty zero-star, habit-stage decay, stretch + initiative bonuses.
/// </summary>
public static class FamilyCurrencyAllocator
{
    public sealed record TaskInput(
        Guid Id,
        string Title,
        string? HabitStage,
        string? CategoryOverride,
        string? StarKindOverride,
        bool? EligibleOverride,
        int? PlanTarget,
        int? ActualProgress,
        bool SelfStarted,
        int HabitStreakDays,
        int RelativeWeight = 1);

    public sealed record TaskAllocation(
        Guid Id,
        string Category,
        string StarKind,
        int BaseStars,
        int StretchBonus,
        int InitiativeBonus,
        int TotalBeforeLate,
        bool Eligible,
        bool Graduated,
        string? MessageVi);

    public static string InferCategory(string? title, string? categoryOverride)
    {
        if (!string.IsNullOrWhiteSpace(categoryOverride)
            && FamilyCurrencyCategories.All.Contains(categoryOverride))
        {
            return categoryOverride.Trim().ToLowerInvariant();
        }

        var t = (title ?? "").Trim().ToLowerInvariant();
        if (IsDutyTitle(t, FamilyCurrencyPreset.BalancedV1))
            return FamilyCurrencyCategories.Duty;

        if (ContainsAny(t, "mini game", "minigame", "check-in", "checkin", "vòng may", "lucky", "video"))
            return FamilyCurrencyCategories.Play;

        if (ContainsAny(t, "giúp", "chia sẻ", "xin lỗi", "cảm ơn", "trung thực", "chăm em", "em học"))
            return FamilyCurrencyCategories.Kindness;

        if (ContainsAny(t, "học", "bài", "toán", "đọc", "sách", "kỹ năng", "luyện"))
            return FamilyCurrencyCategories.Growth;

        if (ContainsAny(t, "rửa", "dọn", "phòng", "bát", "việc nhà", "quét"))
            return FamilyCurrencyCategories.Responsibility;

        if (ContainsAny(t, "đúng giờ", "dậy", "giữ lời", "hứa"))
            return FamilyCurrencyCategories.Cue;

        return FamilyCurrencyCategories.Responsibility;
    }

    public static IReadOnlyList<TaskAllocation> Allocate(
        FamilyCurrencyConfig config,
        int dailyBudget,
        IReadOnlyList<TaskInput> tasks)
    {
        if (tasks.Count == 0)
            return [];

        var classified = tasks.Select(task =>
        {
            var category = InferCategory(task.Title, task.CategoryOverride);
            var duty = FindDutyRule(config, task.Title);
            var stage = string.IsNullOrWhiteSpace(task.HabitStage)
                ? FamilyHabitStages.New
                : task.HabitStage!.Trim().ToLowerInvariant();

            var graduated = stage is FamilyHabitStages.Autonomous or FamilyHabitStages.Maintained;
            var eligible = task.EligibleOverride ?? true;

            if (duty is not null || category == FamilyCurrencyCategories.Duty)
            {
                category = FamilyCurrencyCategories.Duty;
                eligible = IsDutyEligibleForStars(duty, stage, task.EligibleOverride);
            }

            if (graduated && eligible)
            {
                // Base goes to zero; stretch/initiative may still apply later.
            }

            var kind = !string.IsNullOrWhiteSpace(task.StarKindOverride)
                ? FamilyCurrencyStarKinds.Normalize(task.StarKindOverride)
                : ResolveDefaultKind(config, category, duty);

            return new
            {
                task,
                category,
                kind,
                stage,
                graduated,
                eligible,
                duty,
            };
        }).ToList();

        var poolEligible = classified
            .Where(x => x.eligible && x.category != FamilyCurrencyCategories.Duty)
            .Where(x => !x.graduated)
            .ToList();

        var weightByCategory = config.CategoryWeights.ToDictionary(
            w => w.Code,
            w => w.BudgetPct,
            StringComparer.OrdinalIgnoreCase);

        var pools = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in config.CategoryWeights)
            pools[w.Code] = (int)Math.Round(dailyBudget * w.BudgetPct / 100.0);

        // Fix rounding so sum ≈ budget
        var poolSum = pools.Values.Sum();
        if (poolSum != dailyBudget && pools.Count > 0)
        {
            var growKey = FamilyCurrencyCategories.Growth;
            if (pools.ContainsKey(growKey))
                pools[growKey] += dailyBudget - poolSum;
        }

        var baseStars = new Dictionary<Guid, int>();
        foreach (var group in poolEligible.GroupBy(x => x.category, StringComparer.OrdinalIgnoreCase))
        {
            var cat = group.Key;
            if (!pools.TryGetValue(cat, out var pool) || pool <= 0)
            {
                foreach (var item in group)
                    baseStars[item.task.Id] = cat == FamilyCurrencyCategories.Play ? 1 : 0;
                continue;
            }

            var items = group.ToList();
            var totalWeight = items.Sum(i => Math.Max(1, i.task.RelativeWeight));
            var assigned = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int stars;
                if (i == items.Count - 1)
                    stars = Math.Max(0, pool - assigned);
                else
                    stars = (int)Math.Floor(pool * (double)Math.Max(1, item.task.RelativeWeight) / totalWeight);

                var maxEvent = config.CategoryWeights
                    .FirstOrDefault(w => string.Equals(w.Code, cat, StringComparison.OrdinalIgnoreCase))
                    ?.MaxStarsPerEvent;
                if (maxEvent is int max)
                    stars = Math.Min(stars, max);

                // Apply stage multiplier to base share
                if (!config.HabitStageMultipliers.TryGetValue(item.stage, out var mult))
                    mult = 1.0;
                stars = (int)Math.Floor(stars * mult);

                baseStars[item.task.Id] = Math.Max(0, stars);
                assigned += stars;
            }
        }

        var results = new List<TaskAllocation>(classified.Count);
        foreach (var item in classified)
        {
            var baseAward = 0;
            var stretch = 0;
            var initiative = 0;
            string? message = null;
            var eligible = item.eligible;

            if (item.category == FamilyCurrencyCategories.Duty || item.duty is not null)
            {
                if (eligible && item.duty is { FormationStarsEnabled: true } duty
                    && duty.FormationOnlyStages.Any(s =>
                        string.Equals(s, item.stage, StringComparison.OrdinalIgnoreCase)))
                {
                    baseAward = Math.Min(duty.FormationMaxStars, duty.FormationMaxStars);
                }
                else
                {
                    eligible = false;
                    baseAward = 0;
                    if (item.graduated || item.task.HabitStreakDays > 0)
                    {
                        message = config.GraduateCopyVi.Replace(
                            "{streakDays}",
                            Math.Max(item.task.HabitStreakDays, 1).ToString());
                    }
                }
            }
            else if (item.graduated)
            {
                baseAward = 0;
                message = config.GraduateCopyVi.Replace(
                    "{streakDays}",
                    Math.Max(item.task.HabitStreakDays, 1).ToString());
            }
            else
            {
                baseAward = baseStars.GetValueOrDefault(item.task.Id);
            }

            if (item.task.PlanTarget is int plan and > 0
                && item.task.ActualProgress is int actual
                && actual > plan)
            {
                stretch = config.StretchBonusStars;
            }

            if (item.task.SelfStarted)
                initiative = config.InitiativeBonusStars;

            // Play hard-cap
            if (item.category == FamilyCurrencyCategories.Play)
            {
                var playMax = config.CategoryWeights
                    .FirstOrDefault(w => w.Code == FamilyCurrencyCategories.Play)
                    ?.MaxStarsPerEvent ?? 1;
                baseAward = Math.Min(Math.Max(baseAward, baseAward > 0 || !item.graduated ? 1 : 0), playMax);
                if (item.graduated)
                    baseAward = 0;
                stretch = 0;
                initiative = 0;
            }

            var total = baseAward + stretch + initiative;
            results.Add(new TaskAllocation(
                item.task.Id,
                item.category,
                item.kind,
                baseAward,
                stretch,
                initiative,
                total,
                eligible || stretch > 0 || initiative > 0,
                item.graduated,
                message));
        }

        // Soft-cap total day allocation to budget + stretch overflow
        var softCap = dailyBudget
            + (int)Math.Floor(dailyBudget * config.StretchOverflowMaxPctOfBudget / 100.0);
        var dayTotal = results.Sum(r => r.TotalBeforeLate);
        if (dayTotal > softCap && dayTotal > 0)
        {
            // Scale non-bonus bases down; keep bonuses preferred
            var bonusSum = results.Sum(r => r.StretchBonus + r.InitiativeBonus);
            var basePool = Math.Max(0, softCap - bonusSum);
            var currentBase = results.Sum(r => r.BaseStars);
            if (currentBase > 0 && basePool < currentBase)
            {
                var scaled = new List<TaskAllocation>(results.Count);
                var used = 0;
                for (var i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    int newBase;
                    if (i == results.Count - 1)
                        newBase = Math.Max(0, basePool - used);
                    else
                        newBase = (int)Math.Floor(r.BaseStars * (double)basePool / currentBase);
                    used += newBase;
                    scaled.Add(r with
                    {
                        BaseStars = newBase,
                        TotalBeforeLate = newBase + r.StretchBonus + r.InitiativeBonus,
                    });
                }
                return scaled;
            }
        }

        _ = weightByCategory; // reserved for future parent slider diffs
        return results;
    }

    public static TaskAllocation? ForTask(
        FamilyCurrencyConfig config,
        int dailyBudget,
        IReadOnlyList<TaskInput> todaysTasks,
        Guid commitmentId) =>
        Allocate(config, dailyBudget, todaysTasks)
            .FirstOrDefault(a => a.Id == commitmentId);

    private static string ResolveDefaultKind(
        FamilyCurrencyConfig config,
        string category,
        FamilyCurrencyDutyRule? duty)
    {
        if (duty is not null)
            return FamilyCurrencyStarKinds.Normalize(duty.FormationStarKind);

        var weight = config.CategoryWeights.FirstOrDefault(w =>
            string.Equals(w.Code, category, StringComparison.OrdinalIgnoreCase));
        return FamilyCurrencyStarKinds.Normalize(weight?.DefaultStarKind);
    }

    private static FamilyCurrencyDutyRule? FindDutyRule(FamilyCurrencyConfig config, string? title)
    {
        var t = (title ?? "").Trim().ToLowerInvariant();
        return config.DutyRules.FirstOrDefault(r =>
            r.Match.Any(m => t.Contains(m.Trim().ToLowerInvariant(), StringComparison.Ordinal)));
    }

    private static bool IsDutyTitle(string titleLower, FamilyCurrencyConfig config) =>
        config.DutyRules.Any(r =>
            r.Match.Any(m => titleLower.Contains(m.Trim().ToLowerInvariant(), StringComparison.Ordinal)));

    private static bool IsDutyEligibleForStars(
        FamilyCurrencyDutyRule? duty,
        string stage,
        bool? eligibleOverride)
    {
        if (eligibleOverride == false)
            return false;
        if (duty is null)
            return false;
        if (!duty.FormationStarsEnabled)
            return false;
        return duty.FormationOnlyStages.Any(s =>
            string.Equals(s, stage, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));
}
