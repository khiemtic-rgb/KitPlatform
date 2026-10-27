namespace KitPlatform.Packs.PersonalIntelligence.Intent;

public sealed record RecordedPaperDecision(Guid DecisionId, string OperationKey, string Status);

/// <summary>
/// Stores a proposed paper decision after the evaluation gate. It does not approve and does not fill.
/// </summary>
public interface IPaperDecisionRecorder
{
    Task<RecordedPaperDecision> RecordProposedAsync(
        BusinessCaller caller,
        IReadOnlyDictionary<string, string> slots,
        CancellationToken cancellationToken);
}
