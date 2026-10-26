namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Trade repository — completed transactions (core Trading Ledger)
/// </summary>
public interface ITradeRepository
{
    /// <summary>
    /// Get Trade by trade_id
    /// </summary>
    Task<Trade?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Trade by idempotency key (trading_account_id, external_trade_id)
    /// </summary>
    Task<Trade?> GetByExternalIdAsync(
        Guid tradingAccountId,
        string externalTradeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Trades for Person (Q1: Trading History for Person)
    /// </summary>
    Task<IReadOnlyList<Trade>> ListForPersonAsync(
        Guid personId,
        Guid tenantId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Trades by symbol for Person (Q2: Trades by Symbol)
    /// </summary>
    Task<IReadOnlyList<Trade>> ListBySymbolAsync(
        Guid personId,
        Guid tenantId,
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Trades for TradingAccount (Q6: Account Trading Activity)
    /// </summary>
    Task<IReadOnlyList<Trade>> ListForAccountAsync(
        Guid tradingAccountId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create Trade (idempotent on trading_account_id + external_trade_id)
    /// </summary>
    Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        Guid tradingAccountId,
        string externalTradeId,
        string symbol,
        string side,
        decimal quantity,
        decimal price,
        decimal? totalValue,
        decimal? feeAmount,
        string? feeCurrency,
        DateTimeOffset executedAt,
        string source,
        string sourceType,
        bool isObserved,
        Guid? correctsTradeId,
        Guid? orderId,
        Guid? createdBy,
        CancellationToken cancellationToken = default);
}
