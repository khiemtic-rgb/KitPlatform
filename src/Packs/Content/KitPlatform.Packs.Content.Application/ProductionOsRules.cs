using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// PRODUCTION_OS_ARCHITECTURE_V1 / FAMIXA_VIDEO_PRODUCTION_OS_ARCHITECTURE_V1.
/// Layers 1–5 are Famixa. Layer 6 is replaceable infrastructure.
/// Does not generate, approve, lock, or select a provider.
/// </summary>
public static class ProductionOsRules
{
    public const string ArchitectureId = "PRODUCTION_OS_ARCHITECTURE_V1";
    public const string DocumentId = "FAMIXA_VIDEO_PRODUCTION_OS_ARCHITECTURE_V1";
    public const string SuiteId = "FAMIXA_PROVIDER_AGNOSTIC_ARCHITECTURE_REGRESSION";

    public static readonly string[] AuthorityChain =
    [
        "MASTER", "DNA", "CHARACTER_REFERENCE", "PRODUCTION_REFERENCE",
        "SHOT_CONTRACT", "PROMPT_COMPILER", "GENERATION_CONTRACT",
        "GENERATION_EXECUTION", "ARTIFACT",
    ];

    public static readonly string[] FamixaLayers = ["STORY", "WORLD", "PRODUCTION", "GOVERNANCE", "ORCHESTRATION"];
    public static readonly string[] ReplaceableLayer = ["AI_PROVIDERS"];

    public static readonly string[] Capabilities =
    [
        "IMAGE_GENERATION", "VIDEO_GENERATION", "IMAGE_TO_VIDEO", "TEXT_TO_VIDEO",
        "REFERENCE_IMAGE", "MULTI_REFERENCE", "CHARACTER_REFERENCE",
        "CAMERA_CONTROL", "MOTION_CONTROL", "DURATION_5S", "DURATION_10S",
        "AUDIO", "EXTEND_VIDEO",
    ];

    public static readonly string[] QaKinds =
        ["TECHNICAL", "IDENTITY", "COMPOSITION", "CONTINUITY", "CONTRACT", "PROVIDER", "DIRECTOR"];

    public static readonly string[] QaVerdicts = ["PASS", "FAIL", "UNKNOWN", "NOT_APPLICABLE"];

    public static readonly string[] ForbiddenIntentKeys =
    [
        "provider", "providerId", "provider_id", "gemini_prompt", "runway_prompt", "veo_prompt",
        "model_prompt", "model_name", "gen4_turbo", "provider_token", "provider_request",
    ];

    public static readonly string[] ProviderTokens =
    [
        "gemini", "runway", "veo", "gen4_turbo", "gen4_x", "gemini-2.5-flash-image",
        "runway_prompt", "gemini_prompt", "veo_prompt",
    ];

    public static bool AutoFix() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoSelectProvider() => false;
    public static bool AutoDowngrade() => false;
    public static bool BlindRetryAllowed() => false;
    public static bool ArtifactMayMutateAuthority() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY" or "VEO";
    public static bool ProviderIsAuthority() => false;
    public static bool PromptIsAuthority() => false;
    public static bool ArtifactIsAuthority() => false;

    public sealed record ProductionIntent(
        string CharacterId,
        string? ReferencePackId,
        string CharacterState,
        string? LocationId,
        string EnvironmentState,
        string? ObjectIds,
        string ActionType,
        string ActionDescription,
        string ShotSize,
        string CameraAngle,
        string CameraMovement,
        string SubjectPosition,
        string Orientation,
        string LightingStyle,
        string Motion,
        string TimingNote,
        double DurationSeconds,
        string? PreviousShotId,
        bool RequiredIdentity,
        string MasterSha256,
        string DnaSha256,
        string ReferenceSha256,
        string ShotContractSha256);

