using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1 — one Character Factory for every character.
/// Profile → Project Visual Style → Identity → Master → DNA → PRP → CRP 4-view set →
/// consistency → Director review. No character-specific branches. No auto-approve/lock.
/// </summary>
public static class CharacterStudioUnifiedGenerationV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1_REGRESSION";
    public const string Version = "CHARACTER_STUDIO_UNIFIED_GENERATION_V1";
    public const string PipelineVersion = "UNIFIED_CHARACTER_REFERENCE_SET_V1";
    public const string DefaultProject = "FAMIXA";
    public const string DefaultProvider = "GEMINI";

    public const int ReferenceSetVersion = 1;
    public const int ReferenceSetCount = 4;
    public static readonly string[] ReferenceSetViews = CharacterReferencePackRules.RequiredTypes;

    public const string ProfileReady = CharacterStudioV1Rules.ProfileReady;
    public const string AuthorityBuilding = "AUTHORITY_BUILDING";
    public const string ReadyToGenerate = "READY_TO_GENERATE";
    public const string Generating = "GENERATING";
    public const string ConsistencyCheck = "CONSISTENCY_CHECK";
    public const string CrpPendingReview = CharacterStudioV1Rules.CrpPendingReview;
    public const string CrpApproved = CharacterStudioV1Rules.CrpApproved;
    public const string CrpLocked = CharacterStudioV1Rules.CrpLocked;
    public const string CharacterReady = CharacterStudioV1Rules.CharacterReady;
    public const string GenerationFailed = "GENERATION_FAILED";
    public const string ConsistencyFailed = "CONSISTENCY_FAILED";
    public const string CrpRejected = "CRP_REJECTED";
    public const string RegenerationAvailable = "REGENERATION_AVAILABLE";

    public const string GateProfile = "CHARACTER_NOT_READY";
    public const string GateStyle = ProjectVisualStyleV1Rules.GateNotReady;
    public const string GateConfirm = "CONFIRMATION_REQUIRED";
    public const string GateDuplicate = "BLOCK_DUPLICATE";
    public const string GateProvider = "PROVIDER_UNAVAILABLE";
    public const string GateProduction = "CHARACTER_NOT_READY";

    public const string StaffCreate = "Tạo nhân vật";
    public const string StaffGenerate = "Tạo bộ ảnh chuẩn";
    public const string StaffPreparing = "Famixa đang chuẩn bị nhân vật...";
    public const string StaffGenerating = "Đang tạo bộ ảnh...";
    public const string StaffSetReady = "Đã tạo 4/4 ảnh";
    public const string StaffPending = "Đang chờ duyệt";
    public const string StaffPublicPending = "PENDING REVIEW";
    public const string StaffPublicReady = "CHARACTER READY";
    public const string StaffPublicLocked = "LOCKED";
    public const string StaffPublicApproved = "APPROVED";
    public const string StaffPublicGenerating = "GENERATING";
    public const string StaffApprove = "Duyệt bộ ảnh";
    public const string StaffReject = "Không đạt";
    public const string StaffRegenerate = "Tạo lại bộ ảnh";
    public const string StaffNeedStyle = "Project chưa thiết lập phong cách hình ảnh.";
    public const string StaffSetupStyle = "Thiết lập phong cách dự án";
    public const string StaffInherit = "Nhân vật sẽ được tạo theo phong cách hình ảnh đã thiết lập cho Project.";
    public const string StaffNeedProfile = "Cần tên, tuổi, giới tính và mô tả ngoại hình.";
    public const string StaffNeedConfirm = "Cần xác nhận trước khi tạo bộ ảnh chuẩn.";
    public const string StaffNotReady = "Nhân vật chưa sẵn sàng cho sản xuất.";

    public static readonly string[] ConsistencyChecks =
    [
        "identity", "face", "hair", "age", "gender", "proportion",
        "clothing", "visualStyle", "renderingStyle", "angle", "fullBody", "quality",
    ];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRetry() => false;
    public static bool AutoRegenerate() => false;
    public static bool RegeneratesMasterFromCrp() => false;
    public static bool CharacterMaySelectStyle() => false;
    public static bool UsesCharacterSpecificBranch() => false;
    public static bool ApplicationReferencesGeminiSdk() => false;

    public sealed record ReferenceSetDefinition(int Version, int Count, IReadOnlyList<string> Views)
    {
        public string Canonical() => JsonSerializer.Serialize(new
        {
            version = Version,
            count = Count,
            views = Views.Select(v => v.Trim().ToUpperInvariant()).ToArray(),
        }, CanonicalOptions);
    }

    public static ReferenceSetDefinition DefaultSet =>
        new(ReferenceSetVersion, ReferenceSetCount, ReferenceSetViews);

    public static bool SetDefinitionValid(ReferenceSetDefinition? set) =>
        set is not null
        && set.Version == ReferenceSetVersion
        && set.Count == ReferenceSetCount
        && set.Views.Count == ReferenceSetCount
        && ReferenceSetViews.All(t => set.Views.Contains(t, StringComparer.OrdinalIgnoreCase));

    public static bool ProfileReadyForFactory(
        string? name, int? age, string? gender, string? description) =>
        CharacterStudioV1Rules.ProfileValid(name, age, gender, null, description);

    public static object CanonicalIdentity(
        string characterId,
        string name,
        int age,
        string gender,
        string? role,
        string? personality,
        string appearance,
        string? projectVisualStyleId,
        string? projectVisualStyleSha) => new
    {
        character_id = CharacterStudioV1Rules.NormalizeCharacterId(characterId),
        name = name.Trim(),
        age,
        gender = gender.Trim().ToLowerInvariant(),
        role = (role ?? "").Trim(),
        personality = (personality ?? "").Trim(),
        appearance = appearance.Trim(),
        project_visual_style_id = (projectVisualStyleId ?? "").Trim(),
        project_visual_style_sha = (projectVisualStyleSha ?? "").Trim().ToLowerInvariant(),
        identity_version = Version,
    };

    public static string IdentitySha(
        string characterId,
        string name,
        int age,
        string gender,
        string? role,
        string? personality,
        string appearance,
        string? projectVisualStyleId,
        string? projectVisualStyleSha) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            CanonicalIdentity(characterId, name, age, gender, role, personality, appearance,
                projectVisualStyleId, projectVisualStyleSha),
            CanonicalOptions)));

    public static object CanonicalFingerprint(
        string projectId,
        string characterId,
        string? projectVisualStyleSha,
        string identitySha,
        string masterSha,
        string dnaSha,
        string prpSha,
        ReferenceSetDefinition? set = null,
        string? ageProfileSha = null)
    {
        var def = set ?? DefaultSet;
        var age = string.IsNullOrWhiteSpace(ageProfileSha) ? null : ageProfileSha.Trim().ToLowerInvariant();
        return new
        {
            project_id = (projectId ?? DefaultProject).Trim().ToUpperInvariant(),
            character_id = CharacterStudioV1Rules.NormalizeCharacterId(characterId),
            project_visual_style_sha = (projectVisualStyleSha ?? "").Trim().ToLowerInvariant(),
            identity_sha256 = (identitySha ?? "").Trim().ToLowerInvariant(),
            master_sha256 = (masterSha ?? "").Trim().ToLowerInvariant(),
            dna_sha256 = (dnaSha ?? "").Trim().ToLowerInvariant(),
            prp_sha256 = (prpSha ?? "").Trim().ToLowerInvariant(),
            reference_set = new
            {
                version = def.Version,
                count = def.Count,
                views = def.Views.Select(v => v.Trim().ToUpperInvariant()).ToArray(),
            },
            pipeline_version = PipelineVersion,
            age_profile_sha256 = age,
        };
    }

    public static string Fingerprint(
        string projectId,
        string characterId,
        string? projectVisualStyleSha,
        string identitySha,
        string masterSha,
        string dnaSha,
        string prpSha,
        ReferenceSetDefinition? set = null,
        string? ageProfileSha = null) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            CanonicalFingerprint(projectId, characterId, projectVisualStyleSha, identitySha,
                masterSha, dnaSha, prpSha, set, ageProfileSha),
            CanonicalOptions)));

    public static bool FingerprintOmitsRuntime(string canonicalJson) =>
        !canonicalJson.Contains("\"provider\"", StringComparison.OrdinalIgnoreCase)
        && !canonicalJson.Contains("timestamp", StringComparison.OrdinalIgnoreCase)
        && !canonicalJson.Contains("gemini", StringComparison.OrdinalIgnoreCase)
        && !canonicalJson.Contains("DateTime", StringComparison.OrdinalIgnoreCase);

    public static string DuplicatePolicy(string fingerprint, string? existing, bool previousFailed) =>
        previousFailed ? "ALLOW_RETRY"
        : !string.IsNullOrWhiteSpace(existing)
          && string.Equals(fingerprint, existing, StringComparison.OrdinalIgnoreCase)
            ? GateDuplicate
            : "NEW";

    public static bool RegenerationKeepsAuthority(
        string? beforeIdentity, string? afterIdentity,
        string? beforeMaster, string? afterMaster,
        string? beforeDna, string? afterDna,
        string? beforePrp, string? afterPrp,
        string? beforeStyle, string? afterStyle) =>
        CharacterAuthorityInitializationV1Rules.SameSha(beforeIdentity, afterIdentity)
        && CharacterAuthorityInitializationV1Rules.SameSha(beforeMaster, afterMaster)
        && CharacterAuthorityInitializationV1Rules.SameSha(beforeDna, afterDna)
        && CharacterAuthorityInitializationV1Rules.SameSha(beforePrp, afterPrp)
        && CharacterAuthorityInitializationV1Rules.SameSha(beforeStyle, afterStyle);

    public static bool ProductionAllowed(
        string? studioState, bool crpLocked, bool canUse, bool projectStyleReady) =>
        string.Equals(studioState, CharacterReady, StringComparison.OrdinalIgnoreCase)
        && crpLocked
        && canUse
        && projectStyleReady;

    public static string ProductionGate(
        string? studioState, bool crpLocked, bool canUse, bool projectStyleReady) =>
        ProductionAllowed(studioState, crpLocked, canUse, projectStyleReady)
            ? "VALID"
            : GateProduction;

    public static string MapFactoryState(string studioState) =>
        (studioState ?? "").Trim().ToUpperInvariant() switch
        {
            CharacterStudioV1Rules.MasterGenerating
                or CharacterStudioV1Rules.MasterReady
                or CharacterStudioV1Rules.DnaReady
                or CharacterStudioV1Rules.PrpReady => AuthorityBuilding,
            CharacterStudioV1Rules.ReferenceGenerating
                or "GENERATING" or "REGENERATING" => Generating,
            CharacterStudioV1Rules.ReferenceChecking
                or CharacterStudioV1Rules.ReferenceRepairing => ConsistencyCheck,
            CharacterStudioV1Rules.Failed => GenerationFailed,
            CharacterStudioV1Rules.Rejected or CrpRejected => RegenerationAvailable,
            var s => s,
        };

    public static string NextAction(string studioState, bool projectStyleReady)
    {
        if (!projectStyleReady
            && studioState is ProfileReady or CharacterStudioV1Rules.Draft or CharacterStudioV1Rules.Failed)
            return StaffSetupStyle;
        return (studioState ?? "").Trim().ToUpperInvariant() switch
        {
            CharacterStudioV1Rules.CrpPendingReview => StaffApprove,
            CharacterStudioV1Rules.Rejected or CrpRejected => StaffRegenerate,
            CharacterStudioV1Rules.Failed or GenerationFailed => StaffRegenerate,
            CharacterStudioV1Rules.CrpApproved => CharacterStudioV1Rules.StaffLock,
            CharacterReady or CharacterStudioV1Rules.CrpLocked => CharacterStudioV1Rules.StaffReady,
            _ => StaffGenerate,
        };
    }

    public static string PublicPhase(string? studioState, bool crpLocked = false)
    {
        var s = (studioState ?? "").Trim().ToUpperInvariant();
        if (s is CharacterReady) return CharacterReady;
        if (s is CharacterStudioV1Rules.CrpLocked || crpLocked && s is CrpApproved) return CrpLocked;
        if (s is CrpApproved) return CrpApproved;
        if (s is CharacterStudioV1Rules.CrpPendingReview) return "PENDING_REVIEW";
        if (s is CharacterStudioV1Rules.Rejected or CrpRejected) return CrpRejected;
        if (s is CharacterStudioV1Rules.Failed or GenerationFailed) return GenerationFailed;
        if (s.Contains("GENERAT", StringComparison.OrdinalIgnoreCase)
            || s is AuthorityBuilding or ReadyToGenerate or ConsistencyCheck
            || s is CharacterStudioV1Rules.MasterGenerating
            || s is CharacterStudioV1Rules.ReferenceChecking
            || s is CharacterStudioV1Rules.ReferenceRepairing)
            return Generating;
        return s.Length == 0 ? CharacterStudioV1Rules.Draft : s;
    }

    public static string PublicHeadline(string? studioState, int coverage, int required, bool locked)
    {
        var cov = $"{Math.Max(0, coverage)}/{Math.Max(1, required)}";
        return PublicPhase(studioState, locked) switch
        {
            CharacterReady or CharacterStudioV1Rules.CrpLocked => $"{StaffPublicReady} · {cov} · {StaffPublicLocked}",
            CrpApproved => $"{StaffPublicApproved} · {cov}",
            "PENDING_REVIEW" => $"{StaffPublicPending} · {cov}",
            Generating => $"{StaffPublicGenerating} · {cov}",
            CrpRejected => $"{StaffReject} · {cov}",
            GenerationFailed => $"{CharacterStudioV1Rules.StaffFail} · {cov}",
            _ => $"{PublicPhase(studioState, locked).Replace('_', ' ')} · {cov}",
        };
    }

    public static string ReviewStepLabel(string? studioState, bool locked)
    {
        return PublicPhase(studioState, locked) switch
        {
            CharacterReady or CharacterStudioV1Rules.CrpLocked => $"{StaffPublicReady} · {StaffPublicLocked}",
            CrpApproved => StaffPublicApproved,
            "PENDING_REVIEW" => StaffPending,
            CrpRejected => StaffReject,
            _ => StaffPending,
        };
    }

    public static bool ShowsPendingReview(string? studioState) =>
        PublicPhase(studioState) == "PENDING_REVIEW";

    public static string ProgressLabel(string studioState) =>
        (studioState ?? "").Trim().ToUpperInvariant() switch
        {
            CharacterStudioV1Rules.MasterGenerating
                or CharacterStudioV1Rules.MasterReady
                or CharacterStudioV1Rules.DnaReady
                or CharacterStudioV1Rules.PrpReady
                or AuthorityBuilding => StaffPreparing,
            CharacterStudioV1Rules.ReferenceGenerating or Generating => StaffGenerating,
            CharacterStudioV1Rules.CrpPendingReview => StaffPending,
            CharacterStudioV1Rules.Rejected or CrpRejected => StaffReject,
            CharacterReady or CharacterStudioV1Rules.CrpLocked => CharacterStudioV1Rules.StaffReady,
            _ => StaffGenerate,
        };

    public sealed record ConsistencyResult(
        bool Pass,
        IReadOnlyDictionary<string, bool> Checks,
        IReadOnlyList<string> Failed);

    public static ConsistencyResult EvaluateConsistency(IReadOnlyList<CharacterStudioV1Rules.SlotScore> slots)
    {
        var byType = slots.ToDictionary(s => s.Type, StringComparer.OrdinalIgnoreCase);
        bool View(string type) =>
            byType.TryGetValue(type, out var s) && s.Verdict == "PASS";
        bool Dim(Func<CharacterStudioV1Rules.SlotScore, int> read) =>
            CharacterStudioV1Rules.RequiredViews.All(t =>
                byType.TryGetValue(t, out var s) && s.Verdict == "PASS" && read(s) >= CharacterStudioV1Rules.ConsistencyThreshold);

        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["identity"] = Dim(s => s.Face),
            ["face"] = Dim(s => s.Face),
            ["hair"] = Dim(s => s.Hair),
            ["age"] = Dim(s => s.Age),
            ["gender"] = Dim(s => s.Gender),
            ["proportion"] = Dim(s => s.Proportion),
            ["clothing"] = Dim(s => s.Clothing),
            ["visualStyle"] = Dim(s => s.Style),
            ["renderingStyle"] = Dim(s => s.Style),
            ["angle"] = CharacterStudioV1Rules.RequiredViews.All(View),
            ["fullBody"] = View("FULL_BODY"),
            ["quality"] = Dim(s => s.Structure),
        };
        var failed = checks.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
        return new ConsistencyResult(failed.Count == 0 && CharacterStudioV1Rules.ConsistencyPass(slots), checks, failed);
    }

    public static string AfterConsistency(bool pass) =>
        pass ? CrpPendingReview : CrpRejected;

    public static bool ProviderIsGemini(string? provider) =>
        string.Equals(provider, DefaultProvider, StringComparison.OrdinalIgnoreCase);

    public static int LogicalGenerationCount(int providerViewCalls) =>
        providerViewCalls > 0 ? 1 : 0;

    public static readonly string[] DnaStableTraits =
    [
        "face", "eyes", "nose", "mouth", "ears", "hair", "skin",
        "body_proportions", "age_representation", "distinctive_features",
        "clothing_baseline", "visual_rendering",
    ];

    public static bool DnaDescribesStableTraits(object spec)
    {
        var json = JsonSerializer.Serialize(spec, CanonicalOptions);
        return new[] { "face", "hair", "eyes", "skin", "body", "age" }
            .All(k => json.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public static bool PrpIsProviderNeutral(object spec)
    {
        var json = JsonSerializer.Serialize(spec, CanonicalOptions);
        return !json.Contains("gemini", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("models/gemini", StringComparison.OrdinalIgnoreCase);
    }

    public static bool InheritsProjectStyle(string? characterStyleKey, string? projectStyleKey) =>
        string.IsNullOrWhiteSpace(characterStyleKey)
        || string.Equals(
            ProjectVisualStyleV1Rules.NormalizeKey(characterStyleKey),
            ProjectVisualStyleV1Rules.NormalizeKey(projectStyleKey),
            StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
