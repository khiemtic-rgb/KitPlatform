-- KitPlatform 399: tenant-scoped receipt for one Agent Runtime task.
-- Forward-only. Does not alter migrations 385 through 398.
-- Does not add a tenant column to research_trading and does not create a table on trading_agent.
-- Not in migration-files.prod.txt.
-- Not in migration-files.personal-intelligence.txt.
-- Not in migration-files.trading-research.txt.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- Requires public.tenants(id) and kit_uuid_v7() from the platform foundation.
-- The receipt JSON is the task. pi_operation remains the trading-loop receipt.

DO $$
BEGIN
    IF current_database() IN ('novixa_prod', 'trading_research', 'trading_agent', 'kitplatform', 'postgres') THEN
        RAISE EXCEPTION '399_pack_personal_intelligence_agent_task_v1 must not run on %', current_database();
    END IF;
END $$;

CREATE SCHEMA IF NOT EXISTS pack_personal_intelligence;

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.pi_agent_task (
    task_id         UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    task_key        TEXT NOT NULL,
    payload_hash    TEXT NOT NULL,
    status          TEXT NOT NULL,
    receipt         JSONB NOT NULL,
    revision        INT NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pi_agent_task_tenant_key UNIQUE (tenant_id, task_key),
    CONSTRAINT ck_pi_agent_task_status CHECK (status IN (
        'Received', 'Planned', 'Running', 'AwaitingApproval',
        'Verified', 'Completed', 'Failed', 'Cancelled')),
    CONSTRAINT ck_pi_agent_task_revision CHECK (revision >= 1)
);

COMMENT ON TABLE pack_personal_intelligence.pi_agent_task IS
    'One Agent Runtime task per tenant and task key. A retry with the same payload returns the stored receipt. A different payload for the same key is a conflict. revision stops a stale save from replacing a newer receipt.';
