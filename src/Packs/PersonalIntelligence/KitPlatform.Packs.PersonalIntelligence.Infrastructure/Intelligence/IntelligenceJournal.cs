using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;

internal sealed class IntelligenceJournal : IIntelligenceJournal
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public IntelligenceJournal(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Task SaveContextAsync(PiContext context, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            context.TenantId,
            context.PersonId,
            "ContextObserved",
            context.Kind,
            context.Source,
            context.ContextId,
            IntelligenceSql.InsertContext,
            new
            {
                context.ContextId,
                TenantId = _tenant.TenantId,
                context.PersonId,
                context.Summary,
                EpistemicKind = EpistemicKindSql.ToDb(context.Kind),
                context.Source,
            },
            cancellationToken);

    public Task SaveEvidenceAsync(PiEvidence evidence, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            evidence.TenantId,
            evidence.PersonId,
            "EvidenceRecorded",
            evidence.Kind,
            evidence.SourceRef,
            evidence.EvidenceId,
            IntelligenceSql.InsertEvidence,
            new
            {
                evidence.EvidenceId,
                TenantId = _tenant.TenantId,
                evidence.PersonId,
                evidence.ContextId,
                evidence.SourceRef,
                EpistemicKind = EpistemicKindSql.ToDb(evidence.Kind),
            },
            cancellationToken);

    public Task SaveDecisionAsync(PiDecision decision, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            decision.TenantId,
            decision.PersonId,
            "DecisionMade",
            decision.Kind,
            "journal",
            decision.DecisionId,
            IntelligenceSql.InsertDecision,
            new
            {
                decision.DecisionId,
                TenantId = _tenant.TenantId,
                decision.PersonId,
                decision.ContextId,
                decision.Thesis,
                decision.Confidence,
                decision.ExpectedOutcome,
                decision.Invalidation,
                EpistemicKind = EpistemicKindSql.ToDb(decision.Kind),
            },
            cancellationToken);

    internal Task ProbeSecondWriteFailureAsync(PiDecision decision, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            decision.TenantId,
            decision.PersonId,
            "NOT_A_REAL_EVENT",
            decision.Kind,
            "journal",
            decision.DecisionId,
            IntelligenceSql.InsertDecision,
            new
            {
                decision.DecisionId,
                TenantId = _tenant.TenantId,
                decision.PersonId,
                decision.ContextId,
                decision.Thesis,
                decision.Confidence,
                decision.ExpectedOutcome,
                decision.Invalidation,
                EpistemicKind = EpistemicKindSql.ToDb(decision.Kind),
            },
            cancellationToken);

    public Task SaveActionAsync(PiAction action, Guid personId, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            action.TenantId,
            personId,
            "ActionTaken",
            EpistemicKind.Observation,
            "journal",
            action.ActionId,
            IntelligenceSql.InsertAction,
            new
            {
                action.ActionId,
                TenantId = _tenant.TenantId,
                PersonId = personId,
                action.DecisionId,
                action.Intended,
                action.Actual,
                action.Status,
            },
            cancellationToken);

    public Task SaveOutcomeAsync(PiOutcome outcome, Guid personId, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            outcome.TenantId,
            personId,
            "OutcomeObserved",
            outcome.Kind,
            "journal",
            outcome.OutcomeId,
            IntelligenceSql.InsertOutcome,
            new
            {
                outcome.OutcomeId,
                TenantId = _tenant.TenantId,
                PersonId = personId,
                outcome.DecisionId,
                outcome.Summary,
                EpistemicKind = EpistemicKindSql.ToDb(outcome.Kind),
            },
            cancellationToken);

    public Task SaveReflectionAsync(PiReflection reflection, Guid personId, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            reflection.TenantId,
            personId,
            "ReflectionCreated",
            reflection.Kind,
            "journal",
            reflection.ReflectionId,
            IntelligenceSql.InsertReflection,
            new
            {
                reflection.ReflectionId,
                TenantId = _tenant.TenantId,
                PersonId = personId,
                reflection.OutcomeId,
                reflection.Note,
                EpistemicKind = EpistemicKindSql.ToDb(reflection.Kind),
            },
            cancellationToken);

    public Task SaveExperienceAsync(ExperienceCandidate experience, Guid personId, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            experience.TenantId,
            personId,
            "ExperienceDerived",
            experience.Kind,
            "journal",
            experience.ExperienceId,
            IntelligenceSql.InsertExperience,
            new
            {
                experience.ExperienceId,
                TenantId = _tenant.TenantId,
                PersonId = personId,
                experience.Statement,
                experience.Status,
                EpistemicKind = EpistemicKindSql.ToDb(experience.Kind),
            },
            cancellationToken);

    public Task SavePolicyAsync(PolicyProposal policy, Guid personId, CancellationToken cancellationToken = default) =>
        WritePairAsync(
            policy.TenantId,
            personId,
            "PolicyProposed",
            EpistemicKind.Policy,
            "journal",
            policy.PolicyId,
            IntelligenceSql.InsertPolicy,
            new
            {
                policy.PolicyId,
                TenantId = _tenant.TenantId,
                PersonId = personId,
                policy.RuleText,
                policy.Status,
            },
            cancellationToken);

    public async Task<PiDecision?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        TenantScope.RequireCurrent(_tenant.TenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<DecisionRow>(
            IntelligenceSql.SelectDecision,
            new { DecisionId = decisionId, TenantId = _tenant.TenantId });
        return row is null
            ? null
            : new PiDecision(
                row.DecisionId,
                row.TenantId,
                row.PersonId,
                row.ContextId,
                row.Thesis,
                row.Confidence,
                row.ExpectedOutcome,
                row.Invalidation,
                EpistemicKindSql.FromDb(row.EpistemicKind));
    }

    public async Task<IReadOnlyList<PiEventRecord>> ListEventsAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        TenantScope.RequireCurrent(_tenant.TenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<EventRow>(
            IntelligenceSql.SelectEvents,
            new { PersonId = personId, TenantId = _tenant.TenantId });
        return rows.Select(row => new PiEventRecord(
            row.EventId,
            row.TenantId,
            row.PersonId,
            row.EventType,
            EpistemicKindSql.FromDb(row.EpistemicKind),
            row.Source,
            row.CorrelationId,
            IntelligenceSql.EventSink)).ToArray();
    }

    private async Task WritePairAsync(
        Guid requestedTenantId,
        Guid personId,
        string eventType,
        EpistemicKind kind,
        string source,
        Guid correlationId,
        string businessSql,
        object businessArgs,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireMatch(_tenant.TenantId, requestedTenantId);
        if (businessSql == IntelligenceSql.InsertPolicy && PolicyStatus(businessArgs) != "proposed")
            throw new InvalidOperationException("A policy created from this journal must stay proposed until a separate approval.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            await conn.ExecuteAsync(businessSql, businessArgs, tx);
            await conn.ExecuteAsync(
                IntelligenceSql.InsertEvent,
                new
                {
                    EventId = Guid.NewGuid(),
                    TenantId = _tenant.TenantId,
                    PersonId = personId,
                    EventType = eventType,
                    OccurredAt = DateTimeOffset.UtcNow,
                    ActorType = "HUMAN",
                    CorrelationId = correlationId,
                    Source = source,
                    EpistemicKind = EpistemicKindSql.ToDb(kind),
                    ExternalEventId = correlationId.ToString("N"),
                },
                tx);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? PolicyStatus(object businessArgs) =>
        businessArgs.GetType().GetProperty("Status")?.GetValue(businessArgs) as string;

    private sealed class DecisionRow
    {
        public Guid DecisionId { get; set; }
        public Guid TenantId { get; set; }
        public Guid PersonId { get; set; }
        public Guid? ContextId { get; set; }
        public string Thesis { get; set; } = "";
        public decimal Confidence { get; set; }
        public string? ExpectedOutcome { get; set; }
        public string? Invalidation { get; set; }
        public string EpistemicKind { get; set; } = "";
    }

    private sealed class EventRow
    {
        public Guid EventId { get; set; }
        public Guid TenantId { get; set; }
        public Guid PersonId { get; set; }
        public string EventType { get; set; } = "";
        public string EpistemicKind { get; set; } = "";
        public string Source { get; set; } = "";
        public Guid CorrelationId { get; set; }
    }
}
