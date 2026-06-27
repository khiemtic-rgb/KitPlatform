namespace PharmaCore.Application.Catalog;

public interface ICatalogImportService
{
    Task<ProductImportResultDto> ImportProductsAsync(
        IReadOnlyList<ProductImportRowRequest> rows,
        CancellationToken cancellationToken = default);
}
