using System.Data;
using Dapper;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.Pharmacy.Rx;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal static class RxDispensingEnforcer
{
    public sealed record BlockedProduct(Guid ProductId, string ProductCode, string ProductName, string DispensingClass);

    public static async Task EnforceStrictSaleAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        IReadOnlyList<CreateSaleLineRequest> saleItems,
        Guid? prescriptionId,
        TenantRxSettingsDto rxSettings,
        Guid? userId,
        Guid? warehouseId,
        Guid? branchId,
        string auditSource,
        CancellationToken cancellationToken,
        Guid? connectRxHandoffId = null)
    {
        if (RxEnforcementMode.Parse(rxSettings.EnforcementMode) != RxEnforcementMode.Strict)
            return;

        if (saleItems.Count == 0)
            return;

        var productIds = saleItems.Select(i => i.ProductId).Distinct().ToArray();
        const string productSql = """
            SELECT id AS ProductId, product_code AS ProductCode, product_name AS ProductName,
                   dispensing_class AS DispensingClass
            FROM products
            WHERE tenant_id = @TenantId
              AND id = ANY(@ProductIds)
              AND deleted_at IS NULL
            """;
        var productRows = (await conn.QueryAsync<BlockedProduct>(
            productSql,
            new { TenantId = tenantId, ProductIds = productIds.ToArray() },
            tx)).ToList();

        var rxProducts = productRows
            .Where(p => DispensingClass.RequiresPrescription(p.DispensingClass))
            .ToList();

        // Đơn phòng khám qua Connect — đủ ngữ cảnh kê đơn để bán Rx (không cần electronic_prescriptions NT).
        if (connectRxHandoffId is Guid handoffId && !prescriptionId.HasValue)
        {
            var handoffOk = await conn.QuerySingleOrDefaultAsync<bool>(
                """
                SELECT EXISTS(
                    SELECT 1
                    FROM pack_connect.rx_handoffs h
                    WHERE h.id = @HandoffId
                      AND h.pharmacy_tenant_id = @TenantId
                      AND h.handoff_status IN ('pending_pharmacy', 'consumed')
                      AND h.dismissed_at IS NULL
                )
                """,
                new { HandoffId = handoffId, TenantId = tenantId },
                tx);
            if (!handoffOk)
                throw new InvalidOperationException(
                    "Đơn phòng khám (Connect) không hợp lệ hoặc không thuộc nhà thuốc này.");

            // Có handoff hợp lệ → cho phép bán Rx trong giỏ (dòng đã khớp khi nạp POS).
            return;
        }

        if (!prescriptionId.HasValue)
        {
            if (rxProducts.Count == 0)
                return;

            if (rxSettings.PosBlockedAudit)
            {
                foreach (var item in rxProducts)
                {
                    await conn.ExecuteAsync(
                        """
                        INSERT INTO pack_pharmacy.rx_pos_block_events
                            (tenant_id, branch_id, warehouse_id, product_id, user_id, source)
                        VALUES
                            (@TenantId, @BranchId, @WarehouseId, @ProductId, @UserId, @Source)
                        """,
                        new
                        {
                            TenantId = tenantId,
                            BranchId = branchId,
                            WarehouseId = warehouseId,
                            ProductId = item.ProductId,
                            UserId = userId,
                            Source = auditSource,
                        },
                        tx);
                }
            }

            var names = string.Join(", ", rxProducts.Select(b => $"{b.ProductName} ({b.ProductCode})"));
            throw new InvalidOperationException(
                $"Không bán được thuốc kê đơn khi chưa có đơn bác sĩ: {names}. " +
                "Nạp đơn BS / đơn phòng khám (Connect) trước hoặc tắt chế độ strict trong Cài đặt POS.");
        }

        var prescription = await conn.QuerySingleOrDefaultAsync<PrescriptionGuardRow>(
            """
            SELECT
                ep.id AS Id,
                ep.branch_id AS BranchId,
                ep.status AS Status,
                ep.expires_at AS ExpiresAt,
                COALESCE(t.settings->'rx'->>'dispense_branch_policy', 'same_branch') AS BranchPolicy
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN tenants t ON t.id = ep.tenant_id
            WHERE ep.id = @PrescriptionId
              AND ep.tenant_id = @TenantId
            """,
            new
            {
                PrescriptionId = prescriptionId.Value,
                TenantId = tenantId,
            },
            tx);

        if (prescription is null)
            throw new InvalidOperationException("Đơn thuốc không tồn tại.");

        if (prescription.Status is not (PrescriptionStatuses.Verified or PrescriptionStatuses.Signed or PrescriptionStatuses.PartiallyDispensed))
            throw new InvalidOperationException("Đơn thuốc chưa xác minh hoặc không còn hiệu lực cấp phát.");

        if (prescription.ExpiresAt.HasValue && prescription.ExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Đơn thuốc đã hết hạn.");

        if (string.Equals(prescription.BranchPolicy, "same_branch", StringComparison.OrdinalIgnoreCase)
            && prescription.BranchId.HasValue
            && warehouseId.HasValue)
        {
            var warehouseBranchId = await conn.QuerySingleOrDefaultAsync<Guid?>(
                """
                SELECT branch_id
                FROM warehouses
                WHERE id = @WarehouseId
                  AND tenant_id = @TenantId
                  AND deleted_at IS NULL
                """,
                new { WarehouseId = warehouseId.Value, TenantId = tenantId },
                tx);
            if (warehouseBranchId != prescription.BranchId)
                throw new InvalidOperationException("Đơn thuốc không thuộc cùng chi nhánh với kho bán.");
        }

        if (rxProducts.Count == 0)
            return;

        var lineRows = (await conn.QueryAsync<PrescriptionRemainingRow>(
            """
            SELECT
                id AS PrescriptionLineId,
                product_id AS ProductId,
                GREATEST(qty_prescribed - qty_dispensed, 0) AS QtyRemaining
            FROM pack_pharmacy.electronic_prescription_lines
            WHERE tenant_id = @TenantId
              AND prescription_id = @PrescriptionId
            """,
            new
            {
                TenantId = tenantId,
                PrescriptionId = prescriptionId.Value,
            },
            tx)).ToList();
        var lineById = lineRows.ToDictionary(x => x.PrescriptionLineId, x => x);
        var requestedByLine = saleItems
            .Where(i => i.PrescriptionLineId.HasValue)
            .GroupBy(i => i.PrescriptionLineId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var rxProductIds = rxProducts.Select(x => x.ProductId).ToHashSet();
        foreach (var rx in rxProducts)
        {
            var saleLines = saleItems.Where(i => i.ProductId == rx.ProductId).ToList();
            foreach (var saleLine in saleLines)
            {
                if (saleLine.PrescriptionLineId is not Guid lineId)
                {
                    throw new InvalidOperationException(
                        $"Sản phẩm {rx.ProductName} ({rx.ProductCode}) chưa chọn dòng đơn thuốc.");
                }

                if (!lineById.TryGetValue(lineId, out var line))
                    throw new InvalidOperationException($"Dòng đơn thuốc của {rx.ProductName} không hợp lệ.");
                if (line.ProductId != rx.ProductId)
                    throw new InvalidOperationException($"Dòng đơn thuốc không khớp sản phẩm {rx.ProductName}.");
            }
        }

        foreach (var (lineId, requestedQty) in requestedByLine)
        {
            if (!lineById.TryGetValue(lineId, out var line))
                throw new InvalidOperationException("Dòng đơn thuốc không tồn tại.");
            if (!rxProductIds.Contains(line.ProductId))
                continue;
            if (requestedQty > line.QtyRemaining + 0.0001m)
            {
                var product = rxProducts.First(p => p.ProductId == line.ProductId);
                throw new InvalidOperationException(
                    $"Sản phẩm {product.ProductName} ({product.ProductCode}) vượt số lượng còn lại trên đơn (còn {line.QtyRemaining:N2}).");
            }
        }
    }

    private sealed class PrescriptionGuardRow
    {
        public Guid Id { get; init; }
        public Guid? BranchId { get; init; }
        public string Status { get; init; } = "";
        public DateTime? ExpiresAt { get; init; }
        public string BranchPolicy { get; init; } = "same_branch";
    }

    private sealed class PrescriptionRemainingRow
    {
        public Guid PrescriptionLineId { get; init; }
        public Guid ProductId { get; init; }
        public decimal QtyRemaining { get; init; }
    }
}
