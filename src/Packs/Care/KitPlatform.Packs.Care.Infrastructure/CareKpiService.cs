using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareKpiService : ICareKpiService
{
    private readonly CareKpiRepository _repo;

    public CareKpiService(CareKpiRepository repo) => _repo = repo;

    public Task<IReadOnlyList<CareKpiDefinitionDto>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListDefinitionsAsync(cancellationToken);
}
