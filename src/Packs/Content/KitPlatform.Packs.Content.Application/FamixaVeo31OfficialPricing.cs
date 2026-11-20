namespace KitPlatform.Packs.Content;

/// <summary>
/// Official Google Cloud Veo 3.1 per-count pricing. Not a payment. Not a scraper.
/// As-of 2026-09-22. Duration is not a multiplier.
/// </summary>
public sealed record FamixaVeoCostVariant(
    string GenerationMode,
    string Resolution,
    bool GenerateAudio,
    int SampleCount);

public sealed record FamixaProviderCostProvenance(
    string SourceName,
    string SourceType,
    string SourceReference,
    string RetrievedOn,
    string PricingBasis,
    string ApplicableVariant,
    string EffectiveVersion,
    string Validity);

public static class FamixaVeo31OfficialPricing
{
    public const string ProviderId = FamixaProviderIds.Google;
    public const string ModelId = "veo-3.1";
    public const string VertexModel = "veo-3.1-generate-001";
    public const string SourceName = "Google Cloud";
    public const string SourceType = "OFFICIAL_PROVIDER_PRICING";
    public const string SourceReference = "Google Cloud Agent Platform / Generative AI pricing";
    public const string RetrievedOn = "2026-09-22";
    public const string PricingBasis = FamixaProviderCostCatalog.BasisGoogleVeo31PerCount;
    public const string Currency = "USD";
    public const string Unit = "count";
    public const string VideoOnly = "VIDEO_ONLY";
    public const string VideoAudio = "VIDEO_AUDIO";
    public const string Res720p = "720p";
    public const string Res1080p = "1080p";
    public const string Res4k = "4k";
    public const string VariantVideoOnly720p = "VEO_3_1_VIDEO_ONLY_720P";
    public const string VariantVideoAudio720p = "VEO_3_1_VIDEO_AUDIO_720P";
    public const string VariantVideoOnly1080p = "VEO_3_1_VIDEO_ONLY_1080P";
    public const string VariantVideoAudio1080p = "VEO_3_1_VIDEO_AUDIO_1080P";
    public const string VariantVideoOnly4k = "VEO_3_1_VIDEO_ONLY_4K";
    public const string VariantVideoAudio4k = "VEO_3_1_VIDEO_AUDIO_4K";
    public const decimal VideoOnly720Or1080Usd = 0.20m;
    public const decimal VideoAudio720Or1080Usd = 0.40m;
    public const decimal VideoOnly4kUsd = 0.40m;
    public const decimal VideoAudio4kUsd = 0.60m;

    public static FamixaVeoCostVariant ProductionDefault { get; } =
        new(VideoOnly, Res720p, GenerateAudio: false, SampleCount: 1);

    public static FamixaVeoCostVariant VideoOnly720p(int sampleCount = 1) =>
        new(VideoOnly, Res720p, false, sampleCount);

    public static FamixaVeoCostVariant VideoAudio720p(int sampleCount = 1) =>
        new(VideoAudio, Res720p, true, sampleCount);

    public static FamixaVeoCostVariant VideoOnly4k(int sampleCount = 1) =>
        new(VideoOnly, Res4k, false, sampleCount);

    public static FamixaVeoCostVariant VideoAudio4k(int sampleCount = 1) =>
        new(VideoAudio, Res4k, true, sampleCount);

    public static FamixaProviderCostQuote Quote(FamixaVeoCostVariant? variant = null)
    {
        var requested = variant ?? ProductionDefault;
        if (!TryUnitPrice(requested, out var unit, out var variantId))
            return Unknown(requested);
        if (requested.SampleCount < 1)
            return Unknown(requested);

        var amount = unit * requested.SampleCount;
        var provenance = new FamixaProviderCostProvenance(
            SourceName,
            SourceType,
            SourceReference,
            RetrievedOn,
            PricingBasis,
            variantId,
            RetrievedOn,
            "as-of:" + RetrievedOn);
        return new FamixaProviderCostQuote(
            ProviderId,
            ModelId,
            amount,
            Currency,
            FamixaCostKind.Known,
            PricingBasis,
            Unit,
            requested.SampleCount,
            1,
            provenance,
            requested);
    }

    public static bool TryUnitPrice(
        FamixaVeoCostVariant variant,
        out decimal unitUsd,
        out string applicableVariant)
    {
        unitUsd = 0;
        applicableVariant = "";
        var res = NormalizeResolution(variant.Resolution);
        var mode = (variant.GenerationMode ?? "").Trim().ToUpperInvariant();
        var audio = variant.GenerateAudio;
        if (audio && mode is not ("" or VideoAudio))
            return false;
        if (!audio && mode is not ("" or VideoOnly))
            return false;

        if (!audio && res == Res720p)
        {
            unitUsd = VideoOnly720Or1080Usd;
            applicableVariant = VariantVideoOnly720p;
            return true;
        }
        if (audio && res == Res720p)
        {
            unitUsd = VideoAudio720Or1080Usd;
            applicableVariant = VariantVideoAudio720p;
            return true;
        }
        if (!audio && res == Res1080p)
        {
            unitUsd = VideoOnly720Or1080Usd;
            applicableVariant = VariantVideoOnly1080p;
            return true;
        }
        if (audio && res == Res1080p)
        {
            unitUsd = VideoAudio720Or1080Usd;
            applicableVariant = VariantVideoAudio1080p;
            return true;
        }
        if (!audio && res == Res4k)
        {
            unitUsd = VideoOnly4kUsd;
            applicableVariant = VariantVideoOnly4k;
            return true;
        }
        if (audio && res == Res4k)
        {
            unitUsd = VideoAudio4kUsd;
            applicableVariant = VariantVideoAudio4k;
            return true;
        }
        return false;
    }

    public static string NormalizeResolution(string? raw)
    {
        var r = (raw ?? "").Trim().ToLowerInvariant();
        if (r is "720" or "720p") return Res720p;
        if (r is "1080" or "1080p") return Res1080p;
        if (r is "4k" or "2160" or "2160p") return Res4k;
        return r;
    }

    static FamixaProviderCostQuote Unknown(FamixaVeoCostVariant requested) =>
        new(
            ProviderId,
            ModelId,
            null,
            null,
            FamixaCostKind.Unknown,
            FamixaProviderCostCatalog.BasisUnknown,
            "unknown",
            0,
            0,
            null,
            requested);
}
