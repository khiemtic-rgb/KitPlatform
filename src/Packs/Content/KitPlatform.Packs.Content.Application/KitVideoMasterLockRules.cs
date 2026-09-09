namespace KitPlatform.Packs.Content;

public static class KitVideoMasterLockRules
{
    public const string DocumentId = "CHAR-001_MINH_MASTER_REFERENCE_LOCK_IMPLEMENTATION_V1";
    public const string MasterCode = "CHAR-001-MINH-ERA01-MASTER-V1";
    public const string MasterRefCode = "CHAR-001_MASTER_REFERENCE_V1";
    public const string Version = "V1";
    public const string LockedStatus = "MASTER_REFERENCE_LOCKED";
    public const string SourceStatus = "MASTER_REFERENCE_SOURCE";
    public const string CharacterName = "MINH";

    public sealed record LockGate(
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
        bool DirectorPass,
        bool Rejected,
        bool DraftBlocked,
        bool EligibleBlocked,
        bool LockedMaster,
        string? CandidateCode,
        string? ReviewStatus,
        string? Actor);

    public static void EnsureDirector(string? actor)
    {
        var a = (actor ?? "").Trim();
        if (a.Length == 0 || a.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCK_UNAUTHORIZED: chỉ Director được khóa Master Reference.");
    }

    public static void EnsureImmutable(string? action)
    {
        var a = (action ?? "").Trim().ToUpperInvariant();
        if (a is "UPDATE" or "DELETE" or "ARCHIVE" or "OVERWRITE" or "REGENERATE"
            or "CHANGE_ARTIFACT" or "CHANGE_SHA256" or "CHANGE_SOURCE")
            throw new InvalidOperationException("MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST / V2.");
    }

    public static void EnsureNoGenerate(string? action)
    {
        if (string.Equals(action, "GENERATE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "REGENERATE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MASTER_LOCK: không tạo ảnh mới.");
    }

    public static bool SameSource(string? existingCode, string? requestedCode) =>
        string.Equals(existingCode, requestedCode, StringComparison.OrdinalIgnoreCase);

    public static void EnsureCanLock(LockGate gate)
    {
        EnsureDirector(gate.Actor);
        if (!KitVideoMasterReviewRules.IsAllowedCandidate(gate.CandidateCode))
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: chỉ MINH-E01-CANDIDATE-004-D.");
        if (gate.Rejected)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: candidate bị REJECT.");
        var review = (gate.ReviewStatus ?? "").ToUpperInvariant();
        if (review is "LOCKED")
            return;
        if (review is not ("PASS" or "SELECTED" or "APPROVED"))
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: Director chưa PASS.");
        if (gate.DraftBlocked)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: candidate đang DRAFT.");
        if (gate.EligibleBlocked)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: candidate chưa FRONT_RUNNER/ELIGIBLE.");
        if (gate.LockedMaster)
            throw new InvalidOperationException("MASTER_LOCKED: Canon active. Không overwrite V1.");
        if (!gate.Readable)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: artifact chưa đọc được.");
        if (!gate.HashValid)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: HASH_VALID thất bại.");
        if (!gate.VisionPass)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: Vision chưa PASS.");
        if (!gate.ProductionStill)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: imageType phải PRODUCTION_STILL.");
        if (gate.CandidateP0 > 0)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: candidate P0 phải = 0.");
        if (!gate.Eligible)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: candidate NOT_ELIGIBLE.");
        if (!gate.DnaApproved)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: Visual DNA phải APPROVED.");
        if (!gate.IdentityPass || gate.IdentityHave < KitVideoMasterReviewRules.IdentityRequired || gate.IdentityP0 > 0)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: Identity Test phải 7/7 PASS.");
        if (!gate.StressPass || gate.StressHave < KitVideoMasterReviewRules.StressRequired || gate.StressP0 > 0 || !gate.St10Pass)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: Stress Test phải 10/10 PASS.");
        if (!gate.DirectorPass)
            throw new InvalidOperationException("MASTER_LOCK_BLOCKED: Director chưa PASS.");
    }

    public static bool AutoPromote(bool _, bool __, bool ___) => false;
}
