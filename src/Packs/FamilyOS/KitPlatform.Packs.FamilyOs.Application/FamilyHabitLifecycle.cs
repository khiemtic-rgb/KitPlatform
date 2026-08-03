namespace KitPlatform.Packs.FamilyOs;

/// <summary>Behavior OS Wave 1 — habit lifecycle on commitment templates.</summary>
public static class FamilyHabitStages
{
    public const string New = "new";
    public const string Guided = "guided";
    public const string Assisted = "assisted";
    public const string HabitForming = "habit_forming";
    public const string Autonomous = "autonomous";
    public const string Maintained = "maintained";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        New, Guided, Assisted, HabitForming, Autonomous, Maintained,
    };

    public static string LabelVi(string? stage) =>
        (stage ?? "").Trim().ToLowerInvariant() switch
        {
            New => "Mới",
            Guided => "Đang hướng dẫn",
            Assisted => "Cần hỗ trợ",
            HabitForming => "Đang thành thói quen",
            Autonomous => "Tự chủ",
            Maintained => "Duy trì",
            _ => "Mới",
        };

    /// <summary>Map consecutive done streak → stage (PRD Wave 1 thresholds).</summary>
    public static string FromStreak(int streakDays) =>
        streakDays switch
        {
            >= 35 => Maintained,
            >= 21 => Autonomous,
            >= 14 => HabitForming,
            >= 7 => Assisted,
            >= 3 => Guided,
            _ => New,
        };

    public static bool IsReminderSuppressed(string? stage, bool reminderSuppressedFlag) =>
        reminderSuppressedFlag
        || string.Equals(stage, Autonomous, StringComparison.OrdinalIgnoreCase)
        || string.Equals(stage, Maintained, StringComparison.OrdinalIgnoreCase);

    public static bool IsSoftReminderOnly(string? stage) =>
        string.Equals(stage, HabitForming, StringComparison.OrdinalIgnoreCase);
}

public static class FamilyBehaviorEventTypes
{
    public const string CommitmentDone = "commitment_done";
    public const string CommitmentSkipped = "commitment_skipped";
    public const string ReflectionSubmitted = "reflection_submitted";
    public const string HabitStageChanged = "habit_stage_changed";
    public const string ReminderSuppressed = "reminder_suppressed";
    public const string ReminderFired = "reminder_fired";
    public const string ParentNudge = "parent_nudge";
    public const string SelfStart = "self_start";
    public const string RetrievalSubmitted = "retrieval_submitted";
    public const string ConfidenceScored = "confidence_scored";
    public const string EvidenceUploaded = "evidence_uploaded";
    public const string MotivationCued = "motivation_cued";
    public const string InterventionDecided = "intervention_decided";
    public const string ParentNudgeBlocked = "parent_nudge_blocked";
    public const string TwinScored = "twin_scored";
    public const string PredictionFlagged = "prediction_flagged";
    public const string RetirementAdvanced = "retirement_advanced";
    public const string ObserveModeEntered = "observe_mode_entered";
    public const string ObserveModeExited = "observe_mode_exited";
    public const string DependenceWarned = "dependence_warned";
    public const string ParentCoachActed = "parent_coach_acted";
    public const string PatternDetected = "pattern_detected";
    public const string TacticRotated = "tactic_rotated";
    public const string ChildVoiceSubmitted = "child_voice_submitted";
    public const string ParentStrategyTip = "parent_strategy_tip";
    /// <summary>Evidence P0: hard gate blocked done without evidence.</summary>
    public const string CommitmentEvidenceGateBlocked = "commitment_evidence_gate_blocked";
    /// <summary>Evidence P0: min evidence first met (photo | retrieval | parent_verify).</summary>
    public const string CommitmentEvidenceSatisfied = "commitment_evidence_satisfied";
    /// <summary>Evidence P0: kind assigned on ad-hoc / materialize.</summary>
    public const string CommitmentKindAssigned = "commitment_kind_assigned";
}

public static class FamilyReflectionPrompts
{
    public const string Hardest = "hardest";
    public const string Learned = "learned";
    public const string ImproveTomorrow = "improve_tomorrow";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Hardest, Learned, ImproveTomorrow,
    };

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Hardest => "Điều khó nhất hôm nay là gì?",
            Learned => "Con học được gì?",
            ImproveTomorrow => "Mai con muốn cải thiện điều gì?",
            _ => "Con muốn chia sẻ gì?",
        };

    /// <summary>Rotate prompts by commitment id so each day feels slightly different.</summary>
    public static string SuggestFor(Guid commitmentId)
    {
        var codes = new[] { Hardest, Learned, ImproveTomorrow };
        var idx = Math.Abs(commitmentId.GetHashCode()) % codes.Length;
        return codes[idx];
    }
}

