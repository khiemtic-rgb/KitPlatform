using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;
using KitPlatform.Packs.Pharmacy.Knowledge;
using KitPlatform.Api.Controllers.Pharmacy;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

[Collection("kitplatform_test-sales-idempotency")]
public sealed class NovixaW5LifecycleIsolatedTests
{
    private const string ConnectionEnvironmentVariable = "POS_IDEMPOTENCY_TEST_CONNECTION";

    [Fact]
    public async Task Publisher_pin_policy_and_retire_follow_the_tenant_boundary()
    {
        var cs = await RequireAsync();
        var code = "w5life" + Guid.NewGuid().ToString("N")[..12];
        var draft = code + "d";
        var world = await SeedAsync(cs);
        var publisherId = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.InReview, "alpha", 100);
            await InsertVersionAsync(conn, draft, "1", PharmacyKnowledgeApproval.Draft, "secret", 100);
            var publication = new PharmacyKnowledgePublicationService(new NpgsqlConnectionFactory(cs));
            var approval = new PharmacyKnowledgeApprovalService(new NpgsqlConnectionFactory(cs));
            var publisher = new PharmacyKnowledgePublicationController(publication, new MutableTenant(Guid.NewGuid(), publisherId));
            var tenantA = new PharmacyKnowledgeApprovalController(
                approval,
                publication,
                new MutableTenant(world.TenantA, world.UserA),
                PharmacyPlatformSettingsStub.Create());
            var tenantB = new PharmacyKnowledgeApprovalController(
                approval,
                publication,
                new MutableTenant(world.TenantB, world.UserB),
                PharmacyPlatformSettingsStub.Create());

            var draftRejected = await Record.ExceptionAsync(() =>
                publication.PublishAsync(draft, "1", publisherId, CancellationToken.None));
            await publication.VerifySourceAsync(
                code,
                "1",
                publisherId,
                new PharmacyKnowledgeSourceVerificationRequest(
                    PharmacyKnowledgeSourceRules.Accepted,
                    "synthetic-citation",
                    "SYNTH-TEST",
                    "https://example.test/k1-fixture",
                    null,
                    "synthetic-org",
                    null,
                    "fixture section"),
                CancellationToken.None);
            var listed = Assert.IsType<OkObjectResult>((await publisher.List(CancellationToken.None)).Result);
            var all = Assert.IsAssignableFrom<IReadOnlyList<PharmacyKnowledgeVersionDto>>(listed.Value);
            Assert.Contains(all, row => row.RuleCode == code && row.LifecycleStatus == PharmacyKnowledgeApproval.InReview);

            var published = Assert.IsType<OkObjectResult>(
                (await publisher.Publish(code, "1", CancellationToken.None)).Result);
            var publishedRow = Assert.IsType<PharmacyKnowledgeVersionDto>(published.Value);
            Assert.Equal(PharmacyKnowledgeApproval.Published, publishedRow.LifecycleStatus);
            Assert.Contains(publishedRow.Keywords, item => item == "alpha");

