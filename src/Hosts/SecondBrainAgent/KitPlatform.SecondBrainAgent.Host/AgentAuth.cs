using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.SecondBrainAgent.Host;

public static class AgentAuth
{
    public const string Username = "sb-operator";
    public const string TenantCode = "SECOND_BRAIN";
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid UserId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid PersonId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid ExchangeId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public static readonly Guid AccountId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    public static readonly string[] Permissions =
    [
        "personal_intelligence.trading.run",
        "personal_intelligence.trading.approve",
        "personal_intelligence.research.read",
        "personal_intelligence.exchange.read",
    ];

    public static async Task EnsureOperatorAsync(IDbConnectionFactory db, string? password, CancellationToken cancellationToken)
    {
        await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM public.users", cancellationToken: cancellationToken));
        if (existing > 0 || string.IsNullOrWhiteSpace(password))
            return;

        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.tenants (id, tenant_code, tenant_name, settings)
                VALUES (
                    @TenantId, @TenantCode, 'Second Brain',
                    '{"platform":{"enabled_modules":["personal_intelligence"]}}'::jsonb)
                """,
                new { TenantId, TenantCode },
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO public.users (id, tenant_id, username, email, password_hash)
                VALUES (@UserId, @TenantId, @Username, 'sb-operator@second-brain.local', @Hash)
                """,
                new { UserId, TenantId, Username, Hash = hash },
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO second_brain_host.operator (user_id, roles, permissions)
                VALUES (@UserId, ARRAY['ADMIN']::text[], @Permissions)
                """,
                new { UserId, Permissions },
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_personal_intelligence.person (person_id, user_id, tenant_id, display_name)
                VALUES (@PersonId, @UserId, @TenantId, 'Paper operator')
                """,
                new { PersonId, UserId, TenantId },
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_personal_intelligence.exchange (exchange_id, tenant_id, exchange_code, exchange_name)
                VALUES (@ExchangeId, @TenantId, 'PAPER', 'Paper fixture')
                """,
                new { ExchangeId, TenantId },
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_personal_intelligence.trading_account
                    (trading_account_id, person_id, tenant_id, exchange_id, external_account_id, account_name)
                VALUES (@AccountId, @PersonId, @TenantId, @ExchangeId, 'paper-fixture', 'Paper fixture')
                """,
                new { AccountId, PersonId, TenantId, ExchangeId },
                cancellationToken: cancellationToken));
    }

    public static void Map(WebApplication app, string secret)
    {
        app.MapPost("/api/auth/login", async (LoginBody body, IDbConnectionFactory db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
                return Results.BadRequest(new { message = "Username and password are required." });
            var tenantCode = string.IsNullOrWhiteSpace(body.TenantCode) ? TenantCode : body.TenantCode.Trim();
            await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<OperatorRow>(
                new CommandDefinition(
                    """
                    SELECT u.id AS Id, u.tenant_id AS TenantId, t.tenant_code AS TenantCode,
                           u.username AS Username, u.email AS Email, u.password_hash AS PasswordHash,
                           o.roles AS Roles, o.permissions AS Permissions
                    FROM public.users u
                    JOIN public.tenants t ON t.id = u.tenant_id
                    JOIN second_brain_host.operator o ON o.user_id = u.id
                    WHERE lower(u.username) = lower(@Username)
                      AND t.tenant_code = @TenantCode
                      AND u.deleted_at IS NULL
                      AND u.status = 1
                    """,
                    new { Username = body.Username.Trim(), TenantCode = tenantCode },
                    cancellationToken: cancellationToken));
            if (row is null || !BCrypt.Net.BCrypt.Verify(body.Password, row.PasswordHash))
                return Results.Unauthorized();
            return Results.Ok(await IssueAsync(conn, row, secret, cancellationToken));
        });

        app.MapPost("/api/auth/refresh", async (RefreshBody body, IDbConnectionFactory db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(body.RefreshToken))
                return Results.Unauthorized();
            await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
            var hash = Hash(body.RefreshToken);
            var userId = await conn.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    """
                    SELECT user_id FROM second_brain_host.refresh_token
                    WHERE token_hash = @Hash AND revoked_at IS NULL AND expires_at > NOW()
                    """,
                    new { Hash = hash },
                    cancellationToken: cancellationToken));
            if (userId is null)
                return Results.Unauthorized();
            await conn.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE second_brain_host.refresh_token SET revoked_at = NOW() WHERE token_hash = @Hash",
                    new { Hash = hash },
                    cancellationToken: cancellationToken));
            var row = await LoadAsync(conn, userId.Value, cancellationToken);
            if (row is null)
                return Results.Unauthorized();
            return Results.Ok(await IssueAsync(conn, row, secret, cancellationToken));
        });

        app.MapPost("/api/auth/logout", async (RefreshBody body, IDbConnectionFactory db, CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(body.RefreshToken))
            {
                await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE second_brain_host.refresh_token SET revoked_at = NOW() WHERE token_hash = @Hash",
                        new { Hash = Hash(body.RefreshToken) },
                        cancellationToken: cancellationToken));
            }

            return Results.NoContent();
        });

        app.MapGet("/api/auth/me", async (ClaimsPrincipal user, IDbConnectionFactory db, CancellationToken cancellationToken) =>
        {
            if (!CoreAuthority.TryResolveCaller(user, out var caller))
                return Results.Unauthorized();
            await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
            var row = await LoadAsync(conn, caller.UserId, cancellationToken);
            return row is null ? Results.Unauthorized() : Results.Ok(Profile(row));
        }).RequireAuthorization();

        app.MapGet("/api/system/tenant-platform", async (AgentPlatformSettings platform, CancellationToken cancellationToken) =>
        {
            var enabled = await platform.IsModuleEnabledAsync("personal_intelligence", cancellationToken);
            var modules = enabled ? new[] { "personal_intelligence" } : Array.Empty<string>();
            return Results.Ok(new
            {
                schemaVersion = 1,
                vertical = "hybrid",
                enabledModules = modules,
                allowedModules = modules,
                maxBranches = (int?)null,
                i18n = new
                {
                    defaultLocale = "vi-VN",
                    supportedLocales = new[] { "vi-VN" },
                    fallbackLocale = "vi-VN",
                    adminDefaultLocale = "vi-VN",
                    customerAppDefaultLocale = "vi-VN",
                },
                features = new Dictionary<string, bool>(),
                labels = new Dictionary<string, string>(),
            });
        }).RequireAuthorization();
    }

    static async Task<object> IssueAsync(NpgsqlConnection conn, OperatorRow row, string secret, CancellationToken cancellationToken)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var token = WriteToken(row, secret, expires);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO second_brain_host.refresh_token (token_hash, user_id, expires_at)
                VALUES (@Hash, @UserId, @Expires)
                """,
                new { Hash = Hash(refresh), UserId = row.Id, Expires = expires.AddDays(7) },
                cancellationToken: cancellationToken));
        return new
        {
            accessToken = token,
            refreshToken = refresh,
            accessTokenExpiresAt = expires,
            user = Profile(row),
        };
    }

    static object Profile(OperatorRow row) => new
    {
        id = row.Id,
        tenantId = row.TenantId,
        tenantCode = row.TenantCode,
        username = row.Username,
        email = row.Email,
        roles = row.Roles,
        permissions = row.Permissions,
    };

    static string WriteToken(OperatorRow row, string secret, DateTimeOffset expires)
    {
        var claims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, row.Id.ToString()),
            new("tenant_id", row.TenantId.ToString()),
            new("tenant_code", row.TenantCode),
            new("role", "ADMIN"),
        };
        foreach (var permission in row.Permissions)
            claims.Add(new Claim("permission", permission));
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "SecondBrainAgent",
            audience: "SecondBrainAgent",
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);
    }

    static async Task<OperatorRow?> LoadAsync(NpgsqlConnection conn, Guid userId, CancellationToken cancellationToken) =>
        await conn.QuerySingleOrDefaultAsync<OperatorRow>(
            new CommandDefinition(
                """
                SELECT u.id AS Id, u.tenant_id AS TenantId, t.tenant_code AS TenantCode,
                       u.username AS Username, u.email AS Email, u.password_hash AS PasswordHash,
                       o.roles AS Roles, o.permissions AS Permissions
                FROM public.users u
                JOIN public.tenants t ON t.id = u.tenant_id
                JOIN second_brain_host.operator o ON o.user_id = u.id
                WHERE u.id = @UserId AND u.deleted_at IS NULL AND u.status = 1
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public sealed class LoginBody
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? TenantCode { get; set; }
    }

    public sealed class RefreshBody
    {
        public string? RefreshToken { get; set; }
    }

    sealed class OperatorRow
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string TenantCode { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string[] Roles { get; set; } = [];
        public string[] Permissions { get; set; } = [];
    }
}
