using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// Runs only against the local K1 cluster: 127.0.0.1:5434 / novixa_k1_verify.
/// Refuses port 5432 and any production or shared database name.
/// </summary>
public sealed class KnowledgeK1VerificationTests
{
    private const string ConnectionEnvironmentVariable = "K1_VERIFY_CONNECTION";

    private static readonly string[] SeedRuleCodes =
    [
        "KR_COUGH_DRY", "KR_COUGH_PHLEGM", "KR_COUGH", "KR_RUNNY_NOSE", "KR_NASAL_CONG",
        "KR_SORE_THROAT", "KR_SNEEZING", "KR_COMMON_COLD", "KR_FLU", "KR_FEVER",
        "KR_HEADACHE", "KR_BODY_ACHE", "KR_CHILLS", "KR_DIARRHEA", "KR_NAUSEA",
        "KR_VOMITING", "KR_HEARTBURN", "KR_REFLUX", "KR_CONSTIPATION", "KR_BLOATING",
        "KR_ABDOMINAL", "KR_ALLERGY", "KR_ITCHY_SKIN", "KR_RASH", "KR_HIVES",
        "KR_BACK_PAIN", "KR_JOINT_PAIN", "KR_TOOTHACHE", "KR_FATIGUE", "KR_DIZZINESS",
        "KR_INSOMNIA", "KR_OTHER", "KR_DEHYDRATION", "KR_HOARSENESS", "KR_THROAT_CLEARING",
    ];

    [Fact]
    public async Task Isolated_schema_has_the_gate_and_no_seed_rules()
    {
        var cs = await RequireAsync();
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var seedPublished = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pharmacy_knowledge_rule_version
            WHERE rule_code = ANY(@Codes) AND lifecycle_status IN ('PUBLISHED', 'RETIRED')
            """,
            new { Codes = SeedRuleCodes });
        var seedPins = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption WHERE rule_code = ANY(@Codes)",
            new { Codes = SeedRuleCodes });
        var columnDefault = await conn.ExecuteScalarAsync<string>(
            """
            SELECT column_default FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pharmacy_knowledge_rule_version'
              AND column_name = 'source_verification_status'
            """);
        var flags = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pharmacy_safety_flag_review WHERE approval_status <> 'PENDING_REVIEW'");
        var permissions = await conn.ExecuteScalarAsync<bool>("SELECT to_regclass('public.permissions') IS NOT NULL");
        var tasks = await conn.ExecuteScalarAsync<bool>("SELECT to_regclass('pack_learning.operational_task') IS NOT NULL");

        Assert.Equal(0, seedPublished);
        Assert.Equal(0, seedPins);
        Assert.Contains("UNVERIFIED", columnDefault, StringComparison.Ordinal);
        Assert.Equal(0, flags);
        Assert.False(permissions);
        Assert.False(tasks);
    }

    [Fact]
    public async Task Publication_gate_holds_for_the_api_and_for_direct_writes()
    {
        var cs = await RequireAsync();
        var code = "k1syn" + Guid.NewGuid().ToString("N")[..12];
        var other = "KR_OTHER";
        var userId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var publication = new PharmacyKnowledgePublicationService(new NpgsqlConnectionFactory(cs));
        var approval = new PharmacyKnowledgeApprovalService(new NpgsqlConnectionFactory(cs));
        var api = new KitPlatform.Api.Controllers.Pharmacy.PharmacyKnowledgePublicationController(
            publication,
            new MutableTenant(tenantA, userId));

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES
                (@TenantA, @CodeA, @CodeA, 1),
                (@TenantB, @CodeB, @CodeB, 1)
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO pharmacy_knowledge_rule_version (rule_code, version, reason_vi, keywords, lifecycle_status)
            VALUES (@Code, '1', 'synthetic reason', @Keywords, 'DRAFT');
            """,
            new
            {
                TenantA = tenantA,
                TenantB = tenantB,
                CodeA = "k1a" + tenantA.ToString("N")[..8],
                CodeB = "k1b" + tenantB.ToString("N")[..8],
                Code = code,
                Keywords = new[] { "alpha" },
            });

