using System.Data;

namespace PharmaCore.Application.Inventory;

public sealed record BatchAvailabilityDto(
    Guid Id,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal QuantityAvailable,
    decimal UnitCost);

public sealed record FifoAllocationResult(
    Guid BatchId,
    decimal BaseQuantity,
    decimal UnitCost);

public interface IBatchResolver
{
    Task<IReadOnlyList<BatchAvailabilityDto>> GetAvailableBatchesAsync(
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchAvailabilityDto>> GetAvailableBatchesAsync(
        IDbConnection conn,
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken = default);

    IReadOnlyList<FifoAllocationResult> AllocateFromBatches(
        IReadOnlyList<BatchAvailabilityDto> batches,
        decimal baseQuantityNeeded);

    Task<IReadOnlyList<FifoAllocationResult>> AllocateFifoAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid productId,
        decimal baseQuantityNeeded,
        CancellationToken cancellationToken = default);
}
