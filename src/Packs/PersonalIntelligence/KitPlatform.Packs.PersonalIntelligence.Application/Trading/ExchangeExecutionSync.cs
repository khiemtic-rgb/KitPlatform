namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public static class TradeSource
{
    public const string Fixture = "FIXTURE";
    public const string Import = "IMPORT";
    public const string Exchange = "EXCHANGE";
}

public sealed class ExchangeExecutionSyncRequest
{
    public string? Category { get; set; }
    public DateTimeOffset? Start { get; set; }
    public DateTimeOffset? End { get; set; }
}

public sealed class ExchangeExecutionIssueDto
{
    public string? ExecId { get; set; }
    public string? ExecType { get; set; }
    public string Reason { get; set; } = "";
    public List<string> MissingFields { get; set; } = [];
}

public sealed class ExchangeExecutionSyncResult
{
    public bool Committed { get; set; }
    public Guid? SyncId { get; set; }
    public Guid? TradingAccountId { get; set; }
    public string ExchangeCode { get; set; } = "BYBIT";
    public string Environment { get; set; } = "";
    public string Category { get; set; } = "";
    public string Source { get; set; } = TradeSource.Exchange;
    public DateTimeOffset RequestedStart { get; set; }
    public DateTimeOffset RequestedEnd { get; set; }
    public DateTimeOffset? EarliestExecAt { get; set; }
    public DateTimeOffset? LatestExecAt { get; set; }
    public DateTimeOffset? ResumeBefore { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public int Pages { get; set; }
    public int PageLimit { get; set; }
    public int FetchedCount { get; set; }
    public int InsertedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int UnmappedCount { get; set; }
    public bool Truncated { get; set; }
    public List<string> Notes { get; set; } = [];
    public List<string> MissingFields { get; set; } = [];
    public List<ExchangeExecutionIssueDto> Issues { get; set; } = [];
    public List<Guid> TradeIds { get; set; } = [];
}

public sealed class ExchangeExecutionPageDto
{
    public string? NextPageCursor { get; set; }
    public List<ExchangeExecutionItemDto> Items { get; set; } = [];
}

public sealed class ExchangeExecutionItemDto
{
    public bool Mappable { get; set; }
    public string? ExecId { get; set; }
    public string? OrderId { get; set; }
    public string? Symbol { get; set; }
    public string? Side { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? TotalValue { get; set; }
    public string TotalValueBasis { get; set; } = "none";
    public decimal? FeeAmount { get; set; }
    public string? FeeCurrency { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public string? ExecType { get; set; }
    public string? IsMaker { get; set; }
    public string? FeeRate { get; set; }
    public string? ClosedSize { get; set; }
    public string ExternalTradeId { get; set; } = "";
    public string Reason { get; set; } = "";
    public List<string> MissingFields { get; set; } = [];
    public string? ExecPriceText { get; set; }
    public string? ExecQtyText { get; set; }
    public string? ExecFeeText { get; set; }
    public string? ExecValueText { get; set; }
    public string? MarketType { get; set; }
    public string? ProductSubtype { get; set; }
    public string? RealizedPnl { get; set; }
    public string? PositionSide { get; set; }
    public string? QuantityUnit { get; set; }
    public string? SourceEndpoint { get; set; }
}

public interface IExchangeExecutionSync
{
    Task<ExchangeExecutionSyncResult> PreviewAsync(Guid connectionId, ExchangeExecutionSyncRequest request, CancellationToken cancellationToken);
    Task<ExchangeExecutionSyncResult> CommitAsync(Guid connectionId, ExchangeExecutionSyncRequest request, CancellationToken cancellationToken);
    Task<ExchangeExecutionSyncResult?> LatestAsync(Guid connectionId, CancellationToken cancellationToken);
}
