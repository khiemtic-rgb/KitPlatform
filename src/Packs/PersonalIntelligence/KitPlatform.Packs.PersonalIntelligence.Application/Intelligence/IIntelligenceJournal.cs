namespace KitPlatform.Packs.PersonalIntelligence;

public sealed record PiEventRecord(
    Guid EventId,
    Guid TenantId,
    Guid PersonId,
    string EventType,
    EpistemicKind Kind,
    string Source,
    Guid CorrelationId,
    string Sink);

/// <summary>
/// Writes the canonical loop and its pi_events row in one database transaction.
/// </summary>
public interface IIntelligenceJournal
{
    Task SaveContextAsync(PiContext context, CancellationToken cancellationToken = default);

    Task SaveEvidenceAsync(PiEvidence evidence, CancellationToken cancellationToken = default);

    Task SaveDecisionAsync(PiDecision decision, CancellationToken cancellationToken = default);

    Task SaveActionAsync(PiAction action, Guid personId, CancellationToken cancellationToken = default);

    Task SaveOutcomeAsync(PiOutcome outcome, Guid personId, CancellationToken cancellationToken = default);

    Task SaveReflectionAsync(PiReflection reflection, Guid personId, CancellationToken cancellationToken = default);

    Task SaveExperienceAsync(ExperienceCandidate experience, Guid personId, CancellationToken cancellationToken = default);

    Task SavePolicyAsync(PolicyProposal policy, Guid personId, CancellationToken cancellationToken = default);

    Task<PiDecision?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PiEventRecord>> ListEventsAsync(Guid personId, CancellationToken cancellationToken = default);
}
