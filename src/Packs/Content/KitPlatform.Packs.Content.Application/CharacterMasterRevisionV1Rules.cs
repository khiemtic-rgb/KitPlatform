using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_MASTER_REVISION_V1 — shared Master revision workflow.
/// CRP regeneration does not revise Master. Does not branch on character name.
/// Does not call Gemini, auto-approve, auto-lock, or overwrite a LOCKED Master in place.
/// </summary>
public static class CharacterMasterRevisionV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_MASTER_REVISION_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_MASTER_REVISION_V1_REGRESSION";
    public const string ExtraKey = "characterMasterRevision";

    public const string StatusRequested = "MASTER_REVISION_REQUESTED";
    public const string StatusGenerating = "MASTER_REVISION_GENERATING";
    public const string StatusPendingReview = "MASTER_REVISION_PENDING_REVIEW";
    public const string StatusRejected = "MASTER_REVISION_REJECTED";
    public const string StatusApproved = "MASTER_REVISION_APPROVED";
    public const string StatusLocked = "MASTER_REVISION_LOCKED";

    public const string ReasonAge = "AGE_MISMATCH";
    public const string ReasonAppearance = "APPEARANCE_MISMATCH";
    public const string ReasonIdentity = "IDENTITY_MISMATCH";
    public const string ReasonStyle = "STYLE_MISMATCH";
    public const string ReasonGrooming = "GROOMING_MISMATCH";
    public const string ReasonOther = "OTHER";

    public const string GateCharacterNotFound = "CHARACTER_NOT_FOUND";
    public const string GateMasterNotReady = "MASTER_NOT_READY";
    public const string GateIdentityNotReady = "IDENTITY_NOT_READY";
    public const string GateConfirmation = "CONFIRMATION_REQUIRED";
    public const string GateProviderUnsupported = "PROVIDER_CAPABILITY_UNSUPPORTED";
    public const string GateDuplicate = "BLOCK_DUPLICATE";
    public const string GateLockedSilent = "LOCKED_CHARACTER_NO_SILENT_MUTATION";
    public const string GateReasonInvalid = "REVISION_REASON_INVALID";
    public const string GateProviderRequired = "PROVIDER_NOT_SELECTED";

    public const string StaleDna = "DNA_STALE";
    public const string StalePrp = "PRP_STALE";
    public const string StaleCrp = "CRP_STALE";
    public const string GateAuthorityUnreadable = "MASTER_REVISION_AUTHORITY_UNREADABLE";
    public const string StaffAuthorityUnreadable =
        "Không đọc được Master hiện tại. Không dùng Master cũ để tạo bộ 4 ảnh.";

    public const string ContinuityInstruction =
        "Preserve the identity and character concept, but revise the visual appearance as necessary to satisfy the canonical age and appearance profile.";
    public const string AppearanceLockForbidden =
        "CURRENT MASTER IS AN IDENTITY CONTINUITY REFERENCE ONLY. It is not appearance lock. Do not keep the exact aged face, hairstyle, or elderly body language.";

    public static readonly string[] Reasons =
        [ReasonAge, ReasonAppearance, ReasonIdentity, ReasonStyle, ReasonGrooming, ReasonOther];

    public static readonly string[] Preserve =
    [
        "CharacterId", "character identity", "chronological age", "gender",
        "narrative role", "core personality", "Project Visual Style", "canonical character concept",
    ];

    public static readonly string[] MayRevise =
    [
        "facial maturity", "facial structure", "hairstyle", "hair color", "grooming",
        "skin appearance", "clothing", "body presence", "posture", "expression",
        "apparent age", "overall character appearance",
    ];

    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public sealed record RevisionSource(
        bool CharacterExists,
        string? CharacterId,
        bool MasterReady,
        string? CurrentMasterId,
        string? CurrentMasterSha,
        CharacterAppearanceProfile? Appearance,
        AgeExpressionTarget? Age,
        string? IdentitySha,
        string? IdentityBrief,
        string? ProjectVisualStyleSha,
        bool ProjectVisualStyleReady,
        string? RevisionReason,
        string? RevisionNotes,
        bool Confirm,
        string? Provider,
        bool OfficialLocked,
        bool CharacterReady,
        string? LastSucceededFingerprint,
        string? CandidateMasterSha = null,
        string? CandidateStatus = null);

    public sealed record AuthorityState(
        string? CurrentMasterSha,
        string? CandidateMasterSha,
        string Status,
        string? DnaStatus,
        string? PrpStatus,
        string? CrpStatus);

    public sealed record Gate(string? Code, string Status, bool ProviderCalled, bool MayGenerate);

    public static string? Evaluate(RevisionSource source)
    {
        if (!source.CharacterExists || string.IsNullOrWhiteSpace(source.CharacterId))
            return GateCharacterNotFound;
        if (source.OfficialLocked)
            return GateLockedSilent;
        if (!source.MasterReady || !CharacterReferencePackRules.ShaExists(source.CurrentMasterSha))
            return GateMasterNotReady;
        if (source.Appearance is null || !CharacterAppearanceProfileV1Rules.IsReady(source.Appearance))
            return CharacterAppearanceProfileV1Rules.GateNotReady;
        if (source.Age is null
            || CharacterAgeConsistencyV1Rules.ValidateProfile(
                source.Age.ChronologicalAge, source.Age.TargetAppearanceAgeMin, source.Age.TargetAppearanceAgeMax) is not null)
            return CharacterAgeConsistencyV1Rules.GateNotReady;
        if (string.IsNullOrWhiteSpace(source.IdentitySha) || string.IsNullOrWhiteSpace(source.IdentityBrief))
            return GateIdentityNotReady;
        if (!source.ProjectVisualStyleReady || string.IsNullOrWhiteSpace(source.ProjectVisualStyleSha))
            return ProjectVisualStyleV1Rules.GateNotReady;
        if (!IsReason(source.RevisionReason))
            return GateReasonInvalid;
        if (!source.Confirm)
            return GateConfirmation;
        if (string.IsNullOrWhiteSpace(source.Provider))
            return GateProviderRequired;
        if (!ProviderSupportsMaster(source.Provider))
            return GateProviderUnsupported;
        var fp = Fingerprint(source);
        if (!string.IsNullOrWhiteSpace(source.LastSucceededFingerprint)
            && string.Equals(fp, source.LastSucceededFingerprint, StringComparison.OrdinalIgnoreCase))
            return GateDuplicate;
        return null;
    }

    public static Gate ToGate(string? code) => code switch
    {
        null => new Gate(null, StatusRequested, false, true),
        GateConfirmation => new Gate(code, StatusRequested, false, false),
        _ => new Gate(code, StatusRequested, false, false),
    };

    public static bool IsReason(string? raw) =>
        Reasons.Contains((raw ?? "").Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);

    public static string NormalizeReason(string? raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        return IsReason(t) ? t : "";
    }

    public static string CanonicalNotes(string? notes)
    {
        var t = string.Join(" ", (notes ?? "").Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t;
    }

    public static bool ProviderSupportsMaster(string? provider)
    {
        var p = (provider ?? "").Trim().ToUpperInvariant();
        return p is "GEMINI";
    }

    public static bool CurrentMasterIsAppearanceLock(string? reason)
    {
        var r = NormalizeReason(reason);
        return r is not ReasonAge and not ReasonAppearance;
    }

    public static bool MayReviseAppearance(string? reason) =>
        NormalizeReason(reason) is ReasonAge or ReasonAppearance or ReasonGrooming;

    public static string Fingerprint(RevisionSource source)
    {
        var payload = new
        {
            document = DocumentId,
            characterId = CharacterStudioV1Rules.NormalizeCharacterId(source.CharacterId ?? ""),
            currentMasterSha = (source.CurrentMasterSha ?? "").Trim().ToLowerInvariant(),
            identitySha = (source.IdentitySha ?? "").Trim().ToLowerInvariant(),
            ageProfileSha = source.Age is null ? "" : CharacterAgeConsistencyV1Rules.AgeProfileSha(source.Age),
            appearanceProfileSha = source.Appearance?.ProfileSha ?? "",
            projectVisualStyleSha = (source.ProjectVisualStyleSha ?? "").Trim().ToLowerInvariant(),
            revisionReason = NormalizeReason(source.RevisionReason),
            notes = CanonicalNotes(source.RevisionNotes),
        };
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Canonical)));
    }

    public static CharacterMasterGenerationRequest BuildGenerationRequest(
        RevisionSource source, UnifiedVisualContract? contract = null)
    {
        var appearance = source.Appearance!;
        var age = source.Age!;
        var reason = NormalizeReason(source.RevisionReason);
        var compiled = contract;
        if (compiled is null)
        {
            var ingress = CharacterFirstMasterVisualIngressV1Rules.CompileRevision(
                CharacterFirstMasterVisualIngressV1Rules.DefaultSnapshot(), source);
            if (ingress.Gate is not null || ingress.Contract is null)
                throw new InvalidOperationException(ingress.Gate ?? CharacterFirstMasterVisualIngressV1Rules.GateCompile);
            compiled = ingress.Contract;
        }
        var text = compiled.CompiledPrompt;
        return new CharacterMasterGenerationRequest(
            CharacterStudioV1Rules.NormalizeCharacterId(source.CharacterId ?? ""),
            source.IdentityBrief ?? "",
            source.IdentitySha ?? "",
            source.ProjectVisualStyleSha,
            compiled.StyleLayer,
            age.ChronologicalAge,
            age.TargetAppearanceAgeMin,
            age.TargetAppearanceAgeMax,
            CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age, appearance.Gender),
            appearance,
            reason,
            source.RevisionNotes ?? "",
            source.CurrentMasterSha,
            !CurrentMasterIsAppearanceLock(reason),
            text,
            Fingerprint(source),
            ContractSha(source, text),
            compiled.VisualUniverseSha,
            compiled.CdlSha,
            FamixaVisualUniverseAuthorityV1Rules.StyleReferencePackSha(),
            compiled.CalibrationPackSha ?? FamixaVisualUniverseAuthorityV1Rules.StyleCalibrationPackSha(),
            FamixaVisualUniverseAuthorityV1Rules.DesignLanguageBlock(),
            FamixaVisualUniverseAuthorityV1Rules.NegativeConstraints,
            compiled.PvsSha,
            compiled.CompiledPromptSha);
    }

    public static string ComposePrompt(RevisionSource source, string? stylePrompt = null)
    {
        _ = FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix((stylePrompt ?? "").Trim());
        var ageBlock = source.Age is null
            ? ""
            : CharacterAgeConsistencyV1Rules.AgePromptBlock(source.Age, source.Appearance?.Gender);
        _ = ageBlock;
        _ = "Do not keep exact face";
        var ingress = CharacterFirstMasterVisualIngressV1Rules.CompileRevision(
            CharacterFirstMasterVisualIngressV1Rules.DefaultSnapshot(), source);
        if (ingress.Gate is not null || ingress.Contract is null)
            throw new InvalidOperationException(ingress.Gate ?? CharacterFirstMasterVisualIngressV1Rules.GateCompile);
        return ingress.Contract.CompiledPrompt;
    }

    public static string ContractSha(RevisionSource source, string? prompt = null)
    {
        var text = prompt ?? ComposePrompt(source);
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            document = DocumentId,
            fingerprint = Fingerprint(source),
            prompt = text,
        }, Canonical)));
    }

    public static CharacterAuthorityGenerationRequest ToProviderRequest(
        CharacterMasterGenerationRequest request, string eraId, string identityVersion) =>
        new(request.CharacterId, eraId, CharacterFirstMasterVisualIngressV1Rules.GenerationTypeRevision,
            request.CanonicalText, "3:4", identityVersion,
            request.VisualUniverseAuthoritySha, request.PvsSha, request.CharacterDesignLanguageSha,
            request.CompiledPromptSha);

    public static AuthorityState ApplyApprove(AuthorityState state) =>
        state with { Status = StatusApproved };

    public static AuthorityState ApplyReject(AuthorityState state) =>
        state with { Status = StatusRejected };

    public static AuthorityState ApplyLock(AuthorityState state)
    {
        if (state.Status != StatusApproved
            || string.IsNullOrWhiteSpace(state.CandidateMasterSha))
            return state;
        return state with
        {
            Status = StatusLocked,
            CurrentMasterSha = state.CandidateMasterSha,
            DnaStatus = StaleDna,
            PrpStatus = StalePrp,
            CrpStatus = StaleCrp,
        };
    }

    public static bool ReadyAllowed(
        bool masterLocked, bool dnaValid, bool prpValid, bool crpComplete, bool crpLocked,
        bool consistencyPass, string? revisionStatus)
    {
        if (IsOpenRevision(revisionStatus))
            return false;
        return masterLocked && dnaValid && prpValid && crpComplete && crpLocked && consistencyPass;
    }

    public static bool IsOpenRevision(string? status) =>
        status is StatusRequested or StatusGenerating or StatusPendingReview or StatusApproved;

    public static bool CrpStaleForCurrentMaster(
        string? crpMasterSha, string? authorityMasterSha, int coverage = 0)
    {
        if (!CharacterReferencePackRules.ShaExists(authorityMasterSha))
            return false;
        if (!CharacterReferencePackRules.ShaExists(crpMasterSha))
            return false;
        return !string.Equals(crpMasterSha, authorityMasterSha, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CandidateIsLiveAuthority(string? status) =>
        status == StatusLocked;

    public static string? LiveAuthoritySha(
        string? status, string? candidateSha, string? fallbackSha) =>
        CandidateIsLiveAuthority(status) && CharacterReferencePackRules.ShaExists(candidateSha)
            ? candidateSha
            : fallbackSha;

    public static string? LiveAuthorityPath(
        string? status, string? candidatePath, string? fallbackPath) =>
        CandidateIsLiveAuthority(status)
            ? string.IsNullOrWhiteSpace(candidatePath) ? null : candidatePath
            : fallbackPath;

    public static bool LiveAuthorityRequiresCandidateBytes(string? status) =>
        CandidateIsLiveAuthority(status);

    public static string? HistoricalAuthoritySha(
        string? status,
        string? candidateSha,
        string? currentSha,
        string? storeSha,
        string? initSha)
    {
        var live = LiveAuthoritySha(status, candidateSha, currentSha ?? storeSha ?? initSha);
        if (!CandidateIsLiveAuthority(status))
            return currentSha ?? storeSha ?? initSha;
        if (CharacterReferencePackRules.ShaExists(initSha)
            && !string.Equals(initSha, live, StringComparison.OrdinalIgnoreCase))
            return initSha;
        if (CharacterReferencePackRules.ShaExists(currentSha)
            && !string.Equals(currentSha, live, StringComparison.OrdinalIgnoreCase))
            return currentSha;
        if (CharacterReferencePackRules.ShaExists(storeSha)
            && !string.Equals(storeSha, live, StringComparison.OrdinalIgnoreCase))
            return storeSha;
        return initSha;
    }

    public static bool OverlayCrpStale(
        string? revisionStatus,
        string? revisionCrpStatus,
        string? crpMasterSha,
        string? liveMasterSha) =>
        CandidateIsLiveAuthority(revisionStatus)
        && string.Equals(revisionCrpStatus, StaleCrp, StringComparison.OrdinalIgnoreCase)
        && CrpStaleForCurrentMaster(crpMasterSha, liveMasterSha);

    public static bool MayRequest(bool officialLocked, bool masterReady) =>
        !officialLocked && masterReady;

    public static string OverlayStudioStatus(string studioStatus, string? revisionStatus) =>
        IsOpenRevision(revisionStatus) ? revisionStatus! : studioStatus;

    public static string NextVersion(string? current) =>
        CharacterStudioV1Rules.NextVersion(string.IsNullOrWhiteSpace(current) ? "V1" : current);

    public static bool PromptForbidsExactMasterFace(string? prompt, string? reason) =>
        !MayReviseAppearance(reason)
        || (!string.IsNullOrWhiteSpace(prompt)
            && prompt.Contains(ContinuityInstruction, StringComparison.Ordinal)
            && !prompt.Contains("Keep exact face from Master", StringComparison.OrdinalIgnoreCase)
            && !prompt.Contains("Preserve exact face, hairstyle, age", StringComparison.OrdinalIgnoreCase));

    public static bool UsesCharacterName() => false;
    public static bool UsesCharacterSpecificBranch() => false;
    public static bool CrpRegenerationRevisesMaster() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRetry() => false;
    public static bool AutoGenerateDownstream() => false;
    public static bool CallsGemini() => false;
    public static bool GeneratesVideo() => false;
    public static bool ChangesProjectVisualStyle() => false;
    public static bool OverwritesLockedMaster() => false;
    public static bool SilentMutate() => false;
}

public sealed record CharacterMasterGenerationRequest(
    string CharacterId,
    string IdentityBrief,
    string IdentitySha,
    string? ProjectVisualStyleSha,
    string? ProjectVisualStylePrompt,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string AgeAppearanceProfile,
    CharacterAppearanceProfile AppearanceProfile,
    string RevisionReason,
    string RevisionNotes,
    string? CurrentMasterSha,
    bool CurrentMasterIsContinuityOnly,
    string CanonicalText,
    string Fingerprint,
    string GenerationContractSha,
    string? VisualUniverseAuthoritySha = null,
    string? CharacterDesignLanguageSha = null,
    string? StyleReferencePackSha = null,
    string? StyleCalibrationPackSha = null,
    string? CharacterDesignLanguage = null,
    string? NegativeConstraints = null,
    string? PvsSha = null,
    string? CompiledPromptSha = null);

public sealed record CharacterMasterRevisionRequestDto(
    string? RevisionReason = null,
    string? RevisionNotes = null,
    string? Provider = "GEMINI",
    bool Confirm = false,
    string? EraId = null);

public sealed record CharacterMasterRevisionDto(
    string DocumentId,
    string? RevisionId,
    string CharacterId,
    string? CurrentMasterId,
    string? CurrentMasterSha,
    string? CandidateMasterId,
    string? CandidateMasterSha,
    string Status,
    string? RevisionReason,
    string? RevisionNotes,
    string? Provider,
    bool GenerationExecuted,
    bool ProviderCalled,
    bool GeminiCalled,
    string? ProviderRequestId,
    string? Fingerprint,
    string? GateCode,
    string StaffMessage,
    string? AppearanceProfileSha = null,
    string? AgeProfileSha = null,
    string? ProjectVisualStyleSha = null,
    string? IdentitySha = null,
    string? GenerationContractSha = null,
    string? CurrentMasterVersion = null,
    string? CandidateMasterVersion = null,
    CharacterStudioCharacterDto? Character = null,
    string? ReviewedBy = null,
    string? ReviewedAt = null,
    string? ReviewDecision = null,
    string? RejectionReason = null,
    string? ReviewNotes = null);
