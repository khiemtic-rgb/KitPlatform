namespace KitPlatform.Packs.FamilyOs;

/// <summary>Commercial packaging v1 — capability keys (not task counts).</summary>
public static class FamilyCapabilityCodes
{
    public const string CoreRoutine = "core_routine";
    public const string WeeklyInsight = "weekly_insight";
    public const string Timeline = "timeline";
    public const string BehaviorTwin = "behavior_twin";
    public const string AiSuggest = "ai_suggest";
    public const string BehaviorCoach = "behavior_coach";
    public const string ParentingCoach = "parenting_coach";
    public const string GrowthReport = "growth_report";
    public const string ScreenNegotiate = "screen_negotiate";
    public const string MonthlyLetter = "monthly_letter";
    public const string FamilyReplay = "family_replay";
    public const string ParentSuccessCheckin = "parent_success_checkin";
    public const string AiPlusDeep = "ai_plus_deep";
}

public static class FamilyPlanCodes
{
    /// <summary>Legacy SKU — maps to Plus capabilities.</summary>
    public const string StarterMonth = "starter_month";
    public const string PlusMonth = "plus_month";
    public const string FamilyProMonth = "family_pro_month";
    public const string FamilyAiPlusMonth = "family_ai_plus_month";
    public const string PlusYear = "plus_year";
    public const string FamilyProYear = "family_pro_year";
    public const string FamilyAiPlusYear = "family_ai_plus_year";
    public const string StarterTrial = "starter_trial";
    public const string Free = "free";
}

public static class FamilyPlanTiers
{
    public const string Free = "free";
    public const string Plus = "plus";
    public const string Pro = "pro";
    public const string AiPlus = "ai_plus";
}

public sealed record FamilyPlanCatalogItemDto(
    string PlanCode,
    string TierCode,
    string DisplayNameVi,
    string OutcomeNameVi,
    int AmountVnd,
    int IntervalDays,
    string? BlurbVi,
    bool IsHero,
    bool IsActive);

public sealed record FamilyCapabilityPackDto(
    Guid FamilyId,
    string PlanCode,
    string TierCode,
    string DisplayNameVi,
    string OutcomeNameVi,
    bool IsEntitled,
    string Status,
    /// <summary>Null = unlimited child members.</summary>
    int? MaxChildren,
    IReadOnlyList<string> Capabilities,
    string? RecommendedUpgradePlanCode,
    string? UpgradeHintVi);

public static class FamilyPlanCapabilityMatrix
{
    private static readonly string[] FreeCaps =
    [
        FamilyCapabilityCodes.CoreRoutine,
        FamilyCapabilityCodes.WeeklyInsight,
    ];

    private static readonly string[] PlusCaps =
    [
        FamilyCapabilityCodes.CoreRoutine,
        FamilyCapabilityCodes.WeeklyInsight,
        FamilyCapabilityCodes.Timeline,
        FamilyCapabilityCodes.BehaviorTwin,
        FamilyCapabilityCodes.AiSuggest,
    ];

    private static readonly string[] ProCaps =
    [
        FamilyCapabilityCodes.CoreRoutine,
        FamilyCapabilityCodes.WeeklyInsight,
        FamilyCapabilityCodes.Timeline,
        FamilyCapabilityCodes.BehaviorTwin,
        FamilyCapabilityCodes.AiSuggest,
        FamilyCapabilityCodes.BehaviorCoach,
        FamilyCapabilityCodes.ParentingCoach,
        FamilyCapabilityCodes.GrowthReport,
        FamilyCapabilityCodes.ScreenNegotiate,
        FamilyCapabilityCodes.MonthlyLetter,
        FamilyCapabilityCodes.FamilyReplay,
        FamilyCapabilityCodes.ParentSuccessCheckin,
    ];

    private static readonly string[] AiPlusCaps =
        ProCaps.Append(FamilyCapabilityCodes.AiPlusDeep).ToArray();

