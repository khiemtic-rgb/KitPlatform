using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_REFERENCE_GENERATION_V1 — Character Canon → reference candidate.
/// Provider is not authority. Generated image is not Canon until Director accept + register.
/// </summary>
public static class CharacterReferenceGenerationRules
{
    public const string DocumentId = "FAMIXA_CHARACTER_REFERENCE_GENERATION_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_REFERENCE_GENERATION_V1_REGRESSION";
    public const string AllowedProvider = "GEMINI";
    public const string AllowedGeneration = "IMAGE_GENERATION";
    public const string RequiredCapability = "CHARACTER_REFERENCE";
    public const string GenerationConfig = "CHARACTER_REFERENCE_V1";
    public const string CandidateReady = "READY_FOR_DIRECTOR";
    public const string CandidateGenerated = "GENERATED";

    public static readonly string[] SupportedTypes = ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoRetry() => false;
    public static bool AllowsBatch() => false;
    public static bool AllowsRunway() => false;
    public static bool AllowsVeo() => false;
    public static bool OpensFirstRealProduction() => false;
    public static bool GeneratesShot() => false;
    public static bool MutatesMaster() => false;
    public static bool MutatesDna() => false;
    public static bool MutatesPrp() => false;
    public static bool AcceptLocks() => false;
    public static bool AcceptApproves() => false;
    public static bool RegisterApproves() => false;
    public static bool ValidateApproves() => false;
    public static bool CoverageApproves(int ready, int total) => false;
    public static bool ProviderIsAuthority() => false;

    public static string StaffMasterBlock => "Bộ nhân vật chưa sẵn sàng để tạo ảnh.";
    public static string StaffDnaBlock => "Thông tin nhận diện nhân vật chưa hợp lệ.";
    public static string StaffProviderMissing => "Vui lòng chọn công cụ tạo ảnh.";
    public static string StaffCapability => "Công cụ này chưa hỗ trợ kiểu ảnh này.";
    public static string StaffDuplicate => "Ảnh tham chiếu này đã được tạo trước đó.";
    public static string StaffGenerateFail => "Không thể tạo ảnh lúc này. Vui lòng thử lại sau.";
    public static string StaffTypeBlock => "Góc ảnh này chưa được hỗ trợ.";
    public static string StaffConfirm => "Cần xác nhận trước khi tạo ảnh.";
    public static string StaffReject => "Cần ghi lý do khi từ chối.";
    public static string StaffReady => "Ảnh tham chiếu đã được tạo.";
    public static string StaffWaitDirector => "Đang chờ Director kiểm tra.";
    public static string StaffNextGenerate => "Tạo ảnh tham chiếu";

    public sealed record CharacterReferenceGenerationIntent(
        string CharacterId,
        string EraId,
        string ReferenceType,
        string IdentityAuthority,
        string Pose,
        string Framing,
        string Camera,
        string Composition,
        string Lighting,
        string Background,
        string Style,
        IReadOnlyList<string> Constraints,
        string MasterSha256,
        string DnaSha256,
        string PrpSha256,
        string? ReferencePackVersion);

    public sealed record CanonicalReferenceDescription(
        string Kind,
        string Text,
        string IntentSha256,
        string CharacterId,
        string EraId,
        string ReferenceType);

    public sealed record CharacterReferenceGenerationContract(
        string CharacterId,
        string EraId,
        string ReferenceType,
        string IntentSha256,
        string MasterSha256,
        string DnaSha256,
        string PrpSha256,
        string CanonicalText,
        string? SelectedProvider,
        string? CapabilityLevel,
        string ExecutionPolicy);

    public sealed record GateInput(
        bool CharacterExists,
        bool IdentityValid,
        bool MasterExists,
        bool MasterLocked,
        bool DnaExists,
        bool DnaLocked,
        bool DnaMasterMatch,
        bool PrpExists,
        bool PrpAuthorityValid,
        bool ReferenceTypeValid,
        bool IntentValid,
        bool CanonicalValid,
        string? DirectorProvider,
        bool CapabilityReady,
        string? CapabilityCode,
        bool Confirm,
        bool DuplicateFingerprint);

    public sealed record Gate(
        string Status,
        string? Code,
        string StaffMessage,
        bool AuthorityValid,
        bool IntentValid,
        bool ProviderSelected,
        bool CapabilityReady,
        bool GenerationAllowed,
        bool ExecutionNotDuplicate,
        bool MayCallProvider);

    public static bool IsSupportedType(string? type) =>
        SupportedTypes.Contains(CharacterReferencePackRules.NormalizeRefType(type), StringComparer.OrdinalIgnoreCase);

    public static bool ProviderSelected(string? provider) =>
        !string.IsNullOrWhiteSpace(provider);

    public static bool AllowsImageProvider(string? provider) =>
        string.Equals(provider, AllowedProvider, StringComparison.OrdinalIgnoreCase);

