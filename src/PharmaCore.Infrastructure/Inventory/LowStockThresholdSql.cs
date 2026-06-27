namespace PharmaCore.Infrastructure.Inventory;

internal static class LowStockThresholdSql
{
    public const decimal SystemFallback = 10;

    /// <summary>SP → kho → danh mục → tenant settings → fallback query param. Requires aliases p, w, c.</summary>
    public const string EffectiveMinStockExpr = """
        COALESCE(
            p.min_stock_qty,
            w.min_stock_qty,
            c.min_stock_qty,
            NULLIF((SELECT t.settings->>'default_min_stock_qty' FROM tenants t WHERE t.id = @TenantId), '')::numeric,
            @FallbackThreshold
        )
        """;
}
