namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public static class PaperProposalWorkflow
{
    public const string Id = "second-brain.paper-proposal.v1";

    public sealed record Step(string Key, string Kind, IReadOnlyList<string> DependsOn, string Status, string? Note);

    public sealed record Assessment(string TaskStatus, string? Note, IReadOnlyList<Step> Steps);

    public static Assessment Assess(string? decisionStatus, int paperExecutionCount, bool linkMatches, bool readable)
    {
        var task = PaperTaskVerification.Decide(decisionStatus, paperExecutionCount, linkMatches, readable);
        if (!readable || !linkMatches)
        {
            return new Assessment(task.Status, task.Note,
            [
                StepOf("evaluate", "capability_gate", [], "unverified", task.Note),
                StepOf("decide", "human_decision", ["evaluate"], "unverified", task.Note),
                StepOf("settle", "observe_effect", ["decide"], "unverified", task.Note),
            ]);
        }

        string decide;
        string? decideNote;
        if (string.IsNullOrEmpty(decisionStatus))
        {
            decide = "unverified";
            decideNote = "decision_missing";
        }
        else if (decisionStatus == "proposed" && paperExecutionCount == 0)
        {
            decide = "waiting_approval";
            decideNote = null;
        }
        else if (decisionStatus == "accepted")
        {
            decide = "completed";
            decideNote = null;
        }
        else if (decisionStatus == "rejected" && paperExecutionCount == 0)
        {
            decide = "blocked";
            decideNote = null;
        }
        else if (decisionStatus is "proposed" or "rejected")
        {
            decide = "unverified";
            decideNote = "execution_count_mismatch";
        }
        else
        {
            decide = "unverified";
            decideNote = "decision_status_unhandled";
        }

        string settle;
        string? settleNote;
        if (decide == "completed" && paperExecutionCount == 1)
        {
            settle = "completed";
            settleNote = null;
        }
        else if (decide == "completed")
        {
            settle = "unverified";
            settleNote = "execution_count_mismatch";
        }
        else if (decide is "waiting_approval" or "blocked")
        {
            settle = "pending";
            settleNote = "dependency_unmet";
        }
        else
        {
            settle = "unverified";
            settleNote = decideNote;
        }

        return new Assessment(task.Status, task.Note,
        [
            StepOf("evaluate", "capability_gate", [], "completed", null),
            StepOf("decide", "human_decision", ["evaluate"], decide, decideNote),
            StepOf("settle", "observe_effect", ["decide"], settle, settleNote),
        ]);
    }

    static Step StepOf(string key, string kind, string[] dependsOn, string status, string? note) =>
        new(key, kind, dependsOn, status, note);
}
