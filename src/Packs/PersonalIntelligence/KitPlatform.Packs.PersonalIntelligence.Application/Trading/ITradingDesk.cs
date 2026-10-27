namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record TradingPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record TradingSummaryDto(
    int TradeCount,
    int FixtureTradeCount,
    int OtherSourceTradeCount,
    int DecisionCount,
    int PaperExecutionCount,
    int RiskVetoCount,
    string? LatestSource,
    DateTimeOffset? LatestRecordedAt,
    bool OnlyFixtureData,
    IReadOnlyList<string> MissingData,
    int ImportTradeCount,
    int ExchangeTradeCount);

public sealed class TradingAccountDto
{
    public Guid TradingAccountId { get; set; }
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = "";
    public string ExternalAccountId { get; set; } = "";
    public string ExchangeCode { get; set; } = "";
    public string ExchangeName { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed record TradeListQuery(
    int Page,
    int PageSize,
    Guid? TradingAccountId,
    string? Symbol,
    string? Side,
    string? Source,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? MarketType = null,
    string? ProductSubtype = null);

public sealed class TradeRowDto
{
    public Guid TradeId { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public string ExchangeCode { get; set; } = "";
    public string ExternalAccountId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? FeeCurrency { get; set; }
    public string Source { get; set; } = "";
    public string SourceType { get; set; } = "";
    public bool IsObserved { get; set; }
    public string ExternalTradeId { get; set; } = "";
    public string? Category { get; set; }
    public string? MarketType { get; set; }
    public string? ProductSubtype { get; set; }
    public string? RealizedPnl { get; set; }
}

public sealed record DecisionRowDto(
    Guid DecisionId,
    string Thesis,
    decimal Confidence,
    string EpistemicKind,
    string Status,
    bool? RiskAllowed,
    bool? PaperExecuted,
    string? OperationKey);

public sealed record DecisionDetailDto(
    DecisionRowDto Decision,
    string? ContextSummary,
    string? ContextEpistemicKind,
    string? ContextSource,
    IReadOnlyList<EvidenceRowDto> Evidence,
    string? IntendedAction,
    string? ActualAction,
    string? ActionStatus,
    string? OutcomeSummary,
    string? ReflectionNote,
    string? ExperienceStatement,
    string? ExperienceStatus,
    string? PolicyStatus,
    string? PolicyRule,
    IReadOnlyList<string> Unknowns,
    int PaperExecutionCount = 0);

public sealed record EvidenceRowDto(string SourceRef, string EpistemicKind);

public sealed record OperationRowDto(
    Guid OperationId,
    string OperationKey,
    Guid? DecisionId,
    DateTimeOffset CreatedAt,
    bool? RiskAllowed,
    bool? PaperExecuted,
    string? ExecutionMode,
    string? Autonomy,
    string? EvidenceBasis,
    IReadOnlyList<string> Unknowns);

public sealed class PaperExecutionRowDto
{
    public Guid ExecutionId { get; set; }
    public Guid? DecisionId { get; set; }
    public string Mode { get; set; } = "";
    public string? Symbol { get; set; }
    public string? Side { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public string OrderStatus { get; set; } = "";
    public DateTimeOffset ExecutedAt { get; set; }
}

public sealed record TradeImportRequest(Guid TradingAccountId, string Csv, string Source);

public sealed record TradePreviewRowDto(
    string ExternalTradeId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    decimal? FeeAmount,
    string? FeeCurrency);

public sealed record TradeImportPreviewDto(
    int AcceptedCount,
    int DuplicateCount,
    IReadOnlyList<string> DuplicateExternalIds,
    IReadOnlyList<TradePreviewRowDto> Rows);

public sealed record TradeImportCommitDto(int InsertedCount, int DuplicateCount);

public sealed record PaperApprovalDto(Guid DecisionId, Guid ExecutionId, string Mode, bool AlreadyRecorded);

public sealed record PaperRejectionDto(Guid DecisionId, string Status, int ExecutionCount, bool AlreadyRejected);

public interface ITradingDesk
{
    Task<TradingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default);
    Task<TradingPage<TradeRowDto>> ListTradesAsync(TradeListQuery query, CancellationToken cancellationToken = default);
    Task<TradeRowDto?> GetTradeAsync(Guid tradeId, CancellationToken cancellationToken = default);
    Task<TradingPage<DecisionRowDto>> ListDecisionsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<DecisionDetailDto?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default);
    Task<TradingPage<OperationRowDto>> ListOperationsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OperationRowDto?> GetOperationAsync(string operationKey, CancellationToken cancellationToken = default);
    Task<TradingPage<PaperExecutionRowDto>> ListPaperExecutionsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TradeImportPreviewDto> PreviewImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default);
    Task<TradeImportCommitDto> CommitImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default);
    Task<PaperApprovalDto> ApprovePaperAsync(Guid decisionId, CancellationToken cancellationToken = default);
    Task<PaperRejectionDto> RejectPaperAsync(Guid decisionId, CancellationToken cancellationToken = default);
}
