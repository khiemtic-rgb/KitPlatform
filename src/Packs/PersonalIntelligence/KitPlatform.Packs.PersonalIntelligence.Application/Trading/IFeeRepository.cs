namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Fee repository — trading costs
/// </summary>
public interface IFeeRepository
{
    /// <summary>
    /// Get Fee by fee_id
    /// </summary>
    Task<Fee?> GetByIdAsync(
        Guid feeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Fee by external_fee_id (when present)
    /// </summary>
    Task<Fee?> GetByExternalIdAsync(
        Guid tradingAccountId,
        string externalFeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Fees for Person in time range (Q7: Total Fees for Period)
    /// </summary>
    Task<IReadOnlyList<Fee>> ListForPersonAsync(
        Guid personId,
        Guid tenantId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Fees for Trade
    /// </summary>
    Task<IReadOnlyList<Fee>> ListForTradeAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create Fee (idempotent on trading_account_id + external_fee_id when external_fee_id exists)
    /// </summary>
    Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        Guid tradingAccountId,
        Guid? tradeId,
        string feeType,
        decimal feeAmount,
        string feeCurrency,
        string? externalFeeId,
        DateTimeOffset incurredAt,
        string source,
        string sourceType,
        Guid? createdBy,
        CancellationToken cancellationToken = default);
}