    public sealed record CanonicalProductionDescription(
        string Kind,
        string Text,
        string IntentSha256,
        string Subject,
        string Identity,
        string Environment,
        string Action,
        string ShotSize,
        string CameraAngle,
        string CameraMovement,
        string SubjectPosition,
        string Orientation,
        string LightingStyle,
        string Motion,
        double DurationSeconds,
        bool RequiredIdentity,
        string Constraints,
        string MasterSha256,
        string DnaSha256,
        string ReferenceSha256,
        string ShotContractSha256);

    public sealed record ProviderCapabilityProfile(
        string ProviderId,
        string Kind,
        IReadOnlyDictionary<string, string> Capabilities);

    public sealed record ProductionProviderGate(
        string Status,
        string? Code,
        string Message,
        string ProviderId);

    public sealed record ProductionProviderCandidate(string ProviderId, string Kind, string Status, string Capability);

    public sealed record ProductionProviderRequest(
        string ProviderId,
        IReadOnlyDictionary<string, string> Fields);

    public sealed record ProductionProvenance(
        string? StoryId,
        string? EpisodeId,
        string? SceneId,
        string? ShotId,
        string MasterSha256,
        string DnaSha256,
        string ReferenceSha256,
        string ShotContractSha256,
        string? PromptSha256,
        string? GenerationContractSha256,
        string IntentSha256,
        string? Provider,
        string? ProviderRequestId,
        string? ExecutionId,
        string? ArtifactSha256);

    public sealed record ProductionArtifact(
        string ArtifactId,
        string ExecutionId,
        string Provider,
        string? ProviderRequestId,
        string? Path,
        string Mime,
        long Size,
        string Sha256,
        DateTimeOffset CreatedAt);

    public sealed record WorldLocationReference(
        string LocationId,
        string Version,
        IReadOnlyList<string> Views);

    public sealed record WorldObjectReference(
        string ObjectId,
        string Version,
        string Appearance,
        string State);

