using Dapper;
using KitPlatform.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;
using KitPlatform.Packs.Pharmacy.Knowledge;
using KitPlatform.Api.Controllers.Pharmacy;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

[Collection("kitplatform_test-sales-idempotency")]
public sealed class NovixaW5TenantKnowledgeIsolatedTests
{
    private const string ConnectionEnvironmentVariable = "POS_IDEMPOTENCY_TEST_CONNECTION";

    private static readonly HashSet<string> DeniedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitplatform",
        "kitplatform_test",
        "postgres",
        "novixa_prod",
        "pharmacore",
    };

    [Fact]
    public async Task Seeded_rules_stay_in_review_and_safety_flags_stay_pending()
    {
        var cs = await RequireIsolatedAsync();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var lifecycle = await conn.ExecuteScalarAsync<string>(
            """
            SELECT lifecycle_status
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = 'KR_COUGH_DRY' AND version = '1'
            """);
        var pins = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption WHERE rule_code = 'KR_COUGH_DRY'");
        var pending = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pharmacy_safety_flag_review
            WHERE approval_status = 'PENDING_REVIEW'
              AND flag_code = ANY(@Codes)
            """,
            new { Codes = PharmacySafetyFlagInventory.Items.Select(item => item.FlagCode).ToArray() });

        Assert.Equal(PharmacyKnowledgeApproval.InReview, lifecycle);
        Assert.Equal(0, pins);
        Assert.Equal(13, pending);
    }

    [Fact]
    public async Task Tenants_pin_independent_versions_and_unpublished_versions_are_invisible()
    {
        var cs = await RequireIsolatedAsync();
        var code = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.Published, "alpha");
            await InsertVersionAsync(conn, code, "2", PharmacyKnowledgeApproval.Published, "beta");
            await InsertVersionAsync(conn, code, "3", PharmacyKnowledgeApproval.InReview, "hidden");
            var approval = Service(cs);
            await approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source-a", "1"), world.TenantA, world.UserA);
            await approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source-b", "2"), world.TenantB, world.UserB);
            var rejected = await Record.ExceptionAsync(() => approval.TransitionAsync(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source-a", "3"),
                world.TenantA,
                world.UserA));

            var taxonomy = new SymptomTaxonomyRepository(new NpgsqlConnectionFactory(cs));
            var rulesA = await taxonomy.GetKnowledgeRulesAsync(world.TenantA, ["w5sym"], CancellationToken.None);
            var rulesB = await taxonomy.GetKnowledgeRulesAsync(world.TenantB, ["w5sym"], CancellationToken.None);
            var pins = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption WHERE rule_code = @Code",
                new { Code = code });

            Assert.IsType<InvalidOperationException>(rejected);
            Assert.Equal(2, pins);
            Assert.Contains(rulesA, rule => rule.Keywords.Contains("alpha"));
            Assert.DoesNotContain(rulesA, rule => rule.Keywords.Contains("beta") || rule.Keywords.Contains("hidden"));
            Assert.Contains(rulesB, rule => rule.Keywords.Contains("beta"));
            Assert.DoesNotContain(rulesB, rule => rule.Keywords.Contains("alpha"));
        }
        finally
        {
            await CleanupAsync(conn, code, world);
        }
    }

    [Fact]
    public async Task Tenant_without_a_pin_receives_no_product_rules()
    {
        var cs = await RequireIsolatedAsync();
        var code = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.Published, "alpha");
            await Service(cs).TransitionAsync(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source-a", "1"),
                world.TenantA,
                world.UserA);
            var taxonomy = new SymptomTaxonomyRepository(new NpgsqlConnectionFactory(cs));
            var rulesB = await taxonomy.GetKnowledgeRulesAsync(world.TenantB, ["w5sym"], CancellationToken.None);
            Assert.Empty(rulesB);
        }
        finally
        {
            await CleanupAsync(conn, code, world);
        }
    }

    [Fact]
    public async Task Retired_or_expired_version_drops_out_of_copilot()
    {
        var cs = await RequireIsolatedAsync();
        var retiredCode = Code();
        var expiredCode = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, retiredCode, "1", PharmacyKnowledgeApproval.Published, "live");
            await InsertVersionAsync(conn, expiredCode, "1", PharmacyKnowledgeApproval.InReview, "old");
            await conn.ExecuteAsync(
                """
                UPDATE pharmacy_knowledge_rule_version
                SET effective_to = CURRENT_DATE - 1,
                    reference_source = 'synthetic-citation',
                    source_identity = 'SYNTH-TEST',
                    source_url = 'https://example.test/k1-fixture',
                    issuing_organization = 'synthetic-org',
                    evidence_note = 'fixture section',
                    source_verification_status = 'ACCEPTED',
                    verified_by = 'fixture',
                    verified_at = NOW()
                WHERE rule_code = @ExpiredCode AND version = '1' AND lifecycle_status = 'IN_REVIEW';
                UPDATE pharmacy_knowledge_rule_version
                SET lifecycle_status = 'PUBLISHED'
                WHERE rule_code = @ExpiredCode AND version = '1' AND lifecycle_status = 'IN_REVIEW';
                """,
                new { ExpiredCode = expiredCode });
            var approval = Service(cs);
            await approval.TransitionAsync(retiredCode, new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "1"), world.TenantA, world.UserA);
            var expiredPin = await Record.ExceptionAsync(() => approval.TransitionAsync(
                expiredCode,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "1"),
                world.TenantA,
                world.UserA));
            await conn.ExecuteAsync(
                """
                INSERT INTO pharmacy_knowledge_adoption (
                    tenant_id, rule_code, rule_version, status, approved_by, reference_source)
                VALUES (@TenantId, @ExpiredCode, '1', 'APPROVED', 'synthetic', 'source');
                UPDATE pharmacy_knowledge_rule_version
                SET lifecycle_status = 'RETIRED'
                WHERE rule_code = @RetiredCode AND version = '1';
                """,
                new { TenantId = world.TenantA, ExpiredCode = expiredCode, RetiredCode = retiredCode });

            var rules = await new SymptomTaxonomyRepository(new NpgsqlConnectionFactory(cs))
                .GetKnowledgeRulesAsync(world.TenantA, ["w5sym"], CancellationToken.None);
            Assert.IsType<InvalidOperationException>(expiredPin);
            Assert.DoesNotContain(rules, rule => rule.RuleCode == retiredCode || rule.RuleCode == expiredCode);
        }
        finally
        {
            await CleanupAsync(conn, retiredCode, world);
            await CleanupAsync(conn, expiredCode, world);
        }
    }

    [Fact]
    public async Task Policy_can_exclude_words_and_cannot_expand_or_follow_a_new_version()
    {
        var cs = await RequireIsolatedAsync();
        var code = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.Published, "keep", 100);
            await InsertVersionAsync(conn, code, "2", PharmacyKnowledgeApproval.Published, "next", 100);
            var approval = Service(cs);
            await approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "1"), world.TenantA, world.UserA);
            await approval.SavePolicyAsync(
                code,
                new PharmacyKnowledgePolicyRequest(true, ["dropme"], 140),
                world.TenantA,
                world.UserA);
            var expanded = await Record.ExceptionAsync(() => approval.SavePolicyAsync(
                code,
                new PharmacyKnowledgePolicyRequest(true, null, 140, AddsKeywords: true),
                world.TenantA,
                world.UserA));
            var stronger = await Record.ExceptionAsync(() => approval.SavePolicyAsync(
                code,
                new PharmacyKnowledgePolicyRequest(true, null, 10),
                world.TenantA,
                world.UserA));
            var categories = await Record.ExceptionAsync(() => approval.SavePolicyAsync(
                code,
                new PharmacyKnowledgePolicyRequest(true, null, 140, AddsCategories: true),
                world.TenantA,
                world.UserA));

            var taxonomy = new SymptomTaxonomyRepository(new NpgsqlConnectionFactory(cs));
            var before = await taxonomy.GetKnowledgeRulesAsync(world.TenantA, ["w5sym"], CancellationToken.None);
            await approval.SavePolicyAsync(
                code,
                new PharmacyKnowledgePolicyRequest(false, ["dropme"], 140),
                world.TenantA,
                world.UserA);
            var hidden = await SuggestAsync(
                cs,
                world.TenantA,
                world.UserA,
                new PharmacyConsultationFactsDto(35, null, null, ["w5sym"], null, false, false, false, [], null));
            await approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "2"), world.TenantA, world.UserA);
            var after = await taxonomy.GetKnowledgeRulesAsync(world.TenantA, ["w5sym"], CancellationToken.None);

            Assert.IsType<InvalidOperationException>(expanded);
            Assert.IsType<InvalidOperationException>(stronger);
            Assert.IsType<InvalidOperationException>(categories);
            Assert.Contains(before.Single().ExtraExcludeKeywords, item => item == "dropme");
            Assert.Empty(hidden.Suggestions);
            Assert.Equal("Chưa có quy tắc gợi ý đã được duyệt cho triệu chứng này — dược sĩ tư vấn trực tiếp.", hidden.BlockReason);
            Assert.Contains(before.Single().Keywords, item => item == "keep");
            Assert.Contains(after.Single().Keywords, item => item == "next");
            Assert.DoesNotContain(after.Single().Keywords, item => item == "keep");
            Assert.Empty(after.Single().ExtraExcludeKeywords);
        }
        finally
        {
            await CleanupAsync(conn, code, world);
        }
    }

    [Fact]
    public async Task Admin_cannot_approve_and_tenant_a_cannot_change_tenant_b()
    {
        var cs = await RequireIsolatedAsync();
        var code = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.Published, "alpha");
            Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: false));
            Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: false));

            var service = Service(cs);
            var publication = new PharmacyKnowledgePublicationService(new NpgsqlConnectionFactory(cs));
            var controllerA = new PharmacyKnowledgeApprovalController(service, publication, new MutableTenant(world.TenantA, world.UserA), PharmacyPlatformSettingsStub.Create());
            await controllerA.Transition(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source-a", "1"),
                CancellationToken.None);
            var controllerB = new PharmacyKnowledgeApprovalController(service, publication, new MutableTenant(world.TenantB, world.UserB), PharmacyPlatformSettingsStub.Create());
            var history = await controllerB.History(code, CancellationToken.None);
            var denied = await controllerB.SavePolicy(
                code,
                new PharmacyKnowledgePolicyRequest(false, ["nope"], null),
                CancellationToken.None);
            var pinsB = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption
                WHERE tenant_id = @TenantId AND rule_code = @Code
                """,
                new { TenantId = world.TenantB, Code = code });

            var ok = Assert.IsType<OkObjectResult>(history.Result);
            var events = Assert.IsAssignableFrom<IReadOnlyList<PharmacyKnowledgeApprovalEventDto>>(ok.Value);
            Assert.Empty(events);
            Assert.IsType<BadRequestObjectResult>(denied.Result);
            Assert.Equal(0, pinsB);
        }
        finally
        {
            await CleanupAsync(conn, code, world);
        }
    }

    [Fact]
    public async Task Risk_flag_runtime_follows_publication_status_not_is_active()
    {
        var cs = await RequireIsolatedAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var symptom = "w5sym" + suffix;
        var category = "w5cat" + suffix;
        var draftCode = "w5draft" + suffix;
        var liveCode = "w5live" + suffix;
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pharmacy_symptom_category (code, label_vi) VALUES (@Category, @Category);
                INSERT INTO pharmacy_symptom (code, name_vi, category_code, consultation_mode)
                VALUES (@Symptom, @Symptom, @Category, 'otc_assist');
                INSERT INTO pharmacy_consultation_risk_flag (
                    code, name_vi, severity, action, message_vi, safety_level, is_active, publication_status)
                VALUES
                    (@DraftCode, @DraftCode, 'caution', 'caution', 'draft', 'caution', TRUE, 'DRAFT'),
                    (@LiveCode, @LiveCode, 'caution', 'caution', 'live', 'caution', FALSE, 'PUBLISHED');
                INSERT INTO pharmacy_symptom_risk_rule (symptom_id, risk_flag_id, is_active)
                SELECT s.id, f.id, FALSE
                FROM pharmacy_symptom s
                JOIN pharmacy_consultation_risk_flag f ON f.code IN (@DraftCode, @LiveCode)
                WHERE s.code = @Symptom;
                """,
                new { Category = category, Symptom = symptom, DraftCode = draftCode, LiveCode = liveCode });

            var taxonomy = new SymptomTaxonomyRepository(new NpgsqlConnectionFactory(cs));
            var flags = await taxonomy.GetSymptomRiskFlagsAsync([symptom], CancellationToken.None);
            await conn.ExecuteAsync(
                "UPDATE pharmacy_consultation_risk_flag SET is_active = NOT is_active WHERE code IN (@DraftCode, @LiveCode)",
                new { DraftCode = draftCode, LiveCode = liveCode });
            var after = await taxonomy.GetSymptomRiskFlagsAsync([symptom], CancellationToken.None);

            Assert.Contains(flags, flag => flag.FlagCode == liveCode);
            Assert.DoesNotContain(flags, flag => flag.FlagCode == draftCode);
            Assert.Contains(after, flag => flag.FlagCode == liveCode);
            Assert.DoesNotContain(after, flag => flag.FlagCode == draftCode);
        }
        finally
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM pharmacy_symptom_risk_rule
                WHERE risk_flag_id IN (SELECT id FROM pharmacy_consultation_risk_flag WHERE code IN (@DraftCode, @LiveCode));
                DELETE FROM pharmacy_consultation_risk_flag WHERE code IN (@DraftCode, @LiveCode);
                DELETE FROM pharmacy_symptom WHERE code = @Symptom;
                DELETE FROM pharmacy_symptom_category WHERE code = @Category;
                """,
                new { DraftCode = draftCode, LiveCode = liveCode, Symptom = symptom, Category = category });
        }
    }

    [Theory]
    [InlineData("unconscious", false, "stop_sale")]
    [InlineData("chest_pain", false, "refer_medical")]
    [InlineData("pregnant", true, "refer_pharmacist")]
    public async Task Hardcoded_block_levels_hide_products_while_review_rows_stay_pending(
        string signal,
        bool pregnant,
        string expectedLevel)
    {
        var cs = await RequireIsolatedAsync();
        var result = await SuggestAsync(
            cs,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PharmacyConsultationFactsDto(
                35,
                null,
                null,
                [],
                null,
                false,
                pregnant,
                false,
                signal == "pregnant" ? [] : [signal],
                null));

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var approved = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pharmacy_safety_flag_review
            WHERE flag_code = ANY(@Codes) AND approval_status = 'APPROVED'
            """,
            new { Codes = PharmacySafetyFlagInventory.Items.Select(item => item.FlagCode).ToArray() });

        Assert.Equal(expectedLevel, result.SafetyLevel);
        Assert.Empty(result.Suggestions);
        Assert.Equal(0, approved);
    }

    [Fact]
    public async Task Approval_events_reject_mutation_and_a_failed_pin_leaves_no_rows()
    {
        var cs = await RequireIsolatedAsync();
        var code = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.Published, "alpha");
            var approval = Service(cs);
            var missing = await Record.ExceptionAsync(() => approval.TransitionAsync(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "missing"),
                world.TenantA,
                world.UserA));
            var pins = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption WHERE rule_code = @Code",
                new { Code = code });
            var events = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM pharmacy_knowledge_approval_event WHERE rule_code = @Code",
                new { Code = code });
            await approval.TransitionAsync(
                code,
                new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "1"),
                world.TenantA,
                world.UserA);
            var mutate = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                "UPDATE pharmacy_knowledge_approval_event SET reference_source = 'changed' WHERE rule_code = @Code AND tenant_id = @TenantId",
                new { Code = code, TenantId = world.TenantA }));
            var deleted = await Record.ExceptionAsync(() => conn.ExecuteAsync(
                "DELETE FROM pharmacy_knowledge_approval_event WHERE rule_code = @Code AND tenant_id = @TenantId",
                new { Code = code, TenantId = world.TenantA }));

            Assert.IsType<InvalidOperationException>(missing);
            Assert.Equal(0, pins);
            Assert.Equal(0, events);
            Assert.NotNull(mutate);
            Assert.NotNull(deleted);
        }
        finally
        {
            await CleanupAsync(conn, code, world);
        }
    }

    [Fact]
    public async Task Concurrent_pin_changes_leave_one_adoption_row()
    {
        var cs = await RequireIsolatedAsync();
        var code = Code();
        var world = await SeedTenantsAsync(cs);
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await InsertVersionAsync(conn, code, "1", PharmacyKnowledgeApproval.Published, "alpha");
            await InsertVersionAsync(conn, code, "2", PharmacyKnowledgeApproval.Published, "beta");
            var approval = Service(cs);
            await approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "1"), world.TenantA, world.UserA);
            await approval.SavePolicyAsync(code, new PharmacyKnowledgePolicyRequest(true, ["old"], 200), world.TenantA, world.UserA);
            await Task.WhenAll(
                approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "1"), world.TenantA, world.UserA),
                approval.TransitionAsync(code, new PharmacyKnowledgeApprovalRequest("APPROVED", "source", "2"), world.TenantA, world.UserA));

            var pinCount = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)::int
                FROM pharmacy_knowledge_adoption
                WHERE tenant_id = @TenantId AND rule_code = @Code AND status = 'APPROVED'
                """,
                new { TenantId = world.TenantA, Code = code });
            var pinVersion = await conn.ExecuteScalarAsync<string>(
                """
                SELECT rule_version
                FROM pharmacy_knowledge_adoption
                WHERE tenant_id = @TenantId AND rule_code = @Code AND status = 'APPROVED'
                """,
                new { TenantId = world.TenantA, Code = code });
            var policyVersion = await conn.ExecuteScalarAsync<string?>(
                """
                SELECT p.rule_version
                FROM pharmacy_tenant_knowledge_policy p
                INNER JOIN pharmacy_knowledge_adoption a ON a.id = p.adoption_id
                WHERE a.tenant_id = @TenantId AND a.rule_code = @Code
                """,
                new { TenantId = world.TenantA, Code = code });

            Assert.Equal(1, pinCount);
            Assert.Contains(pinVersion, new[] { "1", "2" });
            Assert.True(policyVersion is null || policyVersion == pinVersion);
        }
        finally
        {
            await CleanupAsync(conn, code, world);
        }
    }

    private static PharmacyKnowledgeApprovalService Service(string connectionString) =>
        new(new NpgsqlConnectionFactory(connectionString));

    private static async Task<SuggestPharmacyConsultationResultDto> SuggestAsync(
        string connectionString,
        Guid tenantId,
        Guid userId,
        PharmacyConsultationFactsDto facts)
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
            new SuggestPharmacyConsultationRequest(facts, Guid.NewGuid()),
            CancellationToken.None);
    }

    private static string Code() => "w5" + Guid.NewGuid().ToString("N")[..16];

    private static async Task InsertVersionAsync(
        NpgsqlConnection conn,
        string code,
        string version,
        string lifecycle,
        string keyword,
        int priority = 100)
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

    private static async Task<TenantPair> SeedTenantsAsync(string connectionString)
    {
        var world = new TenantPair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES
                (@TenantA, @CodeA, @CodeA, 1),
                (@TenantB, @CodeB, @CodeB, 1);
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

    private static async Task CleanupAsync(NpgsqlConnection conn, string code, TenantPair world)
    {
        await conn.ExecuteAsync(
            """
            DELETE FROM pharmacy_knowledge_adoption WHERE rule_code = @Code;
            DELETE FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @Code AND lifecycle_status IN ('DRAFT', 'IN_REVIEW');
            DELETE FROM pharmacy_knowledge_rule
            WHERE rule_code = @Code
              AND NOT EXISTS (
                  SELECT 1 FROM pharmacy_knowledge_rule_version WHERE rule_code = @Code
              );
            DELETE FROM tenants
            WHERE id IN (@TenantA, @TenantB)
              AND NOT EXISTS (
                  SELECT 1
                  FROM pharmacy_knowledge_adoption
                  WHERE tenant_id IN (@TenantA, @TenantB)
              );
            """,
            new { Code = code, world.TenantA, world.TenantB });
    }

    private static async Task<string> RequireIsolatedAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Fail($"Stopped before any connection. Set {ConnectionEnvironmentVariable}.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (DeniedDatabases.Contains(builder.Database ?? "")
            || (builder.Database ?? "").Contains("prod", StringComparison.OrdinalIgnoreCase))
            Assert.Fail($"Refusing database '{builder.Database}'.");

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()") ?? "";
        if (!string.Equals(database, builder.Database, StringComparison.Ordinal) || DeniedDatabases.Contains(database))
            Assert.Fail($"Refusing to run against database '{database}'.");

        await NovixaValidationSession.EnsureIsolatedRoleAsync(conn);
        var realTenants = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM tenants
            WHERE tenant_code IN ('DEMO_PHARMACY', 'NT_XUANHOA', 'KIT_MKT')
            """);
        if (realTenants != 0)
            Assert.Fail("Refusing database that already contains a real tenant code.");

        foreach (var relation in new[]
        {
            "public.pharmacy_knowledge_rule_version",
            "public.pharmacy_knowledge_adoption",
            "public.pharmacy_tenant_knowledge_policy",
        })
        {
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT to_regclass(@Relation) IS NOT NULL",
                new { Relation = relation });
            if (!exists)
                Assert.Fail($"Apply migration 416 on the isolated database. Missing {relation}.");
        }

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
