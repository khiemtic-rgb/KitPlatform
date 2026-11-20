using KitPlatform.Api.Authorization;
using System.Runtime.CompilerServices;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Learning;
using KitPlatform.Application.Success;
using Xunit;
using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Knowledge;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Tests;

public class SellableBatchRulesTests
{
    [Fact]
    public void Lot_without_expiry_stays_sellable()
    {
        Assert.True(SellableBatchRules.IsSellableOn(null, new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void Lot_expiring_today_stays_sellable_and_yesterday_does_not()
    {
        var today = new DateOnly(2026, 10, 30);
        Assert.True(SellableBatchRules.IsSellableOn(today, today));
        Assert.False(SellableBatchRules.IsSellableOn(today.AddDays(-1), today));
    }

    [Fact]
    public void Sale_allocation_sql_skips_expired_lots()
    {
        Assert.Contains(SellableBatchRules.SqlUnqualified, BatchResolver.AvailableSql, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", BatchResolver.AvailableForUpdateSql, StringComparison.Ordinal);
        Assert.Contains(SellableBatchRules.SqlUnqualified, BatchResolver.AvailableForUpdateSql, StringComparison.Ordinal);
    }
}

public class SalesShiftSummaryScopeTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Current_vietnam_day_starts_at_previous_utc_evening()
    {
        var (start, end) = SalesShiftSummaryScope.CurrentVietnamDayUtc(NowUtc);
        Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 30, 17, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void Cashier_may_read_today_and_not_a_wider_range()
    {
        var (start, end) = SalesShiftSummaryScope.CurrentVietnamDayUtc(NowUtc);
        Assert.True(SalesShiftSummaryScope.IsContainedInCurrentVietnamDay(start, end, NowUtc));
        Assert.False(SalesShiftSummaryScope.IsContainedInCurrentVietnamDay(
            start.AddDays(-1), end, NowUtc));
    }
}

public class SuccessChecklistAccessTests
{
    [Fact]
    public void Owner_read_can_view_checklist_but_not_tick_it()
    {
        var permissions = new[] { "success.read" };
        Assert.True(SuccessChecklistAccess.CanView(isAdminRole: false, permissions));
        Assert.False(SuccessChecklistAccess.CanMutate(isAdminRole: false, permissions));
    }

    [Fact]
    public void Cashier_pos_and_checklist_permission_can_tick()
    {
        Assert.True(SuccessChecklistAccess.CanMutate(false, ["sales.pos"]));
        Assert.True(SuccessChecklistAccess.CanMutate(false, ["success.checklist"]));
        Assert.False(SuccessChecklistAccess.CanMutate(false, ["sales.write"]));
    }

    [Fact]
    public void Admin_role_can_tick()
    {
        Assert.True(SuccessChecklistAccess.CanMutate(true, []));
    }
}

public class PharmacyOperationalCompletionTests
{
    [Fact]
    public void Refer_and_stop_hide_the_ordinary_otc_list()
    {
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("stop_sale"));
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("refer_medical"));
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("refer_pharmacist"));
        Assert.True(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("caution"));
        Assert.True(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("none"));
    }

    [Fact]
    public void Copilot_uses_only_approved_knowledge_and_cannot_skip_review()
    {
        Assert.False(PharmacyKnowledgeApproval.IsUsableByCopilot("PENDING_REVIEW", "PUBLISHED"));
        Assert.False(PharmacyKnowledgeApproval.IsUsableByCopilot("APPROVED", "IN_REVIEW"));
        Assert.False(PharmacyKnowledgeApproval.IsUsableByCopilot("APPROVED", "RETIRED"));
        Assert.True(PharmacyKnowledgeApproval.IsUsableByCopilot("APPROVED", "PUBLISHED"));
        Assert.Equal("PUBLISHED", PharmacyKnowledgeApproval.Transition("IN_REVIEW", "published"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeApproval.Transition("DRAFT", "PUBLISHED"));
    }

    [Fact]
    public void Finished_operational_task_does_not_reopen()
    {
        Assert.True(OperationalTaskRules.CanTransition("open", "in_progress"));
        Assert.True(OperationalTaskRules.CanTransition("in_progress", "done"));
        Assert.False(OperationalTaskRules.CanTransition("done", "open"));
        Assert.False(OperationalTaskRules.CanTransition("cancelled", "done"));
    }

    [Fact]
    public void Employee_finishes_own_work_and_manager_assigns_or_cancels()
    {
        Assert.True(OperationalTaskRules.CanView(canManage: false, isAssignee: true));
        Assert.False(OperationalTaskRules.CanView(canManage: false, isAssignee: false));
        Assert.True(OperationalTaskRules.CanView(canManage: true, isAssignee: false));
        Assert.False(OperationalTaskRules.CanAssign(hasLearningWrite: false));
        Assert.True(OperationalTaskRules.CanAssign(hasLearningWrite: true));
        Assert.True(OperationalTaskRules.CanTransition("open", "in_progress", canManage: false));
        Assert.True(OperationalTaskRules.CanTransition("in_progress", "done", canManage: false));
        Assert.False(OperationalTaskRules.CanTransition("open", "cancelled", canManage: false));
        Assert.False(OperationalTaskRules.CanTransition("in_progress", "cancelled", canManage: false));
        Assert.True(OperationalTaskRules.CanTransition("open", "cancelled", canManage: true));
        Assert.False(OperationalTaskRules.CanTransition("done", "open", canManage: true));
    }

    [Fact]
    public void Tenant_admin_can_pin_without_a_separate_grant_and_other_roles_cannot()
    {
        Assert.True(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: false, isTenantAdmin: true));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: true, isTenantAdmin: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: true, hasApprovePermission: false, isTenantAdmin: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanApprove(isErpPrincipal: false, hasApprovePermission: true, isTenantAdmin: true));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanReview(isErpPrincipal: true, hasReviewPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanVerifySource(isErpPrincipal: true, hasVerifySourcePermission: false));
    }

