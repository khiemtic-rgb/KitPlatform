using Dapper;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerAddressRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerAddressRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<CustomerAddressDto>> ListAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                a.id AS Id,
                a.label AS Label,
                a.recipient_name AS RecipientName,
                a.phone AS Phone,
                a.address_line AS AddressLine,
                a.ward AS Ward,
                a.district AS District,
                a.province AS Province,
                a.is_default AS IsDefault
            FROM customer_addresses a
            INNER JOIN customers c ON c.id = a.customer_id
            WHERE a.customer_id = @CustomerId AND c.tenant_id = @TenantId
            ORDER BY a.is_default DESC, a.created_at DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerAddressDto>(sql, new { TenantId = tenantId, CustomerId = customerId }))
            .ToList();
    }

    public async Task<CustomerAddressDto?> GetAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                a.id AS Id,
                a.label AS Label,
                a.recipient_name AS RecipientName,
                a.phone AS Phone,
                a.address_line AS AddressLine,
                a.ward AS Ward,
                a.district AS District,
                a.province AS Province,
                a.is_default AS IsDefault
            FROM customer_addresses a
            INNER JOIN customers c ON c.id = a.customer_id
            WHERE a.id = @AddressId AND a.customer_id = @CustomerId AND c.tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAddressDto>(sql, new
        {
            AddressId = addressId,
            CustomerId = customerId,
            TenantId = tenantId,
        });
    }

    public async Task<CustomerAddressDto> CreateAsync(
        Guid tenantId,
        Guid customerId,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await EnsureCustomerAsync(conn, tx, tenantId, customerId);

        if (request.IsDefault)
        {
            await conn.ExecuteAsync(
                "UPDATE customer_addresses SET is_default = FALSE WHERE customer_id = @CustomerId",
                new { CustomerId = customerId },
                tx);
        }

        var id = await conn.QuerySingleAsync<Guid>("""
            INSERT INTO customer_addresses (
                customer_id, label, recipient_name, phone, address_line, ward, district, province, is_default
            )
            VALUES (
                @CustomerId, @Label, @RecipientName, @Phone, @AddressLine, @Ward, @District, @Province, @IsDefault
            )
            RETURNING id
            """, MapParams(customerId, request), tx);

        await tx.CommitAsync(cancellationToken);
        return (await GetAsync(tenantId, customerId, id, cancellationToken))!;
    }

    public async Task<CustomerAddressDto?> UpdateAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var exists = await conn.ExecuteScalarAsync<bool>("""
            SELECT EXISTS(
                SELECT 1 FROM customer_addresses a
                INNER JOIN customers c ON c.id = a.customer_id
                WHERE a.id = @AddressId AND a.customer_id = @CustomerId AND c.tenant_id = @TenantId
            )
            """, new { AddressId = addressId, CustomerId = customerId, TenantId = tenantId }, tx);
        if (!exists)
            return null;

        if (request.IsDefault)
        {
            await conn.ExecuteAsync(
                "UPDATE customer_addresses SET is_default = FALSE WHERE customer_id = @CustomerId",
                new { CustomerId = customerId },
                tx);
        }

        await conn.ExecuteAsync("""
            UPDATE customer_addresses SET
                label = @Label,
                recipient_name = @RecipientName,
                phone = @Phone,
                address_line = @AddressLine,
                ward = @Ward,
                district = @District,
                province = @Province,
                is_default = @IsDefault
            WHERE id = @AddressId AND customer_id = @CustomerId
            """, new
        {
            AddressId = addressId,
            CustomerId = customerId,
            request.Label,
            request.RecipientName,
            request.Phone,
            request.AddressLine,
            request.Ward,
            request.District,
            request.Province,
            request.IsDefault,
        }, tx);

        await tx.CommitAsync(cancellationToken);
        return await GetAsync(tenantId, customerId, addressId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            DELETE FROM customer_addresses a
            USING customers c
            WHERE a.customer_id = c.id
              AND a.id = @AddressId
              AND a.customer_id = @CustomerId
              AND c.tenant_id = @TenantId
            """, new { AddressId = addressId, CustomerId = customerId, TenantId = tenantId });
        return rows > 0;
    }

    private static async Task EnsureCustomerAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid tenantId,
        Guid customerId)
    {
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM customers WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL)",
            new { CustomerId = customerId, TenantId = tenantId },
            tx);
        if (!exists)
            throw new InvalidOperationException("Khách hàng không tồn tại.");
    }

    private static object MapParams(Guid customerId, UpsertCustomerAddressRequest request) => new
    {
        CustomerId = customerId,
        request.Label,
        request.RecipientName,
        request.Phone,
        request.AddressLine,
        request.Ward,
        request.District,
        request.Province,
        request.IsDefault,
    };
}
