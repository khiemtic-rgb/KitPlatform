using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectOrgProfileService : IConnectOrgProfileService
{
    private readonly ConnectOrgProfileRepository _repo;

    public ConnectOrgProfileService(ConnectOrgProfileRepository repo) => _repo = repo;

    public Task<ConnectOrgProfileDto?> GetMyProfileAsync(CancellationToken cancellationToken = default) =>
        _repo.GetMyProfileAsync(cancellationToken);

    public Task<string?> GetOrgKindAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _repo.GetOrgKindAsync(tenantId, cancellationToken);
}
