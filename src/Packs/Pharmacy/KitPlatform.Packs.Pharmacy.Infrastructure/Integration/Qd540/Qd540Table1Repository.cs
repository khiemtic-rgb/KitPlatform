using System.Security.Cryptography;
using System.Text;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Integration.Qd540;
using KitPlatform.Packs.Pharmacy.Procurement;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Integration.Qd540;

internal sealed class Qd540Table1Repository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public Qd540Table1Repository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<bool> BranchMissingRetailCodeAsync(Guid branchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(NULLIF(TRIM(retail_facility_code), ''), '') = ''
            FROM branches
            WHERE id = @BranchId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { BranchId = branchId, TenantId });
    }

    public async Task<IReadOnlyList<Qd540Table1SourceRow>> LoadGrnLinesAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var branchFilter = branchId is Guid bid ? " AND w.branch_id = @BranchId" : string.Empty;
        var sql = $"""
            SELECT
                'grn'::text AS EventKind,
                gri.id AS SourceId,
                w.branch_id AS BranchId,
                b.retail_facility_code AS RetailFacilityCode,
                s.wholesale_facility_code AS WholesaleFacilityCode,
                p.national_drug_id AS NationalDrugId,
                p.national_registration_number AS NationalRegistrationNumber,
                p.product_name AS ProductName,
                p.generic_name AS GenericName,
                ing.ingredient_name AS FirstIngredientName,
                ing.strength AS FirstStrength,
                pb.brand_name AS BrandName,
                p.attributes->>'manufacturer' AS ManufacturerAttr,
                pb.country_code AS CountryCode,
                p.importer_name AS ImporterName,
                p.packaging AS Packaging,
                p.dosage_form AS DosageForm,
                base_u.unit_name AS BaseUnitName,
                pu.conversion_factor AS ConversionFactor,
                gri.quantity AS Quantity,
                gri.unit_cost AS UnitPrice,
                gri.batch_number AS BatchNumber,
                gri.expiry_date AS ExpiryDate,
                s.supplier_name AS SupplierName,
                g.supplier_invoice_number AS SupplierInvoiceNumber,
                g.receipt_date AS EventAt
            FROM goods_receipts g
            INNER JOIN goods_receipt_items gri ON gri.goods_receipt_id = g.id
            INNER JOIN products p ON p.id = gri.product_id
            INNER JOIN product_units pu ON pu.id = gri.product_unit_id
            INNER JOIN warehouses w ON w.id = g.warehouse_id
            INNER JOIN branches b ON b.id = w.branch_id
            INNER JOIN suppliers s ON s.id = g.supplier_id
            LEFT JOIN product_brands pb ON pb.id = p.brand_id
            LEFT JOIN LATERAL (
                SELECT ai.ingredient_name,
                       CASE WHEN pi.strength_value IS NOT NULL
                            THEN TRIM(pi.strength_value::text || ' ' || COALESCE(pi.strength_unit, ''))
                            ELSE NULL END AS strength
                FROM product_ingredients pi
                INNER JOIN active_ingredients ai ON ai.id = pi.ingredient_id
                WHERE pi.product_id = p.id
                ORDER BY ai.ingredient_name
                LIMIT 1
            ) ing ON TRUE
            LEFT JOIN LATERAL (
                SELECT pu2.unit_name
                FROM product_units pu2
                WHERE pu2.product_id = p.id AND pu2.status = 1
                ORDER BY pu2.is_base_unit DESC, pu2.conversion_factor ASC
                LIMIT 1
            ) base_u ON TRUE
            WHERE g.tenant_id = @TenantId
              AND g.status = @Completed
              AND g.deleted_at IS NULL
              AND p.product_kind = 'pharmacy_drug'
              AND g.receipt_date >= @FromUtc
              AND g.receipt_date < @ToExclusiveUtc
              {branchFilter}
            ORDER BY g.receipt_date, gri.id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SourceRow>(sql, new
        {
            TenantId,
            Completed = GoodsReceiptStatuses.Completed,
            FromUtc = fromUtc,
            ToExclusiveUtc = toExclusiveUtc,
            BranchId = branchId,
        });
        return rows.Select(MapRow).ToList();
    }

    public async Task<IReadOnlyList<Qd540Table1SourceRow>> LoadSaleLinesAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var branchFilter = branchId is Guid bid ? " AND so.branch_id = @BranchId" : string.Empty;
        var sql = $"""
            SELECT
                'sale'::text AS EventKind,
                soi.id AS SourceId,
                so.branch_id AS BranchId,
                b.retail_facility_code AS RetailFacilityCode,
                NULL::varchar AS WholesaleFacilityCode,
                p.national_drug_id AS NationalDrugId,
                p.national_registration_number AS NationalRegistrationNumber,
                p.product_name AS ProductName,
                p.generic_name AS GenericName,
                ing.ingredient_name AS FirstIngredientName,
                ing.strength AS FirstStrength,
                pb.brand_name AS BrandName,
                p.attributes->>'manufacturer' AS ManufacturerAttr,
                pb.country_code AS CountryCode,
                p.importer_name AS ImporterName,
                p.packaging AS Packaging,
                p.dosage_form AS DosageForm,
                base_u.unit_name AS BaseUnitName,
                pu.conversion_factor AS ConversionFactor,
                soi.quantity AS Quantity,
                soi.unit_price AS UnitPrice,
                ib.batch_number AS BatchNumber,
                ib.expiry_date AS ExpiryDate,
                NULL::varchar AS SupplierName,
                NULL::varchar AS SupplierInvoiceNumber,
                so.order_date AS EventAt
            FROM sales_orders so
            INNER JOIN sales_order_items soi ON soi.sales_order_id = so.id
            INNER JOIN products p ON p.id = soi.product_id
            INNER JOIN product_units pu ON pu.id = soi.product_unit_id
            INNER JOIN inventory_batches ib ON ib.id = soi.batch_id
            INNER JOIN branches b ON b.id = so.branch_id
            LEFT JOIN product_brands pb ON pb.id = p.brand_id
            LEFT JOIN LATERAL (
                SELECT ai.ingredient_name,
                       CASE WHEN pi.strength_value IS NOT NULL
                            THEN TRIM(pi.strength_value::text || ' ' || COALESCE(pi.strength_unit, ''))
                            ELSE NULL END AS strength
                FROM product_ingredients pi
                INNER JOIN active_ingredients ai ON ai.id = pi.ingredient_id
                WHERE pi.product_id = p.id
                ORDER BY ai.ingredient_name
                LIMIT 1
            ) ing ON TRUE
            LEFT JOIN LATERAL (
                SELECT pu2.unit_name
                FROM product_units pu2
                WHERE pu2.product_id = p.id AND pu2.status = 1
                ORDER BY pu2.is_base_unit DESC, pu2.conversion_factor ASC
                LIMIT 1
            ) base_u ON TRUE
            WHERE so.tenant_id = @TenantId
              AND so.status = @Completed
              AND p.product_kind = 'pharmacy_drug'
              AND so.order_date >= @FromUtc
              AND so.order_date < @ToExclusiveUtc
              {branchFilter}
            ORDER BY so.order_date, soi.id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SourceRow>(sql, new
        {
            TenantId,
            Completed = SalesOrderStatuses.Completed,
            FromUtc = fromUtc,
            ToExclusiveUtc = toExclusiveUtc,
            BranchId = branchId,
        });
        return rows.Select(MapRow).ToList();
    }

    public async Task LogExportAsync(
        Qd540Table1Query query,
        int rowCount,
        string payloadHash,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO qd540_export_log (
                tenant_id, branch_id, exported_from, exported_to,
                row_count, payload_hash, status, error_message, created_by
            )
            VALUES (
                @TenantId, @BranchId, @ExportedFrom, @ExportedTo,
                @RowCount, @PayloadHash, @Status, @ErrorMessage, @CreatedBy
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId,
            query.BranchId,
            ExportedFrom = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ExportedTo = query.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            RowCount = rowCount,
            PayloadHash = payloadHash,
            Status = status,
            ErrorMessage = errorMessage,
            CreatedBy = _tenant.UserId,
        });
    }

    private static Qd540Table1SourceRow MapRow(SourceRow row) =>
        new(
            row.EventKind == "sale" ? Qd540EventKind.Sale : Qd540EventKind.Grn,
            row.SourceId,
            row.BranchId,
            row.RetailFacilityCode,
            row.WholesaleFacilityCode,
            row.NationalDrugId,
            row.NationalRegistrationNumber,
            row.ProductName,
            row.GenericName,
            row.FirstIngredientName,
            row.FirstStrength,
            row.BrandName,
            row.ManufacturerAttr,
            row.CountryCode,
            row.ImporterName,
            row.Packaging,
            row.DosageForm,
            row.BaseUnitName,
            row.ConversionFactor,
            row.Quantity,
            row.UnitPrice,
            row.BatchNumber,
            row.ExpiryDate,
            row.SupplierName,
            row.SupplierInvoiceNumber,
            row.EventAt);

    private sealed class SourceRow
    {
        public string EventKind { get; init; } = "";
        public Guid SourceId { get; init; }
        public Guid BranchId { get; init; }
        public string? RetailFacilityCode { get; init; }
        public string? WholesaleFacilityCode { get; init; }
        public string? NationalDrugId { get; init; }
        public string? NationalRegistrationNumber { get; init; }
        public string ProductName { get; init; } = "";
        public string? GenericName { get; init; }
        public string? FirstIngredientName { get; init; }
        public string? FirstStrength { get; init; }
        public string? BrandName { get; init; }
        public string? ManufacturerAttr { get; init; }
        public string? CountryCode { get; init; }
        public string? ImporterName { get; init; }
        public string? Packaging { get; init; }
        public string? DosageForm { get; init; }
        public string? BaseUnitName { get; init; }
        public decimal ConversionFactor { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public string BatchNumber { get; init; } = "";
        public DateOnly? ExpiryDate { get; init; }
        public string? SupplierName { get; init; }
        public string? SupplierInvoiceNumber { get; init; }
        public DateTime EventAt { get; init; }
    }
}

internal static class Qd540ExportHash
{
    public static string Compute(IReadOnlyList<Qd540Table1RowDto> rows)
    {
        var csv = Qd540Transform.ToCsv(rows);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(csv));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