/// <summary>Pure habit streak / stage transitions (no I/O).</summary>
public static class FamilyHabitLifecycle
{
    public sealed record Snapshot(
        string Stage,
        int StreakDays,
        DateOnly? LastDoneDate,
        bool ReminderSuppressed,
        DateTimeOffset? StageChangedAt);

    public sealed record TransitionResult(
        Snapshot Next,
        bool StageChanged,
        bool BecameSuppressed,
        string? PreviousStage);

    public static TransitionResult ApplyDone(Snapshot current, DateOnly flowDate)
    {
        var prevStage = current.Stage;
        var streak = current.StreakDays;
        if (current.LastDoneDate == flowDate)
        {
            // Same-day redo — keep streak, refresh stage from streak.
            var sameStage = FamilyHabitStages.FromStreak(Math.Max(streak, 1));
            var sameSuppress = FamilyHabitStages.IsReminderSuppressed(sameStage, false);
            return new TransitionResult(
                current with
                {
                    Stage = sameStage,
                    ReminderSuppressed = sameSuppress,
                    StageChangedAt = sameStage != prevStage
                        ? DateTimeOffset.UtcNow
                        : current.StageChangedAt,
                },
                StageChanged: sameStage != prevStage,
                BecameSuppressed: !current.ReminderSuppressed && sameSuppress,
                PreviousStage: prevStage);
        }

        if (current.LastDoneDate is DateOnly last
            && flowDate == last.AddDays(1))
        {
            streak += 1;
        }
        else
        {
            streak = 1;
        }

        var stage = FamilyHabitStages.FromStreak(streak);
        // Maintain: stayed autonomous ≥14 calendar days after stage change.
        if (string.Equals(stage, FamilyHabitStages.Autonomous, StringComparison.OrdinalIgnoreCase)
            && current.StageChangedAt is DateTimeOffset changed
            && (flowDate.ToDateTime(TimeOnly.MinValue) - changed.UtcDateTime.Date).TotalDays >= 14)
        {
            stage = FamilyHabitStages.Maintained;
        }

        var suppressed = FamilyHabitStages.IsReminderSuppressed(stage, false);
        var stageChanged = !string.Equals(stage, prevStage, StringComparison.OrdinalIgnoreCase);
        return new TransitionResult(
            new Snapshot(
                stage,
                streak,
                flowDate,
                suppressed,
                stageChanged ? DateTimeOffset.UtcNow : current.StageChangedAt),
            stageChanged,
            BecameSuppressed: !current.ReminderSuppressed && suppressed,
            PreviousStage: prevStage);
    }

    public static TransitionResult ApplySkip(Snapshot current, DateOnly flowDate)
    {
        var prevStage = current.Stage;
        var streak = Math.Max(0, current.StreakDays - 2);
        var stage = FamilyHabitStages.FromStreak(streak);
        // Never jump to maintained after a skip.
        if (string.Equals(stage, FamilyHabitStages.Maintained, StringComparison.OrdinalIgnoreCase))
            stage = FamilyHabitStages.Autonomous;

        var suppressed = FamilyHabitStages.IsReminderSuppressed(stage, false);
        var stageChanged = !string.Equals(stage, prevStage, StringComparison.OrdinalIgnoreCase);
        return new TransitionResult(
            new Snapshot(
                stage,
                streak,
                current.LastDoneDate,
                suppressed,
                stageChanged ? DateTimeOffset.UtcNow : current.StageChangedAt),
            stageChanged,
            BecameSuppressed: false,
            PreviousStage: prevStage);
    }

    /// <summary>
    /// Apply reminder budget: suppress hot nudges for graduated habits;
    /// habit_forming keeps only "upcoming".
    /// </summary>
    public static (string State, string? Label) ApplyReminderBudget(
        string state,
        string? label,
        string? habitStage,
        bool reminderSuppressed)
    {
        if (FamilyHabitStages.IsReminderSuppressed(habitStage, reminderSuppressed))
            return (FamilyReminderStates.None, null);

        if (FamilyHabitStages.IsSoftReminderOnly(habitStage)
            && state is FamilyReminderStates.DueNow or FamilyReminderStates.Overdue)
            return (FamilyReminderStates.None, null);

        return (state, label);
    }
}
