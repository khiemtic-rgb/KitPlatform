namespace KitPlatform.Packs.Content;

/// <summary>
/// Famixa Provider Orchestration Core V1 — HOW only.
/// Does not decide CURRENT / stale / approved / next command / Canon / Timing.
/// </summary>
public static class FamixaProviderIds
{
    public const string Gemini = "gemini";
    public const string Runway = "runway";
    public const string Wan = "wan";
    public const string ElevenLabs = "elevenlabs";
    public const string Fal = "fal";
}

public enum FamixaProviderCapability
{
    Picture,
    Motion,
    Voice,
    LipSync,
}

public enum FamixaProviderPolicy
{
    Economy,
    Standard,
    Premium,
}

public enum FamixaProviderStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
}

public enum FamixaCostKind
{
    Estimate,
    Unknown,
    FalBilledEstimate,
}

public static class FamixaProviderErrorCodes
{
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    public const string AuthFailed = "AUTH_FAILED";
    public const string RateLimited = "RATE_LIMITED";
    public const string InvalidInput = "INVALID_INPUT";
    public const string ProviderError = "PROVIDER_ERROR";
    public const string Timeout = "TIMEOUT";
    public const string Unsupported = "UNSUPPORTED";
    public const string Unknown = "UNKNOWN";
}

public sealed class FamixaProviderException : InvalidOperationException
{
    public string Code { get; }
    public string ProviderId { get; }

    public FamixaProviderException(string code, string providerId, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = string.IsNullOrWhiteSpace(code) ? FamixaProviderErrorCodes.Unknown : code;
        ProviderId = providerId ?? "";
    }
}

public sealed record FamixaProviderDescriptor(
    string ProviderId,
    string DisplayName,
    IReadOnlyList<FamixaProviderCapability> Capabilities,
    IReadOnlyList<string> Models,
    string QualityTier,
    string CostInfo,
    bool Available);

public sealed record FamixaProviderSelection(
    string? Engine = null,
    string? LipsyncModel = null,
    string? VoiceProvider = null,
    string? ExplicitProviderId = null,
    FamixaProviderPolicy? Policy = null);

public sealed record FamixaProviderResolveResult(string ProviderId, string? ModelId);

public sealed record FamixaPictureRef(string Mime, string Base64, string Label);

public sealed record FamixaPictureProviderRequest(
    string Prompt,
    IReadOnlyList<FamixaPictureRef> References,
    string? AspectRatio);

public sealed record FamixaMotionStartRequest(
    string ImageDataUrl,
    string Prompt,
    string? NegativePrompt,
    int Seconds,
    string Ratio,
    string? LastFrameDataUrl = null);

public sealed record FamixaVoiceSynthesizeRequest(
    string VoiceId,
    string Text,
    string? PublicOwnerId = null,
    string? VoiceName = null,
    string? Accent = null,
    ContentSeriesTtsVoiceSettings? VoiceSettings = null);

public sealed record FamixaLipSyncStartRequest(
    string VideoUrl,
    string AudioUrl,
    string? SyncMode,
    string? ModelKind);

public sealed record FamixaProviderResult(
    string ProviderId,
    string? ModelId,
    FamixaProviderStatus Status,
    string? ProviderRequestId = null,
    string? MimeType = null,
    string? OutputUrl = null,
    byte[]? Bytes = null,
    FamixaCostKind CostKind = FamixaCostKind.Unknown,
    decimal? CostEstimate = null,
    string? CostUnit = null,
    string? ErrorCode = null,
    string? Error = null);

public interface IFamixaPictureProvider
{
    string ProviderId { get; }
    Task<FamixaProviderResult> GenerateAsync(FamixaPictureProviderRequest request, CancellationToken cancellationToken);
}

public interface IFamixaMotionProvider
{
    string ProviderId { get; }
    bool CanHandleTask(string? taskId);
    Task<FamixaProviderResult> StartAsync(FamixaMotionStartRequest request, CancellationToken cancellationToken);
    Task<FamixaProviderResult> GetAsync(string taskId, CancellationToken cancellationToken);
    Task<FamixaProviderResult> RecoverAsync(string taskId, CancellationToken cancellationToken);
}

