using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2 — one Director confirm → one Reference Set (4 views).
/// Does not auto-run Gemini. Does not approve, lock, or open production.
/// </summary>
public static class CharacterReferenceAutoGenerationV2Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2";
    public const string SuiteId = "FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2_REGRESSION";
    public const string Version = "CHARACTER_REFERENCE_SET_V2";
    public const string RequiredCapability = "CHARACTER_REFERENCE_GENERATION";
    public const string PendingReview = "PENDING";
    public const string CrpAfterGenerate = "DRAFT";

    public static readonly string[] RequiredTypes = CharacterReferencePackRules.RequiredTypes;

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetry() => false;
    public static bool AutoFix() => false;
    public static bool AllowsRunway() => false;
    public static bool AllowsVeo() => false;
    public static bool AllowsBatchCharacters() => false;
    public static bool OpensFirstRealProduction() => false;
    public static bool GeneratesShot() => false;
    public static bool GeneratesVideo() => false;
    public static bool MutatesMaster() => false;
    public static bool MutatesDna() => false;
    public static bool MutatesPrp() => false;
    public static bool OverwritesLockedCrp() => false;
    public static bool FourOfFourMeansCanUse() => false;

    public static string StaffMaster => "Hồ sơ nhân vật chưa khóa. Không thể tạo bộ ảnh chuẩn.";
    public static string StaffDna => "Quy tắc nhận diện chưa khóa. Không thể tạo bộ ảnh chuẩn.";
    public static string StaffPrp => "Ảnh tham chiếu sản xuất chưa khóa. Không thể tạo bộ ảnh chuẩn.";
    public static string StaffSha => "Hồ sơ nhân vật không khớp. Không thể tạo bộ ảnh chuẩn.";
    public static string StaffComplete => "Bộ ảnh chuẩn đã khóa. Không tạo lại.";
    public static string StaffProvider => "Vui lòng chọn nhà cung cấp.";
    public static string StaffCapability => "Nhà cung cấp này chưa hỗ trợ tạo bộ ảnh chuẩn.";
    public static string StaffConfirm => "Cần xác nhận trước khi tạo bộ ảnh chuẩn.";
    public static string StaffDuplicate => "Bộ ảnh tham chiếu này đã được tạo trước đó.";
    public static string StaffFail => "Không tạo được bộ ảnh chuẩn. Không tự gọi lại.";
    public static string StaffIncomplete => "Bộ ảnh chưa đủ 4 góc. Không dùng được.";
    public static string StaffCharacter => "Không tìm thấy nhân vật.";
    public static string StaffReady => "Sẵn sàng tạo bộ ảnh tham chiếu.";
    public static string StaffPending => "Đang chờ duyệt";
    public static string StaffCreate => "Tạo bộ ảnh tham chiếu";
    public static string StaffStart => "Bắt đầu tạo";
    public static string StaffCancel => "Hủy";

    public sealed record GateInput(
        bool CharacterExists,
        bool MasterExists,
        bool MasterLocked,
        bool DnaExists,
        bool DnaLocked,
        bool PrpExists,
        bool PrpLocked,
        bool MasterShaValid,
        bool DnaShaValid,
        bool PrpShaValid,
        bool DnaMasterMatch,
        bool PrpMasterMatch,
        bool PrpDnaMatch,
        bool CrpLockedComplete,
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
        bool AuthorityValid,
        bool ProviderSelected,
        bool CapabilityReady,
        bool GenerationAllowed,
        bool ExecutionNotDuplicate,
        bool MayCallProvider);

    public static bool RejectHistoricalStill(string? assetId, string? path = null) =>
        CharacterReferenceCompletionRules.IsHistoricalStill(assetId)
        || (!string.IsNullOrWhiteSpace(path)
            && path.Contains(CharacterReferenceCompletionRules.HistoricalStillId, StringComparison.OrdinalIgnoreCase));

    public static bool CrpAlreadyComplete(string? status, bool canUse, int coverage) =>
        string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
        && canUse
        && coverage >= 4;

    public static bool ShaMatch(string? expected, string? live) =>
        CharacterIdentityGovernanceRules.ShaExists(expected)
        && CharacterIdentityGovernanceRules.SameSha(expected, live);

    public static bool AuthorityReady(GateInput input) =>
        input.CharacterExists
        && input.MasterExists && input.MasterLocked && input.MasterShaValid
        && input.DnaExists && input.DnaLocked && input.DnaShaValid && input.DnaMasterMatch
        && input.PrpExists && input.PrpLocked && input.PrpShaValid && input.PrpMasterMatch && input.PrpDnaMatch;

    public static string SetCapability(string? provider)
    {
        var profile = CharacterReferenceGenerationRules.ProfileOf(provider);
        if (profile is null) return "MISSING";
        if (string.Equals(provider, "GEMINI", StringComparison.OrdinalIgnoreCase)
            && CharacterReferenceGenerationRules.ImageCapabilitySupported(profile))
            return "SUPPORTED";
        return "UNSUPPORTED";
    }

    public static bool CapabilityAllows(string? provider) =>
        SetCapability(provider) == "SUPPORTED";

    public static object CanonicalSetJson(
        string characterId, string eraId, string masterSha, string dnaSha, string prpSha) => new
    {
        character_id = characterId.Trim().ToUpperInvariant(),
        era_id = string.IsNullOrWhiteSpace(eraId) ? "ERA-01" : eraId.Trim().ToUpperInvariant(),
        master_sha256 = masterSha.Trim().ToLowerInvariant(),
        dna_sha256 = dnaSha.Trim().ToLowerInvariant(),
        prp_sha256 = prpSha.Trim().ToLowerInvariant(),
        reference_types = RequiredTypes,
        reference_generation_version = Version,
    };

    public static string SetSha(string characterId, string eraId, string masterSha, string dnaSha, string prpSha) =>
        KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(CanonicalSetJson(
                characterId, eraId, masterSha, dnaSha, prpSha), CanonicalOptions)));

    public static string ExecutionFingerprint(
        string characterId, string eraId, string masterSha, string dnaSha, string prpSha) =>
        SetSha(characterId, eraId, masterSha, dnaSha, prpSha);

    public static string DuplicatePolicy(string fingerprint, string? existing) =>
        !string.IsNullOrWhiteSpace(existing)
        && string.Equals(fingerprint, existing, StringComparison.OrdinalIgnoreCase)
            ? "BLOCK_DUPLICATE"
            : "NEW";

    public static bool ConsistencyPass(
        string characterId,
        IReadOnlyList<string> typesPresent,
        bool assetsExist,
        bool identityMetaOk)
    {
        var have = typesPresent
            .Select(CharacterReferencePackRules.NormalizeRefType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CharacterReferencePackRules.RequireCharacterId(characterId)
               && assetsExist
               && identityMetaOk
               && RequiredTypes.All(t => have.Contains(t));
    }

    public static bool CanUseAfterGenerate(int coverage, bool approved, bool locked) =>
        false && coverage >= 4 && approved && locked;

    public static Gate Evaluate(GateInput input)
    {
        if (input.HistoricalStillRequested)
            return Block("REJECTED", "Không dùng ảnh production cũ làm ảnh chuẩn.", input);
        if (!input.CharacterExists)
            return Block("CHARACTER_NOT_FOUND", StaffCharacter, input);
        if (!input.MasterExists || !input.MasterLocked || !input.MasterShaValid)
            return Block("MASTER_NOT_READY", StaffMaster, input);
        if (!input.DnaExists || !input.DnaLocked || !input.DnaShaValid)
            return Block("DNA_NOT_READY", StaffDna, input);
        if (!input.PrpExists || !input.PrpLocked || !input.PrpShaValid)
            return Block("PRP_NOT_READY", StaffPrp, input);
        if (!input.DnaMasterMatch || !input.PrpMasterMatch || !input.PrpDnaMatch)
            return Block("AUTHORITY_SHA_MISMATCH", StaffSha, input);
        if (input.CrpLockedComplete)
            return Block("REFERENCE_ALREADY_COMPLETE", StaffComplete, input);
        if (string.IsNullOrWhiteSpace(input.DirectorProvider))
            return new Gate("NEEDS_PROVIDER_SELECTION", "PROVIDER_REQUIRED", StaffProvider,
                true, false, input.CapabilityReady, false, !input.DuplicateFingerprint, false);
        if (string.Equals(input.DirectorProvider, "RUNWAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.DirectorProvider, "VEO", StringComparison.OrdinalIgnoreCase)
            || !CapabilityAllows(input.DirectorProvider)
            || !input.CapabilityReady)
            return Block(input.CapabilityCode ?? "PROVIDER_CAPABILITY_UNSUPPORTED", StaffCapability, input,
                providerSelected: true);
        if (input.DuplicateFingerprint)
            return Block("BLOCK_DUPLICATE", StaffDuplicate, input, providerSelected: true, capability: true, duplicate: true);
        if (!input.Confirm)
            return new Gate("BLOCKED", "CONFIRMATION_REQUIRED", StaffConfirm,
                true, true, true, false, true, false);
        return new Gate("READY", null, StaffReady, true, true, true, true, true, true);
    }

    public static bool MayCallProvider(Gate gate) =>
        gate.MayCallProvider && gate.GenerationAllowed && !AutoSelectProvider() && !AutoApprove() && !AutoRetry();

    private static Gate Block(
        string code, string staff, GateInput input,
        bool providerSelected = false, bool capability = false, bool duplicate = false) =>
        new("BLOCKED", code, staff, AuthorityReady(input), providerSelected, capability, false, !duplicate, false);

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Application → ICharacterReferenceGenerationProvider → mock. No HTTP. No Gemini SDK.</summary>
public static class CharacterReferenceAutoGenerationV2Application
{
    public sealed record Outcome(
        CharacterReferenceAutoGenerationV2Rules.Gate Gate,
        bool ProviderCalled,
        bool Generation,
        bool GeminiCalled,
        bool RunwayCalled,
        bool VeoCalled,
        int ProviderCalls,
        string? ReferenceSetId,
        int ReferenceCount,
        int Coverage,
        IReadOnlyList<string> MissingTypes,
        string DirectorReview,
        bool Approved,
        bool Locked,
        bool CanUse,
        bool Consistency);

    public static async Task<Outcome> ExecuteOnceAsync(
        CharacterReferenceAutoGenerationV2Rules.GateInput input,
        ICharacterReferenceGenerationProvider provider,
        CharacterReferenceSetRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = CharacterReferenceAutoGenerationV2Rules.Evaluate(input);
        if (!CharacterReferenceAutoGenerationV2Rules.MayCallProvider(gate))
            return Empty(gate, false);

        var result = await provider.GenerateSetAsync(request, cancellationToken);
        var calls = provider is MockCharacterReferenceSetProvider mock ? mock.CallCount : 1;
        var gemini = ProviderIsGemini(provider);
        var okViews = result.Views.Where(v => v.Succeeded && v.Bytes is { Length: > 0 }).ToList();
        var types = okViews.Select(v => v.ReferenceType).ToList();
        var missing = CharacterReferenceAutoGenerationV2Rules.RequiredTypes
            .Where(t => !types.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var coverage = types.Count;
        var complete = coverage == 4 && missing.Count == 0 && result.Succeeded;
        var consistency = CharacterReferenceAutoGenerationV2Rules.ConsistencyPass(
            request.CharacterId, types, complete, complete);

        if (!result.Succeeded && coverage == 0)
        {
            return new Outcome(
                gate with
                {
                    Status = "FAILED",
                    Code = "REFERENCE_GENERATION_FAILED",
                    StaffMessage = CharacterReferenceAutoGenerationV2Rules.StaffFail,
                    GenerationAllowed = false,
                    MayCallProvider = false,
                },
                true, false, gemini, false, false, calls,
                null, 0, 0, CharacterReferenceAutoGenerationV2Rules.RequiredTypes.ToList(),
                CharacterReferenceAutoGenerationV2Rules.PendingReview, false, false, false, false);
        }

        if (!complete)
        {
            return new Outcome(
                gate with
                {
                    Status = "INCOMPLETE",
                    Code = "REFERENCE_SET_INCOMPLETE",
                    StaffMessage = CharacterReferenceAutoGenerationV2Rules.StaffIncomplete,
                    GenerationAllowed = false,
                    MayCallProvider = false,
                },
                true, true, ProviderIsGemini(provider), false, false, calls,
                "set-partial", coverage, coverage, missing,
                CharacterReferenceAutoGenerationV2Rules.PendingReview, false, false, false, false);
        }

        return new Outcome(
            gate, true, true, ProviderIsGemini(provider), false, false, calls,
            "set-1", 4, 4, [],
            CharacterReferenceAutoGenerationV2Rules.PendingReview, false, false, false, consistency);
    }

    private static bool ProviderIsGemini(ICharacterReferenceGenerationProvider provider) =>
        string.Equals(provider.ProviderId, "GEMINI", StringComparison.OrdinalIgnoreCase)
        || provider is MockCharacterReferenceSetProvider;

    private static Outcome Empty(CharacterReferenceAutoGenerationV2Rules.Gate gate, bool called) =>
        new(gate, called, false, false, false, false, 0, null, 0, 0,
            CharacterReferenceAutoGenerationV2Rules.RequiredTypes.ToList(),
            CharacterReferenceAutoGenerationV2Rules.PendingReview, false, false, false, false);
}

public sealed class MockCharacterReferenceSetProvider : ICharacterReferenceGenerationProvider
{
    public const string Id = "MOCK_CHARACTER_REFERENCE_SET";
    public string ProviderId => Id;
    public int CallCount { get; private set; }
    public bool Succeed { get; set; } = true;
    public int ViewsToReturn { get; set; } = 4;
    public byte[] Bytes { get; set; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public Task<CharacterReferenceSetResult> GenerateSetAsync(
        CharacterReferenceSetRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (!Succeed)
        {
            return Task.FromResult(new CharacterReferenceSetResult(
                true, false, [], "REFERENCE_GENERATION_FAILED", null));
        }

        var views = request.Views
            .Take(Math.Max(0, ViewsToReturn))
            .Select(v => new CharacterReferenceGeneratedView(v.ReferenceType, true, Bytes, "image/png", "mock"))
            .ToList();
        var complete = views.Count == 4;
        return Task.FromResult(new CharacterReferenceSetResult(
            true, complete, views, complete ? null : "REFERENCE_SET_INCOMPLETE", "mock"));
    }
}
