using System.Reflection;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.Pharmacy;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace KitPlatform.Tests;

public class PharmacyOwnedKnowledgeM1Tests
{
    [Fact]
    public void Tenant_admin_can_draft_and_staff_cannot()
    {
        Assert.True(PharmacyOwnedKnowledgeAccess.CanManage(isErpPrincipal: true, isTenantAdmin: true));
        Assert.False(PharmacyOwnedKnowledgeAccess.CanManage(isErpPrincipal: true, isTenantAdmin: false));
        Assert.False(PharmacyOwnedKnowledgeAccess.CanManage(isErpPrincipal: false, isTenantAdmin: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanReview(isErpPrincipal: true, hasReviewPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanVerifySource(isErpPrincipal: true, hasVerifySourcePermission: false));
    }

    [Fact]
    public void Write_request_has_no_tenant_and_only_draft_can_change()
    {
        Assert.Null(typeof(PharmacyOwnedKnowledgeWriteRequest).GetProperty("TenantId"));
        Assert.Null(typeof(PharmacyOwnedKnowledgeWriteRequest).GetProperty("LifecycleStatus"));
        PharmacyOwnedKnowledgeRules.EnsureDraft("DRAFT");
        Assert.Throws<InvalidOperationException>(() => PharmacyOwnedKnowledgeRules.EnsureDraft("APPROVED"));
        Assert.Throws<InvalidOperationException>(() => PharmacyOwnedKnowledgeRules.EnsureDraft("IN_REVIEW"));
        Assert.Throws<InvalidOperationException>(() => PharmacyOwnedKnowledgeRules.EnsureDraft("ACTIVE"));
        Assert.Throws<InvalidOperationException>(() => PharmacyOwnedKnowledgeRules.EnsureDraft("STOPPED"));
        Assert.Throws<InvalidOperationException>(() => PharmacyOwnedKnowledgeRules.EnsureDraft("RETIRED"));
        Assert.Equal("CLINICAL_KNOWLEDGE", PharmacyOwnedKnowledgeRules.NormalizeKind("clinical_knowledge"));
        Assert.Throws<InvalidOperationException>(() => PharmacyOwnedKnowledgeRules.NormalizeKind("KR_OTHER"));
    }

    [Fact]
    public void M1_pipeline_stays_inside_the_tenant_and_out_of_runtime()
    {
        var root = RepoRoot();
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Packs",
            "Pharmacy",
            "KitPlatform.Packs.Pharmacy.Infrastructure",
            "Knowledge",
            "PharmacyOwnedKnowledgeService.cs"));
        Assert.Contains("v.tenant_id = @TenantId", service, StringComparison.Ordinal);
        Assert.Contains("lifecycle_status = 'DRAFT'", service, StringComparison.Ordinal);
        Assert.Contains("CASE WHEN snapshot_fidelity = 'EXACT' THEN body END AS Body", service, StringComparison.Ordinal);
        Assert.Contains("CASE WHEN snapshot_fidelity = 'EXACT' THEN classification END AS Classification", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY created_at, id", service, StringComparison.Ordinal);
        Assert.Contains("PharmacyOwnedKnowledgeRules.ExactSnapshot", service, StringComparison.Ordinal);
        foreach (var method in new[] { "CreateAsync", "UpdateAsync" })
        {
            var body = service.Split(method, 2)[1].Split("InsertEventAsync(", 2)[0];
            Assert.DoesNotContain("CommitAsync", body, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("pharmacy_knowledge_rule_version", service, StringComparison.Ordinal);
        Assert.DoesNotContain("pharmacy_knowledge_adoption", service, StringComparison.Ordinal);
        foreach (var forbidden in new[]
        {
            "sales_orders",
            "sales_order_lines",
            "customers",
            "prescriptions",
            "inventory_batches",
            "stock_movements",
            "sales_shifts",
            "pack_learning",
            "operational_task",
        })
        {
            Assert.DoesNotContain(forbidden, service, StringComparison.OrdinalIgnoreCase);
        }

        var controller = File.ReadAllText(Path.Combine(
            root,
            "src",
            "KitPlatform.Api",
            "Controllers",
            "Pharmacy",
            "PharmacyOwnedKnowledgeController.cs"));
        Assert.Contains("_tenant.TenantId", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("request.Tenant", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Copilot", controller, StringComparison.Ordinal);
        var governance = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Packs",
            "Pharmacy",
            "KitPlatform.Packs.Pharmacy.Infrastructure",
            "Knowledge",
            "PharmacyOwnedKnowledgeGovernance.cs"));
        Assert.Contains("kind <> 'CLINICAL_KNOWLEDGE'", governance, StringComparison.Ordinal);
        Assert.Contains("Tri thức lâm sàng không được đưa vào sử dụng.", governance, StringComparison.Ordinal);
        AssertPolicy(typeof(PharmacyOwnedKnowledgeController), "List", PharmacyOwnedKnowledgePolicies.Manage);
        AssertPolicy(typeof(PharmacyOwnedKnowledgeController), "Get", PharmacyOwnedKnowledgePolicies.Manage);
        AssertPolicy(typeof(PharmacyOwnedKnowledgeController), "Create", PharmacyOwnedKnowledgePolicies.Use);
        AssertPolicy(typeof(PharmacyOwnedKnowledgeController), "Update", PharmacyOwnedKnowledgePolicies.Use);
        AssertPolicy(typeof(PharmacyOwnedKnowledgeController), "Grant", PharmacyOwnedKnowledgePolicies.Manage);
        Assert.False(PharmacyKnowledgeTenantScope.AllowsPharmacyKnowledge("marketing"));
        Assert.False(PharmacyKnowledgeTenantScope.AllowsPharmacyKnowledge("family"));
        Assert.False(PharmacyKnowledgeTenantScope.AllowsPharmacyKnowledge(null));
        Assert.True(PharmacyKnowledgeTenantScope.AllowsPharmacyKnowledge("pharmacy"));
        Assert.False(PharmacyOwnedKnowledgeAccess.Allows(false, "approve", "GUIDANCE", []));
        Assert.False(PharmacyOwnedKnowledgeAccess.Allows(true, "activate", "CLINICAL_KNOWLEDGE", []));
        Assert.True(PharmacyOwnedKnowledgeAccess.Allows(false, "read", "GUIDANCE", [("read", "GUIDANCE")]));
        Assert.False(PharmacyOwnedKnowledgeAccess.Allows(false, "submit", "CLINICAL_KNOWLEDGE", [("clinical", "CLINICAL_KNOWLEDGE")]));
        Assert.False(PharmacyOwnedKnowledgeAccess.Allows(false, "approve", "CLINICAL_KNOWLEDGE", [("submit", "CLINICAL_KNOWLEDGE")]));
        Assert.True(PharmacyOwnedKnowledgeAccess.Allows(false, "submit", "CLINICAL_KNOWLEDGE", [("submit", "CLINICAL_KNOWLEDGE")]));

        var sql = File.ReadAllText(Path.Combine(root, "migrations", "424_pharmacy_owned_knowledge.sql"));
        Assert.Contains("lifecycle_status = 'DRAFT'", sql, StringComparison.Ordinal);
        Assert.Contains("only draft pharmacy-owned knowledge can be edited", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("pharmacy_knowledge_rule_version", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO permissions", sql, StringComparison.OrdinalIgnoreCase);
        foreach (var manifest in new[]
        {
            "migration-files.prod.txt",
            "migration-files.staff-enablement-v1.txt",
            "migration-files.pharmacy-ops-v2.txt",
            "migration-files.content.txt",
            "migration-files.family-os.txt",
            "migration-files.local-os.txt",
        })
        {
            var text = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", manifest));
            Assert.DoesNotContain("424_pharmacy_owned_knowledge.sql", text, StringComparison.Ordinal);
            Assert.DoesNotContain("425_pharmacy_owned_knowledge_event_snapshot.sql", text, StringComparison.Ordinal);
            Assert.DoesNotContain("426_pharmacy_owned_knowledge_event_fidelity.sql", text, StringComparison.Ordinal);
            Assert.DoesNotContain("427_pharmacy_owned_knowledge_governance.sql", text, StringComparison.Ordinal);
            Assert.DoesNotContain("428_pharmacy_owned_knowledge_one_open.sql", text, StringComparison.Ordinal);
        }

        var page = File.ReadAllText(Path.Combine(
            root, "client", "admin", "src", "modules", "pharmacy", "PharmacyOwnedKnowledgePage.tsx"));
        Assert.Contains("useIsAdmin()", page, StringComparison.Ordinal);
        Assert.DoesNotContain("pharmacy.knowledge.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Kích hoạt", page, StringComparison.Ordinal);
        Assert.Contains("Tri thức của nhà thuốc", page, StringComparison.Ordinal);
        Assert.Contains("event.snapshotFidelity === 'EXACT'", page, StringComparison.Ordinal);
        Assert.Contains("Nội dung lịch sử gốc không còn khả dụng.", page, StringComparison.Ordinal);
        Assert.Contains("Áp dụng", page, StringComparison.Ordinal);
        var staffPage = File.ReadAllText(Path.Combine(
            root, "client", "admin", "src", "modules", "pharmacy", "PharmacyKnowledgeInUsePage.tsx"));
        Assert.Contains("Tri thức đang dùng", staffPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Copilot", staffPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Kích hoạt", staffPage, StringComparison.Ordinal);

        var fidelity = File.ReadAllText(Path.Combine(root, "migrations", "426_pharmacy_owned_knowledge_event_fidelity.sql"));
        Assert.Contains("LEGACY_UNAVAILABLE", fidelity, StringComparison.Ordinal);
        Assert.Contains("body = NULL", fidelity, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM pharmacy_owned_knowledge_event", fidelity, StringComparison.Ordinal);
        var prior = File.ReadAllText(Path.Combine(root, "migrations", "425_pharmacy_owned_knowledge_event_snapshot.sql"));
        Assert.DoesNotContain("snapshot_fidelity", prior, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot_fidelity", sql, StringComparison.Ordinal);
    }

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
