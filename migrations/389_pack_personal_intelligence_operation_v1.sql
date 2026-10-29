-- KitPlatform 389: tenant-scoped receipt for one Trading loop operation.
-- Forward-only. Does not rewrite 385, 386, 387, or 388.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- The receipt is not an event. pi_events stays the intelligence event table.
-- This file does not write platform_events.

CREATE TABLE pack_personal_intelligence.pi_operation (
    operation_id    UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    operation_key   TEXT NOT NULL,
    payload_hash    TEXT NOT NULL,
    decision_id     UUID,
    result          JSONB NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pi_operation_tenant_key UNIQUE (tenant_id, operation_key)
);

COMMENT ON TABLE pack_personal_intelligence.pi_operation IS
    'One Trading loop operation per tenant and operation key. A retry returns the stored result. A different payload for the same key is a conflict.';