    public static readonly ProviderCapabilityProfile GeminiImageProfile = new(
        "GEMINI",
        "IMAGE",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["IMAGE_GENERATION"] = "SUPPORTED",
            ["REFERENCE_IMAGE"] = "SUPPORTED",
            ["CHARACTER_REFERENCE"] = "LIMITED",
            ["VIDEO_GENERATION"] = "UNSUPPORTED",
            ["IMAGE_TO_VIDEO"] = "UNSUPPORTED",
            ["DURATION_5S"] = "UNSUPPORTED",
            ["DURATION_10S"] = "UNSUPPORTED",
        });

    public static readonly ProviderCapabilityProfile RunwayVideoProfile = new(
        "RUNWAY",
        "VIDEO",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VIDEO_GENERATION"] = "SUPPORTED",
            ["IMAGE_TO_VIDEO"] = "SUPPORTED",
            ["MULTI_REFERENCE"] = "SUPPORTED",
            ["MOTION_CONTROL"] = "SUPPORTED",
            ["DURATION_5S"] = "SUPPORTED",
            ["DURATION_10S"] = "SUPPORTED",
            ["IMAGE_GENERATION"] = "UNSUPPORTED",
            ["AUDIO"] = "UNSUPPORTED",
        });

    public static readonly ProviderCapabilityProfile VeoVideoProfile = new(
        "VEO",
        "VIDEO",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VIDEO_GENERATION"] = "UNSUPPORTED",
            ["IMAGE_TO_VIDEO"] = "UNSUPPORTED",
            ["TEXT_TO_VIDEO"] = "UNSUPPORTED",
            ["IMAGE_GENERATION"] = "UNSUPPORTED",
            ["DURATION_5S"] = "UNSUPPORTED",
            ["DURATION_10S"] = "UNSUPPORTED",
        });

    public static IReadOnlyList<ProviderCapabilityProfile> DefaultCatalog =>
        [GeminiImageProfile, RunwayVideoProfile, VeoVideoProfile];

    public static ProductionIntent SampleIntent(
        string characterId,
        string masterSha,
        string dnaSha,
        string referenceSha,
        string contractSha,
        double duration = 5) =>
        new(
            characterId, null, "standing", null, "classroom", null,
            "walk_forward", "Nhân vật bước về phía bàn học",
            "medium", "eye_level", "slow_dolly_in",
            "center", "front_three_quarter", "soft_daylight",
            "walk_forward", "",
            duration, null, true, masterSha, dnaSha, referenceSha, contractSha);

    public static bool ContainsProvider(string? raw)
    {
        var t = (raw ?? "").ToLowerInvariant();
        return ProviderTokens.Any(p => t.Contains(p, StringComparison.Ordinal));
    }

    public static bool ContainsForbiddenIntentKey(string? raw)
    {
        var t = (raw ?? "").ToLowerInvariant();
        return ForbiddenIntentKeys.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public static void EnsureProviderAgnostic(string surface, string? raw)
    {
        if (ContainsProvider(raw) || ContainsForbiddenIntentKey(raw))
            throw new InvalidOperationException($"PRODUCTION_OS_INVALID: {surface} không được chứa provider.");
    }

    public static object CanonicalJson(ProductionIntent intent) => new
    {
        character_id = intent.CharacterId.Trim().ToUpperInvariant(),
        reference_pack_id = intent.ReferencePackId,
        character_state = intent.CharacterState,
        location_id = intent.LocationId,
        environment_state = intent.EnvironmentState,
        object_ids = intent.ObjectIds,
        action_type = intent.ActionType,
        action_description = intent.ActionDescription,
        camera = new { shot = intent.ShotSize, angle = intent.CameraAngle, movement = intent.CameraMovement },
        composition = new { intent.SubjectPosition, intent.Orientation },
        lighting = intent.LightingStyle,
        motion = intent.Motion,
        timing = intent.TimingNote,
        duration_seconds = intent.DurationSeconds,
        continuity = new { intent.PreviousShotId, intent.RequiredIdentity },
        master_sha256 = intent.MasterSha256.ToLowerInvariant(),
        dna_sha256 = intent.DnaSha256.ToLowerInvariant(),
        reference_sha256 = intent.ReferenceSha256.ToLowerInvariant(),
        shot_contract_sha256 = intent.ShotContractSha256.ToLowerInvariant(),
    };

    public static string IntentSha(ProductionIntent intent)
    {
        EnsureProviderAgnostic("characterId", intent.CharacterId);
        EnsureProviderAgnostic("action", intent.ActionDescription);
        EnsureProviderAgnostic("camera", $"{intent.ShotSize} {intent.CameraAngle} {intent.CameraMovement}");
        EnsureProviderAgnostic("motion", intent.Motion);
        return KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(CanonicalJson(intent), CanonicalOptions)));
    }

    public static CanonicalProductionDescription Canonical(ProductionIntent intent)
    {
        var sha = IntentSha(intent);
        var text =
            $"{intent.ActionDescription}. Camera {intent.ShotSize} {intent.CameraAngle} {intent.CameraMovement}. " +
            $"Subject {intent.SubjectPosition} {intent.Orientation}. Light {intent.LightingStyle}. " +
            $"Motion {intent.Motion}. Duration {intent.DurationSeconds:0}s. Identity required={intent.RequiredIdentity}.";
        EnsureProviderAgnostic("canonical", text);
        return new CanonicalProductionDescription(
            "CANONICAL_PRODUCTION_DESCRIPTION",
            text,
            sha,
            intent.CharacterId,
            intent.RequiredIdentity ? "required" : "optional",
            intent.EnvironmentState,
            intent.ActionDescription,
            intent.ShotSize,
            intent.CameraAngle,
            intent.CameraMovement,
            intent.SubjectPosition,
            intent.Orientation,
            intent.LightingStyle,
            intent.Motion,
            intent.DurationSeconds,
            intent.RequiredIdentity,
            "identity_and_contract_win; no provider rewrite",
            intent.MasterSha256,
            intent.DnaSha256,
            intent.ReferenceSha256,
            intent.ShotContractSha256);
    }

    public static string CapabilityOf(ProviderCapabilityProfile profile, string capability) =>
        profile.Capabilities.TryGetValue(capability, out var v) ? v.ToUpperInvariant() : "UNSUPPORTED";

    public static ProductionProviderGate EvaluateCapability(
        ProductionIntent intent, ProviderCapabilityProfile profile, string generationType)
    {
        var required = generationType.Equals("VIDEO", StringComparison.OrdinalIgnoreCase)
            ? new List<string> { "VIDEO_GENERATION", "IMAGE_TO_VIDEO", intent.DurationSeconds >= 10 ? "DURATION_10S" : "DURATION_5S" }
            : new List<string> { "IMAGE_GENERATION" };
        if (intent.RequiredIdentity && profile.Capabilities.ContainsKey("CHARACTER_REFERENCE"))
            required.Add("CHARACTER_REFERENCE");
        foreach (var cap in required)
        {
            var level = CapabilityOf(profile, cap);
            if (level == "UNSUPPORTED")
            {
                return new ProductionProviderGate(
                    "BLOCKED",
                    "PROVIDER_CAPABILITY_UNSUPPORTED",
                    "Nhà cung cấp AI này chưa hỗ trợ yêu cầu sản xuất này.",
                    profile.ProviderId);
            }
            if (level == "LIMITED")
            {
                return new ProductionProviderGate(
                    "NEEDS_PROVIDER_SELECTION",
                    "NEEDS_PROVIDER_SELECTION",
                    "Nhà cung cấp AI này hỗ trợ hạn chế. Director chọn bước tiếp.",
                    profile.ProviderId);
            }
        }
        return new ProductionProviderGate("READY", null, "Provider có thể thực thi Intent này.", profile.ProviderId);
    }

    public static IReadOnlyList<ProductionProviderCandidate> ListCandidates(
        ProductionIntent intent, string generationType, IReadOnlyList<ProviderCapabilityProfile> catalog)
    {
        return catalog.Select(p =>
        {
            var gate = EvaluateCapability(intent, p, generationType);
            var cap = generationType.Equals("VIDEO", StringComparison.OrdinalIgnoreCase) ? "IMAGE_TO_VIDEO" : "IMAGE_GENERATION";
            return new ProductionProviderCandidate(p.ProviderId, p.Kind, gate.Status, CapabilityOf(p, cap));
        }).ToList();
    }

    public static ProductionProviderGate DecideSelection(
        IReadOnlyList<ProductionProviderCandidate> candidates, string? directorProviderId)
    {
        if (AutoSelectProvider())
            throw new InvalidOperationException("PRODUCTION_OS_INVALID: không tự chọn provider.");
        if (!string.IsNullOrWhiteSpace(directorProviderId))
        {
            var picked = candidates.FirstOrDefault(c =>
                c.ProviderId.Equals(directorProviderId, StringComparison.OrdinalIgnoreCase));
            if (picked is null)
                return new ProductionProviderGate("BLOCKED", "PROVIDER_UNAVAILABLE", "Provider chưa đăng ký.", directorProviderId);
            if (picked.Status == "BLOCKED")
                return new ProductionProviderGate("BLOCKED", "PROVIDER_CAPABILITY_UNSUPPORTED",
                    "Nhà cung cấp AI này chưa hỗ trợ yêu cầu sản xuất này.", picked.ProviderId);
            if (picked.Status == "NEEDS_PROVIDER_SELECTION")
                return new ProductionProviderGate("NEEDS_PROVIDER_SELECTION", "NEEDS_PROVIDER_SELECTION",
                    "Nhà cung cấp AI này hỗ trợ hạn chế. Director chọn bước tiếp.", picked.ProviderId);
            return new ProductionProviderGate("READY", null, "Director đã chọn provider.", picked.ProviderId);
        }

        var capable = candidates.Count(c => c.Status is "READY" or "NEEDS_PROVIDER_SELECTION");
        if (capable == 0)
            return new ProductionProviderGate("BLOCKED", "PROVIDER_UNAVAILABLE",
                "Không có nhà cung cấp AI nào hỗ trợ yêu cầu sản xuất này.", "");
        return new ProductionProviderGate("NEEDS_PROVIDER_SELECTION", "NEEDS_PROVIDER_SELECTION",
            "Director chọn nhà cung cấp. Hệ thống không tự chọn.", "");
    }

    public static bool MayCompileRequest(
        bool authorityPass, bool governancePass, bool contractPass, bool intentPass,
        bool capabilityReady, bool directorSelected) =>
        authorityPass && governancePass && contractPass && intentPass && capabilityReady && directorSelected
        && !AutoSelectProvider();

    public static ProductionProviderRequest CompileForProvider(
        ProductionIntent intent, CanonicalProductionDescription canonical, string providerId)
    {
        if (AutoSelectProvider())
            throw new InvalidOperationException("PRODUCTION_OS_INVALID: không tự chọn provider.");
        var sha = IntentSha(intent);
        if (sha != canonical.IntentSha256)
            throw new InvalidOperationException("PRODUCTION_OS_INVALID: canonical lệch Intent.");
        return providerId.ToUpperInvariant() switch
        {
            "MOCK_A" => new ProductionProviderRequest("MOCK_A", new Dictionary<string, string>
            {
                ["kind"] = "mock-a-request",
                ["intentSha"] = sha,
                ["text"] = canonical.Text,
            }),
            "MOCK_B" => new ProductionProviderRequest("MOCK_B", new Dictionary<string, string>
            {
                ["kind"] = "mock-b-payload",
                ["intent_sha256"] = sha,
                ["description"] = canonical.Text,
            }),
            _ => throw new InvalidOperationException("PRODUCTION_OS_INVALID: adapter chưa đăng ký. Không gọi provider thật."),
        };
    }

    public static string ExecutionFingerprint(ProductionIntent intent, string providerId, string providerConfig)
    {
        var surface = new
        {
            character_id = intent.CharacterId.Trim().ToUpperInvariant(),
            intent_sha256 = IntentSha(intent),
            master_sha256 = intent.MasterSha256.ToLowerInvariant(),
            dna_sha256 = intent.DnaSha256.ToLowerInvariant(),
            reference_sha256 = intent.ReferenceSha256.ToLowerInvariant(),
            shot_contract_sha256 = intent.ShotContractSha256.ToLowerInvariant(),
            provider = providerId.Trim().ToUpperInvariant(),
            provider_config = providerConfig,
        };
        return KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(surface, CanonicalOptions)));
    }

    public static string DuplicatePolicy(string fingerprint, string? existingFingerprint) =>
        !string.IsNullOrWhiteSpace(existingFingerprint)
        && string.Equals(fingerprint, existingFingerprint, StringComparison.OrdinalIgnoreCase)
            ? "BLOCK_DUPLICATE"
            : "NEW";

    public static ProductionProvenance Provenance(
        ProductionIntent intent,
        string? promptSha = null,
        string? generationContractSha = null,
        string? provider = null,
        string? providerRequestId = null,
        string? executionId = null,
        string? artifactSha = null,
        string? shotId = null) =>
        new(null, null, null, shotId,
            intent.MasterSha256, intent.DnaSha256, intent.ReferenceSha256, intent.ShotContractSha256,
            promptSha, generationContractSha, IntentSha(intent),
            provider, providerRequestId, executionId, artifactSha);

    public static ProductionProviderGate EvaluateAuthority(
        string? masterSha, string? dnaSha, string? referenceSha, string? contractSha, string? directorApproval)
    {
        if (string.IsNullOrWhiteSpace(masterSha))
            return new ProductionProviderGate("BLOCKED", "MASTER_MISSING", "Thiếu Master.", "");
        if (string.IsNullOrWhiteSpace(dnaSha))
            return new ProductionProviderGate("BLOCKED", "DNA_MISSING", "Thiếu DNA.", "");
        if (string.IsNullOrWhiteSpace(referenceSha))
            return new ProductionProviderGate("BLOCKED", "REFERENCE_MISSING", "Thiếu bộ tham chiếu.", "");
        if (string.IsNullOrWhiteSpace(contractSha))
            return new ProductionProviderGate("BLOCKED", "CONTRACT_MISSING", "Thiếu Shot Contract.", "");
        if (!string.Equals(directorApproval, "DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(directorApproval, "APPROVED", StringComparison.OrdinalIgnoreCase))
            return new ProductionProviderGate("BLOCKED", "DIRECTOR_PENDING", "Director chưa duyệt.", "");
        return new ProductionProviderGate("READY", null, "Authority đủ để orchestration.", "");
    }

    public static ProductionProviderGate EvaluateShaMatch(string expected, string live, string name) =>
        string.Equals(expected, live, StringComparison.OrdinalIgnoreCase)
            ? new ProductionProviderGate("READY", null, name + " khớp.", "")
            : new ProductionProviderGate("BLOCKED", "SHA_MISMATCH", name + " không khớp.", "");

    public static bool SameAuthority(ProductionIntent a, ProductionIntent b) =>
        string.Equals(a.MasterSha256, b.MasterSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.DnaSha256, b.DnaSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ReferenceSha256, b.ReferenceSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ShotContractSha256, b.ShotContractSha256, StringComparison.OrdinalIgnoreCase);

    public static bool MayOverwritePayload(string? status) =>
        status is not "LOCKED" and not "DIRECTOR_APPROVED" and not "APPROVED";

    public static string NextVersion(string? current)
    {
        var raw = (current ?? "V1").Trim().TrimStart('V', 'v');
        return int.TryParse(raw, out var n) ? $"V{n + 1}" : "V2";
    }

    public static string NormalizeQa(string? verdict)
    {
        var v = (verdict ?? "UNKNOWN").Trim().ToUpperInvariant();
        if (v is "PASS" or "FAIL" or "NOT_APPLICABLE") return v;
        return "UNKNOWN";
    }

    public static bool UnknownBecomesPass(string? verdict)
    {
        _ = verdict;
        return false;
    }

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public interface IProductionProvider
{
    string ProviderId { get; }
    string Kind { get; }
    ProductionOsRules.ProviderCapabilityProfile Profile { get; }
    ProductionOsRules.ProductionProviderRequest Compile(
        ProductionOsRules.ProductionIntent intent,
        ProductionOsRules.CanonicalProductionDescription canonical);
}

public sealed class MockProductionProviderA : IProductionProvider
{
    public string ProviderId => "MOCK_A";
    public string Kind => "MOCK";
    public ProductionOsRules.ProviderCapabilityProfile Profile { get; } =
        new("MOCK_A", "MOCK", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["IMAGE_GENERATION"] = "SUPPORTED",
            ["VIDEO_GENERATION"] = "SUPPORTED",
            ["IMAGE_TO_VIDEO"] = "SUPPORTED",
            ["DURATION_5S"] = "SUPPORTED",
            ["DURATION_10S"] = "SUPPORTED",
        });

    public ProductionOsRules.ProductionProviderRequest Compile(
        ProductionOsRules.ProductionIntent intent,
        ProductionOsRules.CanonicalProductionDescription canonical) =>
        ProductionOsRules.CompileForProvider(intent, canonical, ProviderId);
}

public sealed class MockProductionProviderB : IProductionProvider
{
    public string ProviderId => "MOCK_B";
    public string Kind => "MOCK";
    public ProductionOsRules.ProviderCapabilityProfile Profile { get; } =
        new("MOCK_B", "MOCK", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["IMAGE_GENERATION"] = "SUPPORTED",
            ["VIDEO_GENERATION"] = "SUPPORTED",
            ["IMAGE_TO_VIDEO"] = "SUPPORTED",
            ["DURATION_5S"] = "SUPPORTED",
            ["DURATION_10S"] = "SUPPORTED",
        });

    public ProductionOsRules.ProductionProviderRequest Compile(
        ProductionOsRules.ProductionIntent intent,
        ProductionOsRules.CanonicalProductionDescription canonical) =>
        ProductionOsRules.CompileForProvider(intent, canonical, ProviderId);
}

