namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Position repository — holdings at a point in time
/// </summary>
public interface IPositionRepository
{
    /// <summary>
    /// Get Position by position_id
    /// </summary>
    Task<Position?> GetByIdAsync(
        Guid positionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Position by idempotency key (trading_account_id, symbol, as_of_at)
    /// </summary>
    Task<Position?> GetByKeyAsync(
        Guid tradingAccountId,
        string symbol,
        DateTimeOffset asOfAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current Positions for Person (Q3: Current Positions for Person)
    /// Returns positions at the latest as_of_at timestamp for each symbol
    /// </summary>
    Task<IReadOnlyList<Position>> GetCurrentForPersonAsync(
        Guid personId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Position history for symbol (Q4: Position History for Symbol)
    /// </summary>
    Task<IReadOnlyList<Position>> ListHistoryForSymbolAsync(
        Guid personId,
        Guid tenantId,
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create Position (idempotent on trading_account_id + symbol + as_of_at)
    /// </summary>
    Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        Guid tradingAccountId,
        string symbol,
        decimal quantity,
        decimal? costBasis,
        decimal? marketValue,
        decimal? unrealizedPnl,
        DateTimeOffset asOfAt,
        string source,
        string sourceType,
        bool isObserved,
        string? externalPositionId,
        Guid? createdBy,
        CancellationToken cancellationToken = default);
}
