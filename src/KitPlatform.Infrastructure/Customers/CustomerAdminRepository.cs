using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Customers;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Party;
using KitPlatform.Infrastructure.Kernel.Pharmacy;

namespace KitPlatform.Infrastructure.Customers;

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
    private Guid? WorkspaceId => _tenant.WorkspaceId;

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
            conditions.Add(KernelPartyReader.CustomerSearchFilter);
            args.Add("Search", $"%{search.Trim()}%");
        }

        var where = string.Join(" AND ", conditions);
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM customers c
            {KernelPartyReader.CustomerPartyJoins}
            WHERE {where}
            """;
        var listSql = $"""
            SELECT
                {KernelPartyReader.CustomerListSelect}
            FROM customers c
            {KernelPartyReader.CustomerPartyJoins}
            WHERE {where}
            ORDER BY FullName
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
        var sql = $"""
            SELECT
                {KernelPartyReader.CustomerDetailSelect}
            FROM customers c
            {KernelPartyReader.CustomerPartyJoins}
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
            FROM pack_pharmacy.v_sales_order o
            WHERE o.tenant_id = @TenantId
              AND o.customer_id = @CustomerId
            """;

        var listSql = $"""
            SELECT
                o.id AS Id,
                o.order_number AS OrderNumber,
                o.status AS Status,
                o.order_date AS OrderDate,
                o.total_amount AS TotalAmount,
                (SELECT COUNT(*)::int FROM sales_order_items i WHERE i.sales_order_id = o.id) AS ItemCount
            FROM {PackPharmacyReadViews.SalesOrder} o
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
        string? addressLine,
        string? idNumber,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? clinicalNotes,
        Guid? customerGroupId,
        CancellationToken cancellationToken)
    {
        var customerId = Guid.NewGuid();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var partyId = await KernelPartyWriter.CreateCustomerPartyFirstAsync(
            conn,
            tx,
            TenantId,
            customerId,
            customerCode,
            fullName,
            phone,
            email,
            WorkspaceId,
            cancellationToken);

        const string sql = """
            INSERT INTO customers (
                id, tenant_id, party_id, customer_code, full_name, phone, email, date_of_birth, gender, status,
                address_line, id_number, emergency_contact_name, emergency_contact_phone, clinical_notes,
                customer_group_id
            )
            VALUES (
                @CustomerId, @TenantId, @PartyId, @CustomerCode, @FullName, @Phone, @Email, @DateOfBirth, @Gender, 1,
                @AddressLine, @IdNumber, @EmergencyContactName, @EmergencyContactPhone, @ClinicalNotes,
                @CustomerGroupId
            )
            RETURNING id
            """;

        var insertedId = await conn.QuerySingleAsync<Guid>(
            sql,
            new
            {
                CustomerId = customerId,
                TenantId,
                PartyId = partyId,
                CustomerCode = customerCode,
                FullName = fullName,
                Phone = phone,
                Email = email,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                AddressLine = addressLine,
                IdNumber = idNumber,
                EmergencyContactName = emergencyContactName,
                EmergencyContactPhone = emergencyContactPhone,
                ClinicalNotes = clinicalNotes,
                CustomerGroupId = customerGroupId,
            },
            tx);

        await tx.CommitAsync(cancellationToken);
        return insertedId;
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
        bool allowCredit,
        decimal? creditLimit,
        string? addressLine,
        string? idNumber,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? clinicalNotes,
        Guid? customerGroupId,
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
                allow_credit = @AllowCredit,
                credit_limit = @CreditLimit,
                address_line = @AddressLine,
                id_number = @IdNumber,
                emergency_contact_name = @EmergencyContactName,
                emergency_contact_phone = @EmergencyContactPhone,
                clinical_notes = @ClinicalNotes,
                customer_group_id = @CustomerGroupId,
                updated_at = NOW()
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

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
                AllowCredit = allowCredit,
                CreditLimit = creditLimit,
                AddressLine = addressLine,
                IdNumber = idNumber,
                EmergencyContactName = emergencyContactName,
                EmergencyContactPhone = emergencyContactPhone,
                ClinicalNotes = clinicalNotes,
                CustomerGroupId = customerGroupId,
            },
            tx);

        if (rows > 0)
        {
            await KernelPartyWriter.SyncCustomerPartyAsync(
                conn,
                tx,
                TenantId,
                customerId,
                customerCode,
                fullName,
                phone,
                email,
                WorkspaceId,
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
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
            ToOffset(row.CreatedAt),
            row.CustomerGroupId,
            row.CustomerGroupName,
            row.GroupDiscountPercent);

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
            row.AppLastLoginAt.HasValue ? ToOffset(row.AppLastLoginAt.Value) : null,
            row.AllowCredit,
            row.CreditLimit,
            row.AddressLine,
            row.IdNumber,
            row.EmergencyContactName,
            row.EmergencyContactPhone,
            row.ClinicalNotes,
            row.CustomerGroupId,
            row.CustomerGroupName,
            row.GroupDiscountPercent);

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
        public Guid? CustomerGroupId { get; init; }
        public string? CustomerGroupName { get; init; }
        public decimal GroupDiscountPercent { get; init; }
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
        public bool AllowCredit { get; init; }
        public decimal? CreditLimit { get; init; }
        public string? AddressLine { get; init; }
        public string? IdNumber { get; init; }
        public string? EmergencyContactName { get; init; }
        public string? EmergencyContactPhone { get; init; }
        public string? ClinicalNotes { get; init; }
        public Guid? CustomerGroupId { get; init; }
        public string? CustomerGroupName { get; init; }
        public decimal GroupDiscountPercent { get; init; }
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
