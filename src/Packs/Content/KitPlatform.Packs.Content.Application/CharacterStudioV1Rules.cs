using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_STUDIO_V1 — one Character Engine for every CHAR-xxx.
/// Orchestrates Profile → Identity → Master → DNA → PRP → chained reference set →
/// consistency → slot repair → Director review → lock → Authority.
/// Does not auto-approve CRP, auto-lock CRP, open production, or mutate locked authority.
/// </summary>
public static class CharacterStudioV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_STUDIO_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_STUDIO_V1_REGRESSION";
    public const string Version = "CHARACTER_STUDIO_V1";
    public const string PipelineVersion = "CHARACTER_GENERATION_PIPELINE_V1";
    public const string ProjectFamixa = "FAMIXA";
    public const string DefaultEra = "ERA-01";
    public const string HistoricalStillId = CharacterReferenceCompletionRules.HistoricalStillId;

    public const string Draft = "DRAFT";
    public const string ProfileReady = "PROFILE_READY";
    public const string MasterGenerating = "MASTER_GENERATING";
    public const string MasterReady = "MASTER_READY";
    public const string DnaReady = "DNA_READY";
    public const string PrpReady = "PRP_READY";
    public const string ReferenceGenerating = "REFERENCE_GENERATING";
    public const string ReferenceChecking = "REFERENCE_CHECKING";
    public const string ReferenceRepairing = "REFERENCE_REPAIRING";
    public const string CrpPendingReview = "CRP_PENDING_REVIEW";
    public const string CrpApproved = "CRP_APPROVED";
    public const string CrpLocked = "CRP_LOCKED";
    public const string CharacterReady = "CHARACTER_READY";
    public const string Failed = "FAILED";
    public const string Rejected = "REJECTED";

    public static readonly string[] RequiredViews = CharacterReferencePackRules.RequiredTypes;
    public static readonly string[] ChainOrder = ["FRONT", "FULL_BODY", "THREE_QUARTER", "SIDE"];

    public const int ConsistencyThreshold = 90;
    public const int MaxSlotRepairAttempts = 2;
    public const int MaxProviderCallsPerJob = 8;

    public static readonly string[] RejectReasonCodes =
    [
        "FACE_MISMATCH", "HAIR_MISMATCH", "BODY_PROPORTION", "AGE_MISMATCH",
        "CLOTHING_MISMATCH", "VIEW_INCONSISTENT", "FULL_BODY_INVALID", "QUALITY", "OTHER",
    ];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetryProviderFailure() => false;
    public static bool OpensFirstRealProduction() => false;
    public static bool GeneratesShot() => false;
    public static bool GeneratesVideo() => false;
    public static bool OverwritesLockedAuthority() => false;
    public static bool AllowsPerSlotDirectorGenerate() => false;
    public static bool UsesHistoricalStill() => false;

    public static string StaffCreate => "Tạo nhân vật";
    public static string StaffStart => CharacterStudioUnifiedGenerationV1Rules.StaffGenerate;
    public static string StaffApprove => "Duyệt bộ ảnh";
    public static string StaffReject => "Không đạt";
    public static string StaffRegenerate => "Tạo lại bộ ảnh";
    public static string StaffLock => "Khóa nhân vật";
    public static string StaffDetails => "Chi tiết kỹ thuật";
    public static string StaffPending => "Đang chờ duyệt";
    public static string StaffReady => "Nhân vật đã sẵn sàng";
    public static string StaffProgress => "Đang tạo nhân vật...";
    public static string StaffNeedReason => "Cần nhập lý do không đạt.";
    public static string StaffNeedProfile => "Cần tên, tuổi, giới tính và mô tả ngắn.";
    public static string StaffNeedStyle => "Project chưa thiết lập phong cách hình ảnh.";
    public static string StaffProjectStyle => "Nhân vật này sẽ sử dụng phong cách hình ảnh của Project.";
    public static string StaffLocked => "Nhân vật đã khóa. Không tạo lại.";
    public static string StaffMasterLocked => "Master đã khóa. Không tạo lại trong Create Character.";
    public static string StaffConfirm => "Cần xác nhận trước khi tạo.";
    public static string StaffProvider => "Vui lòng chọn nhà cung cấp.";
    public static string StaffCapability => "Nhà cung cấp này chưa hỗ trợ.";
    public static string StaffDuplicate => "Bộ nhận diện này đã được tạo trước đó.";
    public static string StaffHistorical => "Không dùng ảnh production cũ làm bộ nhận diện.";
    public static string StaffCharacter => "Không tìm thấy nhân vật.";
    public static string StaffFail => "Không tạo được nhân vật. Không tự gọi lại.";
    public static string StaffConsistency => "Bộ ảnh chưa đồng nhất. Đang điều chỉnh góc lỗi.";
    public static string StaffNeedReview => "Cần duyệt bộ ảnh trước khi khóa.";

    public sealed record StylePreset(
        string StyleId,
        string Label,
        string Visual,
        string Lighting,
        string Material,
        string Camera,
        string Negative);

    public static readonly IReadOnlyList<StylePreset> StylePresets =
    [
        new("STYLE_3D_STYLIZED_REALISM", "3D Stylized Realism",
            "stylized cinematic 3D human, family-friendly, not photoreal, not cartoon squash",
            "soft cinematic key + gentle fill, no harsh beauty lighting",
            "subtle subsurface skin, clean fabric, no plastic shine",
            "neutral portrait / full-body reference camera, eye level",
            "photoreal photograph, celebrity likeness, text overlay, collage"),
        new("STYLE_2D_STORYBOOK", "2D Storybook",
            "2D painted storybook illustration, readable face, consistent line",
            "even storybook light, no noir contrast",
            "painted paper texture, no photoreal pores",
            "neutral illustration camera",
            "photoreal, 3D render, anime cel, text"),
        new("STYLE_ANIME", "Anime",
            "clean anime character sheet, consistent face and hair",
            "even anime lighting",
            "cel shading, no photoreal skin",
            "neutral character-sheet camera",
            "photoreal, western cartoon, text"),
        new("STYLE_CARTOON", "Cartoon",
            "readable cartoon character, simple volumes, consistent silhouette",
            "flat even light",
            "graphic shapes, no photoreal",
            "neutral character-sheet camera",
            "photoreal, gritty realism, text"),
        new("STYLE_PHOTOREALISTIC", "Photorealistic",
            "photoreal human identity plate, natural skin, consistent likeness",
            "neutral studio daylight",
            "real skin and fabric, no stylized paint",
            "neutral identity camera",
            "cartoon, anime, collage, text"),
        new("STYLE_CINEMATIC_REALISM", "Cinematic Realism",
            "cinematic realistic human, film still identity, not a snapshot collage",
            "cinematic motivated light, restrained contrast",
            "natural materials, filmic color",
            "neutral cinematic portrait camera",
            "cartoon, anime, text overlay, multi-panel"),
    ];

    public static StylePreset? StyleOf(string? styleId) =>
        StylePresets.FirstOrDefault(s =>
            string.Equals(s.StyleId, (styleId ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool StyleValid(string? styleId) => StyleOf(styleId) is not null;

    public static bool ProfileValid(string? name, int? age, string? gender, string? styleId, string? description) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Trim().Length >= 1
        && age is >= 1 and <= 120
        && !string.IsNullOrWhiteSpace(gender)
        && !string.IsNullOrWhiteSpace(description)
        && description.Trim().Length >= 3;

    public static bool RejectReasonValid(string? code, string? text)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (!RejectReasonCodes.Contains(c)) return false;
        return (text ?? "").Trim().Length >= 3 || c != "OTHER";
    }

    public static string NormalizeCharacterId(string? raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        if (t.StartsWith("CHAR-", StringComparison.Ordinal) && t.Length >= 8)
            return t;
        return t;
    }

    public static bool RejectHistoricalStill(string? assetId, string? path = null) =>
        CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(assetId, path);

    public static bool MasterCropIsNotFullBody(string? masterSha, string? fullBodySha) =>
        !CharacterAuthorityInitializationV1Rules.SameSha(masterSha, fullBodySha);

    public sealed record StudioSnapshot(
        bool CharacterExists,
        bool ProfileReady,
        bool WorkspaceReady,
        bool OfficialLocked,
        string MasterStatus,
        string DnaStatus,
        string PrpStatus,
        string? CrpStatus,
        int Coverage,
        bool CrpApproved,
        bool CrpLocked,
        bool CrpRejected,
        bool ConsistencyPass,
        string? GateCode = null);

    public static string ResolveState(StudioSnapshot snap)
    {
        if (!snap.CharacterExists) return Draft;
        if (snap.OfficialLocked && snap.CrpLocked && snap.Coverage >= 4) return CharacterReady;
        if (snap.CrpLocked && snap.Coverage >= 4
            && CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus)
            && CharacterAuthorityInitializationV1Rules.IsLocked(snap.DnaStatus)
            && CharacterAuthorityInitializationV1Rules.IsLocked(snap.PrpStatus))
            return CharacterReady;
        if (snap.CrpApproved) return CrpApproved;
        if (snap.CrpRejected) return Rejected;
        if (IsPendingCrp(snap.CrpStatus) && snap.Coverage >= 4) return CrpPendingReview;
        if (string.Equals(snap.CrpStatus, ReferenceRepairing, StringComparison.OrdinalIgnoreCase))
            return ReferenceRepairing;
        if (string.Equals(snap.CrpStatus, ReferenceChecking, StringComparison.OrdinalIgnoreCase))
            return ReferenceChecking;
        if (string.Equals(snap.CrpStatus, ReferenceGenerating, StringComparison.OrdinalIgnoreCase)
            || string.Equals(snap.CrpStatus, "GENERATING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(snap.CrpStatus, "REGENERATING", StringComparison.OrdinalIgnoreCase))
            return ReferenceGenerating;
        if (CharacterAuthorityInitializationV1Rules.IsLocked(snap.PrpStatus)
            || CharacterAuthorityInitializationV1Rules.IsReadyForDirector(snap.PrpStatus))
            return PrpReady;
        if (CharacterAuthorityInitializationV1Rules.IsLocked(snap.DnaStatus)
            || CharacterAuthorityInitializationV1Rules.IsReadyForDirector(snap.DnaStatus))
            return DnaReady;
        if (CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus)
            || CharacterAuthorityInitializationV1Rules.IsReadyForDirector(snap.MasterStatus)
            || CharacterAuthorityInitializationV1Rules.IsApproved(snap.MasterStatus))
            return MasterReady;
        if (string.Equals(snap.MasterStatus, MasterGenerating, StringComparison.OrdinalIgnoreCase))
            return MasterGenerating;
        if (snap.ProfileReady) return ProfileReady;
        return Draft;
    }

    public static bool IsPendingCrp(string? status) =>
        status is not null && (
            string.Equals(status, CrpPendingReview, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CharacterReferenceRegenerationV1Rules.PendingReview, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "CRP_PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));

    public static bool IsRejected(string? status) =>
        status is not null && (
            string.Equals(status, Rejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "CRP_REJECTED", StringComparison.OrdinalIgnoreCase));

    public static bool IsFailed(string? status) =>
        status is not null && (
            string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "GENERATION_FAILED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "CONSISTENCY_FAILED", StringComparison.OrdinalIgnoreCase));

    public static bool MayRegenerate(
        string? status,
        bool officialLocked = false,
        bool crpRejected = false,
        bool ageFailed = false,
        bool crpStale = false) =>
        !officialLocked
        && (crpRejected
            || ageFailed
            || crpStale
            || IsRejected(status)
            || IsFailed(status)
            || IsPendingCrp(status));

    public static bool IsLocked(string? status) =>
        status is not null && (
            string.Equals(status, CrpLocked, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CharacterReady, StringComparison.OrdinalIgnoreCase));

    public static bool TransitionAllowed(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(from, Failed, StringComparison.OrdinalIgnoreCase)
            && to is ProfileReady or MasterGenerating or Rejected)
            return true;
        return (from, to) switch
        {
            (Draft, ProfileReady) => true,
            (ProfileReady, MasterGenerating) => true,
            (MasterGenerating, MasterReady) => true,
            (MasterGenerating, Failed) => true,
            (MasterReady, DnaReady) => true,
            (DnaReady, PrpReady) => true,
            (PrpReady, ReferenceGenerating) => true,
            (ReferenceGenerating, ReferenceChecking) => true,
            (ReferenceGenerating, Failed) => true,
            (ReferenceChecking, ReferenceRepairing) => true,
            (ReferenceChecking, CrpPendingReview) => true,
            (ReferenceRepairing, ReferenceChecking) => true,
            (ReferenceRepairing, Failed) => true,
            (CrpPendingReview, CrpApproved) => true,
            (CrpPendingReview, Rejected) => true,
            (CrpPendingReview, ReferenceGenerating) => true,
            (Rejected, ReferenceGenerating) => true,
            (Failed, ReferenceGenerating) => true,
            (Rejected, ReferenceRepairing) => true,
            (CrpApproved, CrpLocked) => true,
            (CrpLocked, CharacterReady) => true,
            _ => false,
        };
    }

    public static bool CanJumpToReady(string from) =>
        string.Equals(from, CrpLocked, StringComparison.OrdinalIgnoreCase)
        || string.Equals(from, CharacterReady, StringComparison.OrdinalIgnoreCase);

    public sealed record GateInput(
        bool CharacterExists,
        bool ProfileReady,
        bool OfficialLocked,
        bool AuthorityLocked,
        string Action,
        string StudioState,
        string? Provider,
        bool CapabilityReady,
        bool Confirm,
        bool DuplicateFingerprint,
        bool HistoricalStillRequested,
        bool CrpComplete,
        bool CrpRejected,
        IReadOnlyList<string>? RepairSlots = null,
        bool ProjectVisualStyleReady = true,
        string? ProjectVisualStyleSha = null,
        bool AgeFailed = false,
        bool CrpStale = false);

    public sealed record Gate(
        string Status,
        string? Code,
        string StaffMessage,
        bool MayCreate,
        bool MayGenerate,
        bool MayRepairSlot,
        bool MayCallProvider,
        bool MayApprove,
        bool MayReject,
        bool MayLock);

    public static Gate Evaluate(GateInput input)
    {
        if (input.HistoricalStillRequested)
            return Block("HISTORICAL_STILL_REJECTED", StaffHistorical, input);
        if (!input.CharacterExists && input.Action is not ("CREATE" or "LIST" or "GET"))
            return Block("CHARACTER_NOT_FOUND", StaffCharacter, input);
        if (input.OfficialLocked && input.Action is "GENERATE" or "REGENERATE" or "REPAIR")
            return Block(input.Action == "GENERATE" ? "MASTER_ALREADY_LOCKED" : "AUTHORITY_LOCKED",
                input.Action == "GENERATE" ? StaffMasterLocked : StaffLocked, input);

        if (input.Action == "CREATE")
            return input.ProfileReady
                ? Pass(ProfileReady, null, StaffCreate, input, mayCreate: true)
                : Block("PROFILE_INVALID", StaffNeedProfile, input);

        if (input.Action == "APPROVE")
        {
            if (!IsPendingCrp(input.StudioState) && input.StudioState != CrpPendingReview)
                return Block("REVIEW_REQUIRED", StaffNeedReview, input);
            return Pass(CrpPendingReview, null, StaffApprove, input, mayApprove: true);
        }

        if (input.Action == "LOCK")
        {
            if (input.StudioState is not (CrpApproved or CrpLocked or CharacterReady))
                return Block("APPROVAL_REQUIRED", StaffNeedReview, input);
            return Pass(input.StudioState, null, StaffLock, input, mayLock: true);
        }

        if (input.Action == "REJECT")
        {
            if (!IsPendingCrp(input.StudioState) && input.StudioState != CrpPendingReview)
                return Block("REVIEW_REQUIRED", StaffNeedReview, input);
            return Pass(CrpPendingReview, null, StaffReject, input, mayReject: true);
        }

        if (input.Action is "GENERATE" or "REGENERATE" or "REPAIR")
        {
            if (!input.ProjectVisualStyleReady || !ProjectVisualStyleV1Rules.LookLikeSha(input.ProjectVisualStyleSha))
                return Block(ProjectVisualStyleV1Rules.GateNotReady, StaffNeedStyle, input);
            if (input.Action == "GENERATE" && input.CrpComplete && !input.CrpRejected && !input.CrpStale)
                return Block("BLOCK_DUPLICATE", StaffDuplicate, input);
            if (input.Action == "REGENERATE"
                && !MayRegenerate(
                    input.StudioState, input.OfficialLocked, input.CrpRejected, input.AgeFailed, input.CrpStale))
                return Block("CRP_NOT_REJECTED", CharacterReferenceRegenerationV1Rules.StaffNotRejected, input);
            if (string.IsNullOrWhiteSpace(input.Provider))
                return new Gate("NEEDS_PROVIDER_SELECTION", "PROVIDER_REQUIRED", StaffProvider,
                    false, false, false, false, false, false, false);
            if (!CapabilityAllows(input.Provider) || !input.CapabilityReady)
                return Block("PROVIDER_UNAVAILABLE", StaffCapability, input);
            if (input.DuplicateFingerprint && !input.CrpStale)
                return Block("BLOCK_DUPLICATE", StaffDuplicate, input);
            if (!input.Confirm)
                return new Gate("BLOCKED", "CONFIRMATION_REQUIRED", StaffConfirm,
                    false, false, false, false, false, false, false);
            var repair = input.Action == "REPAIR" && (input.RepairSlots?.Count ?? 0) > 0;
            return Pass(input.StudioState, null, StaffProgress, input,
                mayGenerate: input.Action != "REPAIR",
                mayRepair: repair,
                mayCall: true);
        }

        return Pass(input.StudioState, null, StaffMessageFor(input.StudioState), input,
            mayApprove: IsPendingCrp(input.StudioState),
            mayReject: IsPendingCrp(input.StudioState),
            mayLock: input.StudioState is CrpApproved);
    }

    public static bool CapabilityAllows(string? provider) =>
        string.Equals(provider, "GEMINI", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ChainDependencies(string view) =>
        CharacterStudioIdentityLockV1Rules.ViewRefs(view);

    public static bool ViewsAreIndependent(IReadOnlyList<IReadOnlyList<string>> refLabelsPerView) =>
        refLabelsPerView.Count >= 4
        && refLabelsPerView.Skip(1).All(labels =>
            labels.Count == refLabelsPerView[0].Count
            && labels.All(l => refLabelsPerView[0].Contains(l, StringComparer.OrdinalIgnoreCase)));

    public sealed record SlotScore(
        string Type,
        int Face,
        int Hair,
        int Age,
        int Gender,
        int Skin,
        int Structure,
        int Proportion,
        int Clothing,
        int Style,
        int CrossView,
        string Verdict)
    {
        public int Overall =>
            (Face + Hair + Age + Gender + Skin + Structure + Proportion + Clothing + Style + CrossView) / 10;
    }

    public static SlotScore ScoreSlot(
        string type,
        bool artifactPresent,
        bool historical,
        bool sameAsMasterFullBody,
        IReadOnlyDictionary<string, int>? scores = null)
    {
        if (!artifactPresent || historical || (type == "FULL_BODY" && sameAsMasterFullBody))
            return new SlotScore(type, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, CharacterStudioIdentityLockV1Rules.VerdictFail);
        if (scores is null)
            return CharacterStudioIdentityLockV1Rules.UnevaluatedSlot(type);
        int Read(string key) => scores.TryGetValue(key, out var v) ? v : 0;
        var slot = new SlotScore(
            type,
            Read("face"),
            Read("hair"),
            Read("age"),
            Read("gender"),
            Read("skin"),
            Read("structure"),
            Read("proportion"),
            Read("clothing"),
            Read("style"),
            Read("crossView"),
            CharacterStudioIdentityLockV1Rules.VerdictPass);
        return slot with
        {
            Verdict = slot.Overall >= ConsistencyThreshold
                ? CharacterStudioIdentityLockV1Rules.VerdictPass
                : CharacterStudioIdentityLockV1Rules.VerdictFail,
        };
    }

    public static bool ConsistencyPass(IReadOnlyList<SlotScore> slots) =>
        RequiredViews.All(t => slots.Any(s =>
            string.Equals(s.Type, t, StringComparison.OrdinalIgnoreCase) && s.Verdict == "PASS"));

    public static IReadOnlyList<string> FailedSlots(IReadOnlyList<SlotScore> slots) =>
        slots.Where(s => CharacterStudioIdentityLockV1Rules.IsFail(s.Verdict)).Select(s => s.Type).ToList();

    public static string FailureClass(bool providerFailed, bool contentFailed) =>
        providerFailed ? "PROVIDER_EXECUTION_FAILURE"
        : contentFailed ? "CONTENT_INCONSISTENCY"
        : "NONE";

    public static bool MayRepairSlot(string failureClass, int attempt) =>
        failureClass == "CONTENT_INCONSISTENCY"
        && attempt < MaxSlotRepairAttempts
        && !AutoRetryProviderFailure();

    public static IReadOnlyList<string> RegenerationSlots(string? reasonCode, bool regenerateAll)
    {
        if (regenerateAll) return RequiredViews.ToList();
        var code = (reasonCode ?? "").Trim().ToUpperInvariant();
        return code switch
        {
            "FACE_MISMATCH" => RequiredViews.ToList(),
            "FULL_BODY_INVALID" => ["FULL_BODY"],
            "VIEW_INCONSISTENT" => ["THREE_QUARTER", "SIDE"],
            "BODY_PROPORTION" => ["FULL_BODY", "SIDE"],
            "HAIR_MISMATCH" or "AGE_MISMATCH" or "CLOTHING_MISMATCH" => RequiredViews.ToList(),
            _ => RequiredViews.ToList(),
        };
    }

    public sealed record ReferenceSelectRequest(
        string? Framing,
        string? Camera,
        string? Action,
        string? Pose,
        string? Visibility);

    public sealed record ReferenceSelection(
        IReadOnlyList<string> Types,
        string Reason);

    public static ReferenceSelection SelectReferences(ReferenceSelectRequest request)
    {
        var framing = (request.Framing ?? request.Camera ?? request.Pose ?? "").Trim().ToUpperInvariant();
        var action = (request.Action ?? request.Visibility ?? "").Trim().ToUpperInvariant();
        var text = $"{framing} {action}";
        if (text.Contains("SIDE") || text.Contains("PROFILE") || text.Contains("LOOK_LEFT") || text.Contains("LOOK_RIGHT")
            || text.Contains("QUAY") || text.Contains("NGHIÊNG"))
            return new(["SIDE", "THREE_QUARTER", "MASTER"], "SIDE_ACTION");
        if (text.Contains("FULL") || text.Contains("WIDE") || text.Contains("LONG") || text.Contains("TOÀN THÂN")
            || text.Contains("RUN") || text.Contains("CHẠY"))
            return new(["FULL_BODY", "MASTER"], "FULL_BODY");
        if (text.Contains("MEDIUM") || text.Contains("WAIST") || text.Contains("3/4") || text.Contains("THREE"))
            return new(["FRONT", "THREE_QUARTER"], "MEDIUM");
        return new(["MASTER", "FRONT"], "CLOSE_UP");
    }

    public static object CanonicalFingerprintJson(
        string projectId,
        string characterId,
        string profileAuthority,
        string masterSha,
        string dnaSha,
        string prpSha,
        string styleId,
        string contract,
        string? projectVisualStyleSha = null) => new
    {
        project_id = (projectId ?? ProjectFamixa).Trim().ToUpperInvariant(),
        character_id = NormalizeCharacterId(characterId),
        profile_authority = (profileAuthority ?? "").Trim().ToLowerInvariant(),
        master_sha256 = (masterSha ?? "").Trim().ToLowerInvariant(),
        dna_sha256 = (dnaSha ?? "").Trim().ToLowerInvariant(),
        prp_sha256 = (prpSha ?? "").Trim().ToLowerInvariant(),
        style_id = (styleId ?? "").Trim().ToUpperInvariant(),
        project_visual_style_sha = (projectVisualStyleSha ?? "").Trim().ToLowerInvariant(),
        reference_generation_contract = (contract ?? "").Trim().ToUpperInvariant(),
        pipeline_version = PipelineVersion,
    };

    public static string ExecutionFingerprint(
        string projectId,
        string characterId,
        string profileAuthority,
        string masterSha,
        string dnaSha,
        string prpSha,
        string styleId,
        string contract,
        string? projectVisualStyleSha = null) =>
        KitVideoIntegrityRules.Sha256Hex(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                CanonicalFingerprintJson(projectId, characterId, profileAuthority, masterSha, dnaSha, prpSha, styleId, contract, projectVisualStyleSha),
                CanonicalOptions)));

    public static string ProfileAuthority(
        string name, int age, string gender, string styleId, string description) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            name = name.Trim(),
            age,
            gender = gender.Trim().ToLowerInvariant(),
            style_id = styleId.Trim().ToUpperInvariant(),
            description = description.Trim(),
        }, CanonicalOptions)));

    public static string IdentityBrief(
        string characterId, string name, int age, string gender, string styleId, string description,
        string? role = null, string? personality = null)
    {
        var style = StyleOf(styleId);
        var projectStyle = ProjectVisualStyleV1Rules.PresetOf(styleId);
        var roleLine = string.IsNullOrWhiteSpace(role) ? "" : $"Role: {role.Trim()}.";
        var personalityLine = string.IsNullOrWhiteSpace(personality) ? "" : $"Personality: {personality.Trim()}.";
        var ageTarget = CharacterAgeConsistencyV1Rules.FromCanonicalAge(age);
        var ageLine = CharacterAgeConsistencyV1Rules.AgeExpressionBrief(ageTarget, gender);
        return string.Join(" ", new[]
        {
            "Single canonical visual identity of one character.",
            $"Character: {name.Trim()}.",
            $"CharacterId: {NormalizeCharacterId(characterId)}.",
            $"Age appearance: {age}.",
            ageLine,
            $"Gender: {gender.Trim()}.",
            roleLine,
            personalityLine,
            description.Trim(),
            projectStyle is not null ? ProjectVisualStyleV1Rules.BuildPrompt(projectStyle)
                : style is null ? "" : $"Style: {style.Visual}. Lighting: {style.Lighting}. Materials: {style.Material}.",
            "Neutral portrait. One person only. No collage. No text overlay.",
            "This image becomes the canonical visual identity.",
            "Do not copy another character. Do not use a production still.",
            "Use the Project Visual Style Authority. Do not invent a private character style.",
        }.Where(x => x.Length > 0));
    }

    public static string ViewBrief(string view, string identityBrief, IReadOnlyList<string> refLabels)
    {
        var type = (view ?? "").Trim().ToUpperInvariant();
        return string.Join(" ", new[]
        {
            CharacterStudioIdentityLockV1Rules.StudioLock,
            $"Using the exact same character from {string.Join(" and ", refLabels)}.",
            $"Generate {CharacterStudioIdentityLockV1Rules.ViewCamera(type)}.",
            "Preserve exact face, hairstyle, age, skin tone, body proportions, and visual style from the attached MASTER and FRONT references.",
            "Do not redesign the character.",
            identityBrief,
        });
    }

    public static string NextVersion(string? current)
    {
        var t = (current ?? "V1").Trim().ToUpperInvariant();
        if (t.Length < 2 || t[0] != 'V' || !int.TryParse(t[1..], out var n))
            return "V2";
        return $"V{n + 1}";
    }

    public static string MapLegacyReject(string? code) => (code ?? "").Trim().ToUpperInvariant() switch
    {
        "FACE" => "FACE_MISMATCH",
        "HAIR" => "HAIR_MISMATCH",
        "PROPORTION" => "BODY_PROPORTION",
        "AGE" => "AGE_MISMATCH",
        "WARDROBE" => "CLOTHING_MISMATCH",
        "INCONSISTENT" => "VIEW_INCONSISTENT",
        "FULL_BODY" => "FULL_BODY_INVALID",
        "QUALITY" => "QUALITY",
        "OTHER" => "OTHER",
        var x when RejectReasonCodes.Contains(x) => x,
        _ => "OTHER",
    };

    public static string StaffMessageFor(string state) => state switch
    {
        CharacterReady or CrpLocked => StaffReady,
        CrpPendingReview => StaffPending,
        Rejected => StaffReject,
        Failed => StaffFail,
        ReferenceRepairing => StaffConsistency,
        MasterGenerating or ReferenceGenerating or ReferenceChecking => StaffProgress,
        _ => StaffCreate,
    };

    public static string LibraryStatus(string studioState) => studioState switch
    {
        CharacterReady or CrpLocked => CharacterReady,
        CrpApproved => CrpApproved,
        CrpPendingReview => CrpPendingReview,
        Rejected => Rejected,
        Failed => Failed,
        var s when s.Contains("GENERAT", StringComparison.OrdinalIgnoreCase) => s,
        _ => studioState,
    };

    public static bool ProtectedMinhUnchanged(string? master, string? dna, string? prp, string? crp) =>
        CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(master, dna, prp, crp);

    public static bool SameInputsSameState(StudioSnapshot a, StudioSnapshot b) =>
        ResolveState(a) == ResolveState(b);

    private static Gate Pass(
        string status, string? code, string staff, GateInput input,
        bool mayCreate = false, bool mayGenerate = false, bool mayRepair = false,
        bool mayCall = false, bool mayApprove = false, bool mayReject = false, bool mayLock = false) =>
        new(status, code, staff, mayCreate, mayGenerate, mayRepair, mayCall, mayApprove, mayReject, mayLock);

    private static Gate Block(string code, string staff, GateInput input) =>
        new("BLOCKED", code, staff, false, false, false, false, false, false, false);

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public interface ICharacterGenerationProvider
{
    string ProviderId { get; }
    Task<CharacterAuthorityGenerationResult> GenerateMasterAsync(
        CharacterAuthorityGenerationRequest request, CancellationToken cancellationToken);
    Task<CharacterReferenceSetResult> GenerateViewsAsync(
        CharacterReferenceSetRequest request, CancellationToken cancellationToken);
}

