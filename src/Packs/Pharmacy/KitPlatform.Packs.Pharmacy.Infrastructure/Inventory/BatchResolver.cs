using System.Data;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

/// <summary>FEFO batch allocation for Pharmacy sales (BR-INV-001/002).</summary>
internal sealed class BatchResolver : IBatchResolver
{
    internal const string AvailableSql =
        """
        SELECT
            id AS Id,
            batch_number AS BatchNumber,
            expiry_date AS ExpiryDate,
            quantity_available AS QuantityAvailable,
            unit_cost AS UnitCost
        FROM inventory_batches
        WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId
          AND product_id = @ProductId AND quantity_available > 0
          AND 
        """ + SellableBatchRules.SqlUnqualified + """

        ORDER BY expiry_date ASC NULLS LAST, created_at ASC
        """;

    internal const string AvailableForUpdateSql = AvailableSql + "\nFOR UPDATE";

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public BatchResolver(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<BatchAvailabilityDto>> GetAvailableBatchesAsync(
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await GetAvailableBatchesAsync(conn, warehouseId, productId, cancellationToken);
    }

    public async Task<IReadOnlyList<BatchAvailabilityDto>> GetAvailableBatchesAsync(
        IDbConnection conn,
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return (await conn.QueryAsync<BatchAvailabilityDto>(
            AvailableSql, new { TenantId, WarehouseId = warehouseId, ProductId = productId })).ToList();
    }

    public IReadOnlyList<FifoAllocationResult> AllocateFromBatches(
        IReadOnlyList<BatchAvailabilityDto> batches,
        decimal baseQuantityNeeded)
    {
        var result = new List<FifoAllocationResult>();
        var remaining = baseQuantityNeeded;
        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var take = Math.Min(batch.QuantityAvailable, remaining);
            if (take <= 0) continue;
            result.Add(new FifoAllocationResult(batch.Id, take, batch.UnitCost));
            remaining -= take;
        }

        if (remaining > 0.0001m)
            throw new InvalidOperationException("Không đủ tồn kho theo FEFO.");

        return result;
    }

    public async Task<IReadOnlyList<FifoAllocationResult>> AllocateFifoAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid productId,
        decimal baseQuantityNeeded,
        CancellationToken cancellationToken = default)
    {
        var batches = (await conn.QueryAsync<BatchAvailabilityDto>(
            AvailableForUpdateSql,
            new { TenantId, WarehouseId = warehouseId, ProductId = productId },
            tx)).ToList();
        return AllocateFromBatches(batches, baseQuantityNeeded);
    }
}
