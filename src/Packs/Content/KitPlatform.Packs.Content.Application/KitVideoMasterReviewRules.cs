namespace KitPlatform.Packs.Content;

public static class KitVideoMasterReviewRules
{
    public const string DocumentId = "CHAR-001_MINH_MASTER_REVIEW_SPEC_V1";
    public const string MasterRefCode = "CHAR-001_MASTER_REFERENCE_V1";
    public const string AllowedCandidate = "MINH-E01-CANDIDATE-004-D";
    public const string ParentCandidate = "MINH-E01-CANDIDATE-004";
    public const string Variation = "004-D";
    public const int IdentityRequired = 7;
    public const int StressRequired = 10;

    public static readonly string[] IdentityVariants =
    [
        "FRONT", "THREE_QUARTER_LEFT", "THREE_QUARTER_RIGHT", "PROFILE",
        "NEUTRAL", "SAD", "LIGHT_SMILE",
    ];

    public static readonly string[] StressCases =
    [
        "ST-01", "ST-02", "ST-03", "ST-04", "ST-05",
        "ST-06", "ST-07", "ST-08", "ST-09", "ST-10",
    ];

    public static bool Accepts(string? project, string? character, string? era) =>
        string.Equals(project, "FAMIXA", StringComparison.OrdinalIgnoreCase)
        && string.Equals(character, "CHAR-001", StringComparison.OrdinalIgnoreCase)
        && string.Equals(era, "ERA-01", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowedCandidate(string? code) =>
        string.Equals(code, AllowedCandidate, StringComparison.OrdinalIgnoreCase);

    public static bool DnaAllows(string? status) =>
        string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);

    public static bool IsDirectorDecision(string? decision) =>
        decision is "PASS" or "CONDITIONAL" or "REJECT";

    public static bool IsReviewEligible(string? lifecycle, bool selectionFlag, bool frontRunner, bool qaPass)
    {
        var life = (lifecycle ?? "").Trim().ToUpperInvariant();
        if (life is "INELIGIBLE" or "NOT_ELIGIBLE" or "REJECTED") return false;
        if (life is "ELIGIBLE" or "FRONT_RUNNER") return true;
        if (frontRunner || selectionFlag) return true;
        return qaPass;
    }

    public sealed record Gate(
        bool Readable,
        bool HashValid,
        bool VisionPass,
        bool ProductionStill,
        int CandidateP0,
        bool Eligible,
        bool DnaApproved,
        bool IdentityPass,
        int IdentityHave,
        int IdentityP0,
        bool StressPass,
        int StressHave,
        int StressP0,
        bool St10Pass,
        bool LockedMaster);

    public static void EnsureCanOpen(string? candidateCode, Gate gate)
    {
        if (!IsAllowedCandidate(candidateCode))
            throw new InvalidOperationException("MASTER_REVIEW_BLOCKED: chỉ MINH-E01-CANDIDATE-004-D.");
        EnsureGates(gate, "MASTER_REVIEW_BLOCKED");
    }

    public static void EnsureDirectorPass(string? candidateCode, Gate gate)
    {
        if (!IsAllowedCandidate(candidateCode))
            throw new InvalidOperationException("MASTER_REFERENCE_GATE_NOT_SATISFIED: chỉ MINH-E01-CANDIDATE-004-D.");
        EnsureGates(gate, "MASTER_REFERENCE_GATE_NOT_SATISFIED");
    }

    public static void EnsureCanSelect(string? reviewStatus, string? candidateCode, Gate gate)
    {
        EnsureNotLocked(gate.LockedMaster, reviewStatus);
        if (!string.Equals(reviewStatus, "PASS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_REVIEW: Chọn Master chỉ sau Director PASS.");
        EnsureDirectorPass(candidateCode, gate);
    }

    public static void EnsureCanApprove(string? reviewStatus, string? candidateCode, Gate gate)
    {
        EnsureNotLocked(gate.LockedMaster, reviewStatus);
        if (!string.Equals(reviewStatus, "SELECTED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_REVIEW: APPROVE chỉ sau khi đã chọn Master Reference.");
        EnsureDirectorPass(candidateCode, gate);
    }

    public static void EnsureCanLock(string? reviewStatus, string? candidateCode, Gate gate)
    {
        if (gate.LockedMaster || string.Equals(reviewStatus, "LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST.");
        if (reviewStatus is not ("PASS" or "SELECTED" or "APPROVED"))
            throw new InvalidOperationException("MASTER_REVIEW: LOCK chỉ sau Director PASS.");
        EnsureDirectorPass(candidateCode, gate);
    }

    public static void EnsureNotLocked(bool lockedMaster, string? reviewStatus)
    {
        if (lockedMaster || string.Equals(reviewStatus, "LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST.");
    }

    public static void EnsureNoGenerate(string? action)
    {
        if (string.Equals(action, "GENERATE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "REGENERATE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_REVIEW: không tạo ảnh mới.");
    }

    public static bool IsGoldenPath(string? path) =>
        KitVideoMasterReferenceRules.IsLegacyOrGolden(path);

    private static void EnsureGates(Gate gate, string prefix)
    {
        if (gate.LockedMaster)
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST.");
        if (!gate.Readable)
            throw new InvalidOperationException($"{prefix}: artifact chưa đọc được.");
        if (!gate.HashValid)
            throw new InvalidOperationException($"{prefix}: HASH_VALID thất bại.");
        if (!gate.VisionPass)
            throw new InvalidOperationException($"{prefix}: Vision chưa PASS.");
        if (!gate.ProductionStill)
            throw new InvalidOperationException($"{prefix}: imageType phải PRODUCTION_STILL.");
        if (gate.CandidateP0 > 0)
            throw new InvalidOperationException($"{prefix}: candidate P0 phải = 0.");
        if (!gate.Eligible)
            throw new InvalidOperationException($"{prefix}: candidate bị loại (INELIGIBLE / REJECTED). Identity + Stress mới là cổng Review.");
        if (!gate.DnaApproved)
            throw new InvalidOperationException($"{prefix}: Visual DNA phải APPROVED.");
        if (!gate.IdentityPass || gate.IdentityHave < IdentityRequired || gate.IdentityP0 > 0)
            throw new InvalidOperationException($"{prefix}: Identity Test phải 7/7 PASS, P0 = 0.");
        if (!gate.StressPass || gate.StressHave < StressRequired || gate.StressP0 > 0 || !gate.St10Pass)
            throw new InvalidOperationException($"{prefix}: Stress Test phải 10/10 PASS, P0 = 0, ST-10 PASS.");
    }
}
