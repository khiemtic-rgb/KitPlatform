namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;

internal static class IntelligenceSql
{
    public const string EventSink = "pack_personal_intelligence.pi_events";

    public const string InsertEvent = """
        INSERT INTO pack_personal_intelligence.pi_events (
            event_id, tenant_id, person_id, event_type, occurred_at, actor_type,
            correlation_id, source, epistemic_kind, external_event_id, payload
        )
        VALUES (
            @EventId, @TenantId, @PersonId, @EventType, @OccurredAt, @ActorType,
            @CorrelationId, @Source, @EpistemicKind, @ExternalEventId, '{}'::jsonb
        )
        ON CONFLICT (tenant_id, source, external_event_id) WHERE external_event_id IS NOT NULL
        DO NOTHING
        """;

    public const string InsertContext = """
        INSERT INTO pack_personal_intelligence.pi_context (
            context_id, tenant_id, person_id, observed_at, summary, epistemic_kind, source
        )
        VALUES (
            @ContextId, @TenantId, @PersonId, NOW(), @Summary, @EpistemicKind, @Source
        )
        ON CONFLICT (context_id) DO NOTHING
        """;

    public const string InsertEvidence = """
        INSERT INTO pack_personal_intelligence.pi_evidence (
            evidence_id, tenant_id, person_id, context_id, source_ref, epistemic_kind, payload
        )
        VALUES (
            @EvidenceId, @TenantId, @PersonId, @ContextId, @SourceRef, @EpistemicKind, '{}'::jsonb
        )
        ON CONFLICT (evidence_id) DO NOTHING
        """;

    public const string InsertDecision = """
        INSERT INTO pack_personal_intelligence.pi_decision (
            decision_id, tenant_id, person_id, context_id, thesis, confidence,
            expected_outcome, invalidation, epistemic_kind, status
        )
        VALUES (
            @DecisionId, @TenantId, @PersonId, @ContextId, @Thesis, @Confidence,
            @ExpectedOutcome, @Invalidation, @EpistemicKind, 'proposed'
        )
        ON CONFLICT (decision_id) DO NOTHING
        """;

    public const string InsertAction = """
        INSERT INTO pack_personal_intelligence.pi_action (
            action_id, tenant_id, person_id, decision_id, intended, actual, status
        )
        VALUES (
            @ActionId, @TenantId, @PersonId, @DecisionId, @Intended, @Actual, @Status
        )
        ON CONFLICT (action_id) DO NOTHING
        """;

    public const string InsertOutcome = """
        INSERT INTO pack_personal_intelligence.pi_outcome (
            outcome_id, tenant_id, person_id, decision_id, summary, epistemic_kind
        )
        VALUES (
            @OutcomeId, @TenantId, @PersonId, @DecisionId, @Summary, @EpistemicKind
        )
        ON CONFLICT (outcome_id) DO NOTHING
        """;

    public const string InsertReflection = """
        INSERT INTO pack_personal_intelligence.pi_reflection (
            reflection_id, tenant_id, person_id, outcome_id, note, epistemic_kind
        )
        VALUES (
            @ReflectionId, @TenantId, @PersonId, @OutcomeId, @Note, @EpistemicKind
        )
        ON CONFLICT (reflection_id) DO NOTHING
        """;

    public const string InsertExperience = """
        INSERT INTO pack_personal_intelligence.pi_experience (
            experience_id, tenant_id, person_id, statement, status, epistemic_kind
        )
        VALUES (
            @ExperienceId, @TenantId, @PersonId, @Statement, @Status, @EpistemicKind
        )
        ON CONFLICT (experience_id) DO NOTHING
        """;

    public const string InsertPolicy = """
        INSERT INTO pack_personal_intelligence.pi_policy (
            policy_id, tenant_id, person_id, rule_text, status, epistemic_kind
        )
        VALUES (
            @PolicyId, @TenantId, @PersonId, @RuleText, @Status, 'POLICY'
        )
        ON CONFLICT (policy_id) DO NOTHING
        """;

    public const string SelectDecision = """
        SELECT
            decision_id AS DecisionId,
            tenant_id AS TenantId,
            person_id AS PersonId,
            context_id AS ContextId,
            thesis AS Thesis,
            confidence AS Confidence,
            expected_outcome AS ExpectedOutcome,
            invalidation AS Invalidation,
            epistemic_kind AS EpistemicKind
        FROM pack_personal_intelligence.pi_decision
        WHERE decision_id = @DecisionId
          AND tenant_id = @TenantId
        """;

    public const string SelectEvents = """
        SELECT
            event_id AS EventId,
            tenant_id AS TenantId,
            person_id AS PersonId,
            event_type AS EventType,
            epistemic_kind AS EpistemicKind,
            source AS Source,
            correlation_id AS CorrelationId
        FROM pack_personal_intelligence.pi_events
        WHERE person_id = @PersonId
          AND tenant_id = @TenantId
        ORDER BY occurred_at, recorded_at
        """;
}