        var barePublish = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule_version (rule_code, version, reason_vi, lifecycle_status)
            VALUES (@Code, '9', 'synthetic', 'PUBLISHED')
            """,
            new { Code = code }));
        var draftToPublished = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            UPDATE pharmacy_knowledge_rule_version
            SET lifecycle_status = 'PUBLISHED'
            WHERE rule_code = @Code AND version = '1'
            """,
            new { Code = code }));

        await publication.SubmitReviewAsync(code, "1", userId);
        var unverified = await api.Publish(code, "1", CancellationToken.None);
        await publication.VerifySourceAsync(code, "1", userId, Rejected());
        var rejected = await api.Publish(code, "1", CancellationToken.None);
        await publication.VerifySourceAsync(code, "1", userId, Accepted());
        await publication.UpdateUnpublishedContentAsync(
            code,
            "1",
            new PharmacyKnowledgeContentUpdateRequest("synthetic reason", ["beta"], [], [], null));
        var cleared = await conn.QuerySingleAsync<(string Status, string? VerifiedBy)>(
            """
            SELECT source_verification_status AS Status, verified_by AS VerifiedBy
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @Code AND version = '1'
            """,
            new { Code = code });
        var clearedPublish = await api.Publish(code, "1", CancellationToken.None);

        await publication.VerifySourceAsync(code, "1", userId, Accepted());
        await conn.ExecuteAsync(
            """
            UPDATE pharmacy_knowledge_rule_version
            SET keywords = '{gamma}',
                source_verification_status = 'ACCEPTED',
                verified_by = 'bypass',
                verified_at = NOW()
            WHERE rule_code = @Code AND version = '1'
            """,
            new { Code = code });
        var bypass = await conn.QuerySingleAsync<string>(
            """
            SELECT source_verification_status
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @Code AND version = '1'
            """,
            new { Code = code });

        await publication.VerifySourceAsync(code, "1", userId, Accepted());
        var published = await api.Publish(code, "1", CancellationToken.None);
        var frozen = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            "UPDATE pharmacy_knowledge_rule_version SET keywords = '{nope}' WHERE rule_code = @Code AND version = '1'",
            new { Code = code }));
        var sourceFrozen = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            "UPDATE pharmacy_knowledge_rule_version SET reference_source = 'changed' WHERE rule_code = @Code AND version = '1'",
            new { Code = code }));

        await conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule_version (
                rule_code, version, reason_vi, keywords, lifecycle_status,
                reference_source, source_identity, source_url, issuing_organization, evidence_note,
                source_verification_status, verified_by, verified_at)
            VALUES (
                @Other, '1', 'synthetic', '{}', 'IN_REVIEW',
                'synthetic-citation', 'SYNTH-K1', 'https://example.test/k1-fixture',
                'synthetic-org', 'fixture section', 'ACCEPTED', 'fixture', NOW())
            ON CONFLICT (rule_code, version) DO NOTHING
            """,
            new { Other = other });
        var otherDirect = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            "UPDATE pharmacy_knowledge_rule_version SET lifecycle_status = 'PUBLISHED' WHERE rule_code = @Other AND version = '1'",
            new { Other = other }));
        var otherApi = await Record.ExceptionAsync(() => publication.PublishAsync(other, "1", userId));

        await approval.TransitionAsync(
            code,
            new PharmacyKnowledgeApprovalRequest("APPROVED", "synthetic-pin-note", "1"),
            tenantA,
            userId);
        await conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule_version (rule_code, version, reason_vi, keywords, lifecycle_status)
            VALUES (@Code, '2', 'synthetic reason', '{next}', 'IN_REVIEW')
            """,
            new { Code = code });
        await publication.VerifySourceAsync(code, "2", userId, Accepted());
        await publication.PublishAsync(code, "2", userId);
        var pinVersion = await conn.ExecuteScalarAsync<string>(
            "SELECT rule_version FROM pharmacy_knowledge_adoption WHERE tenant_id = @TenantA AND rule_code = @Code",
            new { TenantA = tenantA, Code = code });
        var otherPins = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption WHERE tenant_id = @TenantB AND rule_code = @Code",
            new { TenantB = tenantB, Code = code });
        var unpinned = await Record.ExceptionAsync(() => approval.TransitionAsync(
            code,
            new PharmacyKnowledgeApprovalRequest("APPROVED", "synthetic-pin-note", "9"),
            tenantA,
            userId));

        await publication.RetireAsync(code, "1", userId);
        var retiredEdit = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            "UPDATE pharmacy_knowledge_rule_version SET reason_vi = 'changed' WHERE rule_code = @Code AND version = '1'",
            new { Code = code }));
        var retiredReopen = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            "UPDATE pharmacy_knowledge_rule_version SET lifecycle_status = 'PUBLISHED' WHERE rule_code = @Code AND version = '1'",
            new { Code = code }));
        var deleted = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            "DELETE FROM pharmacy_knowledge_rule_version WHERE rule_code = @Code AND version = '1'",
            new { Code = code }));

        var seedAfter = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pharmacy_knowledge_rule_version WHERE rule_code = ANY(@Codes) AND rule_code <> 'KR_OTHER'",
            new { Codes = SeedRuleCodes });
        var otherStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT lifecycle_status FROM pharmacy_knowledge_rule_version WHERE rule_code = 'KR_OTHER' AND version = '1'");

        Assert.NotNull(barePublish);
        Assert.NotNull(draftToPublished);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(unverified.Result);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(rejected.Result);
        Assert.Equal("UNVERIFIED", cleared.Status);
        Assert.Null(cleared.VerifiedBy);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(clearedPublish.Result);
        Assert.Equal("UNVERIFIED", bypass);
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(published.Result);
        Assert.NotNull(frozen);
        Assert.NotNull(sourceFrozen);
        Assert.NotNull(otherDirect);
        Assert.NotNull(otherApi);
        Assert.Equal("1", pinVersion);
        Assert.Equal(0, otherPins);
        Assert.NotNull(unpinned);
        Assert.NotNull(retiredEdit);
        Assert.NotNull(retiredReopen);
        Assert.NotNull(deleted);
        Assert.Equal(0, seedAfter);
        Assert.Equal("IN_REVIEW", otherStatus);
        Assert.Contains("KR_OTHER", otherApi!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Constraints_block_incomplete_acceptance_and_an_unpublished_pin()
    {
        var cs = await RequireAsync();
        var code = "k1chk" + Guid.NewGuid().ToString("N")[..12];
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var approval = new PharmacyKnowledgeApprovalService(new NpgsqlConnectionFactory(cs));
        var publication = new PharmacyKnowledgePublicationService(new NpgsqlConnectionFactory(cs));

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        var incomplete = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule_version (
                rule_code, version, reason_vi, lifecycle_status, source_verification_status)
            VALUES (@Code, '1', 'synthetic', 'DRAFT', 'ACCEPTED')
            """,
            new { Code = code }));
        var novixa = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule_version (
                rule_code, version, reason_vi, lifecycle_status,
                reference_source, source_identity, source_url, issuing_organization, evidence_note,
                source_verification_status, verified_by, verified_at)
            VALUES (
                @Code, '1', 'synthetic', 'IN_REVIEW',
                'novixa', 'SYNTH-K1', 'https://example.test/k1-fixture',
                'synthetic-org', 'fixture section', 'ACCEPTED', 'fixture', NOW())
            """,
            new { Code = code }));
        var bogus = await Record.ExceptionAsync(() => conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_rule_version (
                rule_code, version, reason_vi, lifecycle_status, source_verification_status)
            VALUES (@Code, '1', 'synthetic', 'DRAFT', 'MAYBE')
            """,
            new { Code = code }));

        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, @TenantCode, 1);
            INSERT INTO pharmacy_knowledge_rule_version (
                rule_code, version, reason_vi, lifecycle_status,
                reference_source, source_identity, document_ref, issuing_organization, evidence_note,
                source_verification_status, verified_by, verified_at)
            VALUES (
                @Code, '1', 'synthetic', 'IN_REVIEW',
                'synthetic-citation', 'SYNTH-K1', 'DOC-ONLY',
                'synthetic-org', 'fixture section', 'ACCEPTED', 'fixture', NOW());
            """,
            new
            {
                TenantId = tenantId,
                TenantCode = "k1c" + tenantId.ToString("N")[..8],
                Code = code,
            });
        var unpublishedPin = await Record.ExceptionAsync(() => approval.TransitionAsync(
            code,
            new PharmacyKnowledgeApprovalRequest("APPROVED", "synthetic-pin-note", "1"),
            tenantId,
            userId));
        var pins = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pharmacy_knowledge_adoption WHERE tenant_id = @TenantId",
            new { TenantId = tenantId });
        await publication.ReturnToDraftAsync(code, "1", userId);
        var draftPublish = await Record.ExceptionAsync(() => publication.PublishAsync(code, "1", userId));
        var status = await conn.QuerySingleAsync<(string Lifecycle, string Source)>(
            """
            SELECT lifecycle_status AS Lifecycle, source_verification_status AS Source
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @Code AND version = '1'
            """,
            new { Code = code });

        Assert.NotNull(incomplete);
        Assert.NotNull(novixa);
        Assert.NotNull(bogus);
        Assert.NotNull(unpublishedPin);
        Assert.Equal(0, pins);
        Assert.NotNull(draftPublish);
        Assert.Equal("DRAFT", status.Lifecycle);
        Assert.Equal("ACCEPTED", status.Source);
    }

    private static PharmacyKnowledgeSourceVerificationRequest Accepted() =>
        new(
            PharmacyKnowledgeSourceRules.Accepted,
            "synthetic-citation",
            "SYNTH-K1",
            "https://example.test/k1-fixture",
            null,
            "synthetic-org",
            null,
            "fixture section");

    private static PharmacyKnowledgeSourceVerificationRequest Rejected() =>
        new(PharmacyKnowledgeSourceRules.Rejected, null, null, null, null, null, null, null);

    private static async Task<string> RequireAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Fail($"Set {ConnectionEnvironmentVariable} to the isolated K1 database.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Port != 5434
            || !string.Equals(builder.Host, "127.0.0.1", StringComparison.Ordinal)
            || !string.Equals(builder.Database, "novixa_k1_verify", StringComparison.Ordinal))
            Assert.Fail("Refusing connection. K1 verification uses only 127.0.0.1:5434/novixa_k1_verify.");

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var identity = await conn.QuerySingleAsync<(string Database, string Addr, int Port)>(
            "SELECT current_database() AS Database, inet_server_addr()::text AS Addr, inet_server_port() AS Port");
        if (identity.Database != "novixa_k1_verify"
            || !identity.Addr.StartsWith("127.0.0.1", StringComparison.Ordinal)
            || identity.Port != 5434)
            Assert.Fail($"Refusing database {identity.Database} at {identity.Addr}:{identity.Port}.");

        var column = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pharmacy_knowledge_rule_version'
              AND column_name = 'source_verification_status'
            """);
        if (column != 1)
            Assert.Fail("Apply migration 423 on novixa_k1_verify before this test.");

        return builder.ConnectionString;
    }

    private sealed class MutableTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
