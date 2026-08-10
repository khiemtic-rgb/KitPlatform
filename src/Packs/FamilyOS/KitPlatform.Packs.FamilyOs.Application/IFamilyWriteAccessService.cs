namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Blocks mutate APIs when the signed-in user is linked as a viewer
/// (GTM demo house / community browse).
/// </summary>
public interface IFamilyWriteAccessService
{
    Task EnsureCanMutateAsync(Guid familyId, CancellationToken cancellationToken = default);
}