            var frozen = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                "UPDATE pharmacy_knowledge_rule_version SET keywords = '{changed}' WHERE rule_code = @Code AND version = '1'",
                new { Code = code }));

            var catalogA = Assert.IsType<OkObjectResult>((await tenantA.Catalog(CancellationToken.None)).Result);
            var viewA = Assert.IsType<PharmacyTenantKnowledgeCatalogDto>(catalogA.Value);
            Assert.Contains(viewA.PublishedVersions, row => row.RuleCode == code && row.Version == "1");
            Assert.DoesNotContain(viewA.PublishedVersions, row => row.RuleCode == draft);
            Assert.DoesNotContain(viewA.Adoptions, row => row.RuleCode == code);

            await tenantA.Transition(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source-a", "1"),
                CancellationToken.None);
            await tenantA.SavePolicy(
                code,
                new PharmacyKnowledgePolicyRequest(true, ["dropme"], 140),
                CancellationToken.None);
            var expanded = await tenantA.SavePolicy(
                code,
                new PharmacyKnowledgePolicyRequest(true, null, 140, AddsKeywords: true),
                CancellationToken.None);

            var taxonomy = new SymptomTaxonomyRepository(new NpgsqlConnectionFactory(cs));
            var rulesA = await taxonomy.GetKnowledgeRulesAsync(world.TenantA, ["w5sym"], CancellationToken.None);
            var rulesB = await taxonomy.GetKnowledgeRulesAsync(world.TenantB, ["w5sym"], CancellationToken.None);
            var catalogB = Assert.IsType<OkObjectResult>((await tenantB.Catalog(CancellationToken.None)).Result);
            var viewB = Assert.IsType<PharmacyTenantKnowledgeCatalogDto>(catalogB.Value);

            var retired = Assert.IsType<OkObjectResult>(
                (await publisher.Retire(code, "1", CancellationToken.None)).Result);
            var retiredRow = Assert.IsType<PharmacyKnowledgeVersionDto>(retired.Value);
            var after = await taxonomy.GetKnowledgeRulesAsync(world.TenantA, ["w5sym"], CancellationToken.None);
            var publicationHistory = await publication.ListHistoryAsync(code, CancellationToken.None);
            var approvalHistory = Assert.IsType<OkObjectResult>((await tenantA.History(code, CancellationToken.None)).Result);
            var approvalEvents = Assert.IsAssignableFrom<IReadOnlyList<PharmacyKnowledgeApprovalEventDto>>(approvalHistory.Value);
            var pin = await conn.ExecuteScalarAsync<string>(
                """
                SELECT status FROM pharmacy_knowledge_adoption
                WHERE tenant_id = @TenantId AND rule_code = @Code
                """,
                new { TenantId = world.TenantA, Code = code });
            var blocked = await SuggestAsync(cs, world.TenantA, world.UserA);

            Assert.IsType<InvalidOperationException>(draftRejected);
            Assert.NotNull(frozen);
            Assert.Contains(rulesA, row => row.RuleCode == code && row.ExtraExcludeKeywords.Contains("dropme"));
            Assert.Empty(rulesB);
            Assert.Contains(viewB.PublishedVersions, row => row.RuleCode == code);
            Assert.DoesNotContain(viewB.Adoptions, row => row.RuleCode == code);
            Assert.IsType<BadRequestObjectResult>(expanded.Result);
            Assert.Equal(PharmacyKnowledgeApproval.Retired, retiredRow.LifecycleStatus);
            Assert.DoesNotContain(after, row => row.RuleCode == code);
            Assert.Equal(2, publicationHistory.Count);
            Assert.All(publicationHistory, item => Assert.Equal(publisherId.ToString(), item.ActorUserId));
            Assert.NotEmpty(approvalEvents);
            Assert.Equal(PharmacyKnowledgeApproval.Approved, pin);
            Assert.Equal("stop_sale", blocked.SafetyLevel);
            Assert.Empty(blocked.Suggestions);

            var mutate = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                "DELETE FROM pharmacy_knowledge_publication_event WHERE rule_code = @Code",
                new { Code = code }));
            Assert.NotNull(mutate);
        }
        finally
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM pharmacy_knowledge_adoption WHERE rule_code IN (@Code, @Draft);
                DELETE FROM pharmacy_knowledge_rule_version
                WHERE rule_code = @Draft AND lifecycle_status NOT IN ('PUBLISHED', 'RETIRED');
                DELETE FROM pharmacy_knowledge_rule WHERE rule_code IN (@Code, @Draft);
                DELETE FROM tenants
                WHERE id IN (@TenantA, @TenantB)
                  AND NOT EXISTS (
                      SELECT 1 FROM pharmacy_knowledge_adoption
                      WHERE tenant_id IN (@TenantA, @TenantB)
                  );
                """,
                new { Code = code, Draft = draft, world.TenantA, world.TenantB });
        }
    }

    private static async Task InsertVersionAsync(
        NpgsqlConnection conn,
        string code,
        string version,
        string lifecycle,
        string keyword,
        int priority)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule (rule_code, reason_vi, version, approval_status)
            VALUES (@Code, 'synthetic', '1', 'PENDING_REVIEW')
            ON CONFLICT (rule_code) DO NOTHING;
            INSERT INTO pharmacy_knowledge_rule_version (
                rule_code, version, reason_vi, keywords, priority, lifecycle_status, symptom_id,
                reference_source, source_identity, source_url, issuing_organization, evidence_note,
                source_verification_status, verified_by, verified_at)
            SELECT @Code, @Version, 'synthetic', @Keywords, @Priority, @Lifecycle, s.id,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'synthetic-citation' ELSE NULL END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'SYNTH-TEST' ELSE NULL END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'https://example.test/k1-fixture' ELSE NULL END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'synthetic-org' ELSE NULL END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'fixture section' ELSE NULL END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'ACCEPTED' ELSE 'UNVERIFIED' END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN 'fixture' ELSE NULL END,
                   CASE WHEN @Lifecycle = 'PUBLISHED' THEN NOW() ELSE NULL END
            FROM pharmacy_symptom s
            WHERE s.code = 'w5sym'
            """,
            new { Code = code, Version = version, Keywords = new[] { keyword }, Priority = priority, Lifecycle = lifecycle });
    }

    private static async Task<TenantPair> SeedAsync(string connectionString)
    {
        var world = new TenantPair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantA, @CodeA, @CodeA, 1), (@TenantB, @CodeB, @CodeB, 1);
            INSERT INTO pharmacy_symptom_category (code, label_vi)
            VALUES ('w5cat', 'w5')
            ON CONFLICT (code) DO NOTHING;
            INSERT INTO pharmacy_symptom (code, name_vi, category_code, consultation_mode)
            VALUES ('w5sym', 'w5', 'w5cat', 'otc_assist')
            ON CONFLICT (code) DO NOTHING;
            """,
            new
            {
                world.TenantA,
                world.TenantB,
                CodeA = "w5a" + world.TenantA.ToString("N")[..8],
                CodeB = "w5b" + world.TenantB.ToString("N")[..8],
            });
        return world;
    }

    private static async Task<SuggestPharmacyConsultationResultDto> SuggestAsync(string connectionString, Guid tenantId, Guid userId)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var tenant = new MutableTenant(tenantId, userId);
        var service = new PharmacyConsultationService(
            new ConsultationRepository(db, tenant),
            new SymptomTaxonomyRepository(db),
            new PharmacyConsultationGeminiClient(
                new HttpClient(new ThrowingHandler()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PharmacyConsultationGeminiClient>.Instance),
            new PharmacyConsultationAiConfigProvider(
                new Moq.Mock<KitPlatform.Application.Configuration.ITenantSettingsService>().Object),
            new AllowBranches(),
            tenant);
        return await service.SuggestAsync(
            new SuggestPharmacyConsultationRequest(
                new PharmacyConsultationFactsDto(35, null, null, [], null, false, false, false, ["unconscious"], null),
                Guid.NewGuid()),
            CancellationToken.None);
    }

    private static async Task<string> RequireAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Fail($"Stopped before any connection. Set {ConnectionEnvironmentVariable}.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await NovixaValidationSession.EnsureIsolatedRoleAsync(conn);
        var ready = await conn.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('public.pharmacy_knowledge_publication_event') IS NOT NULL");
        if (!ready)
            Assert.Fail("Apply migration 417 on the isolated database before this test.");
        var sourceColumn = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pharmacy_knowledge_rule_version'
              AND column_name = 'source_verification_status'
            """);
        if (sourceColumn != 1)
            Assert.Fail("Apply migration 423 on the isolated database before this test.");
        return builder.ConnectionString;
    }

    private sealed record TenantPair(Guid TenantA, Guid TenantB, Guid UserA, Guid UserB);

    private sealed class MutableTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    private sealed class AllowBranches : IBranchAccessService
    {
        public Task<BranchAccessScope> GetScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(BranchAccessScope.UnrestrictedScope());

        public Task EnsureWarehouseAccessAsync(Guid warehouseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task EnsureBranchAccessAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(Guid? WarehouseId, Guid[]? AllowedWarehouseIds)> ResolveWarehouseQueryAsync(
            Guid? requestedWarehouseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Guid?, Guid[]?)>((requestedWarehouseId, null));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Suggest must not call an LLM.");
    }
}