    public static bool ImageCapabilitySupported(ProductionOsRules.ProviderCapabilityProfile profile) =>
        ProductionOsRules.CapabilityOf(profile, AllowedGeneration) == "SUPPORTED";

    public static string ReferenceCapability(ProductionOsRules.ProviderCapabilityProfile profile) =>
        ProductionOsRules.CapabilityOf(profile, RequiredCapability);

    public static bool CapabilityUnsupported(ProductionOsRules.ProviderCapabilityProfile profile) =>
        !ImageCapabilitySupported(profile)
        || ReferenceCapability(profile) == "UNSUPPORTED";

    public static bool CapabilityAllowsDirector(ProductionOsRules.ProviderCapabilityProfile profile) =>
        ImageCapabilitySupported(profile)
        && ReferenceCapability(profile) is "SUPPORTED" or "LIMITED";

    public static ProductionOsRules.ProviderCapabilityProfile? ProfileOf(string? provider) =>
        (provider ?? "").Trim().ToUpperInvariant() switch
        {
            "GEMINI" => ProductionOsRules.GeminiImageProfile,
            "RUNWAY" => ProductionOsRules.RunwayVideoProfile,
            "VEO" => ProductionOsRules.VeoVideoProfile,
            _ => null,
        };

    public static CharacterReferenceGenerationIntent BuildDefault(
        string characterId,
        string eraId,
        string referenceType,
        string masterSha,
        string dnaSha,
        string prpSha,
        string? style = null,
        string? packVersion = null)
    {
        var type = CharacterReferencePackRules.NormalizeRefType(referenceType);
        var (pose, framing) = type switch
        {
            "FRONT" => ("neutral_portrait", "head_and_shoulders"),
            "THREE_QUARTER" => ("neutral_three_quarter", "head_and_shoulders"),
            "SIDE" => ("neutral_profile", "head_and_shoulders"),
            _ => ("neutral_standing", "full_body"),
        };
        return new CharacterReferenceGenerationIntent(
            CharacterReferencePackRules.NormalizeCharacterId(characterId),
            string.IsNullOrWhiteSpace(eraId) ? "ERA-01" : eraId.Trim().ToUpperInvariant(),
            type,
            "CHARACTER_CANON",
            pose,
            framing,
            "eye_level",
            "centered",
            "neutral",
            "plain",
            string.IsNullOrWhiteSpace(style) ? "locked_character_canon" : style.Trim(),
            type == "FULL_BODY"
                ? ["identity_locked", "no_production_still", "no_character_drift", "head_to_toe_visible"]
                : ["identity_locked", "no_production_still", "no_character_drift"],
            masterSha.Trim().ToLowerInvariant(),
            dnaSha.Trim().ToLowerInvariant(),
            prpSha.Trim().ToLowerInvariant(),
            packVersion);
    }

    public static bool IntentValid(CharacterReferenceGenerationIntent? intent)
    {
        if (intent is null) return false;
        if (!CharacterReferencePackRules.RequireCharacterId(intent.CharacterId)) return false;
        if (!IsSupportedType(intent.ReferenceType)) return false;
        if (!CharacterReferencePackRules.ShaExists(intent.MasterSha256)
            || !CharacterReferencePackRules.ShaExists(intent.DnaSha256)
            || !CharacterReferencePackRules.ShaExists(intent.PrpSha256))
            return false;
        if (intent.IdentityAuthority != "CHARACTER_CANON") return false;
        foreach (var field in new[]
        {
            intent.Pose, intent.Framing, intent.Camera, intent.Composition,
            intent.Lighting, intent.Background, intent.Style,
        })
        {
            if (string.IsNullOrWhiteSpace(field)) return false;
            if (ProductionOsRules.ContainsProvider(field) || ProductionOsRules.ContainsForbiddenIntentKey(field))
                return false;
        }
        return true;
    }

    public static object CanonicalJson(CharacterReferenceGenerationIntent intent) => new
    {
        character_id = intent.CharacterId.Trim().ToUpperInvariant(),
        era_id = intent.EraId.Trim().ToUpperInvariant(),
        reference_type = intent.ReferenceType.Trim().ToUpperInvariant(),
        identity_authority = intent.IdentityAuthority,
        pose = intent.Pose,
        framing = intent.Framing,
        camera = intent.Camera,
        composition = intent.Composition,
        lighting = intent.Lighting,
        background = intent.Background,
        style = intent.Style,
        constraints = intent.Constraints.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        master_sha256 = intent.MasterSha256.ToLowerInvariant(),
        dna_sha256 = intent.DnaSha256.ToLowerInvariant(),
        prp_sha256 = intent.PrpSha256.ToLowerInvariant(),
        reference_pack_version = intent.ReferencePackVersion,
    };

