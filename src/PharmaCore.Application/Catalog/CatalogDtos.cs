using System.Text.Json.Serialization;

namespace PharmaCore.Application.Catalog;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record ProductListItemDto(
    Guid Id,
    string ProductCode,
    string ProductName,
    string? GenericName,
    short DrugType,
    string? CategoryName,
    string? BrandName,
    string? PrimaryBarcode,
    decimal? RetailPrice,
    string? PrimaryImageUrl,
    string? SaleUnitName,
    short Status);

public sealed record NextProductCodeDto(string ProductCode);

public sealed record ProductImageDto(
    Guid Id,
    string ImageUrl,
    int SortOrder,
    bool IsPrimary);

public sealed record ProductDetailDto(
    Guid Id,
    string ProductCode,
    string ProductName,
    string? GenericName,
    short DrugType,
    Guid? CategoryId,
    Guid? BrandId,
    string? Description,
    short Status,
    string? SaleUnitName,
    IReadOnlyList<ProductUnitDto> Units,
    IReadOnlyList<ProductBarcodeDto> Barcodes,
    IReadOnlyList<ProductPriceDto> Prices,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductIngredientDto> Ingredients);

public sealed record ProductUnitDto(
    Guid Id,
    string UnitName,
    decimal ConversionFactor,
    bool IsBaseUnit,
    bool IsSaleUnit);

public sealed record ProductBarcodeDto(
    Guid Id,
    string Barcode,
    short BarcodeType,
    bool IsPrimary);

public sealed record ProductPriceDto(
    Guid Id,
    Guid ProductUnitId,
    string UnitName,
    short PriceType,
    string CurrencyCode,
    decimal Price,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public sealed class CreateProductRequest
{
    public string? ProductCode { get; init; }
    public required string ProductName { get; init; }
    public string? GenericName { get; init; }
    public short DrugType { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? BrandId { get; init; }
    public string? Description { get; init; }
    public short Status { get; init; } = 1;
    public string? SaleUnitName { get; init; }
    public string? PrimaryBarcode { get; init; }
    public Guid? RetailProductUnitId { get; init; }
    public decimal? RetailPrice { get; init; }
    [JsonPropertyName("extraBarcodes")]
    public IReadOnlyList<ProductBarcodeItem>? ExtraBarcodes { get; init; }
    [JsonPropertyName("extraPrices")]
    public IReadOnlyList<ProductPriceItem>? ExtraPrices { get; init; }
    [JsonPropertyName("images")]
    public IReadOnlyList<ProductImageItem>? Images { get; init; }
}

public sealed class UpdateProductRequest
{
    public required string ProductName { get; init; }
    public string? GenericName { get; init; }
    public short DrugType { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? BrandId { get; init; }
    public string? Description { get; init; }
    public short Status { get; init; }
    public string? SaleUnitName { get; init; }
    public string? PrimaryBarcode { get; init; }
    public Guid? RetailProductUnitId { get; init; }
    public decimal? RetailPrice { get; init; }
    [JsonPropertyName("extraBarcodes")]
    public IReadOnlyList<ProductBarcodeItem>? ExtraBarcodes { get; init; }
    [JsonPropertyName("extraPrices")]
    public IReadOnlyList<ProductPriceItem>? ExtraPrices { get; init; }
    [JsonPropertyName("images")]
    public IReadOnlyList<ProductImageItem>? Images { get; init; }
}

public class SyncProductCommercialRequest
{
    [JsonPropertyName("barcodes")]
    public IReadOnlyList<ProductBarcodeSyncItem>? Barcodes { get; init; }

    [JsonPropertyName("prices")]
    public IReadOnlyList<ProductPriceItem>? Prices { get; init; }

    [JsonPropertyName("images")]
    public IReadOnlyList<ProductImageItem>? Images { get; init; }
}

public sealed class SyncProductCommercialBodyRequest : SyncProductCommercialRequest
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }
}

public sealed record CreateBarcodeRequest(
    string Barcode,
    short BarcodeType,
    bool IsPrimary);

public sealed record CreatePriceRequest(
    Guid ProductUnitId,
    short PriceType,
    decimal Price,
    string CurrencyCode = "VND");

public sealed record LookupItemDto(Guid Id, string Code, string Name);

public sealed record CategoryDto(
    Guid Id,
    string CategoryCode,
    string CategoryName,
    string? Description,
    Guid? ParentId,
    string? ParentName,
    int SortOrder,
    short Status);

public sealed record CreateCategoryRequest(
    string CategoryCode,
    string CategoryName,
    string? Description,
    Guid? ParentId,
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    string CategoryName,
    string? Description,
    Guid? ParentId,
    int SortOrder,
    short Status);

public sealed record BrandDto(
    Guid Id,
    string BrandCode,
    string BrandName,
    string? CountryCode,
    short Status);

public sealed record CreateBrandRequest(
    string BrandCode,
    string BrandName,
    string? CountryCode);

public sealed record UpdateBrandRequest(
    string BrandName,
    string? CountryCode,
    short Status);

public sealed record BulkDeleteProductsRequest(IReadOnlyList<Guid> Ids);

public sealed record BulkDeleteResult(int DeletedCount);

public sealed record BarcodeCheckResult(
    bool IsAvailable,
    Guid? ExistingProductId,
    string? ExistingProductCode,
    string? ExistingProductName);

public sealed record ProductListFilter(
    string? Search = null,
    short[]? DrugTypes = null,
    Guid[]? CategoryIds = null,
    Guid[]? BrandIds = null,
    short? Status = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    bool? HasBarcode = null,
    bool? HasPrice = null,
    int Page = 1,
    int PageSize = 20);
