using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class CatalogGroundingTests
{
    [Fact]
    public void A_subject_that_exists_only_in_the_catalog_is_planned_from_that_catalog()
    {
        var notes = new CapabilityDefinition(
            "notes.count",
            "notes.collect",
            "personal_intelligence.evidence.read",
            ToolEffect.Read,
            false,
            "pack_personal_intelligence.pi_note",
            "A test catalog entry.",
            new HashSet<string>(StringComparer.Ordinal) { "NOTE" },
            new HashSet<string>(StringComparer.Ordinal) { "atLeast" });
        const string sentence = "Keep at least 2 NOTE rows.";
        var asks = CatalogGrounding.Match("Review the stored rows.", sentence, [notes]);
        var understood = RequestUnderstanding.Analyze("Review the stored rows.", sentence, asks, [notes], "notes.assess");

        Assert.NotNull(asks);
        Assert.Equal("notes.count", asks[0].Capability);
        Assert.Equal("NOTE", asks[0].Subject);
        Assert.Equal("atLeast", asks[0].Relation);
        Assert.Equal("2", asks[0].Target);
        Assert.Equal(UnderstandingState.ReadyToPlan, understood.State);
        Assert.Contains(understood.Plan, step => step.ToolName == "notes.collect");
        Assert.DoesNotContain(understood.Plan, step => step.ToolName == "evidence.collect");
    }

    [Fact]
    public void The_same_sentence_is_not_planned_when_the_catalog_does_not_contain_the_subject()
    {
        const string sentence = "Keep at least 2 NOTE rows.";
        var asks = CatalogGrounding.Match("Review the stored rows.", sentence, GoalTaskRuntime.Catalog);
        var understood = GoalTaskRuntime.Understand("Review the stored rows.", sentence, asks);

        Assert.NotNull(asks);
        Assert.Equal(UnderstandingState.MissingData, understood.State);
        Assert.DoesNotContain(understood.Plan, step => step.ToolName is "notes.collect" or "evidence.collect" or "trading.paper_execute");
    }

    [Fact]
    public void A_new_comparison_sentence_uses_the_registered_count_capability()
    {
        const string sentence = "Is the OBSERVATION count greater than the FACT count?";
        var asks = CatalogGrounding.Match("Review the stored rows.", sentence, GoalTaskRuntime.Catalog);
        var understood = GoalTaskRuntime.Understand("Review the stored rows.", sentence, asks);

        Assert.NotNull(asks);
        Assert.Equal("evidence.count", asks[0].Capability);
        Assert.Equal("OBSERVATION", asks[0].Subject);
        Assert.Equal("greaterThan", asks[0].Relation);
        Assert.Equal("FACT", asks[0].Target);
        Assert.Equal(UnderstandingState.ReadyToPlan, understood.State);
        Assert.Contains(understood.Plan, step => step.ToolName == "evidence.collect");
        Assert.DoesNotContain(understood.Plan, step => step.ToolName == "trading.paper_execute");
    }

    [Fact]
    public void An_unknown_capability_and_an_approval_capability_stay_unexecuted()
    {
        var unknown = CatalogGrounding.Match("Review the stored rows.", "Use the unregistered capability thesis.read.", GoalTaskRuntime.Catalog);
        var unknownPlan = GoalTaskRuntime.Understand("Review the stored rows.", "Use the unregistered capability thesis.read.", unknown);
        var approval = CatalogGrounding.Match("Review the stored rows.", "A person must decide before trading.paper_execute.", GoalTaskRuntime.Catalog);
        var approvalPlan = GoalTaskRuntime.Understand("Review the stored rows.", "A person must decide before trading.paper_execute.", approval);
        Assert.Null(CatalogGrounding.Match("Improve the work.", "Make it good.", GoalTaskRuntime.Catalog));

        Assert.Equal(UnderstandingState.MissingCapability, unknownPlan.State);
        Assert.DoesNotContain(unknownPlan.Plan, step => step.ToolName == "thesis.read");
        Assert.Equal(UnderstandingState.NeedsDecision, approvalPlan.State);
        Assert.True(approvalPlan.ApprovalRequired);
        Assert.DoesNotContain(approvalPlan.Plan, step => step.ToolName == "trading.paper_execute");
    }
}
