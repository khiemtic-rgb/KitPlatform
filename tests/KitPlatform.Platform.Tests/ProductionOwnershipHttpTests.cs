using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using KitPlatform.Api.Controllers.Content;
using KitPlatform.Application.Content;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests;

/// <summary>
/// HTTP calls through the API host. Ownership is the repository predicate reached by
/// <c>ContentController</c>. Hosted workers are not registered: the disposable database
/// is not the application schema, and those workers are not the ownership check.
/// </summary>
public sealed class ProductionOwnershipHttpTests : IClassFixture<ProductionOwnershipApiFactory>
{
    private readonly ProductionOwnershipApiFactory _factory;

    public ProductionOwnershipHttpTests(ProductionOwnershipApiFactory factory) => _factory = factory;

    [Fact]
    public void Production_di_resolves_content_ops_and_article_series_from_the_api_host()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        Assert.NotNull(provider.GetRequiredService<ContentArticleSeriesRepository>());
        Assert.IsType<ContentOpsService>(provider.GetRequiredService<IContentOpsService>());
        Assert.IsType<ContentArticleSeriesService>(provider.GetRequiredService<IContentArticleSeriesService>());
        _ = ActivatorUtilities.CreateInstance<ContentController>(provider);
        _ = ActivatorUtilities.CreateInstance<ContentArticleSeriesController>(provider);
    }

    [Fact]
    public async Task Video_generation_http_does_not_reveal_missing_foreign_or_unowned_rows()
    {
        var db = await OpenDisposableAsync();
        await EnsureFixtureSchemaAsync(db);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var codeA = "KPA15A" + tenantA.ToString("N")[..8];
        var codeB = "KPA15B" + tenantB.ToString("N")[..8];
        await using (var conn = await db.CreateOpenConnectionAsync())
        {
            const string settings = """{"platform":{"enabled_modules":["kit_content"]}}""";
            await conn.ExecuteAsync("""
                INSERT INTO public.tenants (id, tenant_code, tenant_name, settings)
                VALUES (@A, @CodeA, 'Customer A', @Settings::jsonb),
                       (@B, @CodeB, 'Customer B', @Settings::jsonb);
                """, new { A = tenantA, B = tenantB, CodeA = codeA, CodeB = codeB, Settings = settings });
            await InsertAdminAsync(conn, userA, tenantA, codeA);
            await InsertAdminAsync(conn, userB, tenantB, codeB);
        }

        var catalog = await LoadShotTemplateAsync(db);
        var shotsA = new KitVideoProductionShotRepository(db, new FixedTenant(tenantA));
        var shotsB = new KitVideoProductionShotRepository(db, new FixedTenant(tenantB));
        var shotCode = "SHKPA" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var draftA = TemplateShot(catalog, shotCode);
        var draftB = TemplateShot(catalog, shotCode);
        draftA.ShotSeq = 4242;
        draftB.ShotSeq = 4242;
        var shotA = await shotsA.InsertDraftAsync(draftA, CancellationToken.None);
        var shotB = await shotsB.InsertDraftAsync(draftB, CancellationToken.None);
        Assert.Equal(shotA.ShotCode, shotB.ShotCode);

        var fingerprint = "kpa15-http-fp-" + Guid.NewGuid().ToString("N");
        var videosA = new VideoGenerationExecutionRepository(db, new FixedTenant(tenantA));
        var videosB = new VideoGenerationExecutionRepository(db, new FixedTenant(tenantB));
        var videoA = await videosA.InsertAsync(Video(shotA.Id, fingerprint), CancellationToken.None);
        var videoB = await videosB.InsertAsync(Video(shotB.Id, fingerprint), CancellationToken.None);
        var missingShot = Guid.NewGuid();
        var hidden = await ScalarAsync<Guid>(db, "SELECT id FROM pack_content.video_generation_execution WHERE tenant_id IS NULL LIMIT 1");
        var hiddenShot = await ScalarAsync<Guid>(db, "SELECT shot_id FROM pack_content.video_generation_execution WHERE id = @Id", hidden);
        Assert.NotEqual(Guid.Empty, hidden);

        var before = await SnapshotAsync(db, videoB.Id, shotB.Id, hidden, hiddenShot, missingShot);

        using var client = _factory.CreateClient();
        var secret = _factory.Services.GetRequiredService<IConfiguration>()["Jwt:Secret"];
        Assert.False(string.IsNullOrWhiteSpace(secret));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(secret!, userA, tenantA, codeA));

        var own = await GetGenerationAsync(client, shotA.Id);
        var missing = await GetGenerationAsync(client, missingShot);
        var foreign = await GetGenerationAsync(client, shotB.Id);
        var unowned = await GetGenerationAsync(client, hiddenShot);
        var missingReject = await RejectAsync(client, missingShot);
        var foreignReject = await RejectAsync(client, shotB.Id);
        var unownedReject = await RejectAsync(client, hiddenShot);
        var missingArtifact = await GetArtifactAsync(client, missingShot, Guid.NewGuid());
        var foreignArtifact = await GetArtifactAsync(client, shotB.Id, videoB.Id);
        var unownedArtifact = await GetArtifactAsync(client, hiddenShot, hidden);

        Assert.Equal(HttpStatusCode.OK, own.Status);
        Assert.Contains(videoA.Id.ToString(), own.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(videoB.Id.ToString(), own.Body, StringComparison.OrdinalIgnoreCase);

        AssertSameNotFound(missing, foreign, unowned, tenantA, tenantB, fingerprint, videoB.Id);
        AssertSameNotFound(missingReject, foreignReject, unownedReject, tenantA, tenantB, fingerprint, videoB.Id);

        AssertSameNotFound(missingArtifact, foreignArtifact, unownedArtifact, tenantA, tenantB, fingerprint, videoB.Id);

        var after = await SnapshotAsync(db, videoB.Id, shotB.Id, hidden, hiddenShot, missingShot);
        Assert.Equal(before, after);
    }

    private static void AssertSameNotFound(
        HttpBody missing, HttpBody foreign, HttpBody unowned, Guid tenantA, Guid tenantB, string fingerprint, Guid foreignVideoId)
    {
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);
        Assert.Equal(HttpStatusCode.NotFound, foreign.Status);
        Assert.Equal(HttpStatusCode.NotFound, unowned.Status);
        Assert.Equal(ProductionNotFoundException.FixedMessage, missing.Message);
        Assert.Equal(missing.Message, foreign.Message);
        Assert.Equal(missing.Message, unowned.Message);
        Assert.Equal(missing.Body, foreign.Body);
        Assert.Equal(missing.Body, unowned.Body);
        foreach (var body in new[] { missing.Body, foreign.Body, unowned.Body })
        {
            Assert.DoesNotContain(tenantA.ToString(), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(tenantB.ToString(), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fingerprint, body, StringComparison.Ordinal);
            Assert.DoesNotContain(foreignVideoId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<HttpBody> GetGenerationAsync(HttpClient client, Guid shotId)
    {
        var response = await client.GetAsync($"/api/content/video-engine/shots/{shotId}/video-generation-execution");
        var body = await response.Content.ReadAsStringAsync();
        return new HttpBody(response.StatusCode, body, ReadMessage(body));
    }

    private static async Task<HttpBody> RejectAsync(HttpClient client, Guid shotId)
    {
        using var content = new StringContent("""{"reason":"cross tenant refusal"}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(
            $"/api/content/video-engine/shots/{shotId}/video-generation-execution/reject", content);
        var body = await response.Content.ReadAsStringAsync();
        return new HttpBody(response.StatusCode, body, ReadMessage(body));
    }

    private static async Task<HttpBody> GetArtifactAsync(HttpClient client, Guid shotId, Guid executionId)
    {
        var response = await client.GetAsync(
            $"/api/content/video-engine/shots/{shotId}/video-generation-execution/{executionId}/video");
        var body = await response.Content.ReadAsStringAsync();
        return new HttpBody(response.StatusCode, body, ReadMessage(body));
    }

    private static string? ReadMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body[0] != '{')
            return null;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
    }

    private static string Token(string secret, Guid userId, Guid tenantId, string tenantCode)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new("tenant_code", tenantCode),
            new(ClaimTypes.Role, "ADMIN"),
        };
        var jwt = new JwtSecurityToken(
            issuer: "KitPlatform",
            audience: "KitPlatform.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static async Task InsertAdminAsync(NpgsqlConnection conn, Guid userId, Guid tenantId, string tenantCode)
    {
        var roleId = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO public.users (id, tenant_id, username, email, password_hash, status)
            VALUES (@Id, @TenantId, @Username, @Email, 'x', 1);
            INSERT INTO public.roles (id, role_code) VALUES (@RoleId, 'ADMIN');
            INSERT INTO public.user_roles (user_id, role_id) VALUES (@Id, @RoleId);
            """, new
        {
            Id = userId,
            TenantId = tenantId,
            Username = tenantCode.ToLowerInvariant(),
            Email = tenantCode.ToLowerInvariant() + "@example.test",
            RoleId = roleId,
        });
    }

    private static async Task EnsureFixtureSchemaAsync(IDbConnectionFactory db)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync("""
            ALTER TABLE public.tenants ADD COLUMN IF NOT EXISTS deleted_at timestamptz;
            ALTER TABLE public.tenants ADD COLUMN IF NOT EXISTS business_vertical varchar(40);
            CREATE SCHEMA IF NOT EXISTS kit_workspace;
            CREATE TABLE IF NOT EXISTS kit_workspace.workspace_workspace (
                id uuid PRIMARY KEY,
                tenant_id uuid,
                package_code text,
                is_default boolean,
                deleted_at timestamptz
            );
            CREATE TABLE IF NOT EXISTS public.users (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                username text NOT NULL,
                email text NOT NULL,
                password_hash text NOT NULL,
                status smallint NOT NULL DEFAULT 1,
                deleted_at timestamptz
            );
            CREATE TABLE IF NOT EXISTS public.kit_account_memberships (
                user_id uuid,
                account_id uuid,
                status smallint
            );
            CREATE TABLE IF NOT EXISTS public.roles (
                id uuid PRIMARY KEY,
                role_code text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS public.user_roles (
                user_id uuid,
                role_id uuid
            );
            CREATE TABLE IF NOT EXISTS public.role_permissions (
                role_id uuid,
                permission_id uuid
            );
            CREATE TABLE IF NOT EXISTS public.permissions (
                id uuid PRIMARY KEY,
                permission_code text
            );
            """);
    }

    private static async Task<Counts> SnapshotAsync(
        IDbConnectionFactory db, Guid foreignVideo, Guid foreignShot, Guid hiddenVideo, Guid hiddenShot, Guid missingShot)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        var foreignStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT execution_status FROM pack_content.video_generation_execution WHERE id = @Id", new { Id = foreignVideo });
        var hiddenStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT execution_status FROM pack_content.video_generation_execution WHERE id = @Id", new { Id = hiddenVideo });
        var foreignEvents = await conn.ExecuteScalarAsync<long>("""
            SELECT COUNT(*) FROM pack_content.video_generation_execution_event
            WHERE execution_id = @Video OR shot_id = @Shot
            """, new { Video = foreignVideo, Shot = foreignShot });
        var hiddenEvents = await conn.ExecuteScalarAsync<long>("""
            SELECT COUNT(*) FROM pack_content.video_generation_execution_event
            WHERE execution_id = @Video OR shot_id = @Shot
            """, new { Video = hiddenVideo, Shot = hiddenShot });
        var missingEvents = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM pack_content.video_generation_execution_event WHERE shot_id = @Shot",
            new { Shot = missingShot });
        var foreignAudit = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM pack_content.video_identity_governance_audit WHERE shot_id = @Shot",
            new { Shot = foreignShot });
        var hiddenAudit = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM pack_content.video_identity_governance_audit WHERE shot_id = @Shot",
            new { Shot = hiddenShot });
        var hiddenOwner = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT tenant_id FROM pack_content.video_generation_execution WHERE id = @Id", new { Id = hiddenVideo });
        return new Counts(foreignStatus, hiddenStatus, foreignEvents, hiddenEvents, missingEvents, foreignAudit, hiddenAudit, hiddenOwner);
    }

    private static async Task<NpgsqlConnectionFactory> OpenDisposableAsync()
    {
        var cs = Environment.GetEnvironmentVariable("KPA_PHASE15_DISPOSABLE_PG");
        Assert.False(string.IsNullOrWhiteSpace(cs), "KPA_PHASE15_DISPOSABLE_PG is not set.");
        Assert.DoesNotContain("Port=5432", cs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database=kitplatform", cs, StringComparison.OrdinalIgnoreCase);
        var db = new NpgsqlConnectionFactory(cs!);
        await using var probe = await db.CreateOpenConnectionAsync();
        var ident = await probe.QuerySingleAsync<(string Db, int Port)>(
            "SELECT current_database() AS Db, inet_server_port() AS Port;");
        Assert.Equal("kpa_phase15_once", ident.Db);
        Assert.Equal(54341, ident.Port);
        return db;
    }

    private static async Task<ShotTemplate> LoadShotTemplateAsync(IDbConnectionFactory db)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<ShotTemplate>("""
            SELECT master_reference_id AS MasterReferenceId, master_code AS MasterCode, master_sha256 AS MasterSha256,
                   character_dna_id AS CharacterDnaId, dna_code AS DnaCode, dna_sha256 AS DnaSha256,
                   production_pack_id AS ProductionPackId, prp_code AS PrpCode, prp_sha256 AS PrpSha256
            FROM pack_content.video_production_shot
            LIMIT 1;
            """);
    }

    private static KitVideoProductionShotRepository.ShotRow TemplateShot(ShotTemplate catalog, string shotCode) => new()
    {
        Id = Guid.NewGuid(),
        ShotCode = shotCode,
        CharacterId = "CHAR-001",
        CharacterName = "MINH",
        EraId = "ERA-01",
        ShotSeq = 4242,
        ShotVersion = "V9",
        MasterReferenceId = catalog.MasterReferenceId,
        MasterCode = catalog.MasterCode,
        MasterSha256 = catalog.MasterSha256,
        CharacterDnaId = catalog.CharacterDnaId,
        DnaCode = catalog.DnaCode,
        DnaSha256 = catalog.DnaSha256,
        ProductionPackId = catalog.ProductionPackId,
        PrpCode = catalog.PrpCode,
        PrpSha256 = catalog.PrpSha256,
        SpecJson = "{}",
    };

    private static VideoGenerationExecutionRepository.Row Video(Guid shotId, string fingerprint) => new()
    {
        Id = Guid.NewGuid(),
        ShotId = shotId,
        VideoContractId = Guid.NewGuid(),
        StillExecutionId = Guid.NewGuid(),
        ShotContractId = Guid.NewGuid(),
        PromptId = Guid.NewGuid(),
        IgcId = Guid.NewGuid(),
        SeriesId = "FAMIXA",
        CharacterId = "CHAR-001",
        EraId = "ERA-01",
        DocumentId = "PRODUCTION_VIDEO_GENERATION_EXECUTION_V1",
        ExecutionStatus = "PREFLIGHT",
        Provider = "test",
        ProviderStatus = "PREFLIGHT",
        ProviderConfigVersion = "VIDEO_GENERATION_PROVIDER_V1",
        ExecutionFingerprint = fingerprint,
        IdempotencyKey = fingerprint + "-" + Guid.NewGuid().ToString("N")[..8],
        MasterSha256 = "a",
        DnaSha256 = "a",
        PrpSha256 = "a",
        ShotContractSha256 = "a",
        PromptSha256 = "a",
        IgcSha256 = "a",
        VideoContractSha256 = "a",
        StillArtifactSha256 = "a",
        DurationSeconds = 4,
        Resolution = "1280x720",
        Fps = "24",
        AspectRatio = "16:9",
        ArtifactSha256 = "",
        ArtifactMime = "video/mp4",
        QaJson = "{}",
        CreditStatus = "UNKNOWN",
    };

    private static async Task<T> ScalarAsync<T>(IDbConnectionFactory db, string sql, Guid id = default)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<T>(sql, new { Id = id });
    }

    private sealed record HttpBody(HttpStatusCode Status, string Body, string? Message);

    private sealed record Counts(
        string ForeignStatus,
        string HiddenStatus,
        long ForeignEvents,
        long HiddenEvents,
        long MissingEvents,
        long ForeignAudit,
        long HiddenAudit,
        Guid? HiddenOwner);

    private sealed class ShotTemplate
    {
        public Guid MasterReferenceId { get; set; }
        public string MasterCode { get; set; } = "";
        public string MasterSha256 { get; set; } = "";
        public Guid CharacterDnaId { get; set; }
        public string DnaCode { get; set; } = "";
        public string DnaSha256 { get; set; } = "";
        public Guid ProductionPackId { get; set; }
        public string PrpCode { get; set; } = "";
        public string PrpSha256 { get; set; } = "";
    }

    private sealed class FixedTenant(Guid tenantId) : KitPlatform.Application.Abstractions.ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}

public sealed class ProductionOwnershipApiFactory : WebApplicationFactory<Program>
{
    public ProductionOwnershipApiFactory()
    {
        var cs = Environment.GetEnvironmentVariable("KPA_PHASE15_DISPOSABLE_PG");
        if (string.IsNullOrWhiteSpace(cs)
            || cs.Contains("Port=5432", StringComparison.OrdinalIgnoreCase)
            || cs.Contains("Database=kitplatform", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("KPA_PHASE15_DISPOSABLE_PG must name the disposable database.");
        }

        ConnectionString = cs;
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", cs);
    }

    public string ConnectionString { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["PlatformEvents:Enabled"] = "false",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(descriptor);
            services.AddSingleton<IStartupFilter>(new DisposableDatabaseGuard(ConnectionString));
        });
    }

    private sealed class DisposableDatabaseGuard(string connectionString) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT current_database(), inet_server_port()", conn);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            var database = reader.GetString(0);
            var port = reader.GetInt32(1);
            if (database != "kpa_phase15_once" || port != 54341)
                throw new InvalidOperationException("HTTP host is not connected to the disposable database.");
            return next;
        }
    }
}
