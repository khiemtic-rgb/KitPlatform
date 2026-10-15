using Dapper;
using Xunit;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Content;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Npgsql;

namespace KitPlatform.Platform.Tests;

public class ProductionCustomerIsolationTests
{
    [Fact]
    public void Shared_canon_is_separate_from_customer_production()
    {
        Assert.True(FamixaResourceScope.IsSharedCanon("video_project"));
        Assert.True(FamixaResourceScope.IsSharedCanon("video_character_dna"));
        Assert.False(FamixaResourceScope.IsCustomerProduction("video_project"));
        Assert.True(FamixaResourceScope.IsCustomerProduction("video_production"));
        Assert.True(FamixaResourceScope.IsCustomerProduction("video_production_shot"));
        Assert.True(FamixaResourceScope.IsCustomerProduction("video_image_generation_execution"));
        Assert.True(FamixaResourceScope.IsCustomerProduction("video_generation_execution"));
        Assert.False(FamixaResourceScope.IsSharedCanon("video_production"));
        Assert.Equal(ProductionNotFoundException.FixedMessage, new ProductionNotFoundException().Message);
    }

    [Fact]
    public async Task Two_customers_do_not_share_episode_shot_or_generation()
    {
        var cs = Environment.GetEnvironmentVariable("KPA_PHASE15_DISPOSABLE_PG");
        Assert.False(string.IsNullOrWhiteSpace(cs), "KPA_PHASE15_DISPOSABLE_PG is not set.");
        Assert.DoesNotContain("Port=5432", cs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database=kitplatform", cs, StringComparison.OrdinalIgnoreCase);

        var db = new NpgsqlConnectionFactory(cs);
        await using (var probe = await db.CreateOpenConnectionAsync())
        {
            var ident = await probe.QuerySingleAsync<(string Db, int Port)>(
                "SELECT current_database() AS Db, inet_server_port() AS Port;");
            Assert.Equal("kpa_phase15_once", ident.Db);
            Assert.Equal(54341, ident.Port);
        }

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using (var conn = await db.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO public.tenants (id, tenant_code, tenant_name)
                VALUES (@A, @CodeA, 'Customer A'), (@B, @CodeB, 'Customer B');
                """, new
            {
                A = tenantA,
                B = tenantB,
                CodeA = "KPA15A" + tenantA.ToString("N")[..8],
                CodeB = "KPA15B" + tenantB.ToString("N")[..8],
            });
        }

        var seriesRows = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.series_build", Guid.Empty);
        var catalog = await LoadCatalogAsync(db);
        var repoA = new ContentRepository(db, new FixedTenant(tenantA));
        var repoB = new ContentRepository(db, new FixedTenant(tenantB));
        var code = "EP01KPA" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var prodA = await repoA.InsertVideoProductionAsync(
            Guid.NewGuid(), catalog.ProjectId, catalog.UniverseId, code, "A", null, CancellationToken.None);
        var prodB = await repoB.InsertVideoProductionAsync(
            Guid.NewGuid(), catalog.ProjectId, catalog.UniverseId, code, "B", null, CancellationToken.None);
        Assert.NotEqual(prodA.Id, prodB.Id);

        Assert.Null(await repoA.GetVideoProductionAsync(prodB.Id, CancellationToken.None));
        Assert.Null(await repoB.GetVideoProductionAsync(prodA.Id, CancellationToken.None));
        var listed = await repoA.ListVideoProductionsAsync(catalog.ProjectCode, CancellationToken.None);
        Assert.Contains(listed, p => p.Id == prodA.Id);
        Assert.DoesNotContain(listed, p => p.Id == prodB.Id);

        var beforeState = await ScalarAsync<string>(db, "SELECT state FROM pack_content.video_production WHERE id = @Id", prodB.Id);
        var beforeEvents = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_production_event WHERE production_id = @Id", prodB.Id);
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            repoA.UpdateVideoProductionStateAsync(prodB.Id, "SCRIPT_APPROVED", "a", "cross", beforeState, CancellationToken.None));
        Assert.Equal(beforeState, await ScalarAsync<string>(db, "SELECT state FROM pack_content.video_production WHERE id = @Id", prodB.Id));
        Assert.Equal(beforeEvents, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_production_event WHERE production_id = @Id", prodB.Id));

        var hidden = await ScalarAsync<Guid>(db, "SELECT id FROM pack_content.video_production WHERE tenant_id IS NULL LIMIT 1", Guid.Empty);
        Assert.NotEqual(Guid.Empty, hidden);
        var hiddenState = await ScalarAsync<string>(db, "SELECT state FROM pack_content.video_production WHERE id = @Id", hidden);
        var hiddenEvents = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_production_event WHERE production_id = @Id", hidden);
        Assert.Null(await repoA.GetVideoProductionAsync(hidden, CancellationToken.None));
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            repoA.UpdateVideoProductionStateAsync(hidden, "SCRIPT_APPROVED", "a", "null-owner", hiddenState, CancellationToken.None));
        Assert.Equal(hiddenState, await ScalarAsync<string>(db, "SELECT state FROM pack_content.video_production WHERE id = @Id", hidden));
        Assert.Equal(hiddenEvents, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_production_event WHERE production_id = @Id", hidden));
        Assert.Equal(hidden, await ScalarAsync<Guid>(db, "SELECT id FROM pack_content.video_production WHERE id = @Id AND tenant_id IS NULL", hidden));

        var shotCode = "SHKPA" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var shotsA = new KitVideoProductionShotRepository(db, new FixedTenant(tenantA));
        var shotsB = new KitVideoProductionShotRepository(db, new FixedTenant(tenantB));
        var shotA = await shotsA.InsertDraftAsync(Shot(catalog, shotCode), CancellationToken.None);
        var shotB = await shotsB.InsertDraftAsync(Shot(catalog, shotCode), CancellationToken.None);
        Assert.NotEqual(shotA.Id, shotB.Id);
        Assert.Null(await shotsA.GetByIdAsync(shotB.Id, CancellationToken.None));
        var shotStatus = await ScalarAsync<string>(db, "SELECT shot_status FROM pack_content.video_production_shot WHERE id = @Id", shotB.Id);
        var shotEvents = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_production_shot_event WHERE shot_id = @Id", shotB.Id);
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            shotsA.SetStatusAsync(shotB.Id, "REJECTED", "cross", CancellationToken.None));
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            shotsA.InsertEventAsync(shotB.Id, "SHOT_REJECTED", new { cross = true }, "a", CancellationToken.None));
        Assert.Equal(shotStatus, await ScalarAsync<string>(db, "SELECT shot_status FROM pack_content.video_production_shot WHERE id = @Id", shotB.Id));
        Assert.Equal(shotEvents, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_production_shot_event WHERE shot_id = @Id", shotB.Id));

        var hiddenShot = await ScalarAsync<Guid>(db, "SELECT id FROM pack_content.video_production_shot WHERE tenant_id IS NULL LIMIT 1", Guid.Empty);
        if (hiddenShot != Guid.Empty)
        {
            Assert.Null(await shotsA.GetByIdAsync(hiddenShot, CancellationToken.None));
            var hiddenShotStatus = await ScalarAsync<string>(db, "SELECT shot_status FROM pack_content.video_production_shot WHERE id = @Id", hiddenShot);
            await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
                shotsA.SetStatusAsync(hiddenShot, "REJECTED", "null", CancellationToken.None));
            Assert.Equal(hiddenShotStatus, await ScalarAsync<string>(db, "SELECT shot_status FROM pack_content.video_production_shot WHERE id = @Id", hiddenShot));
        }

        var projectHash = await ScalarAsync<string>(db, "SELECT md5(visual_json::text) FROM pack_content.video_project WHERE id = @Id", catalog.ProjectId);
        var shotBHash = await ScalarAsync<string>(db, "SELECT md5(spec_json::text) FROM pack_content.video_production_shot WHERE id = @Id", shotB.Id);
        await shotsA.UpdateSpecAsync(shotA.Id, """{"private":true}""", "DRAFT", "mine", CancellationToken.None);
        Assert.Equal(projectHash, await ScalarAsync<string>(db, "SELECT md5(visual_json::text) FROM pack_content.video_project WHERE id = @Id", catalog.ProjectId));
        Assert.Equal(shotBHash, await ScalarAsync<string>(db, "SELECT md5(spec_json::text) FROM pack_content.video_production_shot WHERE id = @Id", shotB.Id));
        var projectsA = await repoA.ListVideoProjectsAsync(CancellationToken.None);
        var projectsB = await repoB.ListVideoProjectsAsync(CancellationToken.None);
        Assert.Contains(projectsA, p => p.Id == catalog.ProjectId);
        Assert.Contains(projectsB, p => p.Id == catalog.ProjectId);

        if (catalog.Image is not null)
        {
            var imagesA = new ImageGenerationExecutionRepository(db, new FixedTenant(tenantA));
            var imagesB = new ImageGenerationExecutionRepository(db, new FixedTenant(tenantB));
            var executionA = await imagesA.InsertAsync(Image(catalog.Image, shotA.Id), CancellationToken.None);
            Assert.Null(await imagesB.GetByIdAsync(executionA.Id, CancellationToken.None));
            var execStatus = await ScalarAsync<string>(db, "SELECT execution_status FROM pack_content.video_image_generation_execution WHERE id = @Id", executionA.Id);
            var execEvents = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_image_generation_execution_event WHERE execution_id = @Id", executionA.Id);
            executionA.ExecutionStatus = "REJECTED";
            await Assert.ThrowsAsync<ProductionNotFoundException>(() => imagesB.UpdateAsync(executionA, CancellationToken.None));
            await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
                imagesB.InsertEventAsync(executionA.Id, shotA.Id, "DIRECTOR_REJECTED", new { cross = true }, "b", CancellationToken.None));
            Assert.Equal(execStatus, await ScalarAsync<string>(db, "SELECT execution_status FROM pack_content.video_image_generation_execution WHERE id = @Id", executionA.Id));
            Assert.Equal(execEvents, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_image_generation_execution_event WHERE execution_id = @Id", executionA.Id));
        }

        Assert.Equal(seriesRows, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.series_build", Guid.Empty));
    }

    [Fact]
    public async Task Two_customers_do_not_share_video_generation()
    {
        var db = await OpenDisposableAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using (var conn = await db.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO public.tenants (id, tenant_code, tenant_name)
                VALUES (@A, @CodeA, 'Customer A'), (@B, @CodeB, 'Customer B');
                """, new
            {
                A = tenantA,
                B = tenantB,
                CodeA = "KPA15A" + tenantA.ToString("N")[..8],
                CodeB = "KPA15B" + tenantB.ToString("N")[..8],
            });
        }

        var catalog = await LoadCatalogAsync(db);
        var repoA = new ContentRepository(db, new FixedTenant(tenantA));
        var repoB = new ContentRepository(db, new FixedTenant(tenantB));
        var episode = "EP01KPA" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var prodA = await repoA.InsertVideoProductionAsync(
            Guid.NewGuid(), catalog.ProjectId, catalog.UniverseId, episode, "FAMIXA", null, CancellationToken.None);
        var prodB = await repoB.InsertVideoProductionAsync(
            Guid.NewGuid(), catalog.ProjectId, catalog.UniverseId, episode, "FAMIXA", null, CancellationToken.None);
        Assert.NotEqual(prodA.Id, prodB.Id);

        var shotCode = "SHKPA" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var shotsA = new KitVideoProductionShotRepository(db, new FixedTenant(tenantA));
        var shotsB = new KitVideoProductionShotRepository(db, new FixedTenant(tenantB));
        var rowA = Shot(catalog, shotCode);
        var rowB = Shot(catalog, shotCode);
        rowA.ShotSeq = 4242;
        rowB.ShotSeq = 4242;
        rowA.CharacterId = "CHAR-001";
        rowB.CharacterId = "CHAR-001";
        var shotA = await shotsA.InsertDraftAsync(rowA, CancellationToken.None);
        var shotB = await shotsB.InsertDraftAsync(rowB, CancellationToken.None);
        Assert.NotEqual(shotA.Id, shotB.Id);
        Assert.Equal(shotA.ShotCode, shotB.ShotCode);

        var fingerprint = "kpa15-shared-fp-" + Guid.NewGuid().ToString("N");
        var videosA = new VideoGenerationExecutionRepository(db, new FixedTenant(tenantA));
        var videosB = new VideoGenerationExecutionRepository(db, new FixedTenant(tenantB));
        var videoA = await videosA.InsertAsync(Video(shotA.Id, fingerprint), CancellationToken.None);
        var videoB = await videosB.InsertAsync(Video(shotB.Id, fingerprint), CancellationToken.None);
        Assert.NotEqual(videoA.Id, videoB.Id);
        Assert.Equal(fingerprint, videoA.ExecutionFingerprint);
        Assert.Equal(fingerprint, videoB.ExecutionFingerprint);
        Assert.Equal("FAMIXA", videoA.SeriesId);
        Assert.Equal(videoA.SeriesId, videoB.SeriesId);

        Assert.Null(await videosA.GetByIdAsync(videoB.Id, CancellationToken.None));
        Assert.Null(await videosB.GetByIdAsync(videoA.Id, CancellationToken.None));
        var byFingerprint = await videosA.GetByFingerprintAsync(fingerprint, CancellationToken.None);
        Assert.NotNull(byFingerprint);
        Assert.Equal(videoA.Id, byFingerprint.Id);

        var again = await videosA.InsertAsync(Video(shotA.Id, fingerprint), CancellationToken.None);
        Assert.Equal(videoA.Id, again.Id);
        Assert.Null(await videosA.GetByIdAsync(videoB.Id, CancellationToken.None));

        var beforeRows = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_generation_execution WHERE shot_id = @Id", shotB.Id);
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            videosA.InsertAsync(Video(shotB.Id, "kpa15-foreign-shot-" + Guid.NewGuid().ToString("N")), CancellationToken.None));
        Assert.Equal(beforeRows, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_generation_execution WHERE shot_id = @Id", shotB.Id));

        var status = await ScalarAsync<string>(db, "SELECT execution_status FROM pack_content.video_generation_execution WHERE id = @Id", videoB.Id);
        var events = await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_generation_execution_event WHERE execution_id = @Id", videoB.Id);
        videoB.ExecutionStatus = "DIRECTOR_REJECTED";
        await Assert.ThrowsAsync<ProductionNotFoundException>(() => videosA.UpdateAsync(videoB, CancellationToken.None));
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            videosA.InsertEventAsync(videoB.Id, shotB.Id, "VIDEO_DIRECTOR_REJECTED", new { cross = true }, "a", CancellationToken.None));
        Assert.Equal(status, await ScalarAsync<string>(db, "SELECT execution_status FROM pack_content.video_generation_execution WHERE id = @Id", videoB.Id));
        Assert.Equal(events, await ScalarAsync<long>(db, "SELECT COUNT(*) FROM pack_content.video_generation_execution_event WHERE execution_id = @Id", videoB.Id));
        Assert.Equal(videoB.Id, await QueryScalarAsync<Guid>(
            db,
            "SELECT id FROM pack_content.video_generation_execution WHERE id = @Id AND tenant_id = @Owner",
            new { Id = videoB.Id, Owner = tenantB }));

