using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Approved still is gemini-3.1-flash-image. The retired 2.5 model is not the catalog,
/// not a priced fallback, and not accepted for a new credential.
/// </summary>
public static class ProductionGemini31ImageReadinessV1Regression
{
    public const string SuiteId = "FAMIXA_GEMINI_31_IMAGE_READINESS_V1";
    const string Retired = "gemini-2.5-flash-image";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var model = ProductionImageCatalog.GeminiFlashImage;
        Ok(model == "gemini-3.1-flash-image", "catalog model");
        Ok(model != Retired, "catalog is not the retired model");
        Ok(new GeminiImageExecutionAdapter(null!).ModelId == model, "still adapter");
        Ok(new TenantGeminiImageExecutionAdapter(null!, null!).ModelId == model, "tenant adapter");
        Ok(FamixaProviderRouterPriority.DefaultModel(FamixaProviderCapability.Picture, FamixaProviderIds.Gemini) == model,
            "router picture default");
        Ok(FamixaProviderCatalog.Gemini.Models.Count == 1 && FamixaProviderCatalog.Gemini.Models[0] == model,
            "provider catalog has only the approved model");
        Ok(!FamixaProviderCatalog.Gemini.Models.Contains(Retired), "provider catalog does not list the retired model");

        try
        {
            var accepted = ProductionCredentialRegistrationRules.RequireCatalogModel(
                ProductionImageCatalog.GeminiProviderId, model);
            Ok(accepted.ModelId == model, "credential catalog accepts the approved model");
        }
        catch (Exception ex)
        {
            Ok(false, "credential catalog accepts the approved model: " + ex.Message);
        }

        var retiredRejected = false;
        try
        {
            ProductionCredentialRegistrationRules.RequireCatalogModel(ProductionImageCatalog.GeminiProviderId, Retired);
        }
        catch (InvalidOperationException ex)
        {
            retiredRejected = ex.Message == ProductionCredentialRegistrationRules.ModelRejected;
        }
        Ok(retiredRejected, "credential catalog rejects the retired model");

        var quote = FamixaProviderCostCatalog.Quote(FamixaProviderIds.Gemini, model);
        Ok(quote.CostKind == FamixaCostKind.Estimate && quote.EstimatedAmount == 0.045m
            && quote.BillingBasis == "PUBLISHED_LIST_0_5K", "approved model published-list estimate");
        var retiredQuote = FamixaProviderCostCatalog.Quote("GEMINI", Retired);
        Ok(retiredQuote.CostKind == FamixaCostKind.Unknown && retiredQuote.EstimatedAmount is null,
            "retired model is not priced");

        Ok(FamixaEp01UpstreamHold.BlocksMotion(FamixaEp01UpstreamHold.Sh01Keyframe), "SH01 keyframe hold");
        Ok(FamixaEp01UpstreamHold.BlocksMotion(FamixaEp01UpstreamHold.Sh02Keyframe), "SH02 keyframe hold");
        Ok(!FamixaEp01UpstreamHold.BlocksMotion(Guid.NewGuid()), "a new keyframe is not held");
        Ok(FamixaEp01UpstreamHold.BlocksLipsync(FamixaEp01UpstreamHold.Sh01Video), "SH01 video hold");
        Ok(FamixaEp01UpstreamHold.BlocksLipsync(FamixaEp01UpstreamHold.Sh02Video), "SH02 video hold");
        Ok(!FamixaEp01UpstreamHold.BlocksLipsync(Guid.NewGuid()), "a new video is not held");

