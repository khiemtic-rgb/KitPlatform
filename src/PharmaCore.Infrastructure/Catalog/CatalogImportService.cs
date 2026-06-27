using PharmaCore.Application.Catalog;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class CatalogImportService : ICatalogImportService
{
    private readonly CatalogRepository _repository;
    private readonly ICatalogService _catalog;

    public CatalogImportService(CatalogRepository repository, ICatalogService catalog)
    {
        _repository = repository;
        _catalog = catalog;
    }

    public async Task<ProductImportResultDto> ImportProductsAsync(
        IReadOnlyList<ProductImportRowRequest> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("Không có dòng dữ liệu để import.");

        var categories = await _repository.GetCategoryCodeMapAsync(cancellationToken);
        var brands = await _repository.GetBrandCodeMapAsync(cancellationToken);

        var created = 0;
        var skipped = 0;
        var errors = new List<ProductImportErrorDto>();

        foreach (var row in rows)
        {
            try
            {
                var name = row.ProductName?.Trim() ?? "";
                if (name.Length < 2)
                {
                    errors.Add(new ProductImportErrorDto(row.RowNumber, "Tên sản phẩm quá ngắn."));
                    continue;
                }

                var code = row.ProductCode?.Trim();
                if (!string.IsNullOrWhiteSpace(code)
                    && await _repository.ProductCodeExistsAsync(code, cancellationToken))
                {
                    skipped++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(row.Barcode)
                    && await _repository.BarcodeTakenAsync(row.Barcode.Trim(), excludeProductId: null, cancellationToken))
                {
                    errors.Add(new ProductImportErrorDto(row.RowNumber, $"Barcode «{row.Barcode}» đã tồn tại."));
                    continue;
                }

                Guid? categoryId = null;
                if (!string.IsNullOrWhiteSpace(row.CategoryCode))
                {
                    var key = row.CategoryCode.Trim().ToUpperInvariant();
                    if (!categories.TryGetValue(key, out var catId))
                    {
                        errors.Add(new ProductImportErrorDto(row.RowNumber, $"Không tìm thấy danh mục «{row.CategoryCode}»."));
                        continue;
                    }
                    categoryId = catId;
                }

                Guid? brandId = null;
                if (!string.IsNullOrWhiteSpace(row.BrandCode))
                {
                    var key = row.BrandCode.Trim().ToUpperInvariant();
                    if (!brands.TryGetValue(key, out var brId))
                    {
                        errors.Add(new ProductImportErrorDto(row.RowNumber, $"Không tìm thấy thương hiệu «{row.BrandCode}»."));
                        continue;
                    }
                    brandId = brId;
                }

                var drugType = row.DrugType is >= 1 and <= 3 ? row.DrugType : (short)1;

                await _catalog.CreateProductAsync(new CreateProductRequest
                {
                    ProductCode = code,
                    ProductName = name,
                    GenericName = row.GenericName?.Trim(),
                    DrugType = drugType,
                    CategoryId = categoryId,
                    BrandId = brandId,
                    SaleUnitName = string.IsNullOrWhiteSpace(row.SaleUnitName) ? "Viên" : row.SaleUnitName.Trim(),
                    PrimaryBarcode = row.Barcode?.Trim(),
                    RetailPrice = row.RetailPrice,
                    MinStockQty = row.MinStockQty,
                    Status = 1,
                }, cancellationToken);

                created++;
            }
            catch (Exception ex)
            {
                errors.Add(new ProductImportErrorDto(row.RowNumber, ex.Message));
            }
        }

        return new ProductImportResultDto(created, skipped, errors.Count, errors);
    }
}
