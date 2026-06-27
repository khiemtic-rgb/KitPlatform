namespace PharmaCore.Application.Catalog;

public sealed class ProductImportRowRequest
{
    public int RowNumber { get; init; }
    public string? ProductCode { get; init; }
    public required string ProductName { get; init; }
    public string? GenericName { get; init; }
    public short DrugType { get; init; } = 1;
    public string? CategoryCode { get; init; }
    public string? BrandCode { get; init; }
    public string? SaleUnitName { get; init; }
    public string? Barcode { get; init; }
    public decimal? RetailPrice { get; init; }
    public decimal? MinStockQty { get; init; }
}

public sealed record ProductImportErrorDto(int RowNumber, string Message);

public sealed record ProductImportResultDto(
    int Created,
    int Skipped,
    int Failed,
    IReadOnlyList<ProductImportErrorDto> Errors);