public interface ICharacterReferenceSelector
{
    CharacterStudioV1Rules.ReferenceSelection Select(CharacterStudioV1Rules.ReferenceSelectRequest request);
}

public sealed class CharacterReferenceSelector : ICharacterReferenceSelector
{
    public CharacterStudioV1Rules.ReferenceSelection Select(CharacterStudioV1Rules.ReferenceSelectRequest request) =>
        CharacterStudioV1Rules.SelectReferences(request);
}

public sealed class MockCharacterGenerationProvider : ICharacterGenerationProvider
{
    public string ProviderId { get; init; } = "GEMINI";
    public int MasterCalls { get; private set; }
    public int ViewCalls { get; private set; }
    public bool FailMaster { get; init; }
    public bool FailViews { get; init; }
    public IReadOnlyList<string>? FailTypes { get; init; }

    public Task<CharacterAuthorityGenerationResult> GenerateMasterAsync(
        CharacterAuthorityGenerationRequest request, CancellationToken cancellationToken)
    {
        MasterCalls++;
        if (FailMaster)
            return Task.FromResult(new CharacterAuthorityGenerationResult(true, false, null, null, "mock", "MASTER_GENERATION_FAILED"));
        var bytes = Encoding.UTF8.GetBytes($"MASTER:{request.CharacterId}:{request.IdentityVersion}");
        return Task.FromResult(new CharacterAuthorityGenerationResult(true, true, bytes, "image/png", "mock-master", null));
    }

    public Task<CharacterReferenceSetResult> GenerateViewsAsync(
        CharacterReferenceSetRequest request, CancellationToken cancellationToken)
    {
        ViewCalls++;
        if (FailViews)
            return Task.FromResult(new CharacterReferenceSetResult(true, false, [], "REFERENCE_GENERATION_FAILED", "mock"));
        var views = request.Views.Select(v =>
        {
            var fail = FailTypes?.Contains(v.ReferenceType, StringComparer.OrdinalIgnoreCase) == true;
            var bytes = fail ? null : Encoding.UTF8.GetBytes($"VIEW:{request.CharacterId}:{v.ReferenceType}");
            return new CharacterReferenceGeneratedView(v.ReferenceType, !fail, bytes, fail ? null : "image/png", "mock-view");
        }).ToList();
        return Task.FromResult(new CharacterReferenceSetResult(true, views.All(x => x.Succeeded), views,
            views.All(x => x.Succeeded) ? null : "REFERENCE_SET_INCOMPLETE", "mock-set"));
    }
}
