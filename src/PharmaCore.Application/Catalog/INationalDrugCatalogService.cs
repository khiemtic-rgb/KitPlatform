namespace PharmaCore.Application.Catalog;

public interface INationalDrugCatalogService
{
    Task<NationalDrugConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<NationalDrugFieldMapDto> GetFieldMap();

    Task<PagedNationalDrugListResult> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<NationalDrugDetailDto?> GetAsync(string drugId, CancellationToken cancellationToken = default);

    Task<NationalDrugProductPrefillDto?> BuildProductPrefillAsync(
        string drugId,
        CancellationToken cancellationToken = default);
}
