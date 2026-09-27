using Dapper;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerCareCardRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerCareCardRepository(IDbConnectionFactory db) => _db = db;

    public async Task<DateOnly?> GetDateOfBirthAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT date_of_birth
            FROM customers
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateOnly?>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
        });
    }

    public async Task<IReadOnlyList<CustomerCareCardProductRow>> ListRecentProductsAsync(
        Guid tenantId,
        Guid customerId,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.product_name AS ProductName,
                p.generic_name AS GenericName,
                cat.category_name AS CategoryName,
                COALESCE((
                    SELECT string_agg(ai.ingredient_name, ' ')
                    FROM product_ingredients pi
                    INNER JOIN active_ingredients ai ON ai.id = pi.ingredient_id
                    WHERE pi.product_id = p.id
                      AND pi.tenant_id = @TenantId
                ), '') AS Ingredients
            FROM products p
            LEFT JOIN product_categories cat ON cat.id = p.category_id AND cat.tenant_id = @TenantId
            WHERE p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND p.id IN (
                    SELECT i.product_id
                    FROM sales_order_items i
                    INNER JOIN sales_orders so ON so.id = i.sales_order_id
                    WHERE so.tenant_id = @TenantId
                      AND so.customer_id = @CustomerId
                      AND so.status IN (@Completed, @Refunded)
                      AND so.order_date >= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                          - (@LookbackDays || ' days')::interval
                    UNION
                    SELECT mr.product_id
                    FROM medication_reminders mr
                    WHERE mr.tenant_id = @TenantId
                      AND mr.customer_id = @CustomerId
                      AND mr.is_active = TRUE
                      AND mr.family_member_id IS NULL
                      AND mr.product_id IS NOT NULL
              )
            ORDER BY p.product_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerCareCardProductRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            LookbackDays = lookbackDays,
            Completed = SalesOrderStatuses.Completed,
            Refunded = SalesOrderStatuses.Refunded,
        })).ToList();
    }
}
