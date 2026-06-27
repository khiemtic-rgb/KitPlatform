using System.Security.Cryptography;
using System.Text;
using Dapper;
using PharmaCore.Infrastructure.Auth;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerAppAuthRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerAppAuthRepository(IDbConnectionFactory db) => _db = db;

    public async Task<TenantPhoneRow?> ResolveTenantAsync(string tenantCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS TenantId, tenant_code AS TenantCode
            FROM tenants
            WHERE tenant_code = @TenantCode AND deleted_at IS NULL AND status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantPhoneRow>(sql, new { TenantCode = tenantCode });
    }

    public async Task<CustomerAccountRecord?> FindAccountByPhoneAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ca.id AS AccountId,
                ca.customer_id AS CustomerId,
                ca.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                c.full_name AS FullName,
                ca.phone AS Phone
            FROM customer_accounts ca
            INNER JOIN customers c ON c.id = ca.customer_id AND c.deleted_at IS NULL
            INNER JOIN tenants t ON t.id = ca.tenant_id
            WHERE ca.tenant_id = @TenantId
              AND ca.phone = @Phone
              AND ca.status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAccountRecord>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task<CustomerAccountRecord?> EnsureAccountForCustomerPhoneAsync(
        Guid tenantId,
        string tenantCode,
        string phone,
        CancellationToken cancellationToken)
    {
        var existing = await FindAccountByPhoneAsync(tenantId, phone, cancellationToken);
        if (existing is not null)
            return existing;

        const string findCustomerSql = """
            SELECT id AS CustomerId, full_name AS FullName
            FROM customers
            WHERE tenant_id = @TenantId AND phone = @Phone AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var customer = await conn.QuerySingleOrDefaultAsync<(Guid CustomerId, string FullName)>(
            findCustomerSql,
            new { TenantId = tenantId, Phone = phone });

        if (customer.CustomerId == Guid.Empty)
            return null;

        const string insertSql = """
            INSERT INTO customer_accounts (tenant_id, customer_id, phone, is_verified)
            VALUES (@TenantId, @CustomerId, @Phone, FALSE)
            RETURNING id
            """;

        var accountId = await conn.QuerySingleAsync<Guid>(
            insertSql,
            new { TenantId = tenantId, customer.CustomerId, Phone = phone });

        return new CustomerAccountRecord(
            accountId,
            customer.CustomerId,
            tenantId,
            tenantCode,
            customer.FullName,
            phone);
    }

    public async Task<DateTime?> GetLatestOtpCreatedAtAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT created_at
            FROM customer_otp_challenges
            WHERE tenant_id = @TenantId AND phone = @Phone
            ORDER BY created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DateTime?>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task InsertOtpChallengeAsync(
        Guid tenantId,
        string phone,
        string codeHash,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_otp_challenges (tenant_id, phone, code_hash, expires_at)
            VALUES (@TenantId, @Phone, @CodeHash, @ExpiresAt)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantId = tenantId, Phone = phone, CodeHash = codeHash, ExpiresAt = expiresAt });
    }

    public async Task<OtpChallengeRow?> GetActiveOtpChallengeAsync(
        Guid tenantId,
        string phone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, code_hash AS CodeHash, expires_at AS ExpiresAt, attempt_count AS AttemptCount
            FROM customer_otp_challenges
            WHERE tenant_id = @TenantId
              AND phone = @Phone
              AND consumed_at IS NULL
              AND expires_at > NOW()
            ORDER BY created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OtpChallengeRow>(sql, new { TenantId = tenantId, Phone = phone });
    }

    public async Task IncrementOtpAttemptAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_otp_challenges
            SET attempt_count = attempt_count + 1
            WHERE id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = challengeId });
    }

    public async Task ConsumeOtpChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_otp_challenges
            SET consumed_at = NOW()
            WHERE id = @Id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = challengeId });
    }

    public async Task MarkAccountVerifiedAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_accounts
            SET is_verified = TRUE, last_login_at = NOW()
            WHERE id = @AccountId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AccountId = accountId });
    }

    public async Task StoreRefreshTokenAsync(
        Guid accountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_refresh_tokens (account_id, token_hash, expires_at)
            VALUES (@AccountId, @TokenHash, @ExpiresAt)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AccountId = accountId, TokenHash = tokenHash, ExpiresAt = expiresAt });
    }

    public async Task<Guid?> FindAccountIdByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT account_id
            FROM customer_refresh_tokens
            WHERE token_hash = @TokenHash
              AND revoked_at IS NULL
              AND expires_at > NOW()
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TokenHash = tokenHash });
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_refresh_tokens
            SET revoked_at = NOW()
            WHERE token_hash = @TokenHash AND revoked_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TokenHash = tokenHash });
    }

    public async Task<CustomerAccountRecord?> FindAccountByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ca.id AS AccountId,
                ca.customer_id AS CustomerId,
                ca.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                c.full_name AS FullName,
                ca.phone AS Phone
            FROM customer_accounts ca
            INNER JOIN customers c ON c.id = ca.customer_id AND c.deleted_at IS NULL
            INNER JOIN tenants t ON t.id = ca.tenant_id
            WHERE ca.id = @AccountId AND ca.status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerAccountRecord>(sql, new { AccountId = accountId });
    }

    public static string HashOtp(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length >= 11)
            digits = "0" + digits[2..];
        return digits;
    }

    public static string GenerateOtpCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
