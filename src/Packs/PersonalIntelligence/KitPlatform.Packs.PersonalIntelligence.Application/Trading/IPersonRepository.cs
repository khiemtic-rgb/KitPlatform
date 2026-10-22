namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Person repository — canonical PI identity anchor
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    /// Get Person by person_id
    /// </summary>
    Task<Person?> GetByIdAsync(
        Guid personId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Person by user_id and tenant_id
    /// </summary>
    Task<Person?> GetByUserAndTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create Person
    /// </summary>
    Task<Guid> CreateAsync(
        Guid userId,
        Guid tenantId,
        string? displayName,
        string timezone,
        Guid? createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if Person exists
    /// </summary>
    Task<bool> ExistsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