public interface IFamixaVoiceProvider
{
    string ProviderId { get; }
    Task<FamixaProviderResult> SynthesizeAsync(FamixaVoiceSynthesizeRequest request, CancellationToken cancellationToken);
}

public interface IFamixaLipSyncProvider
{
    string ProviderId { get; }
    bool CanHandleTask(string? taskId);
    Task<FamixaProviderResult> StartAsync(FamixaLipSyncStartRequest request, CancellationToken cancellationToken);
    Task<FamixaProviderResult> GetAsync(string taskId, CancellationToken cancellationToken);
    Task<FamixaProviderResult> RecoverAsync(string taskId, CancellationToken cancellationToken);
}

public enum FamixaProviderTaskSource
{
    Explicit,
    LegacyPrefix,
    LegacyUnverified,
}

public sealed record FamixaProviderTaskRef(
    string ProviderId,
    FamixaProviderCapability Capability,
    FamixaProviderTaskSource Source,
    string? ModelId = null);

public interface IFamixaProviderRegistry
{
    IReadOnlyList<FamixaProviderDescriptor> List();
    FamixaProviderDescriptor Describe(string providerId);
    bool HasCapability(string providerId, FamixaProviderCapability capability);
    IFamixaPictureProvider GetPicture(string providerId);
    IFamixaMotionProvider GetMotion(string providerId);
    IFamixaVoiceProvider GetVoice(string providerId);
    IFamixaLipSyncProvider GetLipSync(string providerId);
    FamixaProviderTaskRef DescribeTask(string? taskId, string? providerId = null, FamixaProviderCapability? capability = null);
    IFamixaMotionProvider GetMotionForTask(string? taskId, string? providerId = null);
    IFamixaLipSyncProvider GetLipSyncForTask(string? taskId, string? providerId = null);
}

public static class FamixaProviderCatalog
{
    public static readonly FamixaProviderDescriptor Gemini = new(
        FamixaProviderIds.Gemini,
        "Gemini",
        [FamixaProviderCapability.Picture],
        ["gemini-2.5-flash-image"],
        "standard",
        "UNKNOWN",
        true);

    public static readonly FamixaProviderDescriptor Runway = new(
        FamixaProviderIds.Runway,
        "Runway",
        [FamixaProviderCapability.Motion],
        ["gen4_turbo"],
        "standard",
        "ESTIMATE 5 cr/s",
        true);

    public static readonly FamixaProviderDescriptor Wan = new(
        FamixaProviderIds.Wan,
        "Wan 2.1",
        [FamixaProviderCapability.Motion],
        ["wan-2.1"],
        "economy",
        "FAL_BILLED_ESTIMATE",
        true);

    public static readonly FamixaProviderDescriptor ElevenLabs = new(
        FamixaProviderIds.ElevenLabs,
        "ElevenLabs",
        [FamixaProviderCapability.Voice],
        ["eleven_v3"],
        "standard",
        "UNKNOWN",
        true);

    public static readonly FamixaProviderDescriptor Fal = new(
        FamixaProviderIds.Fal,
        "Fal LipSync",
        [FamixaProviderCapability.LipSync],
        ["1.9", "v3", "ls"],
        "standard",
        "ESTIMATE",
        true);

    public static IReadOnlyList<FamixaProviderDescriptor> All =>
        [Gemini, Runway, Wan, ElevenLabs, Fal];

