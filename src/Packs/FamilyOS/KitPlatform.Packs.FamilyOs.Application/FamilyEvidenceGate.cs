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

/// <summary>Evidence P0 — gate stars / hard-block done for study_focus.</summary>
public static class FamilyEvidenceGate
{
    public const string EvidenceRequiredCode = "evidence_required";

    public const string EvidenceRequiredMessageVi =
        "Cam kết học cần bằng chứng (ảnh, tự kiểm tra nhớ bài, hoặc bố mẹ xác nhận) trước khi tính hoàn thành đủ.";

    public const string SoftGateLabelVi =
        "Cần ảnh, câu hỏi nhớ bài, hoặc bố mẹ xác nhận để nhận sao.";

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

        if (s.EvidenceSatisfiedAt is not null)
            return true;
        if (!string.IsNullOrWhiteSpace(s.EvidenceUrl))
            return true;
        if (s.HasRetrievalCheck)
            return true;
        return false;
    }

    public static string? InferSatisfiedBy(Signals s)
    {
        if (!string.IsNullOrWhiteSpace(s.EvidenceSatisfiedBy))
            return s.EvidenceSatisfiedBy;
        if (!string.IsNullOrWhiteSpace(s.EvidenceUrl))
            return FamilyEvidenceSatisfiedBy.Photo;
        if (s.HasRetrievalCheck)
            return FamilyEvidenceSatisfiedBy.Retrieval;
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
        return IsSatisfied(s) ? null : SoftGateLabelVi;
    }

    public static string InferKindFromTitle(string? title) =>
        FamilyLearningMission.IsLearningTitle(title)
            ? FamilyCommitmentKinds.StudyFocus
            : FamilyCommitmentKinds.Chore;
}
