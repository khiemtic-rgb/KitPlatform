-- KitPlatform 393: Personal Intelligence — multi-exchange market split and durable backfill.
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt
-- Forward-only. Does not rewrite 385–392.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- Existing Bybit rows stay. market_type is filled only when the external id already says spot, linear, or inverse.

ALTER TABLE pack_personal_intelligence.trade
    ADD COLUMN IF NOT EXISTS market_type VARCHAR(16),
    ADD COLUMN IF NOT EXISTS product_subtype VARCHAR(32),
    ADD COLUMN IF NOT EXISTS exchange_code VARCHAR(32);

ALTER TABLE pack_personal_intelligence.trade
    DROP CONSTRAINT IF EXISTS ck_trade_market_type;

ALTER TABLE pack_personal_intelligence.trade
    ADD CONSTRAINT ck_trade_market_type
    CHECK (market_type IS NULL OR market_type IN ('SPOT', 'FUTURES'));

UPDATE pack_personal_intelligence.trade
SET exchange_code = 'BYBIT',
    product_subtype = split_part(external_trade_id, ':', 3),
    market_type = CASE split_part(external_trade_id, ':', 3)
        WHEN 'spot' THEN 'SPOT'
        WHEN 'linear' THEN 'FUTURES'
        WHEN 'inverse' THEN 'FUTURES'
        ELSE NULL
    END
WHERE external_trade_id LIKE 'BYBIT:%:%:%'
  AND market_type IS NULL
  AND split_part(external_trade_id, ':', 3) IN ('spot', 'linear', 'inverse', 'option');

CREATE INDEX IF NOT EXISTS ix_trade_market_executed
    ON pack_personal_intelligence.trade (tenant_id, market_type, product_subtype, executed_at DESC);

COMMENT ON COLUMN pack_personal_intelligence.trade.market_type IS
    'SPOT or FUTURES. NULL means the row was not classified, including Bybit option and CSV rows.';

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
    CONSTRAINT fk_exchange_backfill_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id),
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

COMMENT ON TABLE pack_personal_intelligence.exchange_backfill_job IS
    'Durable per-stream historical fill backfill. One open job per exchange account, market type, and product subtype. Checkpoint is resumed after a restart. PARTIAL means the exchange endpoint does not provide the whole account lifetime, or symbol discovery cannot see every market. It does not place orders.';
