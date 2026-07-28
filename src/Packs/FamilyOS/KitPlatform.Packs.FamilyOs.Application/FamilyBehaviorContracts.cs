namespace KitPlatform.Packs.FamilyOs;

public sealed record CommitmentReflectionDto(
    Guid Id,
    Guid FamilyId,
    Guid CommitmentId,
    Guid? MemberId,
    string PromptCode,
    string PromptLabelVi,
    string AnswerText,
    DateTimeOffset CreatedAt,
    int ConfidenceScore = 0,
    string? ConfidenceLabelVi = null,
    int EvidenceLevel = 0,
    bool NeedsRetrievalCheck = false);

public sealed record SubmitCommitmentReflectionRequest(
    string PromptCode,
    string AnswerText);

public sealed record HabitProgressDto(
    string HabitStage,
    string HabitStageLabelVi,
    int HabitStreakDays,
    bool ReminderSuppressed,
    bool NeedsReflection,
    string? SuggestedReflectionPrompt,
    int ConfidenceScore = 0,
    string? ConfidenceLabelVi = null,
    int EvidenceLevel = 0,
    bool NeedsRetrievalCheck = false);

public sealed record RetrievalOptionDto(string Code, string LabelVi);

public sealed record RetrievalQuestionDto(
    string Code,
    string PromptVi,
    IReadOnlyList<RetrievalOptionDto> Options);

public sealed record RetrievalCheckChallengeDto(
    Guid CommitmentId,
    string Title,
    bool AlreadySubmitted,
    IReadOnlyList<RetrievalQuestionDto> Questions);

public sealed record SubmitRetrievalCheckRequest(
    string MethodAnswer,
    string RecallAnswer);

public sealed record RetrievalCheckResultDto(
    Guid CommitmentId,
    string MethodAnswer,
    string RecallAnswer,
    bool IllusionRisk,
    int ConfidenceScore,
    string ConfidenceLabelVi,
    int EvidenceLevel,
    string EvidenceLevelLabelVi);

public sealed record BehaviorCoachMemberHintDto(
    Guid? MemberId,
    string? MemberName,
    Guid CommitmentId,
    string Title,
    string InterventionLevel,
    string InterventionLabelVi,
    string ParentAdviceVi,
    bool AllowParentPush,
    string? MotivationCueVi);

public sealed record BehaviorCoachDto(
    DateOnly FlowDate,
    int ParentNudgesUsedToday,
    int ParentNudgeBudget,
    int ObserveOnlyCount,
    int AllowParentPushCount,
    IReadOnlyList<BehaviorCoachMemberHintDto> Hints);

public sealed record BehaviorTwinDimensionDto(
    string Code,
    string LabelVi,
    int Score,
    string WhyVi);

public sealed record BehaviorTwinMemberDto(
    Guid MemberId,
    string MemberName,
    int OverallScore,
    string OverallLabelVi,
    string DisclaimerVi,
    string? EveningRiskBand,
    string? EveningRiskLabelVi,
    IReadOnlyList<string> EveningReasonsVi,
    string? EveningSuggestedActionVi,
    IReadOnlyList<BehaviorTwinDimensionDto> Dimensions,
    DateOnly SnapshotDate);

public sealed record BehaviorTwinDto(
    DateOnly AsOfDate,
    string DisclaimerVi,
    IReadOnlyList<BehaviorTwinMemberDto> Members);

public sealed record BehaviorRetirementPolicyDto(
    bool ObserveOnly,
    string? RetirementStage,
    string? RetirementLabelVi,
    int? ParentNudgeBudget,
    string? NotesVi,
    DateTimeOffset UpdatedAt);

public sealed record UpdateBehaviorRetirementPolicyRequest(
    bool? ObserveOnly = null,
    int? ParentNudgeBudget = null,
    string? NotesVi = null);

public sealed record FamilyBehaviorTwinDto(
    DateOnly AsOfDate,
    string DisclaimerVi,
    int FamilyPeaceIndex,
    int FamilyAutonomyIndex,
    int ParentalInterventionIndex,
    string RetirementStage,
    string RetirementLabelVi,
    string RetirementAdviceVi,
    string SiblingBalance,
    string SiblingBalanceLabelVi,
    string SiblingAdviceVi,
    bool DependenceWarning,
    string? DependenceWarningVi,
    bool RecommendObserveOnly,
    bool ObserveOnlyActive,
    BehaviorRetirementPolicyDto Policy,
    IReadOnlyList<BehaviorTwinMemberDto> Children);

public interface IFamilyBehaviorService
{
    /// <summary>Called after commitment done/skipped — updates template habit + emits events.</summary>
    Task<HabitProgressDto?> SyncHabitAfterProgressAsync(
        Guid familyId,
        Guid commitmentId,
        string status,
        DateOnly flowDate,
        CancellationToken cancellationToken = default);

    Task RecordSelfStartAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task RecordParentNudgeAsync(
        Guid familyId,
        Guid? commitmentId,
        Guid? memberId,
        bool allowed,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<BehaviorCoachDto> GetTodayCoachAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<BehaviorTwinDto> GetTwinAsync(
        Guid familyId,
        Guid? memberId = null,
        CancellationToken cancellationToken = default);

    Task<FamilyBehaviorTwinDto> GetFamilyTwinAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<BehaviorRetirementPolicyDto> GetRetirementPolicyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<BehaviorRetirementPolicyDto> UpdateRetirementPolicyAsync(
        Guid familyId,
        UpdateBehaviorRetirementPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<CommitmentReflectionDto> SubmitReflectionAsync(
        Guid familyId,
        Guid commitmentId,
        SubmitCommitmentReflectionRequest request,
        CancellationToken cancellationToken = default);

    Task<CommitmentReflectionDto?> GetReflectionAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task<RetrievalCheckChallengeDto?> GetRetrievalCheckAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task<RetrievalCheckResultDto> SubmitRetrievalCheckAsync(
        Guid familyId,
        Guid commitmentId,
        SubmitRetrievalCheckRequest request,
        CancellationToken cancellationToken = default);
}
