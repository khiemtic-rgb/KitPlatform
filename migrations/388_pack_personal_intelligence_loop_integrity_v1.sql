-- KitPlatform 388: integrity for the Personal Intelligence loop.
-- Forward-only. Does not rewrite 385, 386, or 387.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- pi_events stays the only intelligence event table. This file does not write platform_events.

CREATE OR REPLACE FUNCTION pack_personal_intelligence.reject_pi_event_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'pack_personal_intelligence.pi_events is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_pi_events_append_only ON pack_personal_intelligence.pi_events;
CREATE TRIGGER trg_pi_events_append_only
    BEFORE UPDATE OR DELETE ON pack_personal_intelligence.pi_events
    FOR EACH ROW EXECUTE FUNCTION pack_personal_intelligence.reject_pi_event_mutation();

ALTER TABLE pack_personal_intelligence.pi_events
    DROP CONSTRAINT IF EXISTS ck_pi_event_type;
ALTER TABLE pack_personal_intelligence.pi_events
    ADD CONSTRAINT ck_pi_event_type CHECK (event_type IN (
        'ContextObserved', 'EvidenceRecorded', 'DecisionMade', 'ActionTaken', 'OutcomeObserved',
        'ReflectionCreated', 'ExperienceDerived', 'PolicyProposed', 'PolicyApproved'));

ALTER TABLE pack_personal_intelligence.pi_outcome
    DROP CONSTRAINT IF EXISTS uq_pi_outcome_id_tenant;
ALTER TABLE pack_personal_intelligence.pi_outcome
    ADD CONSTRAINT uq_pi_outcome_id_tenant UNIQUE (outcome_id, tenant_id);

ALTER TABLE pack_personal_intelligence.pi_outcome
    DROP CONSTRAINT IF EXISTS fk_pi_outcome_person_tenant;
ALTER TABLE pack_personal_intelligence.pi_outcome
    ADD CONSTRAINT fk_pi_outcome_person_tenant
    FOREIGN KEY (person_id, tenant_id)
    REFERENCES pack_personal_intelligence.person (person_id, tenant_id);

ALTER TABLE pack_personal_intelligence.pi_reflection
    DROP CONSTRAINT IF EXISTS fk_pi_reflection_outcome;
ALTER TABLE pack_personal_intelligence.pi_reflection
    DROP CONSTRAINT IF EXISTS fk_pi_reflection_outcome_tenant;
ALTER TABLE pack_personal_intelligence.pi_reflection
    ADD CONSTRAINT fk_pi_reflection_outcome_tenant
    FOREIGN KEY (outcome_id, tenant_id)
    REFERENCES pack_personal_intelligence.pi_outcome (outcome_id, tenant_id);

ALTER TABLE pack_personal_intelligence.trading_order
    DROP CONSTRAINT IF EXISTS uq_trading_order_id_tenant;
ALTER TABLE pack_personal_intelligence.trading_order
    ADD CONSTRAINT uq_trading_order_id_tenant UNIQUE (order_id, tenant_id);

ALTER TABLE pack_personal_intelligence.trading_execution
    DROP CONSTRAINT IF EXISTS fk_trading_execution_order;
ALTER TABLE pack_personal_intelligence.trading_execution
    DROP CONSTRAINT IF EXISTS fk_trading_execution_order_tenant;
ALTER TABLE pack_personal_intelligence.trading_execution
    ADD CONSTRAINT fk_trading_execution_order_tenant
    FOREIGN KEY (order_id, tenant_id)
    REFERENCES pack_personal_intelligence.trading_order (order_id, tenant_id);
