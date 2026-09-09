namespace KitPlatform.Packs.Content;

/// <summary>
/// Per-shot Director runtime cap. Not monthly ContentBudget. Not localStorage.
/// Does not change Router V1 semantics.
/// </summary>
public static class FamixaRuntimeMaxCost
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_RUNTIME_MAXCOST_V1";
    public const string InvalidNegative = "MAXCOST_INVALID";

    public static string? Validate(decimal? maxCost)
    {
        if (maxCost is null) return null;
        if (maxCost.Value < 0) return InvalidNegative;
        return null;
    }

    public static void EnsureValid(decimal? maxCost)
    {
        if (Validate(maxCost) is not null)
            throw new InvalidOperationException("MAXCOST_INVALID: MaxCost must be >= 0.");
    }

    public static FamixaProviderSelectionRequirements MotionRequirements(ContentSeriesTurboStartRequest request)
    {
        EnsureValid(request.MaxCost);
        return new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            MaxCost: request.MaxCost,
            Currency: request.Currency,
            DurationSec: request.Seconds,
            Engine: request.Engine);
    }
}
