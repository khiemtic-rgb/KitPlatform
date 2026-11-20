using System.Reflection;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.Pharmacy;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace KitPlatform.Tests;

public class KnowledgeK1GovernanceTests
{
    private static readonly DateTime VerifiedAt = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Review_transitions_exist_and_draft_cannot_publish()
    {
        Assert.Equal("IN_REVIEW", PharmacyKnowledgeApproval.Transition("DRAFT", "IN_REVIEW"));
        Assert.Equal("DRAFT", PharmacyKnowledgeApproval.Transition("IN_REVIEW", "DRAFT"));
        Assert.Equal("PUBLISHED", PharmacyKnowledgeApproval.Transition("IN_REVIEW", "PUBLISHED"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeApproval.Transition("DRAFT", "PUBLISHED"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeApproval.Transition("PUBLISHED", "IN_REVIEW"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeApproval.Transition("RETIRED", "PUBLISHED"));
    }

    [Fact]
    public void Publish_rejects_draft_unverified_rejected_and_incomplete_sources()
    {
        Assert.Throws<InvalidOperationException>(() => Publish(lifecycle: "DRAFT"));
        Assert.Throws<InvalidOperationException>(() => Publish(status: "UNVERIFIED"));
        Assert.Throws<InvalidOperationException>(() => Publish(status: "REJECTED"));
        Assert.Throws<InvalidOperationException>(() => Publish(status: null));
        Assert.Throws<InvalidOperationException>(() => Publish(reference: null));
        Assert.Throws<InvalidOperationException>(() => Publish(reference: " "));
        Assert.Throws<InvalidOperationException>(() => Publish(identity: null));
        Assert.Throws<InvalidOperationException>(() => Publish(organization: null));
        Assert.Throws<InvalidOperationException>(() => Publish(evidence: null));
        Assert.Throws<InvalidOperationException>(() => Publish(url: null, document: null));
        Assert.Throws<InvalidOperationException>(() => Publish(verifiedBy: null));
        Assert.Throws<InvalidOperationException>(() => Publish(passVerifiedAt: false));
        Assert.Throws<InvalidOperationException>(() => Publish(reference: "novixa"));
        Assert.Throws<InvalidOperationException>(() => Publish(identity: "NOVIXA"));
    }

    [Fact]
    public void Publish_accepts_an_in_review_version_with_url_or_document_reference()
    {
        Assert.Equal("PUBLISHED", Publish());
        Assert.Equal("PUBLISHED", Publish(url: null, document: "HS-01"));
    }

    [Fact]
    public void Kr_other_stays_blocked_after_an_accepted_source()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Publish(ruleCode: "KR_OTHER"));
        Assert.Contains("KR_OTHER", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_request_accepts_or_rejects_without_publishing()
    {
        PharmacyKnowledgeSourceRules.EnsureVerificationRequest(
            "ACCEPTED", "citation", "DOC-1", null, "HS-01", "Bộ Y tế", "mục 1");
        PharmacyKnowledgeSourceRules.EnsureVerificationRequest(
            "REJECTED", null, null, null, null, null, null);
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeSourceRules.EnsureVerificationRequest(
                "UNVERIFIED", "citation", "DOC-1", "https://example.test/x", null, "Bộ Y tế", "mục 1"));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeSourceRules.EnsureVerificationRequest(
                "ACCEPTED", "novixa", "DOC-1", "https://example.test/x", null, "Bộ Y tế", "mục 1"));
    }

    [Fact]
    public void Content_edit_clears_acceptance_and_an_unchanged_draft_does_not()
    {
        var symptom = Guid.NewGuid();
        Assert.False(PharmacyKnowledgeSourceRules.ClearsAcceptance(
            "lý do", "lý do", symptom, symptom, ["ho"], ["ho"], ["aspirin"], ["aspirin"], ["HO_HAP"], ["HO_HAP"]));
        Assert.True(PharmacyKnowledgeSourceRules.ClearsAcceptance(
            "lý do", "lý do mới", symptom, symptom, ["ho"], ["ho"], ["aspirin"], ["aspirin"], ["HO_HAP"], ["HO_HAP"]));
        Assert.True(PharmacyKnowledgeSourceRules.ClearsAcceptance(
            "lý do", "lý do", symptom, Guid.NewGuid(), ["ho"], ["ho"], [], [], [], []));
        Assert.True(PharmacyKnowledgeSourceRules.ClearsAcceptance(
            "lý do", "lý do", symptom, symptom, ["ho", "khan"], ["khan", "ho"], [], [], [], []));
        Assert.True(PharmacyKnowledgeSourceRules.ClearsAcceptance(
            "lý do", "lý do", symptom, symptom, ["ho"], ["ho"], ["aspirin"], [], [], []));
        Assert.True(PharmacyKnowledgeSourceRules.ClearsAcceptance(
            "lý do", "lý do", symptom, symptom, ["ho"], ["ho"], [], [], ["HO_HAP"], ["GIAM_DAU"]));
        var kept = Guid.NewGuid();
        Assert.Equal(kept, PharmacyKnowledgeSourceRules.SymptomIdForContentEdit(null, kept));
        Assert.Null(PharmacyKnowledgeSourceRules.SymptomIdForContentEdit(null, null));
        Assert.NotEqual(kept, PharmacyKnowledgeSourceRules.SymptomIdForContentEdit(Guid.NewGuid(), kept));
    }

    [Fact]
    public void Pin_rejects_unverified_rejected_other_and_inactive_versions_before_any_write()
    {
        var today = new DateTime(2026, 11, 1);
        var from = today.AddDays(-1);
        PharmacyKnowledgeApproval.EnsureVersionMayBePinned(
            "KR_FEVER", "PUBLISHED", from, null, today, "ACCEPTED");

        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_FEVER", "PUBLISHED", from, null, today, "UNVERIFIED"));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_FEVER", "PUBLISHED", from, null, today, "REJECTED"));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_FEVER", "PUBLISHED", from, null, today, null));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_OTHER", "PUBLISHED", from, null, today, "ACCEPTED"));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_FEVER", "IN_REVIEW", from, null, today, "ACCEPTED"));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_FEVER", "DRAFT", from, null, today, "ACCEPTED"));
        Assert.Throws<InvalidOperationException>(() =>
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned("KR_FEVER", "PUBLISHED", from, today.AddDays(-1), today, "ACCEPTED"));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: false, isTenantAdmin: false));

        var service = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "Packs",
            "Pharmacy",
            "KitPlatform.Packs.Pharmacy.Infrastructure",
            "Knowledge",
            "PharmacyKnowledgeApprovalService.cs"));
        var gate = service.IndexOf("EnsureVersionMayBePinned", StringComparison.Ordinal);
        var insert = service.IndexOf("INSERT INTO pharmacy_knowledge_adoption", StringComparison.Ordinal);
        Assert.True(gate >= 0 && insert > gate);
        var controller = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "KitPlatform.Api",
            "Controllers",
            "Pharmacy",
            "PharmacyKnowledgeApprovalController.cs"));
        Assert.Contains("[Authorize(Policy = PharmacyKnowledgePolicies.Approve)]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_and_publish_permissions_do_not_substitute_for_each_other()
    {
        Assert.True(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanReview(isErpPrincipal: true, hasReviewPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanVerifySource(isErpPrincipal: true, hasVerifySourcePermission: false));

        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: false, hasPlatformPublishPermission: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanReview(isErpPrincipal: false, hasReviewPermission: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanVerifySource(isErpPrincipal: false, hasVerifySourcePermission: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: false, isTenantAdmin: false));

        Assert.True(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: true));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanReview(isErpPrincipal: true, hasReviewPermission: true));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanVerifySource(isErpPrincipal: true, hasVerifySourcePermission: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanReadCatalog(true, false, false, false));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanReadCatalog(true, true, false, false));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanReadCatalog(true, false, true, false));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanReadCatalog(true, false, false, true));
    }

    [Fact]
    public void Endpoints_keep_review_verification_publication_and_pin_apart()
    {
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "SubmitReview", PharmacyKnowledgePolicies.Review);
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "ReturnToDraft", PharmacyKnowledgePolicies.Review);
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "UpdateContent", PharmacyKnowledgePolicies.Review);
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "VerifySource", PharmacyKnowledgePolicies.VerifySource);
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "Publish", PharmacyKnowledgePolicies.Publish);
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "Retire", PharmacyKnowledgePolicies.Publish);
        AssertPolicy(typeof(PharmacyKnowledgePublicationController), "List", PharmacyKnowledgePolicies.Read);
        AssertPolicy(typeof(PharmacyKnowledgeApprovalController), "Transition", PharmacyKnowledgePolicies.Approve);
    }

    [Fact]
    public void Tenant_admin_maps_to_pin_and_not_to_platform_publication()
    {
        Assert.True(PharmacyKnowledgeApprovalAccess.CanApprove(
            isErpPrincipal: true, hasApprovePermission: false, isTenantAdmin: true));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanApprove(
            isErpPrincipal: true, hasApprovePermission: true, isTenantAdmin: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(
            isErpPrincipal: false, hasApprovePermission: false, isTenantAdmin: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(
            isErpPrincipal: true, hasApprovePermission: false, isTenantAdmin: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanReview(isErpPrincipal: true, hasReviewPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanVerifySource(isErpPrincipal: true, hasVerifySourcePermission: false));

        var root = RepoRoot();
        var auth = File.ReadAllText(Path.Combine(
            root, "src", "KitPlatform.Api", "Authorization", "PharmacyKnowledgeAuthorizationExtensions.cs"));
        Assert.Equal(2, auth.Split("IsInRole(\"ADMIN\")").Length - 1);
        Assert.Contains("HasPermission(ctx, \"pharmacy.knowledge.approve\")", auth, StringComparison.Ordinal);
        var approve = auth.Split("PharmacyKnowledgePolicies.Approve", 2)[1].Split("PharmacyKnowledgePolicies.Publish", 2)[0];
        Assert.Contains("IsInRole(\"ADMIN\")", approve, StringComparison.Ordinal);
        var platform = auth.Split("PharmacyKnowledgePolicies.Publish", 2)[1]
            .Split("PharmacyOwnedKnowledgePolicies.Manage", 2)[0];
        Assert.DoesNotContain("IsInRole(", platform, StringComparison.Ordinal);

        var controller = File.ReadAllText(Path.Combine(
            root, "src", "KitPlatform.Api", "Controllers", "Pharmacy", "PharmacyKnowledgeApprovalController.cs"));
        Assert.Contains("_tenant.TenantId", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("request.Tenant", controller, StringComparison.Ordinal);

        var layout = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "shared", "components", "AppLayout.tsx"));
        Assert.Contains("useCanApproveTenantKnowledge()", layout, StringComparison.Ordinal);
        var publish = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "pharmacy", "KnowledgePublishPage.tsx"));
        Assert.DoesNotContain("useCanApproveTenantKnowledge", publish, StringComparison.Ordinal);
    }

    [Fact]
    public void Safety_gate_and_copilot_pin_query_stay_unchanged()
    {
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("stop_sale"));
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("refer_medical"));
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("refer_pharmacist"));
        Assert.Contains("a.rule_version", File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "Packs",
            "Pharmacy",
            "KitPlatform.Packs.Pharmacy.Infrastructure",
            "Consultation",
            "SymptomTaxonomyRepository.cs")), StringComparison.Ordinal);
        var publication = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "Packs",
            "Pharmacy",
            "KitPlatform.Packs.Pharmacy.Infrastructure",
            "Knowledge",
            "PharmacyKnowledgePublicationService.cs"));
        Assert.Contains("EnsureMayPublish", publication, StringComparison.Ordinal);
        Assert.DoesNotContain("pack_learning.enrollment", publication, StringComparison.Ordinal);
        Assert.DoesNotContain("operational_task", publication, StringComparison.Ordinal);
        Assert.DoesNotContain("effective_from", publication.Split("VerifySourceAsync", 2)[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_423_does_not_accept_or_publish_seeded_rules_or_grant_permissions()
    {
        var root = RepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "migrations", "423_pharmacy_knowledge_source_verification.sql"));
        Assert.Contains("DEFAULT 'UNVERIFIED'", sql, StringComparison.Ordinal);
        Assert.Contains("KR_OTHER", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("KR_COUGH", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("KR_FEVER", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE pharmacy_knowledge_rule", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET source_verification_status = 'ACCEPTED'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SET lifecycle_status", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO permissions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            sql.Split('\n'),
            line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)
                && line.Contains("GRANT ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("pharmacy.knowledge.approve", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("platform.knowledge.publish", sql, StringComparison.Ordinal);

        foreach (var manifest in new[]
        {
            Path.Combine("deploy", "ubuntu", "migration-files.prod.txt"),
            Path.Combine("deploy", "ubuntu", "migration-files.staff-enablement-v1.txt"),
            Path.Combine("deploy", "ubuntu", "migration-files.pharmacy-ops-v2.txt"),
            Path.Combine("deploy", "ubuntu", "migration-files.content.txt"),
        })
        {
            var text = File.ReadAllText(Path.Combine(root, manifest));
            Assert.DoesNotContain("423_pharmacy_knowledge_source_verification.sql", text, StringComparison.Ordinal);
        }

        var picker = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "shared", "auth", "permission-picker.ts"));
        Assert.DoesNotContain(PharmacyKnowledgeApprovalAccess.ReviewPermission, picker, StringComparison.Ordinal);
        Assert.DoesNotContain(PharmacyKnowledgeApprovalAccess.VerifySourcePermission, picker, StringComparison.Ordinal);
        Assert.Contains("pharmacy.knowledge.approve", picker, StringComparison.Ordinal);

        var page = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "pharmacy", "KnowledgePublishPage.tsx"));
        Assert.Contains("useHasExplicitPermission('pharmacy.knowledge.review')", page, StringComparison.Ordinal);
        Assert.Contains("useHasExplicitPermission('pharmacy.knowledge.verify-source')", page, StringComparison.Ordinal);
        Assert.Contains("useHasExplicitPermission('platform.knowledge.publish')", page, StringComparison.Ordinal);
        Assert.DoesNotContain("useHasPermission(", page, StringComparison.Ordinal);
        Assert.Contains("sourceAcceptanceError", page, StringComparison.Ordinal);
        Assert.Contains("publishBlockers", page, StringComparison.Ordinal);
        Assert.Contains("updateKnowledgeContent", page, StringComparison.Ordinal);
        Assert.Contains("Xuất bản không làm nhà thuốc tự áp dụng", page, StringComparison.Ordinal);

        var approval = File.ReadAllText(Path.Combine(root, "client", "admin", "src", "modules", "pharmacy", "KnowledgeApprovalPage.tsx"));
        Assert.Contains("useCanApproveTenantKnowledge()", approval, StringComparison.Ordinal);
        Assert.DoesNotContain("useHasPermission(", approval, StringComparison.Ordinal);
        Assert.DoesNotContain("useHasExplicitPermission(", approval, StringComparison.Ordinal);
        Assert.Contains("Đang áp dụng", approval, StringComparison.Ordinal);
        Assert.Contains("Chưa áp dụng", approval, StringComparison.Ordinal);
        Assert.Contains("Có phiên bản mới", approval, StringComparison.Ordinal);
        Assert.Contains("Không có pin đang hiệu lực", approval, StringComparison.Ordinal);
        Assert.Contains("pharmacyListGroup", approval, StringComparison.Ordinal);

        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Packs",
            "Pharmacy",
            "KitPlatform.Packs.Pharmacy.Infrastructure",
            "Knowledge",
            "PharmacyKnowledgePublicationService.cs"));
        Assert.Contains("SymptomIdForContentEdit", service, StringComparison.Ordinal);
    }

    private static string Publish(
        string ruleCode = "KR_FEVER",
        string lifecycle = "IN_REVIEW",
        string? status = "ACCEPTED",
        string? reference = "Hướng dẫn OTC, mục sốt",
        string? identity = "DOC-1",
        string? url = "https://example.test/source",
        string? document = null,
        string? organization = "Cơ quan ban hành",
        string? evidence = "mục 1",
        string? verifiedBy = "reviewer",
        DateTime? verifiedAt = null,
        bool passVerifiedAt = true) =>
        PharmacyKnowledgeSourceRules.EnsureMayPublish(
            ruleCode,
            lifecycle,
            status,
            reference,
            identity,
            url,
            document,
            organization,
            evidence,
            verifiedBy,
            passVerifiedAt ? verifiedAt ?? VerifiedAt : null);

    private static void AssertPolicy(Type controller, string methodName, string policy)
    {
        var method = controller.GetMethod(methodName);
        Assert.NotNull(method);
        var auth = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(policy, auth?.Policy);
    }

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.prod.txt")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Không thấy gốc repo.");
    }
}
