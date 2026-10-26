namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Command: RecordTrade — create Trade ledger entry
/// </summary>
public sealed record RecordTradeCommand(
    Guid PersonId,
    Guid TenantId,
    Guid TradingAccountId,
    string ExternalTradeId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    decimal? FeeAmount,
    string? FeeCurrency,
    DateTimeOffset ExecutedAt,
    DateTimeOffset RecordedAt,
    string SourceType,
    bool IsObserved,
    Guid? CorrectsTradeId,
    Guid? CreatedBy);

public sealed record RecordTradeResult(
    Guid TradeId);

/// <summary>
/// Command: RecordFee — create Fee ledger entry
/// </summary>
public sealed record RecordFeeCommand(
    Guid PersonId,
    Guid TenantId,
    Guid TradingAccountId,
    Guid? TradeId,
    string? ExternalFeeId,
    string FeeType,
    decimal FeeAmount,
    string FeeCurrency,
    DateTimeOffset IncurredAt,
    DateTimeOffset RecordedAt,
    string SourceType,
    Guid? CreatedBy);

public sealed record RecordFeeResult(
    Guid FeeId);
