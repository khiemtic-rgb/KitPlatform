using System.Data;
using Dapper;
using KitPlatform.Application.Auth;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Auth;

internal sealed class KitAccountRepository
{
    private readonly IDbConnectionFactory _db;

    public KitAccountRepository(IDbConnectionFactory db) => _db = db;

    public async Task<KitAccountRow?> FindByEmailAsync(
        string email,
        IDbConnection? connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                email AS Email,
                password_hash AS PasswordHash,
                display_name AS DisplayName,
                status AS Status
            FROM public.kit_accounts
            WHERE email = @Email
              AND deleted_at IS NULL
            LIMIT 1
            """;

        if (connection is not null)
            return await connection.QuerySingleOrDefaultAsync<KitAccountRow>(
                new CommandDefinition(sql, new { Email = email }, transaction, cancellationToken: cancellationToken));

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<KitAccountRow>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> InsertAccountAsync(
        string email,
        string passwordHash,
        string? displayName,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO public.kit_accounts (email, password_hash, display_name, status)
            VALUES (@Email, @PasswordHash, @DisplayName, 1)
            RETURNING id
            """;

        return await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                sql,
                new { Email = email, PasswordHash = passwordHash, DisplayName = displayName },
                transaction,
                cancellationToken: cancellationToken));
    }

    public async Task<Guid?> FindMembershipUserIdAsync(
        Guid accountId,
        Guid tenantId,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT user_id
            FROM public.kit_account_memberships
            WHERE account_id = @AccountId
              AND tenant_id = @TenantId
              AND status = 1
            LIMIT 1
            """;

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                sql,
                new { AccountId = accountId, TenantId = tenantId },
                transaction,
                cancellationToken: cancellationToken));
    }

    public async Task UpsertMembershipAsync(
        Guid accountId,
        Guid tenantId,
        Guid userId,
        string productCode,
        bool isDefault,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO public.kit_account_memberships (
                account_id, tenant_id, user_id, product_code, is_default, status
            )
            VALUES (@AccountId, @TenantId, @UserId, @ProductCode, @IsDefault, 1)
            ON CONFLICT (user_id) DO UPDATE
            SET account_id = EXCLUDED.account_id,
                tenant_id = EXCLUDED.tenant_id,
                product_code = EXCLUDED.product_code,
                status = 1,
                updated_at = NOW()
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    AccountId = accountId,
                    TenantId = tenantId,
                    UserId = userId,
                    ProductCode = productCode,
                    IsDefault = isDefault,
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetTenantVerticalAsync(
        Guid tenantId,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(business_vertical, '')
            FROM public.tenants
            WHERE id = @TenantId
            """;

        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { TenantId = tenantId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuthWorkspaceDto>> ListWorkspacesByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                m.user_id AS UserId,
                m.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                m.product_code AS ProductCode,
                u.username AS Username,
                m.is_default AS IsDefault
            FROM public.kit_account_memberships m
            INNER JOIN public.tenants t ON t.id = m.tenant_id
            INNER JOIN public.users u ON u.id = m.user_id
            WHERE m.account_id = @AccountId
              AND m.status = 1
              AND u.deleted_at IS NULL
              AND u.status = 1
              AND t.deleted_at IS NULL
              AND t.status = 1
            ORDER BY m.is_default DESC, t.tenant_name ASC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<WorkspaceRow>(
            new CommandDefinition(sql, new { AccountId = accountId }, cancellationToken: cancellationToken));
        return rows
            .Select(r => new AuthWorkspaceDto(
                r.UserId,
                r.TenantId,
                r.TenantCode,
                r.TenantName,
                r.ProductCode,
                r.Username,
                r.IsDefault))
            .ToList();
    }

    public async Task<Guid?> FindAccountIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT account_id
            FROM public.kit_account_memberships
            WHERE user_id = @UserId AND status = 1
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAccountPasswordAsync(
        Guid accountId,
        string passwordHash,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE public.kit_accounts
            SET password_hash = @PasswordHash, updated_at = NOW()
            WHERE id = @AccountId AND deleted_at IS NULL
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { AccountId = accountId, PasswordHash = passwordHash },
                transaction,
                cancellationToken: cancellationToken));
    }

    public async Task SyncMembershipUserPasswordsAsync(
        Guid accountId,
        string passwordHash,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE public.users u
            SET password_hash = @PasswordHash, updated_at = NOW()
            FROM public.kit_account_memberships m
            WHERE m.account_id = @AccountId
              AND m.user_id = u.id
              AND m.status = 1
              AND u.deleted_at IS NULL
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { AccountId = accountId, PasswordHash = passwordHash },
                transaction,
                cancellationToken: cancellationToken));
    }

    public static string MapProductCode(string? businessVertical)
    {
        return (businessVertical ?? "").Trim().ToLowerInvariant() switch
        {
            "pharmacy" => "pharmacy",
            "clinic" => "clinic",
            "family" => "family_os",
            "hybrid" => "family_os",
            "family_os" => "family_os",
            _ => "hybrid",
        };
    }

    private sealed class WorkspaceRow
    {
        public Guid UserId { get; init; }
        public Guid TenantId { get; init; }
        public string TenantCode { get; init; } = string.Empty;
        public string TenantName { get; init; } = string.Empty;
        public string ProductCode { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public bool IsDefault { get; init; }
    }
}

internal sealed class KitAccountRow
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public short Status { get; init; }
}