    public static string IntentSha(CharacterReferenceGenerationIntent intent)
    {
        ProductionOsRules.EnsureProviderAgnostic("characterId", intent.CharacterId);
        ProductionOsRules.EnsureProviderAgnostic("pose", intent.Pose);
        ProductionOsRules.EnsureProviderAgnostic("style", intent.Style);
        return KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(CanonicalJson(intent), CanonicalOptions)));
    }

    public static CanonicalReferenceDescription Canonical(CharacterReferenceGenerationIntent intent, string? identityFacts = null)
    {
        var sha = IntentSha(intent);
        var facts = string.IsNullOrWhiteSpace(identityFacts) ? "" : $" Identity: {identityFacts.Trim()}.";
        var text =
            $"Character reference. Type {intent.ReferenceType}. Pose {intent.Pose}. Framing {intent.Framing}. " +
            $"Camera {intent.Camera}. Composition {intent.Composition}. Lighting {intent.Lighting}. " +
            $"Background {intent.Background}. Style {intent.Style}. " +
            $"Match locked Character Canon. Do not invent a new person." +
            (intent.ReferenceType.Equals("FULL_BODY", StringComparison.OrdinalIgnoreCase)
                ? " Entire figure in frame from head to feet. Do not crop the head or feet."
                : "") +
            facts;
        ProductionOsRules.EnsureProviderAgnostic("canonical", text);
        return new CanonicalReferenceDescription(
            "CANONICAL_REFERENCE_DESCRIPTION",
            text,
            sha,
            intent.CharacterId,
            intent.EraId,
            intent.ReferenceType);
    }

    public static CharacterReferenceGenerationContract Contract(
        CharacterReferenceGenerationIntent intent,
        CanonicalReferenceDescription canonical,
        string? provider,
        string? capabilityLevel) =>
        new(
            intent.CharacterId,
            intent.EraId,
            intent.ReferenceType,
            canonical.IntentSha256,
            intent.MasterSha256,
            intent.DnaSha256,
            intent.PrpSha256,
            canonical.Text,
            string.IsNullOrWhiteSpace(provider) ? null : provider.Trim().ToUpperInvariant(),
            capabilityLevel,
            "DIRECTOR_CONFIRM_ONCE_NO_RETRY");

    public static string ExecutionFingerprint(
        CharacterReferenceGenerationIntent intent, string provider, string config)
    {
        var surface = new
        {
            character_id = intent.CharacterId.Trim().ToUpperInvariant(),
            reference_type = intent.ReferenceType.Trim().ToUpperInvariant(),
            intent_sha256 = IntentSha(intent),
            master_sha256 = intent.MasterSha256.ToLowerInvariant(),
            dna_sha256 = intent.DnaSha256.ToLowerInvariant(),
            prp_sha256 = intent.PrpSha256.ToLowerInvariant(),
            provider = provider.Trim().ToUpperInvariant(),
            generation_config = config,
        };
        return KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(surface, CanonicalOptions)));
    }

    public static string DuplicatePolicy(string fingerprint, string? existingFingerprint) =>
        !string.IsNullOrWhiteSpace(existingFingerprint)
        && string.Equals(fingerprint, existingFingerprint, StringComparison.OrdinalIgnoreCase)
            ? "BLOCK_DUPLICATE"
            : "NEW";

    public static ImageGenerationExecutionRequest CompileProviderRequest(
        CanonicalReferenceDescription canonical,
        IReadOnlyList<ImageGenerationReferenceBytes> references,
        string referenceType)
    {
        ProductionOsRules.EnsureProviderAgnostic("canonical", canonical.Text);
        var aspect = CharacterReferencePackRules.NormalizeRefType(referenceType) == "FULL_BODY" ? "3:4" : "1:1";
        return new ImageGenerationExecutionRequest(canonical.Text, references, aspect, null);
    }

    public static bool MayUseAsIdentitySource(string? assetId, string? path) =>
        !CharacterReferenceCompletionRules.IsHistoricalStill(assetId)
        && !CharacterReferencePackRules.TouchesGolden(path);

    public static bool RejectReasonValid(string? reason) =>
        (reason ?? "").Trim().Length >= 3;

    public static string CandidateAfterGenerate() => CandidateReady;

    public static string SlotState(
        bool hasRegisteredItem,
        string? candidateStatus,
        string? crpStatus,
        bool coverageReady)
    {
        if (string.Equals(crpStatus, "LOCKED", StringComparison.OrdinalIgnoreCase)) return "LOCKED";
        if (string.Equals(crpStatus, "APPROVED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(crpStatus, "DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase))
            return "DIRECTOR_APPROVED";
        if (string.Equals(crpStatus, "VALIDATED", StringComparison.OrdinalIgnoreCase) && coverageReady)
            return "CRP_VALIDATED";
        if (hasRegisteredItem) return "REGISTERED";
        var c = (candidateStatus ?? "").Trim().ToUpperInvariant();
        if (c == "ACCEPTED") return "ACCEPTED";
        if (c is "READY_FOR_DIRECTOR" or "GENERATED") return "READY_FOR_DIRECTOR";
        if (c == "GENERATING") return "GENERATING";
        if (c is "REJECTED" or "FAILED" or "") return hasRegisteredItem ? "REGISTERED" : "GENERATION_READY";
        return "GENERATION_READY";
    }

    public static string GenerationConfigForAttempt(int attempt) =>
        attempt <= 1 ? GenerationConfig : $"{GenerationConfig}#{attempt}";

    public static string GenerateCta(string? referenceType, bool replaceExisting = false) =>
        replaceExisting
            ? (CharacterReferencePackRules.NormalizeRefType(referenceType) == "FULL_BODY"
                ? "TẠO LẠI ẢNH TOÀN THÂN"
                : "TẠO LẠI ẢNH THAM CHIẾU")
            : CharacterReferencePackRules.NormalizeRefType(referenceType) == "FULL_BODY"
                ? "TẠO ẢNH TOÀN THÂN"
                : "TẠO ẢNH THAM CHIẾU";

    public static string StaffAngle(string? type) => CharacterReferencePackRules.StaffViewLabel(type);

    public static Gate Evaluate(GateInput input)
    {
        if (!input.CharacterExists)
            return Block("CHARACTER_MISSING", StaffMasterBlock, input);
        if (!input.MasterExists || !input.MasterLocked)
            return Block("MASTER_NOT_LOCKED", StaffMasterBlock, input);
        if (!input.DnaExists || !input.DnaLocked || !input.IdentityValid || !input.DnaMasterMatch)
            return Block("DNA_INVALID", StaffDnaBlock, input);
        if (!input.PrpExists || !input.PrpAuthorityValid)
            return Block("PRP_INVALID", StaffMasterBlock, input);
        if (!input.ReferenceTypeValid)
            return Block("INVALID_REFERENCE_TYPE", StaffTypeBlock, input);
        if (!input.IntentValid || !input.CanonicalValid)
            return Block("INTENT_INVALID", StaffMasterBlock, input);
        if (!ProviderSelected(input.DirectorProvider))
            return new Gate("NEEDS_PROVIDER_SELECTION", "NEEDS_PROVIDER_SELECTION",
                StaffProviderMissing, true, true, false, input.CapabilityReady, false, !input.DuplicateFingerprint, false);
        if (string.Equals(input.DirectorProvider, "RUNWAY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.DirectorProvider, "VEO", StringComparison.OrdinalIgnoreCase)
            || !AllowsImageProvider(input.DirectorProvider)
            || !input.CapabilityReady)
            return Block(input.CapabilityCode ?? "PROVIDER_CAPABILITY_UNSUPPORTED", StaffCapability, input,
                providerSelected: true);
        if (input.DuplicateFingerprint)
            return Block("BLOCK_DUPLICATE", StaffDuplicate, input, providerSelected: true, capability: true, duplicate: true);
        if (!input.Confirm)
            return new Gate("BLOCKED", "CONFIRM_REQUIRED", StaffConfirm, true, true, true, true, false, true, false);
        return new Gate("READY", null, "Sẵn sàng tạo ảnh tham chiếu.", true, true, true, true, true, true, true);
    }

    public static bool MayCallProvider(Gate gate) =>
        gate.MayCallProvider && gate.GenerationAllowed && !AutoSelectProvider() && !AutoApprove() && !AutoRetry();

    private static Gate Block(
        string code, string staff, GateInput input,
        bool providerSelected = false, bool capability = false, bool duplicate = false) =>
        new("BLOCKED", code, staff,
            input.CharacterExists && input.MasterExists && input.MasterLocked && input.DnaExists && input.DnaLocked,
            input.IntentValid, providerSelected, capability, false, !duplicate, false);

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class MockCharacterReferenceProvider : IImageGenerationProvider
{
    public const string Id = "MOCK_CHARACTER_REFERENCE_PROVIDER";
    public string ProviderId => Id;
    public int CallCount { get; private set; }
    public ImageGenerationExecutionRequest? LastRequest { get; private set; }
    public bool Succeed { get; set; } = true;
    public byte[] Bytes { get; set; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public Task<ImageGenerationProviderResult> GenerateAsync(
        ImageGenerationExecutionRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        if (!Succeed)
        {
            return Task.FromResult(new ImageGenerationProviderResult(
                false, false, "FAILED", null, null, ProviderId, null, null, true, null));
        }
        return Task.FromResult(new ImageGenerationProviderResult(
            true, true, "ACCEPTED", Bytes, "image/png", ProviderId, null, null, true, null));
    }
}
