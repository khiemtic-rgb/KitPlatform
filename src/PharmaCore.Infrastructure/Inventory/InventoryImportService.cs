using PharmaCore.Application.Catalog;
using PharmaCore.Application.Inventory;
using PharmaCore.Infrastructure.Catalog;

namespace PharmaCore.Infrastructure.Inventory;

internal sealed class InventoryImportService : IInventoryImportService
{
    private readonly CatalogRepository _catalog;
    private readonly IInventoryService _inventory;

    public InventoryImportService(CatalogRepository catalog, IInventoryService inventory)
    {
        _catalog = catalog;
        _inventory = inventory;
    }

    public async Task<OpeningBalanceImportResultDto> ImportOpeningBalanceAsync(
        Guid warehouseId,
        string? notes,
        IReadOnlyList<OpeningBalanceImportRowRequest> rows,
        CancellationToken cancellationToken = default)
    {
        var lines = new List<OpeningBalanceLineRequest>();
        var errors = new List<OpeningBalanceImportErrorDto>();

        foreach (var row in rows)
        {
            var key = row.ProductKey?.Trim() ?? "";
            if (key.Length == 0)
            {
                errors.Add(new OpeningBalanceImportErrorDto(row.RowNumber, "Thiếu mã SP hoặc barcode."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.BatchNumber))
            {
                errors.Add(new OpeningBalanceImportErrorDto(row.RowNumber, "Thiếu số lô."));
                continue;
            }

            if (row.Quantity <= 0)
            {
                errors.Add(new OpeningBalanceImportErrorDto(row.RowNumber, "Số lượng phải > 0."));
                continue;
            }

            var productId = await _catalog.ResolveProductIdByKeyAsync(key, cancellationToken);
            if (productId is null)
            {
                errors.Add(new OpeningBalanceImportErrorDto(row.RowNumber, $"Không tìm thấy SP «{key}»."));
                continue;
            }

            lines.Add(new OpeningBalanceLineRequest(
                productId.Value,
                row.BatchNumber.Trim(),
                row.ExpiryDate,
                null,
                row.UnitCost,
                row.Quantity));
        }

        if (lines.Count == 0)
            return new OpeningBalanceImportResultDto(0, [], errors);

        var result = await _inventory.CreateOpeningBalanceAsync(
            new CreateOpeningBalanceRequest(warehouseId, notes, lines),
            cancellationToken);

        return new OpeningBalanceImportResultDto(result.LinesProcessed, result.BatchIds, errors);
    }
}
