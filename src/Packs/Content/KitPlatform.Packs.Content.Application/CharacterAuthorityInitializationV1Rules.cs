using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1 — Identity → Master → DNA → PRP.
/// Director review + lock at each stage. Does not create CRP, shots, production stills, or video.
/// </summary>
public static class CharacterAuthorityInitializationV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1_REGRESSION";
    public const string Version = "AUTHORITY_INITIALIZATION_V1";
    public const string ProtectedCharacterId = "CHAR-001";
    public const string HistoricalStillId = CharacterReferenceCompletionRules.HistoricalStillId;
    public const string ProtectedMasterSha = "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1";
    public const string ProtectedDnaSha = "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc";
    public const string ProtectedPrpSha = "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444";
    public const string ProtectedCrpSha = "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7";
    public const string ProtectedShotArtifactId = "acc2ed9f-a3d8-4197-88a3-1fb67f35a8ee";

    public const string StageMaster = "MASTER";
    public const string StageDna = "DNA";
    public const string StagePrp = "PRP";
    public const string ActionGet = "GET";
    public const string ActionPrepare = "PREPARE";
    public const string ActionExecute = "EXECUTE";
    public const string ActionApprove = "APPROVE";
    public const string ActionReject = "REJECT";
    public const string ActionLock = "LOCK";

    public const string Missing = "MISSING";
    public const string ReadyForDirector = "READY_FOR_DIRECTOR";
    public const string Approved = "APPROVED";
    public const string Locked = "LOCKED";
    public const string Rejected = "REJECTED";
    public const string Failed = "FAILED";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetry() => false;
    public static bool AutoFix() => false;
    public static bool AllowsRunway() => false;
    public static bool AllowsVeo() => false;
    public static bool GeneratesCrp() => false;
    public static bool GeneratesShot() => false;
    public static bool GeneratesProductionStill() => false;
    public static bool GeneratesVideo() => false;
    public static bool MutatesMinh() => false;
    public static bool OverwritesLocked() => false;

    public static string StaffCharacter => "Không tìm thấy nhân vật.";
    public static string StaffIdentity => "Hồ sơ nhân vật chưa sẵn sàng.";
    public static string StaffLocked => "Nhân vật này đã có bộ nhận diện chuẩn. Không tạo lại.";
    public static string StaffMinh => StaffLocked;
    public static string StaffConfirm => "Cần xác nhận trước khi tạo.";
    public static string StaffProvider => "Vui lòng chọn nhà cung cấp.";
    public static string StaffCapability => "Nhà cung cấp này chưa hỗ trợ tạo bộ nhận diện chuẩn.";
    public static string StaffDuplicate => "Bộ nhận diện này đã được tạo trước đó.";
    public static string StaffFail => "Không tạo được. Không tự gọi lại.";
    public static string StaffHistorical => "Không dùng ảnh production cũ làm bộ nhận diện.";
    public static string StaffMasterWait => "Cần khóa Master trước.";
    public static string StaffDnaWait => "Cần khóa DNA trước.";
    public static string StaffApproveWait => "Cần duyệt trước khi khóa.";
    public static string StaffLockWait => "Cần khóa bước trước.";
    public static string StaffMissing => "Chưa có";
    public static string StaffNeedMaster => "Cần tạo Master Reference";
    public static string StaffMasterReview => "Master đang chờ duyệt";
    public static string StaffMasterLocked => "Master đã khóa";
    public static string StaffNeedDna => "Cần tạo DNA";
    public static string StaffDnaReview => "DNA đang chờ duyệt";
    public static string StaffNeedPrp => "Cần tạo hồ sơ sản xuất";
    public static string StaffReady => "Nhân vật đã sẵn sàng tạo bộ ảnh tham chiếu";
    public static string StaffCreateMaster => "Tạo Master";
    public static string StaffCreateDna => "Tạo DNA";
    public static string StaffCreatePrp => "Tạo hồ sơ sản xuất";
    public static string StaffApproveMaster => "Duyệt Master";
    public static string StaffApproveDna => "Duyệt DNA";
    public static string StaffApprovePrp => "Duyệt hồ sơ sản xuất";
    public static string StaffReject => "Không đạt";
    public static string StaffLockMaster => "Khóa Master";
    public static string StaffLockDna => "Khóa DNA";
    public static string StaffLockPrp => "Khóa hồ sơ sản xuất";
    public static string StaffStart => "Bắt đầu tạo";
    public static string StaffCancel => "Hủy";
    public static string StaffDetails => "Chi tiết kỹ thuật";

    public sealed record GateInput(
        bool CharacterExists,
        bool IdentityReady,
        bool AuthorityLocked,
        string Stage,
        string Action,
        string MasterStatus,
        string DnaStatus,
        string PrpStatus,
        string? DirectorProvider,
        bool CapabilityReady,
        string? CapabilityCode,
        bool Confirm,
        bool DuplicateFingerprint,
        bool HistoricalStillRequested = false);

    public sealed record Gate(
        string Status,
        string? Code,
        string StaffMessage,
        bool IdentityReady,
        bool ProviderSelected,
        bool CapabilityReady,
        bool GenerationAllowed,
        bool MayCallProvider,
        bool AuthorityReady);

    public static bool IsAuthorityLocked(string? master, string? dna, string? prp) =>
        IsLocked(master) && IsLocked(dna) && IsLocked(prp);

    public static bool IdentityReady(string? name, string? role, string? visual, string? lifecycle)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(role))
            return false;
        if (string.Equals(visual, "mention", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.Equals(lifecycle, "archived", StringComparison.OrdinalIgnoreCase)
            || string.Equals(lifecycle, "rejected", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public static bool RejectHistoricalStill(string? assetId, string? path = null) =>
        string.Equals((assetId ?? "").Trim(), HistoricalStillId, StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(path)
            && path.Contains(HistoricalStillId, StringComparison.OrdinalIgnoreCase));

    public static bool IsLocked(string? status) =>
        string.Equals(status, Locked, StringComparison.OrdinalIgnoreCase);

    public static bool IsApproved(string? status) =>
        string.Equals(status, Approved, StringComparison.OrdinalIgnoreCase);

    public static bool IsReadyForDirector(string? status) =>
        string.Equals(status, ReadyForDirector, StringComparison.OrdinalIgnoreCase);

    public static bool AuthorityReady(string? master, string? dna, string? prp) =>
        IsLocked(master) && IsLocked(dna) && IsLocked(prp);

    public static bool CanOpenCrpGeneration(bool authorityReady) => authorityReady && !GeneratesCrp();

    public static bool CanOpenProduction(bool authorityReady) =>
        authorityReady && !GeneratesProductionStill() && !GeneratesShot() && false;

    public static string SetCapability(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return "MISSING";
        if (string.Equals(provider, "GEMINI", StringComparison.OrdinalIgnoreCase))
            return "SUPPORTED";
        return "UNSUPPORTED";
    }

    public static bool CapabilityAllows(string? provider) => SetCapability(provider) == "SUPPORTED";

    public static bool CallsImageProvider(string stage, string action) =>
        string.Equals(stage, StageMaster, StringComparison.OrdinalIgnoreCase)
        && string.Equals(action, ActionExecute, StringComparison.OrdinalIgnoreCase);

    public static object CanonicalFingerprintJson(
        string characterId, string eraId, string identityVersion, string generationType) => new
    {
        character_id = characterId.Trim().ToUpperInvariant(),
        era_id = string.IsNullOrWhiteSpace(eraId) ? "ERA-01" : eraId.Trim().ToUpperInvariant(),
        identity_version = (identityVersion ?? "").Trim(),
        generation_type = generationType.Trim().ToUpperInvariant(),
        authority_initialization_version = Version,
    };

    public static string ExecutionFingerprint(
        string characterId, string eraId, string identityVersion, string generationType) =>
        KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                CanonicalFingerprintJson(characterId, eraId, identityVersion, generationType), CanonicalOptions)));

    public static string DuplicatePolicy(string fingerprint, string? existing) =>
        !string.IsNullOrWhiteSpace(existing)
        && string.Equals(fingerprint, existing, StringComparison.OrdinalIgnoreCase)
            ? "BLOCK_DUPLICATE"
            : "NEW";

    public static string CanonicalMasterBrief(
        string characterId, string name, string role, string era, string? gender, string? age, string? biography)
    {
        var ageLine = string.IsNullOrWhiteSpace(age) ? "" : $"Age appearance: {age.Trim()}.";
        var genderLine = string.IsNullOrWhiteSpace(gender) ? "" : $"Gender: {gender.Trim()}.";
        var bio = string.IsNullOrWhiteSpace(biography) ? "" : biography.Trim();
        return string.Join(" ", new[]
        {
            "Single canonical visual representation of one character.",
            $"Character: {name.Trim()}.",
            $"CharacterId: {characterId.Trim().ToUpperInvariant()}.",
            $"Era: {(string.IsNullOrWhiteSpace(era) ? "ERA-01" : era.Trim())}.",
            $"Role: {role.Trim()}.",
            ageLine,
            genderLine,
            bio,
            CharacterAuthorityPipelineV1Rules.GlobalStyleBrief(),
            "One person only. No collage. No text overlay.",
            "Do not copy another character. Do not use a production still. Do not invent a second person.",
        }.Where(x => x.Length > 0));
    }

    public static object CompileDnaSpec(
        string characterId, string name, string role, string era, string masterSha, string identityVersion,
        string? gender, string? age,
        string? projectVisualStyleId = null, string? projectVisualStyleSha = null) => new
    {
        character_id = characterId.Trim().ToUpperInvariant(),
        name = name.Trim(),
        role = role.Trim(),
        era = string.IsNullOrWhiteSpace(era) ? "ERA-01" : era.Trim().ToUpperInvariant(),
        identity_version = identityVersion,
        master_sha256 = masterSha.Trim().ToLowerInvariant(),
        source = "CHARACTER_IDENTITY_PLUS_LOCKED_MASTER",
        project_visual_style_id = string.IsNullOrWhiteSpace(projectVisualStyleId) ? null : projectVisualStyleId.Trim(),
        project_visual_style_sha = string.IsNullOrWhiteSpace(projectVisualStyleSha) ? null : projectVisualStyleSha.Trim().ToLowerInvariant(),
        attributes = new
        {
            face_structure = "maintain locked Master face structure",
            hair = "maintain locked Master hair",
            eyes = "maintain locked Master eyes",
            skin = "maintain locked Master skin",
            body_proportion = "maintain locked Master body proportion",
            age_appearance = string.IsNullOrWhiteSpace(age) ? "maintain locked Master age appearance" : age.Trim(),
            silhouette = "maintain locked Master silhouette",
            clothing_identity = "maintain locked Master clothing identity",
            visual_style = "maintain locked Master visual style",
            recognition_features = "maintain locked Master recognition features",
            gender = gender ?? "",
        },
    };

    public static object CompilePrpSpec(
        string characterId, string name, string era, string masterSha, string dnaSha,
        string? projectVisualStyleId = null, string? projectVisualStyleSha = null,
        object? ageAppearanceProfile = null) => new
    {
        character_id = characterId.Trim().ToUpperInvariant(),
        name = name.Trim(),
        era = string.IsNullOrWhiteSpace(era) ? "ERA-01" : era.Trim().ToUpperInvariant(),
        master_sha256 = masterSha.Trim().ToLowerInvariant(),
        dna_sha256 = dnaSha.Trim().ToLowerInvariant(),
        project_visual_style_id = string.IsNullOrWhiteSpace(projectVisualStyleId) ? null : projectVisualStyleId.Trim(),
        project_visual_style_sha = string.IsNullOrWhiteSpace(projectVisualStyleSha) ? null : projectVisualStyleSha.Trim().ToLowerInvariant(),
        age_appearance_profile = ageAppearanceProfile,
        usage = new
        {
            purpose = "production identity lock",
            must_match_master = true,
            must_match_dna = true,
            forbid_historical_still = true,
            forbid_other_character = true,
            forbid_production_still_as_authority = true,
        },
    };

    public static string SpecSha(object spec) =>
        KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(spec, CanonicalOptions)));

    public static bool MinhShaUnchanged(string? master, string? dna, string? prp, string? crp) =>
        SameSha(master, ProtectedMasterSha)
        && SameSha(dna, ProtectedDnaSha)
        && SameSha(prp, ProtectedPrpSha)
        && (string.IsNullOrWhiteSpace(crp) || SameSha(crp, ProtectedCrpSha));

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static Gate Evaluate(GateInput input)
    {
        var authority = AuthorityReady(input.MasterStatus, input.DnaStatus, input.PrpStatus);
        if (input.HistoricalStillRequested)
            return Block("REJECTED", StaffHistorical, input, authority);
        if (!input.CharacterExists)
            return Block("CHARACTER_NOT_FOUND", StaffCharacter, input, authority);
        if (input.AuthorityLocked && Mutating(input.Action))
            return Block("AUTHORITY_CONFLICT", StaffLocked, input, authority);
        if (!input.IdentityReady)
            return Block("CHARACTER_IDENTITY_NOT_READY", StaffIdentity, input, authority);

        if (string.Equals(input.Stage, StageDna, StringComparison.OrdinalIgnoreCase)
            && !IsLocked(input.MasterStatus))
            return Block("MASTER_NOT_LOCKED", IsApproved(input.MasterStatus) ? StaffLockWait : StaffMasterWait, input, authority);
        if (string.Equals(input.Stage, StagePrp, StringComparison.OrdinalIgnoreCase)
            && (!IsLocked(input.MasterStatus) || !IsLocked(input.DnaStatus)))
            return Block(
                IsLocked(input.MasterStatus) ? "DNA_NOT_LOCKED" : "MASTER_NOT_LOCKED",
                IsLocked(input.MasterStatus) ? StaffDnaWait : StaffMasterWait,
                input, authority);

        if (string.Equals(input.Action, ActionApprove, StringComparison.OrdinalIgnoreCase))
        {
            var status = StageStatus(input);
            if (!IsReadyForDirector(status))
                return Block("REVIEW_REQUIRED", StaffApproveWait, input, authority);
            return Pass("READY", null, StaffMessage(input.Stage, status), input, authority, false);
        }

        if (string.Equals(input.Action, ActionLock, StringComparison.OrdinalIgnoreCase))
        {
            var status = StageStatus(input);
            if (!IsApproved(status))
                return Block("APPROVAL_REQUIRED", StaffApproveWait, input, authority);
            return Pass("READY", null, StaffMessage(input.Stage, status), input, authority, false);
        }

        if (string.Equals(input.Action, ActionReject, StringComparison.OrdinalIgnoreCase))
        {
            var status = StageStatus(input);
            if (!IsReadyForDirector(status) && !IsApproved(status))
                return Block("REVIEW_REQUIRED", StaffApproveWait, input, authority);
            return Pass("READY", null, StaffReject, input, authority, false);
        }

        if (!CallsImageProvider(input.Stage, input.Action)
            && !string.Equals(input.Action, ActionExecute, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(input.Action, ActionPrepare, StringComparison.OrdinalIgnoreCase))
            return Pass("READY", null, StaffMessage(input.Stage, StageStatus(input)), input, authority, false);

        if (CallsImageProvider(input.Stage, input.Action)
            || (string.Equals(input.Action, ActionPrepare, StringComparison.OrdinalIgnoreCase)
                && string.Equals(input.Stage, StageMaster, StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(input.DirectorProvider))
                return new Gate("NEEDS_PROVIDER_SELECTION", "PROVIDER_REQUIRED", StaffProvider,
                    true, false, input.CapabilityReady, false, false, authority);
            if (string.Equals(input.DirectorProvider, "RUNWAY", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input.DirectorProvider, "VEO", StringComparison.OrdinalIgnoreCase)
                || !CapabilityAllows(input.DirectorProvider)
                || !input.CapabilityReady)
                return Block(input.CapabilityCode ?? "PROVIDER_CAPABILITY_UNSUPPORTED", StaffCapability, input, authority,
                    providerSelected: true);
        }

        if (string.Equals(input.Action, ActionExecute, StringComparison.OrdinalIgnoreCase)
            && input.DuplicateFingerprint)
            return Block("BLOCK_DUPLICATE", StaffDuplicate, input, authority, providerSelected: true, capability: true);

        if (string.Equals(input.Action, ActionExecute, StringComparison.OrdinalIgnoreCase) && !input.Confirm)
            return new Gate("BLOCKED", "CONFIRMATION_REQUIRED", StaffConfirm,
                true, true, true, false, false, authority);

        var mayCall = CallsImageProvider(input.Stage, input.Action) && input.Confirm && !input.DuplicateFingerprint;
        return Pass("READY", null, StaffMessage(input.Stage, StageStatus(input)), input, authority, mayCall);
    }

    public static bool MayCallProvider(Gate gate) =>
        gate.MayCallProvider && gate.GenerationAllowed && !AutoSelectProvider() && !AutoApprove() && !AutoRetry();

    public static string StaffMessage(string stage, string status)
    {
        if (string.Equals(stage, StageMaster, StringComparison.OrdinalIgnoreCase))
        {
            if (IsLocked(status)) return StaffMasterLocked;
            if (IsReadyForDirector(status) || IsApproved(status)) return StaffMasterReview;
            return StaffNeedMaster;
        }
        if (string.Equals(stage, StageDna, StringComparison.OrdinalIgnoreCase))
        {
            if (IsLocked(status)) return "DNA đã khóa";
            if (IsReadyForDirector(status) || IsApproved(status)) return StaffDnaReview;
            return StaffNeedDna;
        }
        if (IsLocked(status)) return "Hồ sơ sản xuất đã khóa";
        if (IsReadyForDirector(status) || IsApproved(status)) return "Hồ sơ sản xuất đang chờ duyệt";
        return StaffNeedPrp;
    }

    private static string StageStatus(GateInput input) =>
        string.Equals(input.Stage, StageDna, StringComparison.OrdinalIgnoreCase) ? input.DnaStatus
        : string.Equals(input.Stage, StagePrp, StringComparison.OrdinalIgnoreCase) ? input.PrpStatus
        : input.MasterStatus;

    private static bool Mutating(string action) =>
        action is ActionExecute or ActionApprove or ActionReject or ActionLock;

    private static Gate Pass(
        string status, string? code, string staff, GateInput input, bool authority, bool mayCall) =>
        new(status, code, staff, true,
            !string.IsNullOrWhiteSpace(input.DirectorProvider),
            input.CapabilityReady || !CallsImageProvider(input.Stage, input.Action),
            string.Equals(input.Action, ActionExecute, StringComparison.OrdinalIgnoreCase),
            mayCall, authority);

    private static Gate Block(
        string code, string staff, GateInput input, bool authority,
        bool providerSelected = false, bool capability = false) =>
        new("BLOCKED", code, staff, input.IdentityReady, providerSelected, capability, false, false, authority);

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Application → ICharacterAuthorityGenerationProvider → mock. No HTTP. No Gemini SDK.</summary>
public static class CharacterAuthorityInitializationV1Application
{
    public sealed record Outcome(
        CharacterAuthorityInitializationV1Rules.Gate Gate,
        bool ProviderCalled,
        bool GeminiCalled,
        bool RunwayCalled,
        bool VeoCalled,
        int ProviderCalls,
        string? MasterStatus,
        string? DnaStatus,
        string? PrpStatus,
        bool AuthorityReady,
        bool Approved,
        bool Locked);

    public static async Task<Outcome> ExecuteMasterOnceAsync(
        CharacterAuthorityInitializationV1Rules.GateInput input,
        ICharacterAuthorityGenerationProvider provider,
        CharacterAuthorityGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = CharacterAuthorityInitializationV1Rules.Evaluate(input);
        if (!CharacterAuthorityInitializationV1Rules.MayCallProvider(gate))
            return Empty(gate, input, false);

        var result = await provider.GenerateMasterAsync(request, cancellationToken);
        var calls = provider is MockCharacterAuthorityGenerationProvider mock ? mock.CallCount : 1;
        var gemini = string.Equals(provider.ProviderId, "GEMINI", StringComparison.OrdinalIgnoreCase)
                     || provider is MockCharacterAuthorityGenerationProvider;
        if (!result.Succeeded || result.Bytes is not { Length: > 32 })
        {
            return new Outcome(
                gate with
                {
                    Status = "FAILED",
                    Code = "GENERATION_FAILED",
                    StaffMessage = CharacterAuthorityInitializationV1Rules.StaffFail,
                    GenerationAllowed = false,
                    MayCallProvider = false,
                },
                true, gemini, false, false, calls,
                CharacterAuthorityInitializationV1Rules.Failed,
                input.DnaStatus, input.PrpStatus, false, false, false);
        }

        return new Outcome(
            gate, true, gemini, false, false, calls,
            CharacterAuthorityInitializationV1Rules.ReadyForDirector,
            input.DnaStatus, input.PrpStatus, false, false, false);
    }

    public static Outcome CompileDna(CharacterAuthorityInitializationV1Rules.GateInput input)
    {
        var gate = CharacterAuthorityInitializationV1Rules.Evaluate(
            input with { Stage = CharacterAuthorityInitializationV1Rules.StageDna });
        if (gate.Status == "BLOCKED")
            return Empty(gate, input, false);
        return new Outcome(gate, false, false, false, false, 0,
            input.MasterStatus, CharacterAuthorityInitializationV1Rules.ReadyForDirector, input.PrpStatus,
            false, false, false);
    }

    public static Outcome CompilePrp(CharacterAuthorityInitializationV1Rules.GateInput input)
    {
        var gate = CharacterAuthorityInitializationV1Rules.Evaluate(
            input with { Stage = CharacterAuthorityInitializationV1Rules.StagePrp });
        if (gate.Status == "BLOCKED")
            return Empty(gate, input, false);
        return new Outcome(gate, false, false, false, false, 0,
            input.MasterStatus, input.DnaStatus, CharacterAuthorityInitializationV1Rules.ReadyForDirector,
            false, false, false);
    }

    private static Outcome Empty(
        CharacterAuthorityInitializationV1Rules.Gate gate,
        CharacterAuthorityInitializationV1Rules.GateInput input,
        bool called) =>
        new(gate, called, false, false, false, 0,
            input.MasterStatus, input.DnaStatus, input.PrpStatus, gate.AuthorityReady, false, false);
}

public sealed class MockCharacterAuthorityGenerationProvider : ICharacterAuthorityGenerationProvider
{
    public const string Id = "MOCK_CHARACTER_AUTHORITY";
    public string ProviderId => Id;
    public int CallCount { get; private set; }
    public bool Succeed { get; set; } = true;
    public byte[] Bytes { get; set; } = Enumerable.Repeat((byte)0x5A, 48).ToArray();

    public Task<CharacterAuthorityGenerationResult> GenerateMasterAsync(
        CharacterAuthorityGenerationRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (!Succeed)
        {
            return Task.FromResult(new CharacterAuthorityGenerationResult(
                true, false, null, null, null, "GENERATION_FAILED"));
        }

        return Task.FromResult(new CharacterAuthorityGenerationResult(
            true, true, Bytes, "image/jpeg", "mock", null));
    }
}
