namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// PortfolioSnapshot repository — person-wide portfolio state at a point in time
/// </summary>
public interface IPortfolioSnapshotRepository
{
    /// <summary>
    /// Get PortfolioSnapshot by portfolio_snapshot_id
    /// </summary>
    Task<PortfolioSnapshot?> GetByIdAsync(
        Guid portfolioSnapshotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get PortfolioSnapshot by idempotency key (person_id, as_of_at)
    /// </summary>
    Task<PortfolioSnapshot?> GetByKeyAsync(
        Guid personId,
        DateTimeOffset asOfAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List PortfolioSnapshot timeline for Person (Q5: Portfolio Timeline)
    /// </summary>
    Task<IReadOnlyList<PortfolioSnapshot>> ListTimelineAsync(
        Guid personId,
        Guid tenantId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get latest PortfolioSnapshot for Person
    /// </summary>
    Task<PortfolioSnapshot?> GetLatestAsync(
        Guid personId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create PortfolioSnapshot (idempotent on person_id + as_of_at)
    /// </summary>
    Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        decimal? totalValue,
        string baseCurrency,
        decimal? cashBalance,
        decimal? positionsValue,
        decimal? unrealizedPnl,
        decimal? realizedPnlInception,
        DateTimeOffset asOfAt,
        string source,
        string sourceType,
        bool isObserved,
        string? externalSnapshotId,
        Guid? createdBy,
        CancellationToken cancellationToken = default);
}