        var hidden = await ScalarAsync<Guid>(db, "SELECT id FROM pack_content.video_generation_execution WHERE tenant_id IS NULL LIMIT 1", Guid.Empty);
        Assert.NotEqual(Guid.Empty, hidden);
        var hiddenShot = await ScalarAsync<Guid>(db, "SELECT shot_id FROM pack_content.video_generation_execution WHERE id = @Id", hidden);
        var hiddenStatus = await ScalarAsync<string>(db, "SELECT execution_status FROM pack_content.video_generation_execution WHERE id = @Id", hidden);
        var hiddenEvents = await QueryScalarAsync<long>(
            db,
            "SELECT COUNT(*) FROM pack_content.video_generation_execution_event WHERE execution_id = @Id OR shot_id = @Shot",
            new { Id = hidden, Shot = hiddenShot });
        Assert.Null(await videosA.GetByIdAsync(hidden, CancellationToken.None));
        var hiddenRow = new VideoGenerationExecutionRepository.Row { Id = hidden, ShotId = hiddenShot, ExecutionStatus = "DIRECTOR_REJECTED" };
        await Assert.ThrowsAsync<ProductionNotFoundException>(() => videosA.UpdateAsync(hiddenRow, CancellationToken.None));
        await Assert.ThrowsAsync<ProductionNotFoundException>(() =>
            videosA.InsertEventAsync(hidden, hiddenShot, "VIDEO_DIRECTOR_REJECTED", new { cross = true }, "a", CancellationToken.None));
        Assert.Equal(hiddenStatus, await ScalarAsync<string>(db, "SELECT execution_status FROM pack_content.video_generation_execution WHERE id = @Id", hidden));
        Assert.Equal(hiddenEvents, await QueryScalarAsync<long>(
            db,
            "SELECT COUNT(*) FROM pack_content.video_generation_execution_event WHERE execution_id = @Id OR shot_id = @Shot",
            new { Id = hidden, Shot = hiddenShot }));
        Assert.Equal(hidden, await ScalarAsync<Guid>(db, "SELECT id FROM pack_content.video_generation_execution WHERE id = @Id AND tenant_id IS NULL", hidden));
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

