namespace KitPlatform.Packs.Pharmacy.Inventory;

public sealed record InventoryStockReviewDto(
    Guid ProductId,
    Guid WarehouseId,
    DateTimeOffset ConfirmedAt,
    Guid? ConfirmedBy,
    decimal QtyAtConfirm,
    decimal ValueAtConfirm);

public sealed record ConfirmStockReviewRequest(
    Guid ProductId,
    Guid WarehouseId,
    decimal Qty,
    decimal Value);

public interface IInventoryStockReviewService
{
    Task<IReadOnlyList<InventoryStockReviewDto>> ListConfirmedAsync(
        CancellationToken cancellationToken = default);

    Task<InventoryStockReviewDto> ConfirmAsync(
        ConfirmStockReviewRequest request,
        CancellationToken cancellationToken = default);

    Task UnconfirmAsync(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
}
