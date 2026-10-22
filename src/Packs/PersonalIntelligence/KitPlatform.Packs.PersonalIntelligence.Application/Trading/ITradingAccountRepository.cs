namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// TradingAccount repository — Person's account at an Exchange
/// </summary>
public interface ITradingAccountRepository
{
    /// <summary>
    /// Get TradingAccount by trading_account_id
    /// </summary>
    Task<TradingAccount?> GetByIdAsync(
        Guid tradingAccountId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get TradingAccount by idempotency key (person_id, exchange_id, external_account_id)
    /// </summary>
    Task<TradingAccount?> GetByExternalIdAsync(
        Guid personId,
        Guid exchangeId,
        string externalAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List TradingAccounts for Person
    /// </summary>
    Task<IReadOnlyList<TradingAccount>> ListForPersonAsync(
        Guid personId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create TradingAccount (idempotent on person_id + exchange_id + external_account_id)
    /// </summary>
    Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        Guid exchangeId,
        string externalAccountId,
        string? accountName,
        string? accountType,
        Guid? createdBy,
        CancellationToken cancellationToken = default);
}
