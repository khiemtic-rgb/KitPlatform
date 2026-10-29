-- KitPlatform 387: Personal Intelligence canonical loop + Trading adapter extensions
-- Does not alter migration 385 destructively.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- SB-004: pack_personal_intelligence.pi_events is the intelligence event source of truth.
-- This migration does not write platform_events.

CREATE SCHEMA IF NOT EXISTS pack_personal_intelligence;

-- =============================================================================
-- pi_events — append-only intelligence lifecycle
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_events (
    event_id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    person_id           UUID NOT NULL,
    event_type          VARCHAR(64) NOT NULL,
    occurred_at         TIMESTAMPTZ NOT NULL,
    recorded_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    actor_type          VARCHAR(32) NOT NULL,
    actor_id            UUID,
    correlation_id      UUID NOT NULL,
    causation_id        UUID,
    source              VARCHAR(200) NOT NULL,
    epistemic_kind      VARCHAR(32) NOT NULL,
    external_event_id   VARCHAR(200),
    payload             JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT fk_pi_event_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT ck_pi_event_type CHECK (event_type IN (
        'ContextObserved', 'DecisionMade', 'ActionTaken', 'OutcomeObserved',
        'ReflectionCreated', 'ExperienceDerived', 'PolicyProposed', 'PolicyApproved')),
    CONSTRAINT ck_pi_event_actor CHECK (actor_type IN ('HUMAN', 'SYSTEM', 'AGENT', 'EXTERNAL_SYSTEM', 'IMPORT')),
    CONSTRAINT ck_pi_event_epistemic CHECK (epistemic_kind IN (
        'FACT', 'OBSERVATION', 'INFERENCE', 'HYPOTHESIS', 'POLICY', 'PREFERENCE'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_pi_event_external
    ON pack_personal_intelligence.pi_events (tenant_id, source, external_event_id)
    WHERE external_event_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_pi_event_person_occurred
    ON pack_personal_intelligence.pi_events (person_id, tenant_id, occurred_at);

COMMENT ON TABLE pack_personal_intelligence.pi_events IS
    'Append-only Personal Intelligence lifecycle. Not an audit log, not platform_events, not RLS-protected in V1.';

-- =============================================================================
-- Canonical loop. Trading columns stay off these tables.
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_context (
    context_id      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    observed_at     TIMESTAMPTZ NOT NULL,
    summary         TEXT NOT NULL,
    epistemic_kind  VARCHAR(32) NOT NULL,
    source          VARCHAR(200) NOT NULL,
    CONSTRAINT fk_pi_context_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT uq_pi_context_id_tenant UNIQUE (context_id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_evidence (
    evidence_id     UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    context_id      UUID,
    source_ref      VARCHAR(200) NOT NULL,
    epistemic_kind  VARCHAR(32) NOT NULL,
    payload         JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT fk_pi_evidence_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_pi_evidence_context_tenant
        FOREIGN KEY (context_id, tenant_id)
        REFERENCES pack_personal_intelligence.pi_context (context_id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_decision (
    decision_id         UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    person_id           UUID NOT NULL,
    context_id          UUID,
    thesis              TEXT NOT NULL,
    confidence          NUMERIC(5, 4) NOT NULL,
    expected_outcome    TEXT,
    invalidation        TEXT,
    epistemic_kind      VARCHAR(32) NOT NULL,
    status              VARCHAR(32) NOT NULL DEFAULT 'proposed',
    CONSTRAINT fk_pi_decision_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_pi_decision_context_tenant
        FOREIGN KEY (context_id, tenant_id)
        REFERENCES pack_personal_intelligence.pi_context (context_id, tenant_id),
    CONSTRAINT uq_pi_decision_id_tenant UNIQUE (decision_id, tenant_id),
    CONSTRAINT ck_pi_decision_confidence CHECK (confidence >= 0 AND confidence <= 1),
    CONSTRAINT ck_pi_decision_status CHECK (status IN ('proposed', 'accepted', 'rejected', 'superseded'))
);

COMMENT ON TABLE pack_personal_intelligence.pi_decision IS
    'A decision is not a trade. Observed trades stay in pack_personal_intelligence.trade.';

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_action (
    action_id       UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    decision_id     UUID NOT NULL,
    intended        TEXT NOT NULL,
    actual          TEXT,
    status          VARCHAR(32) NOT NULL,
    CONSTRAINT fk_pi_action_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_pi_action_decision_tenant
        FOREIGN KEY (decision_id, tenant_id)
        REFERENCES pack_personal_intelligence.pi_decision (decision_id, tenant_id),
    CONSTRAINT ck_pi_action_status CHECK (status IN ('intended', 'paper_filled', 'refused', 'observed'))
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_outcome (
    outcome_id      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    decision_id     UUID NOT NULL,
    summary         TEXT NOT NULL,
    epistemic_kind  VARCHAR(32) NOT NULL,
    realized_pnl    NUMERIC(24, 8),
    CONSTRAINT fk_pi_outcome_decision_tenant
        FOREIGN KEY (decision_id, tenant_id)
        REFERENCES pack_personal_intelligence.pi_decision (decision_id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_reflection (
    reflection_id   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    outcome_id      UUID NOT NULL,
    note            TEXT NOT NULL,
    epistemic_kind  VARCHAR(32) NOT NULL,
    CONSTRAINT fk_pi_reflection_outcome
        FOREIGN KEY (outcome_id)
        REFERENCES pack_personal_intelligence.pi_outcome (outcome_id)
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_experience (
    experience_id   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    reflection_id   UUID,
    statement       TEXT NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'candidate',
    epistemic_kind  VARCHAR(32) NOT NULL,
    CONSTRAINT ck_pi_experience_status CHECK (status IN ('candidate', 'retained', 'rejected'))
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_policy (
    policy_id       UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    rule_text       TEXT NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'proposed',
    epistemic_kind  VARCHAR(32) NOT NULL DEFAULT 'POLICY',
    CONSTRAINT ck_pi_policy_status CHECK (status IN ('proposed', 'approved', 'retired'))
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_knowledge (
    knowledge_id    UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    statement       TEXT NOT NULL,
    epistemic_kind  VARCHAR(32) NOT NULL
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_memory (
    memory_id       UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    experience_id   UUID,
    note            TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_evaluation (
    evaluation_id   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    basis           VARCHAR(32) NOT NULL,
    summary         TEXT NOT NULL,
    CONSTRAINT ck_pi_evaluation_basis CHECK (basis IN ('FIXTURE', 'LIVE_OBSERVED'))
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_agent (
    agent_id        UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    agent_code      VARCHAR(64) NOT NULL,
    autonomy        VARCHAR(32) NOT NULL DEFAULT 'recommend_only',
    CONSTRAINT uq_pi_agent_code UNIQUE (tenant_id, person_id, agent_code),
    CONSTRAINT ck_pi_agent_autonomy CHECK (autonomy IN ('recommend_only'))
);

-- =============================================================================
-- Trading adapter extensions. Orders are not decisions.
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.trading_order (
    order_id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    person_id               UUID NOT NULL,
    trading_account_id      UUID NOT NULL,
    external_order_id       VARCHAR(200) NOT NULL,
    symbol                  VARCHAR(64) NOT NULL,
    side                    VARCHAR(10) NOT NULL,
    quantity                NUMERIC(24, 12) NOT NULL,
    limit_price             NUMERIC(24, 12),
    status                  VARCHAR(32) NOT NULL,
    source                  VARCHAR(200) NOT NULL,
    submitted_at            TIMESTAMPTZ NOT NULL,
    decision_id             UUID,
    CONSTRAINT uq_trading_order_external UNIQUE (trading_account_id, external_order_id),
    CONSTRAINT fk_trading_order_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id),
    CONSTRAINT fk_trading_order_decision_tenant
        FOREIGN KEY (decision_id, tenant_id)
        REFERENCES pack_personal_intelligence.pi_decision (decision_id, tenant_id),
    CONSTRAINT ck_trading_order_side CHECK (side IN ('BUY', 'SELL')),
    CONSTRAINT ck_trading_order_status CHECK (status IN ('proposed', 'paper', 'rejected', 'observed'))
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.trading_execution (
    execution_id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    trading_account_id      UUID NOT NULL,
    order_id                UUID,
    trade_id                UUID,
    external_execution_id   VARCHAR(200) NOT NULL,
    mode                    VARCHAR(16) NOT NULL,
    executed_at             TIMESTAMPTZ NOT NULL,
    CONSTRAINT uq_trading_execution_external UNIQUE (trading_account_id, external_execution_id),
    CONSTRAINT fk_trading_execution_order
        FOREIGN KEY (order_id)
        REFERENCES pack_personal_intelligence.trading_order (order_id),
    CONSTRAINT ck_trading_execution_mode CHECK (mode IN ('PAPER', 'OBSERVED'))
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.market_snapshot (
    snapshot_id     UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    person_id       UUID NOT NULL,
    symbol          VARCHAR(64) NOT NULL,
    observed_at     TIMESTAMPTZ NOT NULL,
    price           NUMERIC(24, 12) NOT NULL,
    source          VARCHAR(200) NOT NULL,
    epistemic_kind  VARCHAR(32) NOT NULL DEFAULT 'OBSERVATION'
);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.funding_payment (
    funding_id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    trading_account_id      UUID NOT NULL,
    symbol                  VARCHAR(64) NOT NULL,
    amount                  NUMERIC(24, 8) NOT NULL,
    currency                VARCHAR(32) NOT NULL,
    funding_time            TIMESTAMPTZ NOT NULL,
    external_funding_id     VARCHAR(200) NOT NULL,
    source                  VARCHAR(200) NOT NULL,
    CONSTRAINT uq_funding_external UNIQUE (trading_account_id, external_funding_id),
    CONSTRAINT fk_funding_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id)
);
