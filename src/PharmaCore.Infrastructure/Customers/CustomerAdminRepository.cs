using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Customers;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Customers;

internal sealed class CustomerAdminRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CustomerAdminRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<(IReadOnlyList<CustomerAdminListItemDto> Items, int Total)> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "c.tenant_id = @TenantId", "c.deleted_at IS NULL" };
        var args = new DynamicParameters();
        args.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(c.full_name ILIKE @Search OR c.phone ILIKE @Search OR c.customer_code ILIKE @Search OR c.email ILIKE @Search)");
            args.Add("Search", $"%{search.Trim()}%");
        }

        var where = string.Join(" AND ", conditions);
        var countSql = $"SELECT COUNT(*)::int FROM customers c WHERE {where}";
        var listSql = $"""
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone,
                c.email AS Email,
                c.status AS Status,
                c.created_at AS CreatedAt
            FROM customers c
            WHERE {where}
            ORDER BY c.full_name
            LIMIT @PageSize OFFSET @Offset
            """;

        args.Add("PageSize", pageSize);
        args.Add("Offset", (page - 1) * pageSize);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var rows = await conn.QueryAsync<CustomerListRow>(listSql, args);
        var items = rows.Select(MapListItem).ToList();
        return (items, total);
    }

    public async Task<CustomerDetailDto?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone,
                c.email AS Email,
                c.date_of_birth AS DateOfBirth,
                c.gender AS Gender,
                c.status AS Status,
                c.created_at AS CreatedAt,
                (ca.id IS NOT NULL) AS HasAppAccount,
                ca.is_verified AS AppVerified,
                ca.last_login_at AS AppLastLoginAt
            FROM customers c
            LEFT JOIN customer_accounts ca
                ON ca.customer_id = c.id
               AND ca.tenant_id = c.tenant_id
               AND ca.status = 1
            WHERE c.id = @CustomerId
              AND c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<CustomerDetailRow>(
            sql,
            new { CustomerId = customerId, TenantId });
        return row is null ? null : MapDetail(row);
    }

    public async Task<(IReadOnlyList<CustomerOrderListItemDto> Items, int Total)> GetOrdersAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string countSql = """
            SELECT COUNT(*)::int
            FROM sales_orders o
            WHERE o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
            """;

        const string listSql = """
            SELECT
                o.id AS Id,
                o.order_number AS OrderNumber,
                o.status AS Status,
                o.order_date AS OrderDate,
                o.total_amount AS TotalAmount,
                (SELECT COUNT(*)::int FROM sales_order_items i WHERE i.sales_order_id = o.id) AS ItemCount
            FROM sales_orders o
            WHERE o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
            ORDER BY o.order_date DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        var args = new
        {
            TenantId,
            CustomerId = customerId,
            PageSize = pageSize,
            Offset = (page - 1) * pageSize,
        };

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var rows = await conn.QueryAsync<CustomerOrderRow>(listSql, args);
        var items = rows.Select(MapOrder).ToList();
        return (items, total);
    }

    public async Task<string> GenerateCustomerCodeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(MAX(
                CASE
                    WHEN customer_code ~ '^KH[0-9]+$'
                    THEN CAST(SUBSTRING(customer_code FROM 3) AS INT)
                END
            ), 0) + 1
            FROM customers
            WHERE tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var next = await conn.ExecuteScalarAsync<int>(sql, new { TenantId });
        return $"KH{next:D3}";
    }

    public async Task<bool> PhoneExistsAsync(
        string phone,
        Guid? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE tenant_id = @TenantId
                  AND phone = @Phone
                  AND deleted_at IS NULL
                  AND (@ExcludeCustomerId IS NULL OR id <> @ExcludeCustomerId)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            sql,
            new { TenantId, Phone = phone, ExcludeCustomerId = excludeCustomerId });
    }

    public async Task<bool> CustomerCodeExistsAsync(
        string customerCode,
        Guid? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE tenant_id = @TenantId
                  AND UPPER(customer_code) = UPPER(@CustomerCode)
                  AND deleted_at IS NULL
                  AND (@ExcludeCustomerId IS NULL OR id <> @ExcludeCustomerId)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            sql,
            new { TenantId, CustomerCode = customerCode, ExcludeCustomerId = excludeCustomerId });
    }

    public async Task<Guid> CreateAsync(
        string customerCode,
        string fullName,
        string phone,
        string? email,
        DateOnly? dateOfBirth,
        short? gender,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customers (
                tenant_id, customer_code, full_name, phone, email, date_of_birth, gender, status
            )
            VALUES (
                @TenantId, @CustomerCode, @FullName, @Phone, @Email, @DateOfBirth, @Gender, 1
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(
            sql,
            new
            {
                TenantId,
                CustomerCode = customerCode,
                FullName = fullName,
                Phone = phone,
                Email = email,
                DateOfBirth = dateOfBirth,
                Gender = gender,
            });
    }

    public async Task<bool> UpdateAsync(
        Guid customerId,
        string customerCode,
        string fullName,
        string phone,
        string? email,
        DateOnly? dateOfBirth,
        short? gender,
        short status,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customers SET
                customer_code = @CustomerCode,
                full_name = @FullName,
                phone = @Phone,
                email = @Email,
                date_of_birth = @DateOfBirth,
                gender = @Gender,
                status = @Status,
                updated_at = NOW()
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(
            sql,
            new
            {
                CustomerId = customerId,
                TenantId,
                CustomerCode = customerCode,
                FullName = fullName,
                Phone = phone,
                Email = email,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                Status = status,
            });
        return rows > 0;
    }

    private static CustomerAdminListItemDto MapListItem(CustomerListRow row) =>
        new(
            row.Id,
            row.CustomerCode,
            row.FullName,
            row.Phone,
            row.Email,
            row.Status,
            ToOffset(row.CreatedAt));

    private static CustomerDetailDto MapDetail(CustomerDetailRow row) =>
        new(
            row.Id,
            row.CustomerCode,
            row.FullName,
            row.Phone,
            row.Email,
            row.DateOfBirth,
            row.Gender,
            row.Status,
            ToOffset(row.CreatedAt),
            row.HasAppAccount,
            row.AppVerified,
            row.AppLastLoginAt.HasValue ? ToOffset(row.AppLastLoginAt.Value) : null);

    private static CustomerOrderListItemDto MapOrder(CustomerOrderRow row) =>
        new(
            row.Id,
            row.OrderNumber,
            row.Status,
            ToOffset(row.OrderDate),
            row.TotalAmount,
            row.ItemCount);

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class CustomerListRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public string? Email { get; init; }
        public short Status { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class CustomerDetailRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public string? Email { get; init; }
        public DateOnly? DateOfBirth { get; init; }
        public short? Gender { get; init; }
        public short Status { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool HasAppAccount { get; init; }
        public bool? AppVerified { get; init; }
        public DateTime? AppLastLoginAt { get; init; }
    }

    private sealed class CustomerOrderRow
    {
        public Guid Id { get; init; }
        public string OrderNumber { get; init; } = "";
        public short Status { get; init; }
        public DateTime OrderDate { get; init; }
        public decimal TotalAmount { get; init; }
        public int ItemCount { get; init; }
    }
}
