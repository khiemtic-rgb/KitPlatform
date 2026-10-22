namespace KitPlatform.Packs.PersonalIntelligence;

/// <summary>
/// Canonical loop. These types do not carry exchange, ticker, or order fields.
/// </summary>
public sealed record PiContext(Guid ContextId, Guid TenantId, Guid PersonId, string Summary, EpistemicKind Kind, string Source);

public sealed record PiEvidence(Guid EvidenceId, Guid TenantId, Guid PersonId, Guid? ContextId, string SourceRef, EpistemicKind Kind);

public sealed record PiDecision(
    Guid DecisionId,
    Guid TenantId,
    Guid PersonId,
    Guid? ContextId,
    string Thesis,
    decimal Confidence,
    string? ExpectedOutcome,
    string? Invalidation,
    EpistemicKind Kind);

public sealed record PiAction(Guid ActionId, Guid TenantId, Guid DecisionId, string Intended, string? Actual, string Status);

public sealed record PiOutcome(Guid OutcomeId, Guid TenantId, Guid DecisionId, string Summary, EpistemicKind Kind);

public sealed record PiReflection(Guid ReflectionId, Guid TenantId, Guid OutcomeId, string Note, EpistemicKind Kind);

public sealed record ExperienceCandidate(Guid ExperienceId, Guid TenantId, string Statement, string Status, EpistemicKind Kind);

public sealed record PolicyProposal(Guid PolicyId, Guid TenantId, string RuleText, string Status);

public static class IntelligenceLoop
{
    public const string EventSink = "pack_personal_intelligence.pi_events";

    public static PiDecision Decide(
        Guid tenantId,
        Guid personId,
        Guid? contextId,
        string thesis,
        decimal confidence,
        string? expectedOutcome,
        string? invalidation,
        EpistemicKind kind)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A decision requires a tenant.");
        if (string.IsNullOrWhiteSpace(thesis))
            throw new InvalidOperationException("A decision requires a thesis.");
        if (confidence is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        return new PiDecision(
            Guid.NewGuid(),
            tenantId,
            personId,
            contextId,
            thesis.Trim(),
            confidence,
            expectedOutcome,
            invalidation,
            EpistemicGuard.AsAgentClaim(kind));
    }

    public static PolicyProposal ProposePolicyFromSingleOutcome(Guid tenantId, string ruleText) =>
        new(Guid.NewGuid(), tenantId, ruleText.Trim(), Status: "proposed");

    public static PiEventDraft RecordEvent(
        Guid tenantId,
        Guid personId,
        string eventType,
        EpistemicKind kind,
        string source,
        Guid correlationId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("An intelligence event requires a tenant.");

        return new PiEventDraft(
            Guid.NewGuid(),
            tenantId,
            personId,
            eventType,
            DateTimeOffset.UtcNow,
            kind,
            source,
            correlationId,
            EventSink);
    }
}

public sealed record PiEventDraft(
    Guid EventId,
    Guid TenantId,
    Guid PersonId,
    string EventType,
    DateTimeOffset OccurredAt,
    EpistemicKind Kind,
    string Source,
    Guid CorrelationId,
    string Sink);
