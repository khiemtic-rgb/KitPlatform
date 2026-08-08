namespace KitPlatform.Packs.FamilyOs;

/// <summary>Behavior OS Wave 3 — motivation drivers (lite graph nodes).</summary>
public static class FamilyMotivationDrivers
{
    public const string Autonomy = "autonomy";
    public const string Progress = "progress";
    public const string Mastery = "mastery";
    public const string Relatedness = "relatedness";
    public const string Rest = "rest";

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Autonomy => "Tự chủ",
            Progress => "Tiến bộ",
            Mastery => "Làm chủ",
            Relatedness => "Gắn kết",
            Rest => "Nghỉ hồi phục",
            _ => "Động lực",
        };
}

/// <summary>Intervention levels — Intervention ≠ Reminder.</summary>
public static class FamilyInterventionLevels
{
    public const string None = "none";
    public const string SelfCue = "self_cue";
    public const string SoftNudge = "soft_nudge";
    public const string ParentNudge = "parent_nudge";
    public const string ObserveOnly = "observe_only";

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            ObserveOnly => "Nên quan sát — đừng nhắc",
            SelfCue => "Gợi ý cho con tự bắt đầu",
            SoftNudge => "Nhắc nhẹ (không ép)",
            ParentNudge => "Bố mẹ có thể nhắc",
            _ => "Không can thiệp",
        };
}

/// <summary>
/// Wave 3 + S2 Motivation Engine — rule playbook: driver + intervention + pattern tactic.
/// No LLM. Changing tactic never raises parent nudge budget.
/// </summary>
public static class FamilyMotivationIntervention
{
    public const int DefaultParentNudgeBudgetPerDay = 3;

    public sealed record Input(
        string Status,
        string ReminderState,
        string? HabitStage,
        bool ReminderSuppressed,
        int HabitStreakDays,
        bool IsLearningMission,
        string? SkipReason,
        int ParentNudgesUsedToday,
        int ParentNudgeBudget = DefaultParentNudgeBudgetPerDay,
        bool FamilyObserveOnly = false,
        TimeOnly? WindowEnd = null,
        string? Title = null,
        string? ForcedPatternCode = null,
        string? ForcedTacticCode = null,
        /// <summary>SCH-01c — child is in school quiet bubble (at_school).</summary>
        bool SchoolQuiet = false);

    public sealed record Decision(
        string MotivationDriver,
        string MotivationCueVi,
        string InterventionLevel,
        string InterventionLabelVi,
        bool AllowParentPush,
        bool AllowChildChime,
        string ParentAdviceVi,
        string? BehaviorPatternCode = null,
        string? BehaviorTacticCode = null);

    public static Decision Decide(Input i)
    {
        if (i.Status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
        {
            return new Decision(
                FamilyMotivationDrivers.Autonomy,
                "",
                FamilyInterventionLevels.None,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.None),
                AllowParentPush: false,
                AllowChildChime: false,
                ParentAdviceVi: "");
        }

        // SCH-01c — hot parent push + kid chime off while at school (incl. học thêm).
        if (i.SchoolQuiet)
        {
            return new Decision(
                FamilyMotivationDrivers.Rest,
                "Đang giờ học — Fami im lặng, mình ghi nhận khi tan học nhé.",
                FamilyInterventionLevels.ObserveOnly,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.ObserveOnly),
                AllowParentPush: false,
                AllowChildChime: false,
                ParentAdviceVi: "Con đang ở giờ học — không nhắc push; đợi tan học rồi động viên một việc.");
        }

        var stage = (i.HabitStage ?? FamilyHabitStages.New).Trim().ToLowerInvariant();
        var reminder = (i.ReminderState ?? FamilyReminderStates.None).Trim().ToLowerInvariant();
        var graduated = FamilyHabitStages.IsReminderSuppressed(stage, i.ReminderSuppressed)
            || stage is FamilyHabitStages.Autonomous or FamilyHabitStages.Maintained;

