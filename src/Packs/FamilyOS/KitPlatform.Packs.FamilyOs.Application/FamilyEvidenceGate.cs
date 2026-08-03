namespace KitPlatform.Packs.FamilyOs;

public static class FamilyCommitmentKinds
{
    public const string Chore = "chore";
    public const string StudyFocus = "study_focus";
    public const string Relation = "relation";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Chore, StudyFocus, Relation,
    };

    public static string Normalize(string? kind) =>
        (kind ?? "").Trim().ToLowerInvariant() switch
        {
            StudyFocus => StudyFocus,
            Relation => Relation,
            _ => Chore,
        };

    public static string LabelVi(string? kind) =>
        Normalize(kind) switch
        {
            StudyFocus => "Học / tập trung",
            Relation => "Quan hệ",
            _ => "Việc nhà",
        };

    public static string DefaultPolicy(string? kind) =>
        Normalize(kind) == StudyFocus
            ? FamilyEvidencePolicies.RequiredSoft
            : FamilyEvidencePolicies.Optional;
}

public static class FamilyEvidencePolicies
{
    public const string Optional = "optional";
    public const string RequiredSoft = "required_soft";
    public const string RequiredHard = "required_hard";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Optional, RequiredSoft, RequiredHard,
    };

    public static string Normalize(string? policy) =>
        (policy ?? "").Trim().ToLowerInvariant() switch
        {
            RequiredHard => RequiredHard,
            RequiredSoft => RequiredSoft,
            _ => Optional,
        };
}

public static class FamilyEvidenceSatisfiedBy
{
    public const string Photo = "photo";
    public const string Retrieval = "retrieval";
    public const string ParentVerify = "parent_verify";
    public const string DeviceSignal = "device_signal";
}

/// <summary>Evidence P0/P0.5 — gate stars / hard-block done for study_focus.</summary>
public static class FamilyEvidenceGate
{
    public const string EvidenceRequiredCode = "evidence_required";

    public const string EvidenceRequiredMessageVi =
        "Cam kết học cần bằng chứng (ảnh được xác nhận, tự kiểm tra nhớ bài, hoặc bố mẹ xác nhận) trước khi tính hoàn thành đủ.";

    public const string SoftGateLabelVi =
        "Ảnh chỉ là nộp bài — cần bố mẹ xác nhận (đúng bài hôm nay) hoặc câu hỏi nhớ bài mới được sao.";

    public const string HardGateLabelVi =
        "Chặn hoàn thành — cần xác nhận bằng chứng đúng cam kết (không chỉ ảnh).";

    public const string DurationNotMetCode = "duration_not_met";

    public const string DurationNotMetMessageVi =
        "Chưa đủ thời lượng cam kết (khoảng 70% thời gian dự kiến). Con học thêm hoặc bố mẹ xác nhận vượt thời lượng.";

    public const string ChecklistIncompleteCode = "checklist_incomplete";

    public const string ChecklistIncompleteMessageVi =
        "Cần xác nhận đủ 3 mục: bài hôm nay, đúng khung giờ, đúng nội dung cam kết.";

    /// <summary>P0.5 — minimum fraction of expected duration before evidence can satisfy.</summary>
    public const double MinDurationFraction = 0.7;

    public sealed record Signals(
        string CommitmentKind,
        string EvidencePolicy,
        string? EvidenceUrl,
        bool HasRetrievalCheck,
        DateTimeOffset? EvidenceSatisfiedAt,
        string? EvidenceSatisfiedBy);

    public static bool RequiresEvidence(string? kind, string? policy)
    {
        var p = FamilyEvidencePolicies.Normalize(policy);
        if (p is FamilyEvidencePolicies.RequiredSoft or FamilyEvidencePolicies.RequiredHard)
            return true;
        return FamilyCommitmentKinds.Normalize(kind) == FamilyCommitmentKinds.StudyFocus
            && p != FamilyEvidencePolicies.Optional;
    }

    /// <summary>
    /// P0.5: photo URL alone does NOT satisfy study_focus.
    /// Only evidence_satisfied_at (set by retrieval / parent_verify checklist) counts.
    /// </summary>
    public static bool IsSatisfied(Signals s)
    {
        var policy = FamilyEvidencePolicies.Normalize(s.EvidencePolicy);
        if (policy == FamilyEvidencePolicies.Optional
            && FamilyCommitmentKinds.Normalize(s.CommitmentKind) != FamilyCommitmentKinds.StudyFocus)
        {
            return true;
        }

        if (policy == FamilyEvidencePolicies.Optional
            && FamilyCommitmentKinds.Normalize(s.CommitmentKind) == FamilyCommitmentKinds.StudyFocus)
        {
            // study_focus should use required_*; treat as soft if mis-set
            policy = FamilyEvidencePolicies.RequiredSoft;
        }

        if (policy == FamilyEvidencePolicies.Optional)
            return true;

        return s.EvidenceSatisfiedAt is not null;
    }

    /// <summary>True when study photo was uploaded but not yet parent/retrieval-satisfied.</summary>
    public static bool HasSubmittedPhoto(Signals s) =>
        !string.IsNullOrWhiteSpace(s.EvidenceUrl);

    public static bool MeetsMinStudyDuration(
        DateTimeOffset? startedAt,
        int? expectedDurationMinutes,
        DateTimeOffset now,
        bool overrideDuration)
    {
        if (overrideDuration)
            return true;
        if (expectedDurationMinutes is null or <= 0)
            return true;
        if (startedAt is null)
            return false;
        var min = TimeSpan.FromMinutes(expectedDurationMinutes.Value * MinDurationFraction);
        return now - startedAt.Value >= min;
    }

    public static int? MinRequiredDurationMinutes(int? expectedDurationMinutes)
    {
        if (expectedDurationMinutes is null or <= 0)
            return null;
        return (int)Math.Ceiling(expectedDurationMinutes.Value * MinDurationFraction);
    }

    public static string? InferSatisfiedBy(Signals s)
    {
        if (!string.IsNullOrWhiteSpace(s.EvidenceSatisfiedBy))
            return s.EvidenceSatisfiedBy;
        // Do not infer photo from URL — photo is submit-only until verified.
        return null;
    }

    public static bool BlocksDone(Signals s) =>
        FamilyEvidencePolicies.Normalize(s.EvidencePolicy) == FamilyEvidencePolicies.RequiredHard
        && !IsSatisfied(s);

    public static bool GatesStars(Signals s) =>
        RequiresEvidence(s.CommitmentKind, s.EvidencePolicy) && !IsSatisfied(s);

    public static string? GateLabelVi(Signals s)
    {
        if (!RequiresEvidence(s.CommitmentKind, s.EvidencePolicy))
            return null;
        if (IsSatisfied(s))
            return null;
        return FamilyEvidencePolicies.Normalize(s.EvidencePolicy) == FamilyEvidencePolicies.RequiredHard
            ? HardGateLabelVi
            : SoftGateLabelVi;
    }

    public static string InferKindFromTitle(string? title) =>
        FamilyLearningMission.IsLearningTitle(title)
            ? FamilyCommitmentKinds.StudyFocus
            : FamilyCommitmentKinds.Chore;
}