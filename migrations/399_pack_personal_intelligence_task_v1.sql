-- KitPlatform 399: Personal Intelligence — durable task for work that waits on approval.
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt
-- Forward-only. Does not rewrite 385–393.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- Does not grant permissions, seed rows, or change pi_decision / pi_operation / trading_execution.
-- Task status blocked is a human rejection of the linked decision.
-- Unverified is a read that does not prove completed or blocked. It is not a rejection.

CREATE TABLE pack_personal_intelligence.pi_task (
    task_id                 UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    task_key                TEXT NOT NULL,
    payload_hash            TEXT NOT NULL,
    status                  VARCHAR(32) NOT NULL,
    goal                    TEXT NOT NULL,
    completion_conditions   JSONB NOT NULL,
    plan_snapshot           JSONB NOT NULL,
    evidence                JSONB NOT NULL,
    evidence_sha256         TEXT NOT NULL,
    decision_id             UUID NOT NULL,
    operation_key           TEXT NOT NULL,
    last_verification       JSONB,
    verification_note       TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pi_task_id_tenant UNIQUE (task_id, tenant_id),
    CONSTRAINT uq_pi_task_tenant_key UNIQUE (tenant_id, task_key),
    CONSTRAINT fk_pi_task_decision_tenant
        FOREIGN KEY (decision_id, tenant_id)
        REFERENCES pack_personal_intelligence.pi_decision (decision_id, tenant_id),
    CONSTRAINT fk_pi_task_operation_tenant
        FOREIGN KEY (tenant_id, operation_key)
        REFERENCES pack_personal_intelligence.pi_operation (tenant_id, operation_key),
    CONSTRAINT ck_pi_task_status CHECK (
        status IN ('waiting_approval', 'completed', 'blocked', 'unverified')
    )
);

CREATE INDEX ix_pi_task_decision
    ON pack_personal_intelligence.pi_task (tenant_id, decision_id);

COMMENT ON TABLE pack_personal_intelligence.pi_task IS
    'One Second Brain task per tenant and task key. task_key equals the paper operation_key. payload_hash equals that operation payload_hash. A retry with the same hash continues this row. A different hash conflicts. completed and blocked are set only after re-reading pi_decision and PAPER executions.';
