namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public sealed record PaperTaskSnapshot(
    Guid TaskId,
    string TaskKey,
    string Status,
    string Goal,
    Guid DecisionId,
    string OperationKey,
    string DecisionStatus,
    int PaperExecutionCount,
    string? VerificationNote,
    IReadOnlyList<PaperProposalWorkflow.Step> Steps);

public interface IPaperTaskStore
{
    Task<PaperTaskSnapshot> ContinueProposedAsync(
        BusinessCaller caller,
        Guid decisionId,
        string operationKey,
        IReadOnlyDictionary<string, string> slots,
        string evidenceJson,
        string evidenceSha256,
        CancellationToken cancellationToken);

    Task<PaperTaskSnapshot?> VerifyAsync(Guid taskId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaperTaskSnapshot>> ListAsync(CancellationToken cancellationToken);
}

public static class PaperTaskVerification
{
    public static (string Status, string? Note) Decide(string? decisionStatus, int paperExecutionCount, bool linkMatches, bool readable)
    {
        if (!readable)
            return ("unverified", "read_failed");
        if (!linkMatches)
            return ("unverified", "decision_link_mismatch");
        if (string.IsNullOrEmpty(decisionStatus))
            return ("unverified", "decision_missing");
        if (decisionStatus == "proposed" && paperExecutionCount == 0)
            return ("waiting_approval", null);
        if (decisionStatus == "accepted" && paperExecutionCount == 1)
            return ("completed", null);
        if (decisionStatus == "rejected" && paperExecutionCount == 0)
            return ("blocked", null);
        if (decisionStatus is "proposed" or "accepted" or "rejected")
            return ("unverified", "execution_count_mismatch");
        return ("unverified", "decision_status_unhandled");
    }
}
