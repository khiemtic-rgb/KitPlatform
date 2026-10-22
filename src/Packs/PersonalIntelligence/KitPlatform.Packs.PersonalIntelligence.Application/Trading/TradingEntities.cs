namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Person — canonical PI identity anchor (references Core Identity)
/// Schema: pack_personal_intelligence.person
/// </summary>
public sealed record Person(
    Guid PersonId,
    Guid UserId,
    Guid TenantId,
    string? DisplayName,
    string Timezone,
    int RowVersion,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy,
    DateTimeOffset? DeletedAt);

/// <summary>
/// Exchange — trading venue identity (e.g., Binance, Coinbase)
/// Schema: pack_personal_intelligence.exchange
/// </summary>
public sealed record Exchange(
    Guid ExchangeId,
    Guid TenantId,
    string ExchangeCode,
    string ExchangeName,
    string? ExchangeType,
    string? ExternalProvider,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);

/// <summary>
/// TradingAccount — Person's account at an Exchange
/// Schema: pack_personal_intelligence.trading_account
/// </summary>
public sealed record TradingAccount(
    Guid TradingAccountId,
    Guid PersonId,
    Guid TenantId,
    Guid ExchangeId,
    string ExternalAccountId,
    string? AccountName,
    string? AccountType,
    string Status,
    int RowVersion,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy,
    DateTimeOffset? DeletedAt);

/// <summary>
/// Trade — completed transaction (core Trading Ledger fact)
/// Schema: pack_personal_intelligence.trade
/// </summary>
public sealed record Trade(
    Guid TradeId,
    Guid PersonId,
    Guid TenantId,
    Guid TradingAccountId,
    string ExternalTradeId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    decimal? TotalValue,
    decimal? FeeAmount,
    string? FeeCurrency,
    DateTimeOffset ExecutedAt,
    DateTimeOffset RecordedAt,
    string Source,
    string SourceType,
    bool IsObserved,
    Guid? CorrectsTradeId,
    Guid? OrderId,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);

/// <summary>
/// Fee — trading costs (may be embedded in Trade or separate)
/// Schema: pack_personal_intelligence.fee
/// </summary>
public sealed record Fee(
    Guid FeeId,
    Guid PersonId,
    Guid TenantId,
    Guid TradingAccountId,
    Guid? TradeId,
    string FeeType,
    decimal FeeAmount,
    string FeeCurrency,
    string? ExternalFeeId,
    DateTimeOffset IncurredAt,
    DateTimeOffset RecordedAt,
    string Source,
    string SourceType,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);

/// <summary>
/// Position — holdings at a point in time
/// Schema: pack_personal_intelligence.position
/// </summary>
public sealed record Position(
    Guid PositionId,
    Guid PersonId,
    Guid TenantId,
    Guid TradingAccountId,
    string Symbol,
    decimal Quantity,
    decimal? CostBasis,
    decimal? MarketValue,
    decimal? UnrealizedPnl,
    DateTimeOffset AsOfAt,
    DateTimeOffset RecordedAt,
    string Source,
    string SourceType,
    bool IsObserved,
    string? ExternalPositionId,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);

/// <summary>
/// PortfolioSnapshot — person-wide portfolio state at a point in time
/// Schema: pack_personal_intelligence.portfolio_snapshot
/// </summary>
public sealed record PortfolioSnapshot(
    Guid PortfolioSnapshotId,
    Guid PersonId,
    Guid TenantId,
    decimal? TotalValue,
    string BaseCurrency,
    decimal? CashBalance,
    decimal? PositionsValue,
    decimal? UnrealizedPnl,
    decimal? RealizedPnlInception,
    DateTimeOffset AsOfAt,
    DateTimeOffset RecordedAt,
    string Source,
    string SourceType,
    bool IsObserved,
    string? ExternalSnapshotId,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);
