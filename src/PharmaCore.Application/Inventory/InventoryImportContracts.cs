namespace PharmaCore.Application.Inventory;

public sealed class OpeningBalanceImportRowRequest
{
    public int RowNumber { get; init; }
    /// <summary>Mã SP hoặc barcode.</summary>
    public required string ProductKey { get; init; }
    public required string BatchNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

public sealed record OpeningBalanceImportErrorDto(int RowNumber, string Message);

public sealed record OpeningBalanceImportResultDto(
    int LinesProcessed,
    IReadOnlyList<Guid> BatchIds,
    IReadOnlyList<OpeningBalanceImportErrorDto> Errors);
