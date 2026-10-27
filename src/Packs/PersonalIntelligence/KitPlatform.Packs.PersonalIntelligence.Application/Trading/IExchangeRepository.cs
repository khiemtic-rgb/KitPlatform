namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Exchange repository — trading venue identity
/// </summary>
public interface IExchangeRepository
{
    /// <summary>
    /// Get Exchange by exchange_id
    /// </summary>
    Task<Exchange?> GetByIdAsync(
        Guid exchangeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Exchange by tenant_id and exchange_code (idempotency lookup)
    /// </summary>
    Task<Exchange?> GetByCodeAsync(
        Guid tenantId,
        string exchangeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Exchanges for tenant
    /// </summary>
    Task<IReadOnlyList<Exchange>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create Exchange (idempotent on tenant_id + exchange_code)
    /// </summary>
    Task<Guid> CreateAsync(
        Guid tenantId,
        string exchangeCode,
        string exchangeName,
        string? exchangeType,
        string? externalProvider,
        Guid? createdBy,
        CancellationToken cancellationToken = default);
}
