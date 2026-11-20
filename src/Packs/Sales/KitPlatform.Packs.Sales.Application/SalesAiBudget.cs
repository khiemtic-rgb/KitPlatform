namespace KitPlatform.Packs.Sales;

/// <summary>
/// Weekly token ceiling for KIT Sales AI. Budget 0 means AI is off; a call is refused
/// unless its pessimistic reserve still fits under the ceiling.
/// </summary>
public static class SalesAiBudget
{
    public const string ConfigKey = "KitSales:Ai:WeeklyTokenBudget";
    public const string AgentCode = "ai_voice";
    public const string DefaultModel = "gemini-flash-latest";
    public const int WindowDays = 7;
    public const long OutputReserve = 1024;

    public static string? Deny(string? apiKey, long weeklyTokenBudget, long usedTokens, long reserveTokens)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "no_key";
        if (weeklyTokenBudget <= 0)
            return "budget_zero";
        if (usedTokens < 0 || reserveTokens <= 0 || usedTokens + reserveTokens > weeklyTokenBudget)
            return "budget_exhausted";
        return null;
    }

    public static long Reserve(string system, string user) =>
        (system.Length + user.Length + 2) / 3 + OutputReserve;

    public static long Charge(long? reportedTotal, long reserve) =>
        reportedTotal is > 0 ? reportedTotal.Value : reserve;
}
