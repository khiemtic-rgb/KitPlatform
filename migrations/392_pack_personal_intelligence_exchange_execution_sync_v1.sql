-- KitPlatform 392: Personal Intelligence — Bybit execution history provenance.
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt
-- Forward-only. Does not rewrite 385–391.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
--
-- trade.source is free text. The application previously stored only FIXTURE or IMPORT.
-- trade.source_type did not allow an exchange value, so a Bybit fill could not be
-- distinguished from a CSV import. EXCHANGE is the source for GET /v5/execution/list.
-- Funding and other non-fill execution types are not inserted into trade or fee.

ALTER TABLE pack_personal_intelligence.trade
    DROP CONSTRAINT IF EXISTS ck_trade_source_type;

ALTER TABLE pack_personal_intelligence.trade
    ADD CONSTRAINT ck_trade_source_type
    CHECK (source_type IN ('IMPORT', 'MANUAL', 'DERIVED', 'CORRECTED', 'EXCHANGE'));

COMMENT ON CONSTRAINT ck_trade_source_type ON pack_personal_intelligence.trade IS
    'EXCHANGE marks a fill synced from an exchange execution API. It is not FIXTURE and it is not a CSV IMPORT.';

CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange_execution_sync (
    sync_id                 UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    connection_id           UUID NOT NULL,
    person_id               UUID NOT NULL,
    trading_account_id      UUID,
    exchange_code           VARCHAR(32) NOT NULL,
    environment             VARCHAR(16) NOT NULL,
    category                VARCHAR(16) NOT NULL,
    requested_start         TIMESTAMPTZ NOT NULL,
    requested_end           TIMESTAMPTZ NOT NULL,
    earliest_exec_at        TIMESTAMPTZ,
    latest_exec_at          TIMESTAMPTZ,
    pages                   INT NOT NULL,
    page_limit              INT NOT NULL,
    fetched_count           INT NOT NULL,
    inserted_count          INT NOT NULL,
    duplicate_count         INT NOT NULL,
    unmapped_count          INT NOT NULL,
    truncated               BOOLEAN NOT NULL,
    report                  JSONB NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID,
    CONSTRAINT uq_exchange_execution_sync_id_tenant UNIQUE (sync_id, tenant_id),
    CONSTRAINT fk_exchange_execution_sync_connection_tenant
        FOREIGN KEY (connection_id, tenant_id)
        REFERENCES pack_personal_intelligence.exchange_connection (connection_id, tenant_id),
    CONSTRAINT fk_exchange_execution_sync_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_exchange_execution_sync_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id),
    CONSTRAINT ck_exchange_execution_sync_environment CHECK (environment IN ('MAINNET', 'TESTNET')),
    CONSTRAINT ck_exchange_execution_sync_category CHECK (category IN ('spot', 'linear', 'inverse', 'option')),
    CONSTRAINT ck_exchange_execution_sync_counts CHECK (
        pages >= 0 AND page_limit > 0 AND fetched_count >= 0
        AND inserted_count >= 0 AND duplicate_count >= 0 AND unmapped_count >= 0
    )
);

CREATE INDEX IF NOT EXISTS ix_exchange_execution_sync_connection
    ON pack_personal_intelligence.exchange_execution_sync (tenant_id, connection_id, created_at DESC);

COMMENT ON TABLE pack_personal_intelligence.exchange_execution_sync IS
    'One committed read of GET /v5/execution/list. Preview is not stored. A later sync inserts another row and does not update trades already stored. report holds unmapped executions, missing fields, and limits. This table does not mean the whole account history was reconstructed.';
