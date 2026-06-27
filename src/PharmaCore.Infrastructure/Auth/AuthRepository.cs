using Dapper;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Auth;

internal sealed class AuthRepository
{
    private readonly IDbConnectionFactory _db;

    public AuthRepository(IDbConnectionFactory db) => _db = db;

    public async Task<UserRecord?> FindByCredentialsAsync(string tenantCode, string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.id AS Id,
                u.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                u.username AS Username,
                u.email AS Email,
                u.password_hash AS PasswordHash,
                u.status AS Status,
                COALESCE(array_agg(DISTINCT r.role_code) FILTER (WHERE r.role_code IS NOT NULL), '{}') AS Roles,
                COALESCE(array_agg(DISTINCT p.permission_code) FILTER (WHERE p.permission_code IS NOT NULL), '{}') AS Permissions
            FROM users u
            INNER JOIN tenants t ON t.id = u.tenant_id
            LEFT JOIN user_roles ur ON ur.user_id = u.id
            LEFT JOIN roles r ON r.id = ur.role_id
            LEFT JOIN role_permissions rp ON rp.role_id = r.id
            LEFT JOIN permissions p ON p.id = rp.permission_id
            WHERE t.tenant_code = @TenantCode
              AND u.username = @Username
              AND u.deleted_at IS NULL
              AND t.deleted_at IS NULL
              AND t.status = 1
            GROUP BY u.id, u.tenant_id, t.tenant_code, u.username, u.email, u.password_hash, u.status
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<UserRecord>(sql, new { TenantCode = tenantCode, Username = username });
    }

    public async Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.id AS Id,
                u.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                u.username AS Username,
                u.email AS Email,
                u.password_hash AS PasswordHash,
                u.status AS Status,
                COALESCE(array_agg(DISTINCT r.role_code) FILTER (WHERE r.role_code IS NOT NULL), '{}') AS Roles,
                COALESCE(array_agg(DISTINCT p.permission_code) FILTER (WHERE p.permission_code IS NOT NULL), '{}') AS Permissions
            FROM users u
            INNER JOIN tenants t ON t.id = u.tenant_id
            LEFT JOIN user_roles ur ON ur.user_id = u.id
            LEFT JOIN roles r ON r.id = ur.role_id
            LEFT JOIN role_permissions rp ON rp.role_id = r.id
            LEFT JOIN permissions p ON p.id = rp.permission_id
            WHERE u.id = @UserId
              AND u.deleted_at IS NULL
              AND t.deleted_at IS NULL
              AND t.status = 1
            GROUP BY u.id, u.tenant_id, t.tenant_code, u.username, u.email, u.password_hash, u.status
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<UserRecord>(sql, new { UserId = userId });
    }

    public async Task UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE users SET last_login_at = NOW() WHERE id = @UserId";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task StoreRefreshTokenAsync(Guid userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO refresh_tokens (user_id, token_hash, expires_at)
            VALUES (@UserId, @TokenHash, @ExpiresAt)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { UserId = userId, TokenHash = tokenHash, ExpiresAt = expiresAt });
    }

    public async Task<Guid?> FindUserIdByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT user_id
            FROM refresh_tokens
            WHERE token_hash = @TokenHash
              AND revoked_at IS NULL
              AND expires_at > NOW()
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TokenHash = tokenHash });
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE refresh_tokens SET revoked_at = NOW() WHERE token_hash = @TokenHash AND revoked_at IS NULL";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TokenHash = tokenHash });
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE refresh_tokens SET revoked_at = NOW() WHERE user_id = @UserId AND revoked_at IS NULL";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { UserId = userId });
    }
}