        Ok(ProductionPaidStagePolicy.Decide(0, false, false, 0m, 0.067m) == ProductionPaidStagePolicy.Allow,
            "first attempt inside the ceiling");
        Ok(ProductionPaidStagePolicy.Decide(1, true, true, 0.067m, 0.067m) == ProductionPaidStagePolicy.Retry,
            "one corrected retry");
        Ok(ProductionPaidStagePolicy.Decide(1, true, false, 0.067m, 0.067m) == ProductionPaidStagePolicy.Blocked,
            "unchanged input is not retried");
        Ok(ProductionPaidStagePolicy.Decide(1, false, true, 0.067m, 0.067m) == ProductionPaidStagePolicy.Blocked,
            "unclassified failure is not retried");
        Ok(ProductionPaidStagePolicy.Decide(2, true, true, 1m, 0.045m) == ProductionPaidStagePolicy.Retry,
            "the third attempt is the last allowed");
        Ok(ProductionPaidStagePolicy.Decide(2, true, false, 1m, 0.045m) == ProductionPaidStagePolicy.Blocked,
            "an unchanged third attempt is blocked");
        Ok(ProductionPaidStagePolicy.Decide(3, true, true, 1m, 0.045m) == ProductionPaidStagePolicy.Blocked,
            "a fourth attempt is blocked");
        Ok(ProductionPaidStagePolicy.Cap("EP01-SC01-SH01", "CREATE_VIDEO") == 4
            && ProductionPaidStagePolicy.Cap("EP01-SC01-SH01", "CREATE_IMAGE") == 3
            && ProductionPaidStagePolicy.Cap("EP01-SC01-SH03", "CREATE_IMAGE") == 4
            && ProductionPaidStagePolicy.Cap("EP01-SC01-SH03", "CREATE_VIDEO") == 5
            && ProductionPaidStagePolicy.Cap("EP01-SC01-SH03", "CREATE_VOICE") == 3
            && ProductionPaidStagePolicy.Decide(4, true, true, 0m, 0m, 5) == ProductionPaidStagePolicy.Retry
            && ProductionPaidStagePolicy.Decide(5, true, true, 0m, 0m, 5) == ProductionPaidStagePolicy.Blocked
            && ProductionPaidStagePolicy.Decide(3, true, true, 0m, 0m, 4) == ProductionPaidStagePolicy.Retry
            && ProductionPaidStageLedger.Gate(3, false, true, true, 0m, 0m, 4) == ProductionPaidStagePolicy.Retry
            && ProductionPaidStagePolicy.Decide(4, true, true, 0m, 0m, 4) == ProductionPaidStagePolicy.Blocked,
            "the owner allowed one SH01 video quality test and a fifth stays blocked");
        Ok(ProductionPaidStageLedger.Gate(1, true, true, true, 0m, 0.045m) == ProductionFailureReasons.InFlightReplay,
            "an open attempt is not called again");
        Ok(ProductionPaidStageLedger.Gate(3, false, true, true, 0.09m, 0.045m) == ProductionFailureReasons.PaidStageExhausted,
            "three paid attempts end the stage");
        Ok(ProductionPaidStageLedger.IsProviderBalanceRefusal("Fal upload 403: User is locked. Reason: Exhausted balance.")
            && !ProductionPaidStageLedger.IsProviderBalanceRefusal("INTERNAL.BAD_OUTPUT.CODE01"),
            "a topped-up provider balance retries the same input");
        Ok(ProductionPaidStageLedger.ImageFingerprint("512", "CHAR-001:abc")
                != ProductionPaidStageLedger.ImageFingerprint("0.5K", "CHAR-001:abc"),
            "a corrected image size is a different input");
        Ok(ProductionPaidStageLedger.ImageFingerprint("512", "CHAR-001:abc", "strap on the shoulder")
                != ProductionPaidStageLedger.ImageFingerprint("512", "CHAR-001:abc", "bag held in the hand"),
            "a corrected acting line is a different input");
        Ok(FamixaEp01UpstreamHold.BlocksMotion(FamixaEp01UpstreamHold.Sh01KeyframeEpoch1),
            "the first epoch still is held");
        Ok(FamixaEp01UpstreamHold.BlocksLipsync(FamixaEp01UpstreamHold.Sh01VideoEpoch1),
            "the first epoch video is held");
        Ok(ProductionPaidStageLedger.HistoricalImageSize("os-ui:CREATE_IMAGE:EP01-SC01-SH01:0.5K:1", "image_size '0.5K'") == "0.5K",
            "the rejected size stays on the ledger");
        var epoch = ProductionPaidStagePolicy.CountFrom;
        Ok(!ProductionPaidStageLedger.CountsTowardCap(epoch.AddMinutes(-1), "runway", "gen4_turbo", true),
            "an API call before the new epoch is not counted");
        Ok(ProductionPaidStageLedger.CountsTowardCap(epoch, "runway", "gen4_turbo", true),
            "an API call at the epoch is counted");
        Ok(!ProductionPaidStageLedger.CountsTowardCap(epoch.AddMinutes(1), "import", "existing-video", true),
            "an imported file is not an API call");
        Ok(!ProductionPaidStageLedger.CountsTowardCap(epoch.AddMinutes(1), "fixture", "fixture-lipsync-v1", true),
            "a fixture is not an API call");
        Ok(ProductionPaidStageLedger.CountsTowardCap(epoch.AddMinutes(1), "GEMINI", "gemini-3.1-flash-image", true),
            "a Gemini call after the epoch is counted");
        Ok(ProductionPaidStagePolicy.Decide(0, false, false, 13.95m, 0.067m) == ProductionPaidStagePolicy.Blocked,
            "the ceiling blocks the next call");
        Ok(ProductionOsRules.ProviderTokens.Contains(model) && ProductionOsRules.ProviderTokens.Contains(Retired),
            "both model ids stay out of prompts");
        Ok(ProductionPromptCompilerRules.ProviderTokens.Contains(model), "compiler denylist includes the approved model");
        Ok(ProductionEventTypes.StageGate == "STAGE_GATE", "stage gate event type");
        Ok(ShortProductionQualityPipelineV1.Sh01FutureActing.Contains("Exactly two intact people", StringComparison.Ordinal)
            && ShortProductionQualityPipelineV1.Sh01FutureActing.Contains("Minh entering", StringComparison.Ordinal),
            "SH01 still contract");
        Ok(ShortProductionQualityPipelineV1.Sh02FutureActing.Contains("Minh is standing", StringComparison.Ordinal)
            && ShortProductionQualityPipelineV1.Sh02FutureActing.Contains("red mark is already visible", StringComparison.Ordinal),
            "SH02 still contract");
        Ok(ShortProductionQualityPipelineV1.Sh03RecordedLine == "Điểm thấp thì lần sau gỡ được mà.", "SH03 line is the approved sentence");

        return fail;
    }
}