        var patternCode = i.ForcedPatternCode
            ?? FamilyBehaviorPatterns.InferCode(
                new FamilyBehaviorPatterns.InferSignals(
                    i.WindowEnd,
                    reminder,
                    i.IsLearningMission,
                    i.Title,
                    stage,
                    i.HabitStreakDays,
                    i.ParentNudgesUsedToday,
                    i.SkipReason));
        var pattern = FamilyBehaviorPatterns.Get(patternCode);
        var tacticCode = !string.IsNullOrWhiteSpace(i.ForcedTacticCode)
            ? i.ForcedTacticCode!.Trim()
            : patternCode is null
                ? null
                : FamilyBehaviorPatterns.PickTacticCode(
                    patternCode,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    memberId: null);
        var tactic = FamilyBehaviorPatterns.GetTactic(patternCode, tacticCode);

        var driver = pattern?.DefaultDriver ?? PickDriver(i, stage);
        var cue = tactic?.ChildCueVi ?? CueVi(driver, i.HabitStreakDays);
        var patternAdvice = tactic?.ParentAdviceVi;

        Decision WithPattern(Decision d) =>
            d with
            {
                MotivationCueVi = tactic is not null ? cue : d.MotivationCueVi,
                ParentAdviceVi = patternAdvice ?? d.ParentAdviceVi,
                BehaviorPatternCode = patternCode,
                BehaviorTacticCode = tacticCode,
            };

