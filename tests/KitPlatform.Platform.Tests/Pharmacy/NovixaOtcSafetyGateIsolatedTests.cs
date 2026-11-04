using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// W6 OTC gate against the real suggest path. Runs only when
/// POS_IDEMPOTENCY_TEST_CONNECTION is an isolated database that already has
/// migrations 411-415. Refuses kitplatform, kitplatform_test, and production.
/// </summary>
[Collection("kitplatform_test-sales-idempotency")]
public sealed class NovixaOtcSafetyGateIsolatedTests
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

    [Theory]
    [InlineData("unconscious", false, "stop_sale", true)]
    [InlineData("chest_pain", false, "refer_medical", true)]
    [InlineData("pregnant", true, "refer_pharmacist", false)]
    public async Task Safety_gate_hides_ordinary_otc_products(
        string signal,
        bool pregnant,
        string expectedLevel,
        bool expectedBlocked)
    {
        var cs = await RequireIsolatedAsync();
        await using var world = await OtcWorld.SeedAsync(cs);
        var rules = await world.Taxonomy.GetKnowledgeRulesAsync(
            world.Tenant.TenantId,
            [world.SymptomCode],
            CancellationToken.None);
        Assert.Contains(rules, rule => rule.RuleCode == world.ApprovedCode);
        Assert.DoesNotContain(rules, rule => rule.RuleCode == world.PendingCode);

        var result = await world.SuggestAsync(world.Facts(signal, pregnant));

        Assert.Equal(expectedLevel, result.SafetyLevel);
        Assert.Equal(expectedBlocked, result.Blocked);
        Assert.Empty(result.Suggestions);
        Assert.DoesNotContain(world.ApprovedToken, result.BlockReason ?? "", StringComparison.Ordinal);
        Assert.Equal(0, world.LlmCalls);
    }

    [Fact]
    public async Task Approved_rule_is_used_and_pending_or_other_tenant_stock_is_not()
    {
        var cs = await RequireIsolatedAsync();
        await using var world = await OtcWorld.SeedAsync(cs);

        var own = await world.SuggestAsync(world.Facts("none", false));
        world.Tenant.TenantId = world.OtherTenantId;
        var other = await world.SuggestAsync(world.Facts("none", false), world.OtherWarehouseId);

        Assert.Equal("none", own.SafetyLevel);
        Assert.False(own.Blocked);
        Assert.Contains(own.Suggestions, item => item.ProductId == world.ApprovedProductId);
        Assert.DoesNotContain(own.Suggestions, item => item.ProductId == world.PendingProductId);
        Assert.DoesNotContain(own.Suggestions, item => item.ProductId == world.OtherProductId);
        Assert.DoesNotContain(own.Suggestions, item => item.ProductId == world.CategoryOnlyProductId);
        Assert.All(own.Suggestions, item => Assert.Equal("keyword", item.MatchSource));
        Assert.Empty(other.Suggestions);
        Assert.Equal(0, world.LlmCalls);
    }

    private static async Task<string> RequireIsolatedAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail(
                $"Stopped before any connection. Set {ConnectionEnvironmentVariable} to an empty isolated database. "
                + "kitplatform and kitplatform_test are refused.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (DeniedDatabases.Contains(builder.Database ?? "")
            || (builder.Database ?? "").Contains("prod", StringComparison.OrdinalIgnoreCase))
            Assert.Fail($"Refusing database '{builder.Database}'. No connection was opened for writes.");

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()") ?? "";
        if (!string.Equals(database, builder.Database, StringComparison.Ordinal) || DeniedDatabases.Contains(database))
            Assert.Fail($"Refusing to run against database '{database}'. No writes were attempted.");

        await NovixaValidationSession.EnsureIsolatedRoleAsync(conn);

        var realTenants = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM tenants
            WHERE tenant_code IN ('DEMO_PHARMACY', 'NT_XUANHOA', 'KIT_MKT')
            """);
        if (realTenants != 0)
            Assert.Fail("Refusing database that already contains a real tenant code. No test rows were inserted.");

        foreach (var relation in new[]
        {
            "public.pharmacy_symptom",
            "public.pharmacy_knowledge_rule",
            "public.pharmacy_safety_flag_review",
            "public.products",
            "public.inventory_batches",
        })
        {
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT to_regclass(@Relation) IS NOT NULL",
                new { Relation = relation });
            if (!exists)
                Assert.Fail($"Isolated schema is missing {relation}. No test rows were inserted.");
        }

        var approval = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pharmacy_knowledge_rule'
              AND column_name = 'approval_status'
            """);
        if (approval != 1)
            Assert.Fail("Apply migration 414 before the OTC gate test. No test rows were inserted.");

        var sourceColumn = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pharmacy_knowledge_rule_version'
              AND column_name = 'source_verification_status'
            """);
        if (sourceColumn != 1)
            Assert.Fail("Apply migration 423 before the OTC gate test. No test rows were inserted.");

        return builder.ConnectionString;
    }

    private sealed class OtcWorld : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly NpgsqlConnectionFactory _db;
        private readonly ThrowingHandler _llm = new();

        private OtcWorld(string connectionString, Guid tenantId, Guid otherTenantId)
        {
            _connectionString = connectionString;
            _db = new NpgsqlConnectionFactory(connectionString);
            TenantId = tenantId;
            Tenant = new MutableTenant(tenantId, Guid.NewGuid());
            OtherTenantId = otherTenantId;
            Taxonomy = new SymptomTaxonomyRepository(_db);
            var suffix = Guid.NewGuid().ToString("N")[..12];
            SymptomCode = "z6sym" + suffix;
            CategoryCode = "z6cat" + suffix;
            ProductCategoryCode = "Z6PRD" + suffix.ToUpperInvariant();
            ApprovedCode = "z6app" + suffix;
            PendingCode = "z6pen" + suffix;
            ApprovedToken = "z6tok" + suffix;
            PendingToken = "z6hid" + suffix;
            ReviewCode = "z6rev" + suffix;
            ApprovedReason = "synthetic approved " + ApprovedToken;
        }

        public Guid TenantId { get; }
        public MutableTenant Tenant { get; }
        public Guid OtherTenantId { get; }
        public Guid WarehouseId { get; private set; }
        public Guid OtherWarehouseId { get; private set; }
        public Guid ApprovedProductId { get; private set; }
        public Guid PendingProductId { get; private set; }
        public Guid OtherProductId { get; private set; }
        public Guid CategoryOnlyProductId { get; private set; }
        public string SymptomCode { get; }
        public string CategoryCode { get; }
        public string ProductCategoryCode { get; }
        public string ApprovedCode { get; }
        public string PendingCode { get; }
        public string ApprovedToken { get; }
        public string PendingToken { get; }
        public string ReviewCode { get; }
        public string ApprovedReason { get; }
        public SymptomTaxonomyRepository Taxonomy { get; }
        public int LlmCalls => _llm.Calls;

        public static async Task<OtcWorld> SeedAsync(string connectionString)
        {
            var world = new OtcWorld(connectionString, Guid.NewGuid(), Guid.NewGuid());
            await world.SeedAsync();
            return world;
        }

        public PharmacyConsultationFactsDto Facts(string signal, bool pregnant) =>
            new(
                35,
                null,
                null,
                [SymptomCode],
                null,
                false,
                pregnant,
                false,
                signal is "none" or "pregnant" ? [] : [signal],
                null);

        public async Task<SuggestPharmacyConsultationResultDto> SuggestAsync(
            PharmacyConsultationFactsDto facts,
            Guid? warehouseId = null)
        {
            var repository = new ConsultationRepository(_db, Tenant);
            using var http = new HttpClient(_llm, disposeHandler: false);
            var gemini = new PharmacyConsultationGeminiClient(http, NullLogger<PharmacyConsultationGeminiClient>.Instance);
            var ai = new PharmacyConsultationAiConfigProvider(new Mock<ITenantSettingsService>().Object);
            var service = new PharmacyConsultationService(
                repository,
                Taxonomy,
                gemini,
                ai,
                new AllowBranches(),
                Tenant);
            return await service.SuggestAsync(
                new SuggestPharmacyConsultationRequest(facts, warehouseId ?? WarehouseId),
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await conn.ExecuteAsync(
                """
                DELETE FROM inventory_batches WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM product_prices WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM product_units WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM products WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM product_categories WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM warehouses WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM branches WHERE tenant_id IN (@TenantId, @OtherTenantId);
                DELETE FROM pharmacy_knowledge_adoption WHERE rule_code IN (@ApprovedCode, @PendingCode);
                DELETE FROM pharmacy_knowledge_rule_version
                WHERE rule_code IN (@ApprovedCode, @PendingCode)
                  AND lifecycle_status IN ('DRAFT', 'IN_REVIEW');
                DELETE FROM pharmacy_knowledge_rule
                WHERE rule_code IN (@ApprovedCode, @PendingCode)
                  AND NOT EXISTS (
                      SELECT 1 FROM pharmacy_knowledge_rule_version v
                      WHERE v.rule_code = pharmacy_knowledge_rule.rule_code
                  );
                DELETE FROM tenants WHERE id IN (@TenantId, @OtherTenantId);
                DELETE FROM pharmacy_symptom
                WHERE code = @SymptomCode
                  AND NOT EXISTS (
                      SELECT 1
                      FROM pharmacy_knowledge_rule_version v
                      WHERE v.symptom_id = pharmacy_symptom.id
                        AND v.lifecycle_status IN ('PUBLISHED', 'RETIRED')
                  );
                DELETE FROM pharmacy_symptom_category
                WHERE code = @CategoryCode
                  AND NOT EXISTS (
                      SELECT 1 FROM pharmacy_symptom WHERE category_code = @CategoryCode
                  );
                DELETE FROM pharmacy_safety_flag_review WHERE flag_code = @ReviewCode;
                """,
                new
                {
                    TenantId,
                    OtherTenantId,
                    ApprovedCode,
                    PendingCode,
                    SymptomCode,
                    CategoryCode,
                    ReviewCode,
                });
        }

        private async Task SeedAsync()
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()") ?? "";
            if (DeniedDatabases.Contains(database))
                throw new InvalidOperationException($"Refusing OTC seed against database '{database}'.");

            WarehouseId = Guid.NewGuid();
            OtherWarehouseId = Guid.NewGuid();
            ApprovedProductId = Guid.NewGuid();
            PendingProductId = Guid.NewGuid();
            OtherProductId = Guid.NewGuid();
            CategoryOnlyProductId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var otherBranchId = Guid.NewGuid();
            var approvedUnitId = Guid.NewGuid();
            var pendingUnitId = Guid.NewGuid();
            var otherUnitId = Guid.NewGuid();
            var categoryOnlyUnitId = Guid.NewGuid();
            var productCategoryId = Guid.NewGuid();

            await conn.ExecuteAsync(
                """
                INSERT INTO pharmacy_symptom_category (code, label_vi)
                VALUES (@CategoryCode, @CategoryCode);
                """,
                new { CategoryCode });
            var symptomId = await conn.ExecuteScalarAsync<Guid>(
                """
                INSERT INTO pharmacy_symptom (code, name_vi, category_code, consultation_mode)
                VALUES (@SymptomCode, @SymptomCode, @CategoryCode, 'otc_assist')
                RETURNING id
                """,
                new { SymptomCode, CategoryCode });
            await conn.ExecuteAsync(
                """
                INSERT INTO pharmacy_knowledge_rule (
                    rule_code, reason_vi, symptom_id, keywords, approval_status, version)
                VALUES
                    (@ApprovedCode, @ApprovedReason, @SymptomId, @ApprovedKeywords, 'PENDING_REVIEW', '1'),
                    (@PendingCode, 'synthetic pending', @SymptomId, @PendingKeywords, 'PENDING_REVIEW', '1');
                INSERT INTO pharmacy_knowledge_rule_version (
                    rule_code, version, symptom_id, category_codes, keywords, reason_vi, priority, lifecycle_status,
                    reference_source, source_identity, source_url, issuing_organization, evidence_note,
                    source_verification_status, verified_by, verified_at)
                VALUES
                    (@ApprovedCode, '1', @SymptomId, @ProductCategories, @ApprovedKeywords, @ApprovedReason, 100, 'PUBLISHED',
                     'synthetic-citation', 'SYNTH-TEST', 'https://example.test/k1-fixture', 'synthetic-org', 'fixture section',
                     'ACCEPTED', 'fixture', NOW()),
                    (@PendingCode, '1', @SymptomId, '{}', @PendingKeywords, 'synthetic pending', 100, 'IN_REVIEW',
                     NULL, NULL, NULL, NULL, NULL, 'UNVERIFIED', NULL, NULL);

                INSERT INTO pharmacy_safety_flag_review (
                    flag_code, safety_level, message_vi, source_ref, approval_status)
                VALUES (
                    @ReviewCode, 'none', 'synthetic review row', 'otc-gate-test', 'APPROVED');
                """,
                new
                {
                    ApprovedCode,
                    ApprovedReason,
                    PendingCode,
                    SymptomId = symptomId,
                    ApprovedKeywords = new[] { ApprovedToken },
                    PendingKeywords = new[] { PendingToken },
                    ProductCategories = new[] { ProductCategoryCode },
                    ReviewCode,
                });

            await conn.ExecuteAsync(
                """
                INSERT INTO tenants (id, tenant_code, tenant_name, status)
                VALUES
                    (@TenantId, @TenantCode, @TenantCode, 1),
                    (@OtherTenantId, @OtherCode, @OtherCode, 1);
                INSERT INTO pharmacy_knowledge_adoption (
                    tenant_id, rule_code, rule_version, status, approved_by, reference_source)
                VALUES (
                    @TenantId, @ApprovedCode, '1', 'APPROVED', 'synthetic', 'synthetic-source');
                INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
                VALUES
                    (@BranchId, @TenantId, @TenantCode, @TenantCode, 1),
                    (@OtherBranchId, @OtherTenantId, @OtherCode, @OtherCode, 1);
                INSERT INTO warehouses (
                    id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
                VALUES
                    (@WarehouseId, @TenantId, @BranchId, @TenantCode, @TenantCode, 1, 1),
                    (@OtherWarehouseId, @OtherTenantId, @OtherBranchId, @OtherCode, @OtherCode, 1, 1);

                INSERT INTO product_categories (id, tenant_id, category_code, category_name, status)
                VALUES (@ProductCategoryId, @TenantId, @ProductCategoryCode, @ProductCategoryCode, 1);
                INSERT INTO products (id, tenant_id, category_id, product_code, product_name, status)
                VALUES
                    (@ApprovedProductId, @TenantId, NULL, @ApprovedToken, @ApprovedName, 1),
                    (@PendingProductId, @TenantId, NULL, @PendingToken, @PendingName, 1),
                    (@OtherProductId, @OtherTenantId, NULL, @OtherToken, @ApprovedName, 1),
                    (@CategoryOnlyProductId, @TenantId, @ProductCategoryId, @CategoryOnlyToken, @CategoryOnlyName, 1);
                INSERT INTO product_units (
                    id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit, status)
                VALUES
                    (@ApprovedUnitId, @TenantId, @ApprovedProductId, 'Viên', 1, TRUE, TRUE, 1),
                    (@PendingUnitId, @TenantId, @PendingProductId, 'Viên', 1, TRUE, TRUE, 1),
                    (@OtherUnitId, @OtherTenantId, @OtherProductId, 'Viên', 1, TRUE, TRUE, 1),
                    (@CategoryOnlyUnitId, @TenantId, @CategoryOnlyProductId, 'Viên', 1, TRUE, TRUE, 1);
                INSERT INTO product_prices (
                    id, tenant_id, product_id, product_unit_id, price_type, price, status)
                VALUES
                    (@ApprovedPriceId, @TenantId, @ApprovedProductId, @ApprovedUnitId, 1, 1000, 1),
                    (@PendingPriceId, @TenantId, @PendingProductId, @PendingUnitId, 1, 1000, 1),
                    (@OtherPriceId, @OtherTenantId, @OtherProductId, @OtherUnitId, 1, 1000, 1),
                    (@CategoryOnlyPriceId, @TenantId, @CategoryOnlyProductId, @CategoryOnlyUnitId, 1, 1000, 1);
                INSERT INTO inventory_batches (
                    id, tenant_id, warehouse_id, product_id, batch_number,
                    unit_cost, quantity_received, quantity_available, status)
                VALUES
                    (@ApprovedBatchId, @TenantId, @WarehouseId, @ApprovedProductId, 'LOT-A', 100, 5, 5, 1),
                    (@PendingBatchId, @TenantId, @WarehouseId, @PendingProductId, 'LOT-P', 100, 5, 5, 1),
                    (@OtherBatchId, @OtherTenantId, @OtherWarehouseId, @OtherProductId, 'LOT-B', 100, 5, 5, 1),
                    (@CategoryOnlyBatchId, @TenantId, @WarehouseId, @CategoryOnlyProductId, 'LOT-C', 100, 5, 5, 1);
                """,
                new
                {
                    TenantId,
                    OtherTenantId,
                    ApprovedCode,
                    TenantCode = "z6" + TenantId.ToString("N")[..10],
                    OtherCode = "z6" + OtherTenantId.ToString("N")[..10],
                    BranchId = branchId,
                    OtherBranchId = otherBranchId,
                    WarehouseId,
                    OtherWarehouseId,
                    ApprovedProductId,
                    PendingProductId,
                    OtherProductId,
                    CategoryOnlyProductId,
                    ProductCategoryId = productCategoryId,
                    ProductCategoryCode,
                    ApprovedToken,
                    PendingToken,
                    OtherToken = "z6oth" + OtherTenantId.ToString("N")[..8],
                    CategoryOnlyToken = "z6catonly" + TenantId.ToString("N")[..8],
                    ApprovedName = "Opsv2 " + ApprovedToken,
                    PendingName = "Opsv2 " + PendingToken,
                    CategoryOnlyName = "Category shelf only",
                    ApprovedUnitId = approvedUnitId,
                    PendingUnitId = pendingUnitId,
                    OtherUnitId = otherUnitId,
                    CategoryOnlyUnitId = categoryOnlyUnitId,
                    ApprovedPriceId = Guid.NewGuid(),
                    PendingPriceId = Guid.NewGuid(),
                    OtherPriceId = Guid.NewGuid(),
                    CategoryOnlyPriceId = Guid.NewGuid(),
                    ApprovedBatchId = Guid.NewGuid(),
                    PendingBatchId = Guid.NewGuid(),
                    OtherBatchId = Guid.NewGuid(),
                    CategoryOnlyBatchId = Guid.NewGuid(),
                });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("OTC suggest must not call an LLM.");
        }
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

    private sealed class MutableTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; set; } = tenantId;
        public Guid UserId { get; set; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