public interface IProductionProviderSelector
{
    IReadOnlyList<ProductionOsRules.ProductionProviderCandidate> ListCandidates(
        ProductionOsRules.ProductionIntent intent, string generationType);
    ProductionOsRules.ProductionProviderGate Evaluate(
        ProductionOsRules.ProductionIntent intent, string providerId, string generationType);
    ProductionOsRules.ProductionProviderGate DecideSelection(
        ProductionOsRules.ProductionIntent intent, string generationType, string? directorProviderId);
}

public interface IProductionOsService
{
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<ProductionOsRules.ProviderCapabilityProfile> Catalog();
    ProductionOsRules.CanonicalProductionDescription Describe(ProductionOsRules.ProductionIntent intent);
    IReadOnlyList<ProductionOsRules.ProductionProviderCandidate> ListCandidates(
        ProductionOsRules.ProductionIntent intent, string generationType);
    ProductionOsRules.ProductionProviderGate DecideSelection(
        ProductionOsRules.ProductionIntent intent, string generationType, string? directorProviderId);
}

public interface IImageGenerationProvider
{
    string ProviderId { get; }
    Task<ImageGenerationProviderResult> GenerateAsync(ImageGenerationExecutionRequest request, CancellationToken cancellationToken);
}

public interface ICharacterReferenceGenerationProvider
{
    string ProviderId { get; }
    Task<CharacterReferenceSetResult> GenerateSetAsync(
        CharacterReferenceSetRequest request, CancellationToken cancellationToken);
}

