-- KitPlatform 394: receipt for the independent paper-trading host.
-- Forward-only. Does not alter migrations 385 through 393.
-- This is not a trading_order, not an exchange execution, and not an observed trade.
-- No foreign key to public.tenants: the host must not invent a platform tenant.
-- Do not add this file to migration-files.prod.txt.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.

DO $$
BEGIN
    IF current_database() IN ('novixa_prod', 'trading_research') THEN
        RAISE EXCEPTION '394_pack_personal_intelligence_paper_host_receipt_v1 must not run on %', current_database();
    END IF;
END $$;

CREATE SCHEMA IF NOT EXISTS pack_personal_intelligence;

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.paper_host_receipt (
    operation_key   TEXT PRIMARY KEY,
    payload_hash    TEXT NOT NULL,
    status          TEXT NOT NULL,
    result          TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_paper_host_receipt_status CHECK (
        status IN ('paper_filled', 'risk_refused', 'session_notional_refused'))
);

COMMENT ON TABLE pack_personal_intelligence.paper_host_receipt IS
    'Idempotency receipt for Paper Trading Host V1. A repeated operation key does not insert a second fill. This table is not an exchange order.';