    public static string ResolveTier(string? planCode, bool isEntitled, string? status)
    {
        if (!isEntitled)
            return FamilyPlanTiers.Free;

        var code = (planCode ?? "").Trim().ToLowerInvariant();
        if (status is FamilySubscriptionStatuses.Trial
            || code is FamilyPlanCodes.StarterTrial)
            return FamilyPlanTiers.Pro; // trial shows hero value

        return code switch
        {
            FamilyPlanCodes.FamilyAiPlusMonth or FamilyPlanCodes.FamilyAiPlusYear => FamilyPlanTiers.AiPlus,
            FamilyPlanCodes.FamilyProMonth or FamilyPlanCodes.FamilyProYear => FamilyPlanTiers.Pro,
            FamilyPlanCodes.PlusMonth or FamilyPlanCodes.PlusYear or FamilyPlanCodes.StarterMonth => FamilyPlanTiers.Plus,
            _ => FamilyPlanTiers.Pro, // unknown entitled → treat as Pro (safe default for paid)
        };
    }

    public static IReadOnlyList<string> CapabilitiesForTier(string tier) =>
        tier switch
        {
            FamilyPlanTiers.AiPlus => AiPlusCaps,
            FamilyPlanTiers.Pro => ProCaps,
            FamilyPlanTiers.Plus => PlusCaps,
            _ => FreeCaps,
        };

    public static int? MaxChildrenForTier(string tier) =>
        tier switch
        {
            FamilyPlanTiers.Free => 1,
            FamilyPlanTiers.Plus => 2,
            _ => null,
        };

    public static string DisplayNameVi(string tier) =>
        tier switch
        {
            FamilyPlanTiers.AiPlus => "Family AI+",
            FamilyPlanTiers.Pro => "Family Pro",
            FamilyPlanTiers.Plus => "Plus",
            _ => "Free",
        };

    public static string OutcomeNameVi(string tier) =>
        tier switch
        {
            FamilyPlanTiers.AiPlus => "Đồng hành AI chuyên sâu",
            FamilyPlanTiers.Pro => "Family Peace Plan",
            FamilyPlanTiers.Plus => "Family Growth Plan",
            _ => "Trải nghiệm Famixa",
        };

    public static bool Has(string tier, string capability) =>
        CapabilitiesForTier(tier).Contains(capability, StringComparer.OrdinalIgnoreCase);

    public static string? RecommendedUpgrade(string tier) =>
        tier switch
        {
            FamilyPlanTiers.Free => FamilyPlanCodes.FamilyProMonth,
            FamilyPlanTiers.Plus => FamilyPlanCodes.FamilyProMonth,
            FamilyPlanTiers.Pro => FamilyPlanCodes.FamilyAiPlusMonth,
            _ => null,
        };

    public static string UpgradeHintVi(string tier) =>
        tier switch
        {
            FamilyPlanTiers.Free =>
                "Nâng Family Peace Plan (199.000đ) để mở Coach, ROP và thư tháng — định vị AI đồng hành, không phải app checklist.",
            FamilyPlanTiers.Plus =>
                "Nâng Family Pro để mở AI Parenting Coach, Growth Report và Letter/Replay.",
            FamilyPlanTiers.Pro =>
                "AI+ dành cho gia đình muốn đồng hành chuyên sâu hơn (sắp mở rộng).",
            _ => "Bạn đang ở tầng cao nhất hiện có.",
        };

    public static bool IsPaidCheckoutPlan(string planCode)
    {
        var code = (planCode ?? "").Trim().ToLowerInvariant();
        return code is FamilyPlanCodes.StarterMonth
            or FamilyPlanCodes.PlusMonth
            or FamilyPlanCodes.PlusYear
            or FamilyPlanCodes.FamilyProMonth
            or FamilyPlanCodes.FamilyProYear
            or FamilyPlanCodes.FamilyAiPlusMonth
            or FamilyPlanCodes.FamilyAiPlusYear;
    }
}