    [Fact]
    public void Coded_safety_warnings_stay_pending_review()
    {
        Assert.Equal(13, PharmacySafetyFlagInventory.Items.Count);
        Assert.Contains(PharmacySafetyFlagInventory.Items, item => item.FlagCode == "stop_sale" || item.SafetyLevel == "stop_sale");
        Assert.Contains(PharmacySafetyFlagInventory.Items, item => item.SafetyLevel == "refer_medical");
        Assert.Contains(PharmacySafetyFlagInventory.Items, item => item.SafetyLevel == "refer_pharmacist");
        Assert.DoesNotContain(PharmacySafetyFlagInventory.Items, item => item.FlagCode == "APPROVED");
        Assert.Equal(
            PharmacyKnowledgeApproval.PendingReview,
            PharmacyKnowledgeApproval.PendingReview);
        Assert.False(PharmacyKnowledgeApproval.IsUsableByCopilot(
            PharmacyKnowledgeApproval.PendingReview,
            PharmacyKnowledgeApproval.Published));
    }

    [Fact]
    public void Copilot_product_query_requires_approved_rules_only()
    {
        Assert.Contains("tenant_id", PharmacyKnowledgeCopilotQuery.TenantPublishedPin, StringComparison.Ordinal);
        Assert.Contains("APPROVED", PharmacyKnowledgeCopilotQuery.TenantPublishedPin, StringComparison.Ordinal);
        Assert.Contains("PUBLISHED", PharmacyKnowledgeCopilotQuery.TenantPublishedPin, StringComparison.Ordinal);
        Assert.DoesNotContain("approval_status", PharmacyKnowledgeCopilotQuery.TenantPublishedPin, StringComparison.Ordinal);
        Assert.DoesNotContain("pharmacy_safety_flag_review", PharmacyKnowledgeCopilotQuery.TenantPublishedPin, StringComparison.Ordinal);
        Assert.Equal("KR_OTHER", PharmacyKnowledgeCopilotQuery.OperationallyExcludedRuleCode);
        Assert.Contains(
            "v.rule_code <> 'KR_OTHER'",
            PharmacyKnowledgeCopilotQuery.ExcludeUnverifiedCatchAll,
            StringComparison.Ordinal);
        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: false));
        Assert.False(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: false, hasPlatformPublishPermission: true));
        Assert.True(PharmacyKnowledgeApprovalAccess.CanPublish(isErpPrincipal: true, hasPlatformPublishPermission: true));
        Assert.DoesNotContain(
            PharmacyKnowledgeApprovalAccess.PlatformPublishPermission,
            "pharmacy.knowledge.approve",
            StringComparison.Ordinal);
    }

    [Fact]
    public void Several_open_shifts_are_not_auto_selected()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Assert.Null(ShiftChecklistShiftSelection.Resolve([first, second], null, required: false));
        Assert.Equal(first, ShiftChecklistShiftSelection.Resolve([first, second], first, required: false));
        Assert.Equal(first, ShiftChecklistShiftSelection.Resolve([first], null, required: false));
        Assert.Throws<InvalidOperationException>(() =>
            ShiftChecklistShiftSelection.Resolve([first, second], null, required: true));
    }

    [Fact]
    public void Staff_repurchase_query_is_scoped_by_tenant_and_customer()
    {
        Assert.Contains("tenant_id", StaffRepurchaseQuery.ByCustomer, StringComparison.Ordinal);
        Assert.Contains("customer_id", StaffRepurchaseQuery.ByCustomer, StringComparison.Ordinal);
        Assert.DoesNotContain("customer_account_id", StaffRepurchaseQuery.ByCustomer, StringComparison.Ordinal);
    }

    [Fact]
    public void Operational_task_visibility_follows_assignee_and_manager()
    {
        Assert.False(OperationalTaskRules.CanAssign(hasLearningWrite: false));
        Assert.True(OperationalTaskRules.CanAssign(hasLearningWrite: true));
        Assert.True(OperationalTaskRules.CanView(canManage: false, isAssignee: true));
        Assert.False(OperationalTaskRules.CanView(canManage: false, isAssignee: false));
        Assert.True(OperationalTaskRules.CanView(canManage: true, isAssignee: false));
    }

    [Theory]
    [InlineData("stop_sale")]
    [InlineData("refer_medical")]
    [InlineData("refer_pharmacist")]
    public void Safety_levels_hide_ordinary_otc_products(string level)
    {
        Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList(level));
    }

    [Fact]
    public void Return_key_cannot_replay_against_a_different_order()
    {
        var order = Guid.NewGuid();
        var other = Guid.NewGuid();
        SaleReturnIdempotency.EnsureSameOrder(order, order);
        Assert.Throws<InvalidOperationException>(() => SaleReturnIdempotency.EnsureSameOrder(order, other));
    }

    [Fact]
    public void Approval_keeps_the_first_reference_and_version_column()
    {
        Assert.Equal("Bộ Y tế", PharmacyKnowledgeApproval.PreserveReference("Bộ Y tế", "nguồn khác"));
        Assert.Equal("nguồn mới", PharmacyKnowledgeApproval.PreserveReference(null, "nguồn mới"));
        Assert.StartsWith("reference_source = COALESCE(reference_source,", PharmacyKnowledgeApproval.PreserveReferenceSql, StringComparison.Ordinal);
        Assert.DoesNotContain("COALESCE(@ReferenceSource, reference_source)", PharmacyKnowledgeApproval.PreserveReferenceSql, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeApproval.Transition("RETIRED", "PUBLISHED"));
        Assert.Throws<InvalidOperationException>(() => PharmacyKnowledgeApproval.Transition("PUBLISHED", "IN_REVIEW"));
    }

    [Fact]
    public void Inventoried_refer_and_stop_flags_hide_otc_products()
    {
        foreach (var item in PharmacySafetyFlagInventory.Items)
        {
            if (item.SafetyLevel is "stop_sale" or "refer_medical" or "refer_pharmacist")
                Assert.False(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList(item.SafetyLevel));
        }

        Assert.True(PharmacyOtcSuggestionGate.ShowsOrdinaryOtcList("caution"));
    }

    [Fact]
    public void Pharmacy_ops_migrations_stay_out_of_production_and_can_be_reversed()
    {
        var root = RepoRoot();
        var prod = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.prod.txt"));
        var ops = File.ReadAllLines(Path.Combine(root, "deploy", "ubuntu", "migration-files.pharmacy-ops-v2.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
        var expected = new[]
        {
            "411_sales_return_client_return_id.sql",
            "412_success_checklist_pos_shift.sql",
            "413_learning_operational_task.sql",
            "414_pharmacy_knowledge_approval.sql",
            "415_pharmacy_knowledge_approval_history.sql",
        };
        Assert.Equal(expected, ops);
        foreach (var file in expected)
        {
            Assert.DoesNotContain(file, prod, StringComparison.Ordinal);
            Assert.Contains("IF NOT EXISTS", File.ReadAllText(Path.Combine(root, "migrations", file)), StringComparison.Ordinal);
        }

        var down = File.ReadAllText(Path.Combine(root, "migrations", "411_415_pharmacy_ops_v2_down.sql"));
        Assert.Contains("DROP TABLE IF EXISTS pharmacy_knowledge_approval_event", down, StringComparison.Ordinal);
        Assert.Contains("client_return_id", down, StringComparison.Ordinal);
        Assert.Contains("BEGIN;", down, StringComparison.Ordinal);
        Assert.Contains("COMMIT;", down, StringComparison.Ordinal);
        Assert.Contains("Không hoàn nguyên 412", down, StringComparison.Ordinal);
        Assert.DoesNotContain("411_415_pharmacy_ops_v2_down.sql", prod, StringComparison.Ordinal);
        var history = File.ReadAllText(Path.Combine(root, "migrations", "415_pharmacy_knowledge_approval_history.sql"));
        Assert.Contains("BEFORE UPDATE OR DELETE", history, StringComparison.Ordinal);
        Assert.Contains("'PENDING_REVIEW'", history, StringComparison.Ordinal);
        Assert.DoesNotContain("'APPROVED'", history.Split("INSERT", 2)[1], StringComparison.Ordinal);
    }

    private static string RepoRoot([CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.pharmacy-ops-v2.txt")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Không thấy gốc repo.");
    }
}
