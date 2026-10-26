-- KitPlatform Migration 396: Personal Intelligence exchange connection audit
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt only.
-- Do not add this file to migration-files.prod.txt.
-- kit_audit.activity_log is not present on the isolated validation database,
-- and migration 076 also creates unrelated workspace objects. This table is the
-- narrow substitute from the Phase 1 design: actor, connection, operation,
-- result, and time. It has no secret, ciphertext, signature, or balance column.

-- One transaction so a failed guard aborts the CREATE even when the client
-- does not set ON_ERROR_STOP. The allowlist is explicit. kit_pi_validation is
-- the isolated database. A production database name is added to this list only
-- after the owner records that approval. A name seen in a preflight report is
-- not approval. Do not execute this file against production from prep work.
BEGIN;

DO $$
BEGIN
    IF current_database() NOT IN ('kit_pi_validation') THEN
        RAISE EXCEPTION '396_pack_personal_intelligence_exchange_connection_audit_v1 refuses database %', current_database();
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_connection_audit (
    audit_id        UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    actor_user_id   UUID NOT NULL REFERENCES public.users(id),
    connection_id   UUID,
    operation       VARCHAR(32) NOT NULL,
    result          VARCHAR(16) NOT NULL,
    error_code      VARCHAR(64),
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pi_exchange_audit_operation CHECK (
        operation IN ('test', 'save', 'refresh', 'disconnect', 'read', 'list')
    ),
    CONSTRAINT ck_pi_exchange_audit_result CHECK (result IN ('ok', 'rejected')),
    CONSTRAINT ck_pi_exchange_audit_code CHECK (
        error_code IS NULL OR error_code ~ '^[a-z0-9_]{1,64}$'
    )
);

CREATE INDEX IF NOT EXISTS ix_pi_exchange_audit_tenant_time
    ON pack_personal_intelligence.exchange_connection_audit (tenant_id, occurred_at DESC);

COMMENT ON TABLE pack_personal_intelligence.exchange_connection_audit IS
    'Connection operations only. No API key, secret, passphrase, ciphertext, signature, or account balance.';

COMMIT;
