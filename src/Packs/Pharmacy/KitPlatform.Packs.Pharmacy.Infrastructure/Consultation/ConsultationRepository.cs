using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal sealed class ConsultationRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConsultationRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<string?> GetTenantCodeAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT tenant_code
            FROM tenants
            WHERE id = @TenantId AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<string?>(sql, new { TenantId });
    }

    public sealed class SessionRow
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid StaffUserId { get; set; }
        public Guid? SalesOrderId { get; set; }
        public short ConsultationLevel { get; set; }
        public string Status { get; set; } = "";
        public string QuickSymptomsJson { get; set; } = "[]";
        public string? NaturalLanguageInput { get; set; }
        public string? ExtractedJson { get; set; }
        public string ConfirmedFactsJson { get; set; } = "{}";
        public string SafetyFlagsJson { get; set; } = "[]";
        public string SafetyLevel { get; set; } = "none";
        public string? PreliminaryAssessmentJson { get; set; }
        public string? CustomerProfileSnapshotJson { get; set; }
        public string? AiModel { get; set; }
        public string ExtractionSource { get; set; } = "manual";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ConfirmedAt { get; set; }
    }

    public async Task<Guid> InsertAsync(SessionRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pharmacy_consultation_sessions (
                id, tenant_id, branch_id, customer_id, staff_user_id, sales_order_id,
                consultation_level, status, quick_symptoms, natural_language_input,
                extracted_json, confirmed_facts, safety_flags, safety_level,
                preliminary_assessment_json, customer_profile_snapshot_json,
                ai_model, extraction_source, confirmed_at
            ) VALUES (
                @Id, @TenantId, @BranchId, @CustomerId, @StaffUserId, @SalesOrderId,
                @ConsultationLevel, @Status, @QuickSymptomsJson::jsonb, @NaturalLanguageInput,
                @ExtractedJson::jsonb, @ConfirmedFactsJson::jsonb, @SafetyFlagsJson::jsonb, @SafetyLevel,
                @PreliminaryAssessmentJson::jsonb, @CustomerProfileSnapshotJson::jsonb,
                @AiModel, @ExtractionSource, @ConfirmedAt
            )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
        return row.Id;
    }

    public async Task<SessionRow?> GetAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                branch_id AS BranchId,
                customer_id AS CustomerId,
                staff_user_id AS StaffUserId,
                sales_order_id AS SalesOrderId,
                consultation_level AS ConsultationLevel,
                status AS Status,
                quick_symptoms::text AS QuickSymptomsJson,
                natural_language_input AS NaturalLanguageInput,
                extracted_json::text AS ExtractedJson,
                confirmed_facts::text AS ConfirmedFactsJson,
                safety_flags::text AS SafetyFlagsJson,
                safety_level AS SafetyLevel,
                preliminary_assessment_json::text AS PreliminaryAssessmentJson,
                customer_profile_snapshot_json::text AS CustomerProfileSnapshotJson,
                ai_model AS AiModel,
                extraction_source AS ExtractionSource,
                created_at AS CreatedAt,
                confirmed_at AS ConfirmedAt
            FROM pharmacy_consultation_sessions
            WHERE id = @Id AND tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SessionRow>(sql, new { Id = id, TenantId });
    }

    public sealed class RecentSessionRow
    {
        public Guid Id { get; set; }
        public DateTimeOffset ConfirmedAt { get; set; }
        public string Status { get; set; } = "";
        public Guid? SalesOrderId { get; set; }
        public string QuickSymptomsJson { get; set; } = "[]";
        public string ConfirmedFactsJson { get; set; } = "{}";
        public string? PreliminaryAssessmentJson { get; set; }
        public string SafetyLevel { get; set; } = "none";
        public string? NaturalLanguageInput { get; set; }
    }

    public async Task<IReadOnlyList<RecentSessionRow>> ListRecentByCustomerAsync(
        Guid customerId,
        int limit,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                id AS Id,
                confirmed_at AS ConfirmedAt,
                status AS Status,
                sales_order_id AS SalesOrderId,
                quick_symptoms::text AS QuickSymptomsJson,
                confirmed_facts::text AS ConfirmedFactsJson,
                preliminary_assessment_json::text AS PreliminaryAssessmentJson,
                safety_level AS SafetyLevel,
                natural_language_input AS NaturalLanguageInput
            FROM pharmacy_consultation_sessions
            WHERE tenant_id = @TenantId
              AND customer_id = @CustomerId
            ORDER BY confirmed_at DESC
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<RecentSessionRow>(sql, new
        {
            TenantId,
            CustomerId = customerId,
            Limit = Math.Clamp(limit, 1, 20),
        });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> GetOrderProductNamesAsync(Guid salesOrderId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.product_name
            FROM sales_order_items soi
            INNER JOIN products p ON p.id = soi.product_id
            INNER JOIN sales_orders so ON so.id = soi.sales_order_id AND so.tenant_id = @TenantId
            WHERE soi.sales_order_id = @SalesOrderId
            ORDER BY p.product_name
            LIMIT 8
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var names = await conn.QueryAsync<string>(sql, new { TenantId, SalesOrderId = salesOrderId });
        return names.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    public async Task<bool> LinkOrderAsync(Guid id, Guid salesOrderId, CancellationToken ct)
    {
        const string sql = """
            UPDATE pharmacy_consultation_sessions
            SET sales_order_id = @SalesOrderId,
                status = 'linked',
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(sql, new { Id = id, SalesOrderId = salesOrderId, TenantId });
        return rows > 0;
    }

    public async Task<bool> OrderBelongsToTenantAsync(Guid salesOrderId, CancellationToken ct)
    {
        const string sql = """
            SELECT 1 FROM sales_orders
            WHERE id = @SalesOrderId AND tenant_id = @TenantId
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<int?>(sql, new { SalesOrderId = salesOrderId, TenantId }) == 1;
    }

    public sealed class OtcSuggestionRow
    {
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string? GenericName { get; set; }
        public string LookupCode { get; set; } = "";
        public Guid ProductUnitId { get; set; }
        public string UnitName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal StockAvailable { get; set; }
        public string? CategoryCode { get; set; }
    }

    public async Task<IReadOnlyList<OtcSuggestionRow>> SearchOtcSuggestionsAsync(
        Guid warehouseId,
        short priceType,
        IReadOnlyList<string> categoryCodes,
        IReadOnlyList<string> keywords,
        IReadOnlyList<string> excludeKeywords,
        int limit,
        CancellationToken ct)
    {
        var categories = categoryCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var keywordPatterns = keywords
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => $"%{x.Trim()}%")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var excludePatterns = excludeKeywords
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => $"%{x.Trim()}%")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (categories.Length == 0 && keywordPatterns.Length == 0)
            return [];

        const string sql = """
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                p.generic_name AS GenericName,
                COALESCE((
                    SELECT bc.barcode
                    FROM product_barcodes bc
                    WHERE bc.product_id = p.id AND bc.tenant_id = @TenantId
                      AND bc.is_primary = TRUE AND bc.status = 1
                    LIMIT 1
                ), p.product_code) AS LookupCode,
                u.id AS ProductUnitId,
                u.unit_name AS UnitName,
                COALESCE(pr.price, 0) AS UnitPrice,
                COALESCE(st.stock_available, 0) AS StockAvailable,
                c.category_code AS CategoryCode
            FROM products p
            LEFT JOIN product_categories c
                ON c.id = p.category_id AND c.tenant_id = p.tenant_id
            INNER JOIN LATERAL (
                SELECT id, unit_name
                FROM product_units pu
                WHERE pu.product_id = p.id AND pu.tenant_id = @TenantId
                ORDER BY pu.is_sale_unit DESC, pu.is_base_unit DESC, pu.unit_name
                LIMIT 1
            ) u ON TRUE
            LEFT JOIN LATERAL (
                SELECT price FROM product_prices pp
                WHERE pp.tenant_id = @TenantId AND pp.product_id = p.id
                  AND pp.product_unit_id = u.id AND pp.price_type = @PriceType
                  AND pp.status = 1 AND pp.effective_from <= NOW()
                  AND (pp.effective_to IS NULL OR pp.effective_to > NOW())
                ORDER BY pp.effective_from DESC
                LIMIT 1
            ) pr ON TRUE
            INNER JOIN LATERAL (
                SELECT COALESCE(SUM(b.quantity_available), 0) AS stock_available
                FROM inventory_batches b
                WHERE b.tenant_id = @TenantId AND b.warehouse_id = @WarehouseId
                  AND b.product_id = p.id AND b.quantity_available > 0
            ) st ON TRUE
            LEFT JOIN LATERAL (
                -- Rank by what this tenant actually sells (90d), then stock.
                SELECT COALESCE(SUM(oi.quantity), 0) AS sold_qty_90d
                FROM sales_order_items oi
                INNER JOIN sales_orders o ON o.id = oi.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND oi.product_id = p.id
                  AND o.status = @CompletedStatus
                  AND o.order_date >= (NOW() - INTERVAL '90 days')
            ) sales ON TRUE
            WHERE p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND p.status = 1
              AND COALESCE(p.dispensing_class, CASE COALESCE(p.drug_type, 1) WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END) = 'otc'
              AND COALESCE(p.drug_type, 1) = 1
              AND COALESCE(c.category_code, '') <> 'KHANG_SINH'
              AND st.stock_available > 0
              AND (
                (cardinality(@CategoryCodes::text[]) > 0 AND c.category_code = ANY(@CategoryCodes))
                OR (
                  cardinality(@KeywordPatterns::text[]) > 0
                  AND (
                    p.product_name ILIKE ANY(@KeywordPatterns)
                    OR COALESCE(p.generic_name, '') ILIKE ANY(@KeywordPatterns)
                    OR COALESCE(p.description, '') ILIKE ANY(@KeywordPatterns)
                  )
                )
              )
              AND (
                cardinality(@ExcludePatterns::text[]) = 0
                OR NOT (
                    p.product_name ILIKE ANY(@ExcludePatterns)
                    OR COALESCE(p.generic_name, '') ILIKE ANY(@ExcludePatterns)
                    OR COALESCE(p.description, '') ILIKE ANY(@ExcludePatterns)
                )
              )
            ORDER BY COALESCE(sales.sold_qty_90d, 0) DESC, st.stock_available DESC, p.product_name
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<OtcSuggestionRow>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            PriceType = priceType,
            CategoryCodes = categories,
            KeywordPatterns = keywordPatterns,
            ExcludePatterns = excludePatterns,
            CompletedStatus = SalesOrderStatuses.Completed,
            Limit = Math.Clamp(limit, 1, 12),
        });
        return rows.ToList();
    }

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value);
}