        // Wave 5 — family-level Observe-only / AI Retirement runtime
        if (i.FamilyObserveOnly)
        {
            return WithPattern(new Decision(
                driver,
                cue,
                FamilyInterventionLevels.ObserveOnly,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.ObserveOnly),
                AllowParentPush: false,
                AllowChildChime: reminder is FamilyReminderStates.DueNow or FamilyReminderStates.Upcoming,
                ParentAdviceVi: "Nhà đang Observe-only — AI nghỉ nhắc bố mẹ; để con tự chủ."));
        }

        if (graduated)
        {
            return WithPattern(new Decision(
                driver,
                cue,
                FamilyInterventionLevels.ObserveOnly,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.ObserveOnly),
                AllowParentPush: false,
                AllowChildChime: false,
                ParentAdviceVi: "Thói quen đang tự chủ — hãy quan sát, đừng nhắc thêm."));
        }

        if (reminder is FamilyReminderStates.None)
        {
            return new Decision(
                driver,
                "",
                FamilyInterventionLevels.None,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.None),
                AllowParentPush: false,
                AllowChildChime: false,
                ParentAdviceVi: "",
                patternCode,
                tacticCode);
        }

        if (reminder == FamilyReminderStates.Upcoming)
        {
            return WithPattern(new Decision(
                driver,
                cue,
                FamilyInterventionLevels.SelfCue,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.SelfCue),
                AllowParentPush: false,
                AllowChildChime: false,
                ParentAdviceVi: "Sắp tới giờ — để con tự nhớ trước."));
        }

        // due_now / overdue — budget never increases; patterns may lower push
        var budgetLeft = i.ParentNudgesUsedToday < Math.Max(0, i.ParentNudgeBudget);
        var preferNoParentPush =
            patternCode is FamilyBehaviorPatternCodes.NudgeDependent
                or FamilyBehaviorPatternCodes.EveningFatigue;

        if (stage == FamilyHabitStages.HabitForming)
        {
            var level = reminder == FamilyReminderStates.Overdue
                ? FamilyInterventionLevels.SoftNudge
                : FamilyInterventionLevels.SelfCue;
            return WithPattern(new Decision(
                driver,
                cue,
                level,
                FamilyInterventionLevels.LabelVi(level),
                AllowParentPush: false,
                AllowChildChime: reminder == FamilyReminderStates.DueNow,
                ParentAdviceVi: "Đang thành thói quen — chỉ gợi ý cho con, không đẩy parent push."));
        }

        if (stage == FamilyHabitStages.Assisted)
        {
            if (reminder == FamilyReminderStates.DueNow)
            {
                return WithPattern(new Decision(
                    driver,
                    cue,
                    FamilyInterventionLevels.SelfCue,
                    FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.SelfCue),
                    AllowParentPush: false,
                    AllowChildChime: true,
                    ParentAdviceVi: "Đến giờ — để con tự bắt đầu trước."));
            }

            var softLevel = budgetLeft && !preferNoParentPush
                ? FamilyInterventionLevels.SoftNudge
                : FamilyInterventionLevels.ObserveOnly;
            return WithPattern(new Decision(
                driver,
                cue,
                softLevel,
                FamilyInterventionLevels.LabelVi(softLevel),
                AllowParentPush: softLevel == FamilyInterventionLevels.SoftNudge,
                AllowChildChime: true,
                ParentAdviceVi: softLevel == FamilyInterventionLevels.SoftNudge
                    ? "Quá giờ — nhắc nhẹ được (còn ngân sách hôm nay)."
                    : preferNoParentPush
                        ? "Pattern đang gợi ý đổi chiến thuật — không tăng nhắc, hãy thử cách khác."
                        : "Đã đủ lần nhắc hôm nay — dừng để tránh phụ thuộc."));
        }

        // new / guided (and unknown)
        if (reminder == FamilyReminderStates.DueNow)
        {
            return WithPattern(new Decision(
                driver,
                cue,
                FamilyInterventionLevels.SoftNudge,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.SoftNudge),
                AllowParentPush: false,
                AllowChildChime: true,
                ParentAdviceVi: "Đến giờ — ưu tiên gợi ý cho con, chưa cần bố mẹ nhắc."));
        }

        // overdue + early stage
        if (!budgetLeft || preferNoParentPush)
        {
            return WithPattern(new Decision(
                driver,
                cue,
                FamilyInterventionLevels.ObserveOnly,
                FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.ObserveOnly),
                AllowParentPush: false,
                AllowChildChime: !preferNoParentPush && budgetLeft,
                ParentAdviceVi: preferNoParentPush
                    ? "Đổi chiến thuật thay vì nhắc thêm — xem gợi ý pattern tuần này."
                    : "Hết ngân sách nhắc hôm nay — quan sát thêm."));
        }

        return WithPattern(new Decision(
            driver,
            cue,
            FamilyInterventionLevels.ParentNudge,
            FamilyInterventionLevels.LabelVi(FamilyInterventionLevels.ParentNudge),
            AllowParentPush: true,
            AllowChildChime: true,
            ParentAdviceVi: "Quá giờ và thói quen còn mới — bố mẹ có thể nhắc một lần."));
    }

    private static string PickDriver(Input i, string stage)
    {
        if (string.Equals(i.SkipReason, FamilySkipReasons.NeedHelp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.SkipReason, FamilySkipReasons.NotReady, StringComparison.OrdinalIgnoreCase))
            return FamilyMotivationDrivers.Relatedness;

        if (i.IsLearningMission)
            return FamilyMotivationDrivers.Mastery;

        if (stage is FamilyHabitStages.Autonomous or FamilyHabitStages.Maintained
            or FamilyHabitStages.HabitForming)
            return FamilyMotivationDrivers.Autonomy;

        if (i.HabitStreakDays >= 3)
            return FamilyMotivationDrivers.Progress;

        if (string.Equals(i.SkipReason, FamilySkipReasons.Sick, StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.SkipReason, FamilySkipReasons.Busy, StringComparison.OrdinalIgnoreCase))
            return FamilyMotivationDrivers.Rest;

        return FamilyMotivationDrivers.Relatedness;
    }

    private static string CueVi(string driver, int streak) =>
        driver switch
        {
            FamilyMotivationDrivers.Autonomy =>
                "Con tự chọn lúc bắt đầu — mình tin con làm được.",
            FamilyMotivationDrivers.Progress =>
                streak > 1
                    ? $"Chuỗi {streak} ngày đang đẹp — thêm một bước nữa thôi."
                    : "Mỗi lần xong là một bước tiến rõ.",
            FamilyMotivationDrivers.Mastery =>
                "Làm chậm một ý khó cũng được — hiểu mới quan trọng.",
            FamilyMotivationDrivers.Rest =>
                "Nghỉ ngắn rồi quay lại cũng ổn.",
            _ =>
                "Nhà mình đang ở đây cùng con — bắt đầu khi sẵn sàng nhé.",
        };
}