public interface ICharacterAuthorityGenerationProvider
{
    string ProviderId { get; }
    Task<CharacterAuthorityGenerationResult> GenerateMasterAsync(
        CharacterAuthorityGenerationRequest request, CancellationToken cancellationToken);
}

public sealed record CharacterAuthorityGenerationRequest(
    string CharacterId,
    string EraId,
    string GenerationType,
    string CanonicalText,
    string AspectRatio,
    string IdentityVersion,
    string? VisualUniverseSha = null,
    string? PvsSha = null,
    string? CdlSha = null,
    string? CompiledPromptSha = null);

public sealed record CharacterAuthorityGenerationResult(
    bool ProviderCalled,
    bool Succeeded,
    byte[]? Bytes,
    string? Mime,
    string? ProviderRequestId,
    string? ErrorCode);

public sealed record CharacterReferenceSetRequest(
    string CharacterId,
    string EraId,
    string MasterSha256,
    string DnaSha256,
    string PrpSha256,
    IReadOnlyList<CharacterReferenceViewRequest> Views,
    int? ChronologicalAge = null,
    int? TargetAppearanceAgeMin = null,
    int? TargetAppearanceAgeMax = null,
    string? AgeAppearanceProfile = null,
    string? ProjectVisualStyleSha = null,
    string? IdentitySha = null,
    string? CharacterAppearanceProfile = null,
    string? AppearanceProfileSha = null);

public sealed record CharacterReferenceViewRequest(
    string ReferenceType,
    string CanonicalText,
    string AspectRatio,
    IReadOnlyList<ImageGenerationReferenceBytes> IdentityRefs);

public sealed record CharacterReferenceGeneratedView(
    string ReferenceType,
    bool Succeeded,
    byte[]? Bytes,
    string? Mime,
    string? ProviderRequestId);

public sealed record CharacterReferenceSetResult(
    bool ProviderCalled,
    bool Succeeded,
    IReadOnlyList<CharacterReferenceGeneratedView> Views,
    string? ErrorCode,
    string? ProviderRequestId);
