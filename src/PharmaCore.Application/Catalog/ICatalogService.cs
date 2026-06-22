using PharmaCore.Application.Catalog;

namespace PharmaCore.Application.Catalog;

public interface ICatalogService
{
    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductListFilter filter, CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> GetNextProductCodeAsync(CancellationToken cancellationToken = default);
    Task<ProductDetailDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> SyncProductCommercialAsync(Guid id, SyncProductCommercialRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteProductsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
    Task<ProductBarcodeDto?> AddBarcodeAsync(Guid productId, CreateBarcodeRequest request, CancellationToken cancellationToken = default);
    Task<ProductPriceDto?> AddPriceAsync(Guid productId, CreatePriceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<SimilarProductNamesResult> FindSimilarProductNamesAsync(
        string productName,
        Guid? excludeProductId = null,
        double similarityThreshold = 0.95,
        CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> SyncProductUnitsAsync(Guid id, SyncProductUnitsRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> SyncProductIngredientsAsync(Guid id, SyncProductIngredientsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LookupItemDto>> GetIngredientsAsync(CancellationToken cancellationToken = default);
    Task<BarcodeCheckResult> CheckBarcodeAsync(string barcode, Guid? excludeProductId = null, CancellationToken cancellationToken = default);
}
