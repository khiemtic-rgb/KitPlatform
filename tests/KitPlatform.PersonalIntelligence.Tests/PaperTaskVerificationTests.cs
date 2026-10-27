using KitPlatform.Packs.PersonalIntelligence.Intent;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PaperTaskVerificationTests
{
    [Theory]
    [InlineData("proposed", 0, true, true, "waiting_approval", null)]
    [InlineData("accepted", 1, true, true, "completed", null)]
    [InlineData("rejected", 0, true, true, "blocked", null)]
    [InlineData("accepted", 0, true, true, "unverified", "execution_count_mismatch")]
    [InlineData("accepted", 2, true, true, "unverified", "execution_count_mismatch")]
    [InlineData("rejected", 1, true, true, "unverified", "execution_count_mismatch")]
    [InlineData("superseded", 0, true, true, "unverified", "decision_status_unhandled")]
    [InlineData(null, 0, true, true, "unverified", "decision_missing")]
    [InlineData("accepted", 1, false, true, "unverified", "decision_link_mismatch")]
    [InlineData("accepted", 1, true, false, "unverified", "read_failed")]
    public void Verdict_matches_the_re_read_rule(
        string? decisionStatus,
        int paperCount,
        bool linkMatches,
        bool readable,
        string status,
        string? note)
    {
        var verdict = PaperTaskVerification.Decide(decisionStatus, paperCount, linkMatches, readable);
        Assert.Equal(status, verdict.Status);
        Assert.Equal(note, verdict.Note);
        var steps = PaperProposalWorkflow.Assess(decisionStatus, paperCount, linkMatches, readable);
        Assert.Equal(status, steps.TaskStatus);
    }

    [Fact]
    public void Paper_steps_wait_then_complete_only_after_one_paper_execution()
    {
        var waiting = PaperProposalWorkflow.Assess("proposed", 0, true, true);
        Assert.Equal("waiting_approval", waiting.TaskStatus);
        Assert.Equal("completed", waiting.Steps[0].Status);
        Assert.Equal("waiting_approval", waiting.Steps[1].Status);
        Assert.Equal("pending", waiting.Steps[2].Status);
        Assert.Equal("dependency_unmet", waiting.Steps[2].Note);

        var done = PaperProposalWorkflow.Assess("accepted", 1, true, true);
        Assert.Equal("completed", done.TaskStatus);
        Assert.All(done.Steps, step => Assert.Equal("completed", step.Status));

        var rejected = PaperProposalWorkflow.Assess("rejected", 0, true, true);
        Assert.Equal("blocked", rejected.TaskStatus);
        Assert.Equal("blocked", rejected.Steps[1].Status);
        Assert.Equal("pending", rejected.Steps[2].Status);
    }

    [Fact]
    public void Mismatched_execution_stays_unverified_and_does_not_settle()
    {
        var missing = PaperProposalWorkflow.Assess("accepted", 0, true, true);
        Assert.Equal("unverified", missing.TaskStatus);
        Assert.Equal("completed", missing.Steps[0].Status);
        Assert.Equal("completed", missing.Steps[1].Status);
        Assert.Equal("unverified", missing.Steps[2].Status);

        var extra = PaperProposalWorkflow.Assess("rejected", 1, true, true);
        Assert.Equal("unverified", extra.TaskStatus);
        Assert.Equal("unverified", extra.Steps[1].Status);
        Assert.NotEqual("completed", extra.Steps[2].Status);
        Assert.NotEqual("blocked", extra.TaskStatus);
    }
}
