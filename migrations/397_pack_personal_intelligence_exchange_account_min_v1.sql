-- KitPlatform Migration 397: narrow Account API schema.
-- Same person row as migration 385: one person per user per tenant, referencing
-- public.users and public.tenants. This file does not create a second identity
-- and does not create the trading ledger, trade, trading_account, or exchange venue.
-- Not in migration-files.prod.txt. Not in migration-files.personal-intelligence.txt.
-- Apply this file only by an explicit command after the owner adds the approved
-- database name to the allowlist below. A preflight report is not that approval.
--
-- Requires kit_uuid_v7() and kit_bump_row_version() from the platform foundation.
-- The backfill job is stored so save and disconnect have a row to write. Binance
-- history stays blocked in the adapter. trading_account_id stays null and has no
-- foreign key until the ledger table exists.

BEGIN;

DO $$
BEGIN
    IF current_database() NOT IN ('kit_pi_validation') THEN
        RAISE EXCEPTION '397_pack_personal_intelligence_exchange_account_min_v1 refuses database %', current_database();
    END IF;
END $$;

CREATE SCHEMA IF NOT EXISTS pack_personal_intelligence;

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.person (
    person_id       UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    user_id         UUID NOT NULL REFERENCES public.users(id),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    display_name    VARCHAR(200),
    timezone        VARCHAR(64) NOT NULL DEFAULT 'UTC',
    settings        JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version     INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      UUID,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by      UUID,
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_person_user_tenant UNIQUE (user_id, tenant_id),
    CONSTRAINT uq_person_id_tenant UNIQUE (person_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_person_user_tenant ON pack_personal_intelligence.person (user_id, tenant_id);
CREATE INDEX IF NOT EXISTS ix_person_tenant ON pack_personal_intelligence.person (tenant_id);

DROP TRIGGER IF EXISTS trg_person_row_version ON pack_personal_intelligence.person;
CREATE TRIGGER trg_person_row_version
    BEFORE UPDATE ON pack_personal_intelligence.person
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

COMMENT ON TABLE pack_personal_intelligence.person IS
    'Personal Intelligence identity anchor — references Core Identity (public.users, public.tenants). One Person per User per Tenant.';

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_connection (
    connection_id           UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    person_id               UUID NOT NULL,
    exchange_code           VARCHAR(32) NOT NULL,
    environment             VARCHAR(16) NOT NULL,
    status                  VARCHAR(20) NOT NULL,
    api_key_mask            VARCHAR(16) NOT NULL,
    api_key_fingerprint     CHAR(64) NOT NULL,
    key_id                  VARCHAR(64),
    nonce                   BYTEA,
    ciphertext              BYTEA,
    tag                     BYTEA,
    read_only_verified      BOOLEAN NOT NULL DEFAULT FALSE,
    permission_names        JSONB NOT NULL DEFAULT '[]'::jsonb,
    external_user_id        VARCHAR(64),
    margin_mode             VARCHAR(64),
    unified_margin_status   INTEGER,
    uta                     INTEGER,
    last_checked_at         TIMESTAMPTZ,
    last_error              TEXT,
    last_probe_status       VARCHAR(32) NOT NULL DEFAULT 'NOT_RUN',
    disconnected_at         TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_exchange_connection_scope UNIQUE (tenant_id, person_id, exchange_code, environment),
    CONSTRAINT uq_exchange_connection_id_tenant UNIQUE (connection_id, tenant_id),
    CONSTRAINT fk_exchange_connection_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT ck_exchange_connection_status CHECK (status IN ('CONNECTED', 'DISCONNECTED')),
    CONSTRAINT ck_exchange_connection_environment CHECK (environment IN ('MAINNET', 'TESTNET')),
    CONSTRAINT ck_exchange_connection_probe CHECK (last_probe_status IN ('NOT_RUN', 'OK', 'BALANCE_UNAVAILABLE', 'REJECTED')),
    CONSTRAINT ck_exchange_connection_secret CHECK (
        (status = 'CONNECTED' AND nonce IS NOT NULL AND ciphertext IS NOT NULL AND tag IS NOT NULL AND key_id IS NOT NULL)
        OR (status = 'DISCONNECTED' AND nonce IS NULL AND ciphertext IS NULL AND tag IS NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_exchange_connection_tenant_person
    ON pack_personal_intelligence.exchange_connection (tenant_id, person_id, status);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_account_snapshot (
    snapshot_id             UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    connection_id           UUID NOT NULL,
    tenant_id               UUID NOT NULL,
    captured_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source_endpoints        TEXT NOT NULL,
    account_type            TEXT,
    margin_mode             TEXT,
    unified_margin_status   INTEGER,
    total_equity            TEXT,
    total_wallet_balance    TEXT,
    total_available_balance TEXT,
    balance_error           TEXT,
    CONSTRAINT uq_exchange_snapshot_id_tenant UNIQUE (snapshot_id, tenant_id),
    CONSTRAINT fk_exchange_snapshot_connection_tenant
        FOREIGN KEY (connection_id, tenant_id)
        REFERENCES pack_personal_intelligence.exchange_connection (connection_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_exchange_snapshot_connection_time
    ON pack_personal_intelligence.exchange_account_snapshot (tenant_id, connection_id, captured_at DESC);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_asset_balance (
    balance_id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    snapshot_id             UUID NOT NULL,
    tenant_id               UUID NOT NULL,
    coin                    VARCHAR(32) NOT NULL,
    wallet_balance          TEXT,
    equity                  TEXT,
    usd_value               TEXT,
    available_to_withdraw   TEXT,
    CONSTRAINT fk_exchange_asset_snapshot_tenant
        FOREIGN KEY (snapshot_id, tenant_id)
        REFERENCES pack_personal_intelligence.exchange_account_snapshot (snapshot_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_exchange_asset_snapshot
    ON pack_personal_intelligence.exchange_asset_balance (tenant_id, snapshot_id, coin);

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_backfill_job (
    job_id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    connection_id       UUID NOT NULL,
    person_id           UUID NOT NULL,
    trading_account_id  UUID,
    exchange_code       VARCHAR(32) NOT NULL,
    environment         VARCHAR(16) NOT NULL,
    market_type         VARCHAR(16) NOT NULL,
    product_subtype     VARCHAR(32) NOT NULL,
    status              VARCHAR(20) NOT NULL,
    mode                VARCHAR(16) NOT NULL,
    checkpoint          JSONB NOT NULL DEFAULT '{}'::jsonb,
    history_floor       TIMESTAMPTZ,
    window_start        TIMESTAMPTZ,
    window_end          TIMESTAMPTZ,
    oldest_exec_at      TIMESTAMPTZ,
    newest_exec_at      TIMESTAMPTZ,
    pages               INT NOT NULL DEFAULT 0,
    batches             INT NOT NULL DEFAULT 0,
    fetched_count       INT NOT NULL DEFAULT 0,
    inserted_count      INT NOT NULL DEFAULT 0,
    duplicate_count     INT NOT NULL DEFAULT 0,
    skipped_count       INT NOT NULL DEFAULT 0,
    error_count         INT NOT NULL DEFAULT 0,
    attempt_count       INT NOT NULL DEFAULT 0,
    lease_until         TIMESTAMPTZ,
    next_retry_at       TIMESTAMPTZ,
    last_error          TEXT,
    cutoff_at           TIMESTAMPTZ,
    cutoff_reason       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    finished_at         TIMESTAMPTZ,
    CONSTRAINT uq_exchange_backfill_job_id_tenant UNIQUE (job_id, tenant_id),
    CONSTRAINT fk_exchange_backfill_connection_tenant
        FOREIGN KEY (connection_id, tenant_id)
        REFERENCES pack_personal_intelligence.exchange_connection (connection_id, tenant_id),
    CONSTRAINT fk_exchange_backfill_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT ck_exchange_backfill_status
        CHECK (status IN ('QUEUED', 'RUNNING', 'COMPLETED', 'INTERRUPTED', 'PARTIAL', 'FAILED', 'PAUSED')),
    CONSTRAINT ck_exchange_backfill_mode CHECK (mode IN ('FULL', 'INCREMENTAL')),
    CONSTRAINT ck_exchange_backfill_market CHECK (market_type IN ('SPOT', 'FUTURES')),
    CONSTRAINT ck_exchange_backfill_environment CHECK (environment IN ('MAINNET', 'TESTNET')),
    CONSTRAINT ck_exchange_backfill_counts CHECK (
        pages >= 0 AND batches >= 0 AND fetched_count >= 0 AND inserted_count >= 0
        AND duplicate_count >= 0 AND skipped_count >= 0 AND error_count >= 0 AND attempt_count >= 0
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_exchange_backfill_open
    ON pack_personal_intelligence.exchange_backfill_job (tenant_id, connection_id, market_type, product_subtype)
    WHERE status IN ('QUEUED', 'RUNNING', 'PAUSED', 'INTERRUPTED');

CREATE INDEX IF NOT EXISTS ix_exchange_backfill_claim
    ON pack_personal_intelligence.exchange_backfill_job (status, updated_at)
    WHERE status IN ('QUEUED', 'INTERRUPTED', 'RUNNING');

CREATE INDEX IF NOT EXISTS ix_exchange_backfill_connection
    ON pack_personal_intelligence.exchange_backfill_job (tenant_id, connection_id, updated_at DESC);

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

INSERT INTO public.permissions (id, permission_code, permission_name, module_name)
VALUES
    ('e1111111-1111-1111-1111-111111111101', 'personal_intelligence.exchange.connect', 'Quản lý kết nối API sàn', 'personal_intelligence'),
    ('e1111111-1111-1111-1111-111111111102', 'personal_intelligence.exchange.read', 'Xem tài khoản sàn đã kết nối', 'personal_intelligence')
ON CONFLICT (permission_code) DO NOTHING;

COMMIT;
