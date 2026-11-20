namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Confirms the caller-owned target build contains the shot.
/// A historical NULL-tenant build is rejected.
/// </summary>
public interface IProductionTargetShotCheck
{
    Task RequireAsync(Guid buildId, string shotCode, CancellationToken ct = default);
}
