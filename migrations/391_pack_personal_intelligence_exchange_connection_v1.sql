-- KitPlatform 391: Personal Intelligence — read-only exchange connection.
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt
-- Forward-only. Does not rewrite 385–390.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.
-- Ciphertext is AES-256-GCM. The encryption key is not stored in this database.

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

COMMENT ON TABLE pack_personal_intelligence.exchange_connection IS
    'Read-only exchange API credential for one person. API secret is AES-256-GCM ciphertext. The key lives outside the database. Disconnect nulls the ciphertext and keeps the row.';

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

COMMENT ON TABLE pack_personal_intelligence.exchange_account_snapshot IS
    'Append-only account read from the exchange. Missing balance fields stay null. A later read inserts a new row.';

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

COMMENT ON TABLE pack_personal_intelligence.exchange_asset_balance IS
    'Coin balances copied from one exchange snapshot. Values are the exchange strings. Null means the exchange did not provide the field.';

INSERT INTO public.permissions (id, permission_code, permission_name, module_name)
VALUES
    ('e1111111-1111-1111-1111-111111111101', 'personal_intelligence.exchange.connect', 'Quản lý kết nối API sàn', 'personal_intelligence'),
    ('e1111111-1111-1111-1111-111111111102', 'personal_intelligence.exchange.read', 'Xem tài khoản sàn đã kết nối', 'personal_intelligence')
ON CONFLICT (permission_code) DO NOTHING;