    private static async Task<Catalog> LoadCatalogAsync(IDbConnectionFactory db)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        var project = await conn.QuerySingleAsync<(Guid ProjectId, string ProjectCode, Guid UniverseId)>( """
            SELECT p.id AS ProjectId, p.project_code AS ProjectCode, u.id AS UniverseId
            FROM pack_content.video_project p
            JOIN pack_content.video_universe u ON u.project_id = p.id
            ORDER BY p.project_code
            LIMIT 1;
            """);
        var shot = await conn.QuerySingleAsync<KitVideoProductionShotRepository.ShotRow>("""
            SELECT master_reference_id AS MasterReferenceId, master_code AS MasterCode, master_sha256 AS MasterSha256,
                   character_dna_id AS CharacterDnaId, dna_code AS DnaCode, dna_sha256 AS DnaSha256,
                   production_pack_id AS ProductionPackId, prp_code AS PrpCode, prp_sha256 AS PrpSha256
            FROM pack_content.video_production_shot
            LIMIT 1;
            """);
        var image = await conn.QuerySingleOrDefaultAsync<ImageGenerationExecutionRepository.Row>("""
            SELECT igc_id AS IgcId, shot_contract_id AS ShotContractId, prompt_id AS PromptId,
                   series_id AS SeriesId, character_id AS CharacterId, era_id AS EraId
            FROM pack_content.video_image_generation_execution
            LIMIT 1;
            """);
        return new Catalog(project.ProjectId, project.ProjectCode, project.UniverseId, shot, image);
    }

    private static KitVideoProductionShotRepository.ShotRow Shot(Catalog catalog, string shotCode) => new()
    {
        Id = Guid.NewGuid(),
        ShotCode = shotCode,
        CharacterId = "CHAR-001",
        CharacterName = "MINH",
        EraId = "ERA-01",
        ShotSeq = Random.Shared.Next(1000, 9000),
        ShotVersion = "V9",
        MasterReferenceId = catalog.Shot.MasterReferenceId,
        MasterCode = catalog.Shot.MasterCode,
        MasterSha256 = catalog.Shot.MasterSha256,
        CharacterDnaId = catalog.Shot.CharacterDnaId,
        DnaCode = catalog.Shot.DnaCode,
        DnaSha256 = catalog.Shot.DnaSha256,
        ProductionPackId = catalog.Shot.ProductionPackId,
        PrpCode = catalog.Shot.PrpCode,
        PrpSha256 = catalog.Shot.PrpSha256,
        SpecJson = "{}",
    };

    private static ImageGenerationExecutionRepository.Row Image(ImageGenerationExecutionRepository.Row source, Guid shotId) => new()
    {
        Id = Guid.NewGuid(),
        ShotId = shotId,
        IgcId = source.IgcId,
        ShotContractId = source.ShotContractId,
        PromptId = source.PromptId,
        SeriesId = source.SeriesId,
        CharacterId = source.CharacterId,
        EraId = source.EraId,
        ExecutionFingerprint = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        QaJson = "{}",
    };

    private static async Task<T> ScalarAsync<T>(IDbConnectionFactory db, string sql, Guid id)
        => await QueryScalarAsync<T>(db, sql, new { Id = id });

    private static async Task<T> QueryScalarAsync<T>(IDbConnectionFactory db, string sql, object args)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<T>(sql, args);
    }

    private sealed record Catalog(
        Guid ProjectId,
        string ProjectCode,
        Guid UniverseId,
        KitVideoProductionShotRepository.ShotRow Shot,
        ImageGenerationExecutionRepository.Row? Image);

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
