namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed class TradingLoopRequest
{
    public Guid PersonId { get; set; }
    public Guid TradingAccountId { get; set; }
    public string FixtureCsv { get; set; } = "";
    public string Thesis { get; set; } = "";
    public string? ExpectedOutcome { get; set; }
    public string? Invalidation { get; set; }
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Leverage { get; set; } = 1m;
    public decimal Confidence { get; set; }
    public decimal MaxNotional { get; set; }
    public decimal MaxLeverage { get; set; }
    public decimal MinConfidence { get; set; }
    public string? ApprovedBy { get; set; }
    public string ReflectionNote { get; set; } = "";
    public string PolicyRule { get; set; } = "";
    public Guid? RequestedTenantId { get; set; }
    public bool Live { get; set; }
    public string OperationKey { get; set; } = "";
    public Guid[]? TradeIds { get; set; }
}

public sealed class TradingOperationConflictException : Exception
{
    public TradingOperationConflictException()
        : base("The operation key was already used with a different payload.")
    {
    }
}

public sealed record TradingLoopResult(
    Guid LoopId,
    Guid DecisionId,
    string PolicyStatus,
    bool RiskAllowed,
    bool PaperExecuted,
    string? ExecutionMode,
    string Autonomy,
    string EvidenceBasis,
    decimal ReconstructedNetQuantity,
    int StoredTradeCount,
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> Unknowns,
    Guid TradingAccountId = default,
    Guid PersonId = default,
    string Symbol = "",
    string Side = "",
    decimal Quantity = 0,
    decimal Price = 0,
    IReadOnlyList<string>? RiskReasons = null);

public interface ITradingOperationalLoop
{
    Task<TradingLoopResult> RunFixtureAsync(TradingLoopRequest request, CancellationToken cancellationToken = default);
}