    public static FamixaProviderDescriptor? Find(string? providerId) =>
        All.FirstOrDefault(d => d.ProviderId.Equals((providerId ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Legacy policy → provider map. Preference fallback only — not routing authority.</summary>
public static class FamixaProviderPolicyV1
{
    public static FamixaProviderResolveResult Map(FamixaProviderCapability capability, FamixaProviderPolicy policy) =>
        (capability, policy) switch
        {
            (FamixaProviderCapability.Motion, FamixaProviderPolicy.Economy) =>
                new(FamixaProviderIds.Wan, "wan-2.1"),
            (FamixaProviderCapability.LipSync, FamixaProviderPolicy.Premium) =>
                new(FamixaProviderIds.Fal, "v3"),
            (FamixaProviderCapability.LipSync, FamixaProviderPolicy.Economy) =>
                new(FamixaProviderIds.Fal, "ls"),
            (FamixaProviderCapability.Picture, _) =>
                new(FamixaProviderIds.Gemini, null),
            (FamixaProviderCapability.Motion, _) =>
                new(FamixaProviderIds.Runway, "gen4_turbo"),
            (FamixaProviderCapability.Voice, _) =>
                new(FamixaProviderIds.ElevenLabs, "eleven_v3"),
            (FamixaProviderCapability.LipSync, _) =>
                new(FamixaProviderIds.Fal, "1.9"),
            _ => throw new FamixaProviderException(
                FamixaProviderErrorCodes.Unsupported, "", "PROVIDER_UNAVAILABLE: capability chưa đăng ký."),
        };
}

public static class FamixaProviderResolver
{
    /// <summary>Series selection SoT is <see cref="FamixaProviderSelectionFoundation"/>. This returns ProviderId/ModelId only.</summary>
    public static FamixaProviderResolveResult Resolve(
        FamixaProviderCapability capability,
        FamixaProviderSelection? selection = null)
    {
        var decision = FamixaProviderSelectionFoundation.Select(
            FamixaProviderSelectionFoundation.From(capability, selection));
        return new(decision.ProviderId, decision.ModelId);
    }

    /// <summary>Legacy capability map used only inside Selection Foundation. Not a Series start entry point.</summary>
    public static FamixaProviderResolveResult ResolveLegacyMap(
        FamixaProviderCapability capability,
        FamixaProviderSelection? selection = null)
    {
        var s = selection ?? new FamixaProviderSelection();
        var explicitId = First(s.ExplicitProviderId);
        if (explicitId is not null)
        {
            var desc = FamixaProviderCatalog.Find(explicitId)
                ?? throw new FamixaProviderException(
                    FamixaProviderErrorCodes.Unsupported, explicitId, $"PROVIDER_UNAVAILABLE: {explicitId}.");
            if (!desc.Capabilities.Contains(capability))
                throw new FamixaProviderException(
                    FamixaProviderErrorCodes.Unsupported,
                    explicitId,
                    $"UNSUPPORTED: {explicitId} không có {capability}.");
            return new(desc.ProviderId, ModelFor(capability, desc.ProviderId, s));
        }

        if (capability == FamixaProviderCapability.Motion)
        {
            if (IsWanEngine(s.Engine))
                return new(FamixaProviderIds.Wan, "wan-2.1");
            if (!string.IsNullOrWhiteSpace(s.Engine))
                return new(FamixaProviderIds.Runway, "gen4_turbo");
        }

        if (capability == FamixaProviderCapability.LipSync && !string.IsNullOrWhiteSpace(s.LipsyncModel))
            return new(FamixaProviderIds.Fal, NormalizeLipsyncModel(s.LipsyncModel));

        if (capability == FamixaProviderCapability.Voice && !string.IsNullOrWhiteSpace(s.VoiceProvider))
        {
            var v = s.VoiceProvider.Trim();
            if (v.Equals("f5", StringComparison.OrdinalIgnoreCase))
                throw new FamixaProviderException(
                    FamixaProviderErrorCodes.Unsupported,
                    "f5",
                    "UNSUPPORTED: F5-TTS chưa có runtime.");
            if (!v.Equals(FamixaProviderIds.ElevenLabs, StringComparison.OrdinalIgnoreCase))
                throw new FamixaProviderException(
                    FamixaProviderErrorCodes.Unsupported, v, $"PROVIDER_UNAVAILABLE: {v}.");
        }

        var policy = s.Policy ?? FamixaProviderPolicy.Standard;
        return FamixaProviderPolicyV1.Map(capability, policy);
    }

    public static bool IsWanEngine(string? engine) =>
        string.Equals((engine ?? "").Trim(), "wan", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeLipsyncModel(string? raw)
    {
        var m = (raw ?? "").Trim().ToLowerInvariant();
        if (m is "v3" or "sync-lipsync-v3") return "v3";
        if (m is "ls" or "latentsync" or "latent") return "ls";
        return "1.9";
    }

    private static string? ModelFor(FamixaProviderCapability capability, string providerId, FamixaProviderSelection s)
    {
        if (capability == FamixaProviderCapability.LipSync)
            return NormalizeLipsyncModel(s.LipsyncModel);
        if (capability == FamixaProviderCapability.Motion && providerId == FamixaProviderIds.Wan)
            return "wan-2.1";
        if (capability == FamixaProviderCapability.Motion)
            return "gen4_turbo";
        if (capability == FamixaProviderCapability.Voice)
            return "eleven_v3";
        return null;
    }

    private static string? First(string? raw)
    {
        var t = (raw ?? "").Trim();
        return t.Length == 0 ? null : t;
    }
}

/// <summary>
/// Poll/Recover identity. Explicit providerId wins.
/// Prefix / no-prefix matching is LEGACY compatibility only — not canonical routing.
/// </summary>
public static class FamixaProviderTaskIdentity
{
    public static FamixaProviderTaskRef Resolve(
        string? taskId,
        string? providerId = null,
        FamixaProviderCapability? capability = null)
    {
        var explicitId = (providerId ?? "").Trim();
        if (explicitId.Length > 0)
        {
            var desc = FamixaProviderCatalog.Find(explicitId)
                ?? throw new FamixaProviderException(
                    FamixaProviderErrorCodes.Unsupported, explicitId, $"PROVIDER_UNAVAILABLE: {explicitId}.");
            var cap = capability ?? InferCapability(desc, taskId);
            if (!desc.Capabilities.Contains(cap))
                throw new FamixaProviderException(
                    FamixaProviderErrorCodes.Unsupported,
                    explicitId,
                    $"UNSUPPORTED: {explicitId} không có {cap}.");
            return new(desc.ProviderId, cap, FamixaProviderTaskSource.Explicit, ModelFor(desc.ProviderId, cap, taskId));
        }

        var raw = (taskId ?? "").Trim();
        if (IsLegacyLipsyncPrefix(raw))
            return new(FamixaProviderIds.Fal, FamixaProviderCapability.LipSync, FamixaProviderTaskSource.LegacyPrefix, LipsyncModelOf(raw));
        if (IsLegacyWanPrefix(raw))
            return new(FamixaProviderIds.Wan, FamixaProviderCapability.Motion, FamixaProviderTaskSource.LegacyPrefix, "wan-2.1");
        if (raw.Length == 0)
            throw new FamixaProviderException(
                FamixaProviderErrorCodes.Unsupported, "", "PROVIDER_UNAVAILABLE: thiếu task id.");
        if (capability == FamixaProviderCapability.LipSync)
            return new(FamixaProviderIds.Fal, FamixaProviderCapability.LipSync, FamixaProviderTaskSource.LegacyUnverified, "1.9");
        return new(
            FamixaProviderIds.Runway,
            FamixaProviderCapability.Motion,
            FamixaProviderTaskSource.LegacyUnverified,
            "gen4_turbo");
    }

    public static bool IsLegacy(FamixaProviderTaskSource source) =>
        source is FamixaProviderTaskSource.LegacyPrefix or FamixaProviderTaskSource.LegacyUnverified;

    public static bool CanHandle(string providerId, FamixaProviderCapability capability, string? taskId)
    {
        var raw = (taskId ?? "").Trim();
        if (raw.Length == 0) return false;
        var id = (providerId ?? "").Trim();
        if (capability == FamixaProviderCapability.LipSync)
            return IsLegacyLipsyncPrefix(raw) && id.Equals(FamixaProviderIds.Fal, StringComparison.OrdinalIgnoreCase);
        if (capability == FamixaProviderCapability.Motion && id.Equals(FamixaProviderIds.Wan, StringComparison.OrdinalIgnoreCase))
            return IsLegacyWanPrefix(raw);
        if (capability == FamixaProviderCapability.Motion && id.Equals(FamixaProviderIds.Runway, StringComparison.OrdinalIgnoreCase))
            return !IsLegacyWanPrefix(raw) && !IsLegacyLipsyncPrefix(raw);
        return false;
    }

    public static bool IsLegacyWanPrefix(string? taskId) =>
        (taskId ?? "").StartsWith("wan_", StringComparison.OrdinalIgnoreCase);

    public static bool IsLegacyLipsyncPrefix(string? taskId)
    {
        var t = taskId ?? "";
        return t.StartsWith("lipsync_", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("lipsync_v3_", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("lipsync_v1_", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("lipsync_ls_", StringComparison.OrdinalIgnoreCase);
    }

    public static string LipsyncModelOf(string? taskId)
    {
        var t = (taskId ?? "").Trim();
        if (t.StartsWith("lipsync_v3_", StringComparison.OrdinalIgnoreCase)) return "v3";
        if (t.StartsWith("lipsync_ls_", StringComparison.OrdinalIgnoreCase)) return "ls";
        return "1.9";
    }

    public static string MapVendorStatus(string? status) =>
        (status ?? "").Trim().ToUpperInvariant() switch
        {
            "SUCCESS" or "SUCCEEDED" or "COMPLETE" or "COMPLETED" => "SUCCEEDED",
            "FAILED" or "ERROR" or "CANCELLED" or "CANCELED" => "FAILED",
            "RUNNING" or "PROCESSING" or "IN_PROGRESS" => "RUNNING",
            _ => "PENDING",
        };

    public static FamixaProviderStatus ToProviderStatus(string? vendorStatus) =>
        MapVendorStatus(vendorStatus) switch
        {
            "SUCCEEDED" => FamixaProviderStatus.Succeeded,
            "FAILED" => FamixaProviderStatus.Failed,
            "RUNNING" => FamixaProviderStatus.Running,
            _ => FamixaProviderStatus.Pending,
        };

    public static string ToTurboStatus(FamixaProviderStatus status) =>
        status switch
        {
            FamixaProviderStatus.Succeeded => "SUCCEEDED",
            FamixaProviderStatus.Failed => "FAILED",
            FamixaProviderStatus.Running => "RUNNING",
            _ => "PENDING",
        };

    private static FamixaProviderCapability InferCapability(FamixaProviderDescriptor desc, string? taskId)
    {
        if (desc.Capabilities.Count == 1) return desc.Capabilities[0];
        if (IsLegacyLipsyncPrefix(taskId)) return FamixaProviderCapability.LipSync;
        return desc.Capabilities[0];
    }

    private static string? ModelFor(string providerId, FamixaProviderCapability capability, string? taskId)
    {
        if (capability == FamixaProviderCapability.LipSync)
            return LipsyncModelOf(taskId);
        if (providerId == FamixaProviderIds.Wan) return "wan-2.1";
        if (providerId == FamixaProviderIds.Runway) return "gen4_turbo";
        return null;
    }
}

public sealed record FamixaProviderCostQuote(
    string ProviderId,
    string? ModelId,
    decimal? EstimatedAmount,
    string? Currency,
    FamixaCostKind CostKind,
    string BillingBasis,
    string BillingUnit,
    double BilledQuantity,
    double MinimumBillableQuantity);

public static class FamixaProviderCostCatalog
{
    public const string BasisRunwayCreditPerSec = "RUNWAY_CREDIT_PER_SEC";
    public const string BasisFalBilledEstimate = "FAL_BILLED_ESTIMATE";
    public const string BasisFalUsdPerMin = "FAL_USD_PER_MIN";
    public const string BasisFalLatentSyncClip = "FAL_LATENTSYNC_CLIP";
    public const string BasisUnknown = "UNKNOWN";

    /// <summary>Adapter wrapper. Prefer <see cref="Quote"/> — this is not vendor actual.</summary>
    public static (FamixaCostKind Kind, decimal? Amount, string? Unit) Estimate(
        string providerId,
        string? modelId,
        double? seconds = null)
    {
        var quote = Quote(providerId, modelId, seconds);
        return (quote.CostKind, quote.EstimatedAmount, quote.Currency);
    }

    /// <summary>
    /// Cost SoT. Editorial duration is not a parameter — callers must pass provider/performance quantity.
    /// Never returns CostKind Actual. Wan amount is null (FAL_BILLED_ESTIMATE), never 0.
    /// </summary>
    public static FamixaProviderCostQuote Quote(
        string providerId,
        string? modelId,
        double? quantity = null)
    {
        var id = (providerId ?? "").Trim().ToLowerInvariant();
        var model = FamixaProviderResolver.NormalizeLipsyncModel(modelId);
        if (id == FamixaProviderIds.Runway)
        {
            var sec = quantity is >= 8 ? 10 : 5;
            return new FamixaProviderCostQuote(
                FamixaProviderIds.Runway,
                string.IsNullOrWhiteSpace(modelId) ? "gen4_turbo" : modelId,
                sec * 5,
                "credit",
                FamixaCostKind.Estimate,
                BasisRunwayCreditPerSec,
                "second",
                sec,
                5);
        }
        if (id == FamixaProviderIds.Wan)
        {
            var sec = quantity is > 0 ? quantity.Value : 5;
            return new FamixaProviderCostQuote(
                FamixaProviderIds.Wan,
                string.IsNullOrWhiteSpace(modelId) ? "wan-2.1" : modelId,
                null,
                "fal",
                FamixaCostKind.FalBilledEstimate,
                BasisFalBilledEstimate,
                "second",
                sec,
                5);
        }
        if (id == FamixaProviderIds.Fal)
        {
            var sec = Math.Max(5, quantity is > 0 ? quantity.Value : 5);
            if (model == "v3")
            {
                return new FamixaProviderCostQuote(
                    FamixaProviderIds.Fal,
                    "v3",
                    Math.Round((decimal)(sec / 60.0 * 8.0), 2),
                    "usd",
                    FamixaCostKind.Estimate,
                    BasisFalUsdPerMin,
                    "second",
                    sec,
                    5);
            }
            if (model == "ls")
            {
                return new FamixaProviderCostQuote(
                    FamixaProviderIds.Fal,
                    "ls",
                    sec <= 40 ? 0.20m : Math.Round((decimal)(sec * 0.005), 2),
                    "usd",
                    FamixaCostKind.Estimate,
                    BasisFalLatentSyncClip,
                    "clip",
                    sec,
                    5);
            }
            return new FamixaProviderCostQuote(
                FamixaProviderIds.Fal,
                "1.9",
                Math.Round((decimal)(sec / 60.0 * 0.70), 2),
                "usd",
                FamixaCostKind.Estimate,
                BasisFalUsdPerMin,
                "second",
                sec,
                5);
        }
        return new FamixaProviderCostQuote(
            id,
            modelId,
            null,
            null,
            FamixaCostKind.Unknown,
            BasisUnknown,
            "unknown",
            0,
            0);
    }
}

public static class FamixaProviderErrors
{
    public static FamixaProviderException Wrap(string providerId, Exception ex)
    {
        if (ex is FamixaProviderException already) return already;
        var message = ex.Message ?? "";
        return new FamixaProviderException(Classify(message), providerId, message, ex);
    }

    public static string Classify(string? message)
    {
        var m = message ?? "";
        if (m.Contains("401", StringComparison.OrdinalIgnoreCase)
            || m.Contains("403", StringComparison.OrdinalIgnoreCase)
            || m.Contains("key", StringComparison.OrdinalIgnoreCase)
            || m.Contains("chưa cấu hình", StringComparison.OrdinalIgnoreCase)
            || m.Contains("chưa có", StringComparison.OrdinalIgnoreCase))
            return FamixaProviderErrorCodes.AuthFailed;
        if (m.Contains("429", StringComparison.OrdinalIgnoreCase)
            || m.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || m.Contains("rate", StringComparison.OrdinalIgnoreCase)
            || m.Contains("402", StringComparison.OrdinalIgnoreCase))
            return FamixaProviderErrorCodes.RateLimited;
        if (m.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || m.Contains("504", StringComparison.OrdinalIgnoreCase))
            return FamixaProviderErrorCodes.Timeout;
        if (m.Contains("thiếu", StringComparison.OrdinalIgnoreCase)
            || m.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || m.Contains("không hợp lệ", StringComparison.OrdinalIgnoreCase))
            return FamixaProviderErrorCodes.InvalidInput;
        if (m.Contains("UNSUPPORTED", StringComparison.OrdinalIgnoreCase)
            || m.Contains("PROVIDER_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
            return FamixaProviderErrorCodes.Unsupported;
        return FamixaProviderErrorCodes.ProviderError;
    }
}
