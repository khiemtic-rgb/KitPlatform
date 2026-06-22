using PharmaCore.Application.Catalog;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class CatalogService : ICatalogService
{
    private readonly CatalogRepository _repository;

    public CatalogService(CatalogRepository repository) => _repository = repository;

    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(
        ProductListFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var normalized = filter with { Page = page, PageSize = pageSize };
        var (items, total) = await _repository.GetProductsAsync(normalized, cancellationToken);
        return new PagedResult<ProductListItemDto>(items, total, page, pageSize);
    }

    public Task<ProductDetailDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetProductAsync(id, cancellationToken);

    public Task<string> GetNextProductCodeAsync(CancellationToken cancellationToken = default) =>
        _repository.GenerateNextProductCodeAsync(cancellationToken);

    public async Task<ProductDetailDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _repository.CreateProductAsync(request, cancellationToken);
        await ApplyCommercialDataAsync(
            id,
            request.SaleUnitName,
            request.PrimaryBarcode,
            request.ExtraBarcodes,
            request.RetailProductUnitId,
            request.RetailPrice,
            request.ExtraPrices,
            request.Images,
            cancellationToken);
        return (await _repository.GetProductAsync(id, cancellationToken))!;
    }

    public async Task<ProductDetailDto?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var updated = await _repository.UpdateProductAsync(id, request, cancellationToken);
        if (!updated) return null;

        if (!string.IsNullOrWhiteSpace(request.SaleUnitName))
            await _repository.UpdateSaleUnitNameAsync(id, request.SaleUnitName, cancellationToken);

        if (HasCommercialPayload(request))
        {
            await ApplyCommercialDataAsync(
                id,
                request.SaleUnitName,
                request.PrimaryBarcode,
                request.ExtraBarcodes,
                request.RetailProductUnitId,
                request.RetailPrice,
                request.ExtraPrices,
                request.Images,
                cancellationToken);
        }

        return await _repository.GetProductAsync(id, cancellationToken);
    }

    public async Task<ProductDetailDto?> SyncProductCommercialAsync(
        Guid id,
        SyncProductCommercialRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.ProductExistsAsync(id, cancellationToken) is null)
            return null;

        await ValidateBarcodesForProductAsync(id, request.Barcodes, cancellationToken);
        await _repository.SyncProductBarcodesListAsync(id, request.Barcodes, cancellationToken);

        var defaultUnitId = await _repository.GetBaseUnitIdAsync(id, cancellationToken);
        if (defaultUnitId is not null)
            await _repository.SyncProductPricesListAsync(id, defaultUnitId.Value, request.Prices, cancellationToken);

        await _repository.SyncProductImagesAsync(id, request.Images, cancellationToken);
        return await _repository.GetProductAsync(id, cancellationToken);
    }

    public Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.SoftDeleteProductAsync(id, cancellationToken);

    public Task<int> BulkDeleteProductsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        _repository.BulkSoftDeleteProductsAsync(ids, cancellationToken);

    public async Task<ProductBarcodeDto?> AddBarcodeAsync(Guid productId, CreateBarcodeRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.ProductExistsAsync(productId, cancellationToken) is null)
            return null;
        return await _repository.AddBarcodeAsync(productId, request, cancellationToken);
    }

    public async Task<ProductPriceDto?> AddPriceAsync(Guid productId, CreatePriceRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.ProductExistsAsync(productId, cancellationToken) is null)
            return null;
        return await _repository.AddPriceAsync(productId, request, cancellationToken);
    }

    public Task<IReadOnlyList<LookupItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        _repository.GetCategoriesAsync(cancellationToken);

    public Task<IReadOnlyList<LookupItemDto>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetBrandsAsync(cancellationToken);

    public Task<SimilarProductNamesResult> FindSimilarProductNamesAsync(
        string productName,
        Guid? excludeProductId = null,
        double similarityThreshold = 0.95,
        CancellationToken cancellationToken = default) =>
        _repository.FindSimilarProductNamesAsync(productName, excludeProductId, similarityThreshold, cancellationToken);

    public async Task<ProductDetailDto?> SyncProductUnitsAsync(
        Guid id,
        SyncProductUnitsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.ProductExistsAsync(id, cancellationToken) is null)
            return null;

        var units = request.Units ?? [];
        ValidateUnits(units);
        await _repository.SyncProductUnitsListAsync(id, units, cancellationToken);
        return await _repository.GetProductAsync(id, cancellationToken);
    }

    public async Task<ProductDetailDto?> SyncProductIngredientsAsync(
        Guid id,
        SyncProductIngredientsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.ProductExistsAsync(id, cancellationToken) is null)
            return null;

        await _repository.SyncProductIngredientsListAsync(id, request.Ingredients, cancellationToken);
        return await _repository.GetProductAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<LookupItemDto>> GetIngredientsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetIngredientsAsync(cancellationToken);

    public Task<BarcodeCheckResult> CheckBarcodeAsync(
        string barcode,
        Guid? excludeProductId = null,
        CancellationToken cancellationToken = default) =>
        _repository.CheckBarcodeAsync(barcode, excludeProductId, cancellationToken);

    private async Task ValidateBarcodesForProductAsync(
        Guid productId,
        IReadOnlyList<ProductBarcodeSyncItem>? barcodes,
        CancellationToken cancellationToken)
    {
        foreach (var item in barcodes ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Barcode))
                continue;
            var check = await _repository.CheckBarcodeAsync(item.Barcode, productId, cancellationToken);
            if (!check.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"Mã barcode \"{item.Barcode.Trim()}\" đã được dùng cho sản phẩm {check.ExistingProductCode} — {check.ExistingProductName}.");
            }
        }
    }

    private static void ValidateUnits(IReadOnlyList<ProductUnitSyncItem> units)
    {
        if (units.Count == 0)
            throw new InvalidOperationException("Sản phẩm phải có ít nhất một đơn vị tính.");

        var names = units.Select(u => u.UnitName.Trim().ToLowerInvariant()).ToList();
        if (names.Distinct().Count() != names.Count)
            throw new InvalidOperationException("Tên đơn vị tính không được trùng nhau.");

        var baseUnits = units.Where(u => u.IsBaseUnit).ToList();
        if (baseUnits.Count != 1)
            throw new InvalidOperationException("Phải có đúng một đơn vị cơ sở.");

        if (baseUnits[0].ConversionFactor != 1)
            throw new InvalidOperationException("Đơn vị cơ sở phải có hệ số quy đổi = 1.");

        if (!units.Any(u => u.IsSaleUnit))
            throw new InvalidOperationException("Phải có ít nhất một đơn vị bán.");

        foreach (var unit in units.Where(u => !u.IsBaseUnit))
        {
            if (unit.ConversionFactor <= 0)
                throw new InvalidOperationException($"Hệ số quy đổi của \"{unit.UnitName}\" phải lớn hơn 0.");
        }
    }

    private static bool HasCommercialPayload(UpdateProductRequest request) =>
        request.PrimaryBarcode is not null
        || request.ExtraBarcodes is not null
        || request.ExtraPrices is not null
        || request.Images is not null
        || request.RetailPrice is not null
        || request.RetailProductUnitId is not null;

    private async Task ApplyCommercialDataAsync(
        Guid productId,
        string? saleUnitName,
        string? primaryBarcode,
        IReadOnlyList<ProductBarcodeItem>? extraBarcodes,
        Guid? retailProductUnitId,
        decimal? retailPrice,
        IReadOnlyList<ProductPriceItem>? extraPrices,
        IReadOnlyList<ProductImageItem>? images,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(saleUnitName))
            await _repository.UpdateSaleUnitNameAsync(productId, saleUnitName, cancellationToken);

        var barcodeList = BuildBarcodeList(primaryBarcode, extraBarcodes);
        await _repository.SyncProductBarcodesListAsync(productId, barcodeList, cancellationToken);

        var defaultUnitId = await _repository.GetBaseUnitIdAsync(productId, cancellationToken);
        if (defaultUnitId is not null)
        {
            var priceList = new List<ProductPriceItem>();
            if (retailPrice is > 0)
            {
                priceList.Add(new ProductPriceItem(
                    1,
                    retailPrice.Value,
                    retailProductUnitId ?? defaultUnitId));
            }

            if (extraPrices is not null)
                priceList.AddRange(extraPrices.Where(p => p.Price > 0 && p.PriceType != 1));

            await _repository.SyncProductPricesListAsync(productId, defaultUnitId.Value, priceList, cancellationToken);
        }

        await _repository.SyncProductImagesAsync(productId, images, cancellationToken);
    }

    private static List<ProductBarcodeSyncItem> BuildBarcodeList(
        string? primaryBarcode,
        IReadOnlyList<ProductBarcodeItem>? extraBarcodes)
    {
        var list = new List<ProductBarcodeSyncItem>();
        if (!string.IsNullOrWhiteSpace(primaryBarcode))
            list.Add(new ProductBarcodeSyncItem(primaryBarcode.Trim(), true));

        foreach (var item in extraBarcodes ?? [])
        {
            var code = item.Barcode.Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (list.Any(b => string.Equals(b.Barcode, code, StringComparison.Ordinal)))
                continue;
            list.Add(new ProductBarcodeSyncItem(code, false, item.BarcodeType));
        }

        if (list.Count > 0 && list.All(b => !b.IsPrimary))
            list[0] = list[0] with { IsPrimary = true };

        return list;
    }
}
