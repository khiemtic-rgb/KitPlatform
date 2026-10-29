-- KitPlatform 385: Personal Intelligence Pack — Trading Ledger V1
-- Specification: KPA-SECOND-BRAIN-004, KPA-SECOND-BRAIN-005, KPA-SECOND-BRAIN-005A, KPA-SECOND-BRAIN-005B
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt
-- Layer: Pack:PersonalIntelligence
-- Purpose: Historical trading observation ledger — accounts, trades, positions, fees, portfolio snapshots

-- =============================================================================
-- Schema
-- =============================================================================
CREATE SCHEMA IF NOT EXISTS pack_personal_intelligence;

COMMENT ON SCHEMA pack_personal_intelligence IS
    'Personal Intelligence Pack — Knowledge, Evidence, Context, Decision, Action, Outcome, Experience (domain-independent)';

-- =============================================================================
-- Person — canonical PI identity anchor
-- =============================================================================
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

-- Row version trigger
DROP TRIGGER IF EXISTS trg_person_row_version ON pack_personal_intelligence.person;
CREATE TRIGGER trg_person_row_version
    BEFORE UPDATE ON pack_personal_intelligence.person
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

COMMENT ON TABLE pack_personal_intelligence.person IS
    'Personal Intelligence identity anchor — references Core Identity (public.users, public.tenants). One Person per User per Tenant.';

-- =============================================================================
-- Exchange — trading venue identity
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.exchange (
    exchange_id         UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    exchange_code       VARCHAR(64) NOT NULL,
    exchange_name       VARCHAR(200) NOT NULL,
    exchange_type       VARCHAR(32),
    external_provider   VARCHAR(100),
    status              VARCHAR(20) NOT NULL DEFAULT 'active',
    metadata            JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    CONSTRAINT uq_exchange_tenant_code UNIQUE (tenant_id, exchange_code),
    CONSTRAINT uq_exchange_id_tenant UNIQUE (exchange_id, tenant_id),
    CONSTRAINT ck_exchange_status CHECK (status IN ('active', 'inactive', 'deprecated'))
);

CREATE INDEX IF NOT EXISTS ix_exchange_tenant_status
    ON pack_personal_intelligence.exchange (tenant_id, status);

COMMENT ON TABLE pack_personal_intelligence.exchange IS
    'Trading venue identity (e.g., Binance, Coinbase, Kraken). Tenant-scoped.';

-- =============================================================================
-- TradingAccount — Person account at an Exchange
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.trading_account (
    trading_account_id      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    person_id               UUID NOT NULL,
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    exchange_id             UUID NOT NULL,
    external_account_id     VARCHAR(200) NOT NULL,
    account_name            VARCHAR(200),
    account_type            VARCHAR(64),
    status                  VARCHAR(20) NOT NULL DEFAULT 'active',
    metadata                JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version             INT NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID,
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by              UUID,
    deleted_at              TIMESTAMPTZ,
    CONSTRAINT uq_trading_account_id_tenant UNIQUE (trading_account_id, tenant_id),
    CONSTRAINT uq_trading_account_person_exchange_external UNIQUE (person_id, exchange_id, external_account_id),
    CONSTRAINT fk_trading_account_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_trading_account_exchange_tenant
        FOREIGN KEY (exchange_id, tenant_id)
        REFERENCES pack_personal_intelligence.exchange (exchange_id, tenant_id),
    CONSTRAINT ck_trading_account_status CHECK (status IN ('active', 'inactive', 'closed'))
);

CREATE INDEX IF NOT EXISTS ix_trading_account_person_tenant
    ON pack_personal_intelligence.trading_account (person_id, tenant_id);
CREATE INDEX IF NOT EXISTS ix_trading_account_exchange
    ON pack_personal_intelligence.trading_account (exchange_id);

-- Row version trigger
DROP TRIGGER IF EXISTS trg_trading_account_row_version ON pack_personal_intelligence.trading_account;
CREATE TRIGGER trg_trading_account_row_version
    BEFORE UPDATE ON pack_personal_intelligence.trading_account
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

COMMENT ON TABLE pack_personal_intelligence.trading_account IS
    'Person account at an Exchange. External account identity preserved. Idempotency: (person_id, exchange_id, external_account_id).';

-- =============================================================================
-- Trade — completed transaction
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.trade (
    trade_id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    person_id               UUID NOT NULL,
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    trading_account_id      UUID NOT NULL,
    external_trade_id       VARCHAR(200) NOT NULL,
    
    -- Trade execution details
    symbol                  VARCHAR(64) NOT NULL,
    side                    VARCHAR(10) NOT NULL,
    quantity                NUMERIC(24, 12) NOT NULL,
    price                   NUMERIC(24, 12) NOT NULL,
    total_value             NUMERIC(24, 4),
    
    -- Embedded fee (optional summary)
    fee_amount              NUMERIC(24, 8),
    fee_currency            VARCHAR(32),
    
    -- Timestamps
    executed_at             TIMESTAMPTZ NOT NULL,
    recorded_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Provenance
    source                  VARCHAR(200) NOT NULL,
    source_type             VARCHAR(32) NOT NULL DEFAULT 'IMPORT',
    
    -- Epistemic classification
    is_observed             BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Correction chain
    corrects_trade_id       UUID,
    
    -- Optional order reference (V1 may defer Order entity)
    order_id                UUID,
    
    -- Metadata
    metadata                JSONB NOT NULL DEFAULT '{}'::jsonb,
    
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID,
    
    CONSTRAINT uq_trade_id_tenant UNIQUE (trade_id, tenant_id),
    CONSTRAINT uq_trade_account_external UNIQUE (trading_account_id, external_trade_id),
    CONSTRAINT fk_trade_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_trade_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id),
    CONSTRAINT fk_trade_corrects_tenant
        FOREIGN KEY (corrects_trade_id, tenant_id)
        REFERENCES pack_personal_intelligence.trade (trade_id, tenant_id),
    CONSTRAINT ck_trade_side CHECK (side IN ('BUY', 'SELL')),
    CONSTRAINT ck_trade_quantity_positive CHECK (quantity > 0),
    CONSTRAINT ck_trade_price_positive CHECK (price > 0),
    CONSTRAINT ck_trade_source_type CHECK (source_type IN ('IMPORT', 'MANUAL', 'DERIVED', 'CORRECTED'))
);

CREATE INDEX IF NOT EXISTS ix_trade_person_tenant_executed
    ON pack_personal_intelligence.trade (person_id, tenant_id, executed_at);
CREATE INDEX IF NOT EXISTS ix_trade_account_executed
    ON pack_personal_intelligence.trade (trading_account_id, executed_at);
CREATE INDEX IF NOT EXISTS ix_trade_symbol_executed
    ON pack_personal_intelligence.trade (symbol, executed_at);
CREATE INDEX IF NOT EXISTS ix_trade_recorded
    ON pack_personal_intelligence.trade (recorded_at);
CREATE INDEX IF NOT EXISTS ix_trade_correction
    ON pack_personal_intelligence.trade (corrects_trade_id)
    WHERE corrects_trade_id IS NOT NULL;

COMMENT ON TABLE pack_personal_intelligence.trade IS
    'Completed transaction (historical observation). Immutable. Idempotency: (trading_account_id, external_trade_id). fee_amount/fee_currency are optional embedded summary; detailed fees in separate fee table.';

-- =============================================================================
-- Fee — trading costs (commission, spread, gas, withdrawal, etc.)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.fee (
    fee_id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    person_id               UUID NOT NULL,
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    trading_account_id      UUID NOT NULL,
    
    -- Optional Trade association. Null means the fee is not a trade charge.
    trade_id                UUID,
    
    -- Fee details
    fee_type                VARCHAR(32) NOT NULL,
    fee_amount              NUMERIC(24, 8) NOT NULL,
    fee_currency            VARCHAR(32) NOT NULL,
    
    -- External identity when the source provides one. Not a uniqueness constraint for fallback.
    external_fee_id         VARCHAR(200),
    
    -- Timestamps
    incurred_at             TIMESTAMPTZ NOT NULL,
    recorded_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Provenance
    source                  VARCHAR(200) NOT NULL,
    source_type             VARCHAR(32) NOT NULL DEFAULT 'IMPORT',
    
    -- Metadata
    metadata                JSONB NOT NULL DEFAULT '{}'::jsonb,
    
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID,
    
    CONSTRAINT fk_fee_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_fee_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id),
    CONSTRAINT fk_fee_trade_tenant
        FOREIGN KEY (trade_id, tenant_id)
        REFERENCES pack_personal_intelligence.trade (trade_id, tenant_id),
    CONSTRAINT ck_fee_type CHECK (fee_type IN ('commission', 'spread', 'gas', 'withdrawal', 'deposit', 'funding', 'other')),
    CONSTRAINT ck_fee_amount_non_negative CHECK (fee_amount >= 0)
);

CREATE INDEX IF NOT EXISTS ix_fee_person_tenant_incurred
    ON pack_personal_intelligence.fee (person_id, tenant_id, incurred_at);
CREATE INDEX IF NOT EXISTS ix_fee_trade
    ON pack_personal_intelligence.fee (trade_id)
    WHERE trade_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_fee_account_incurred
    ON pack_personal_intelligence.fee (trading_account_id, incurred_at);

-- Partial unique index for external_fee_id (trading-account-scoped)
CREATE UNIQUE INDEX IF NOT EXISTS uq_fee_account_external
    ON pack_personal_intelligence.fee (trading_account_id, external_fee_id)
    WHERE external_fee_id IS NOT NULL;

COMMENT ON TABLE pack_personal_intelligence.fee IS
    'Trading costs. May be embedded in Trade (trade.fee_amount) or separate detailed row. trade_id nullable (withdrawal/deposit/funding fees exist without trades). Same-type fees on one Trade are allowed. Idempotency: (trading_account_id, external_fee_id) when external_fee_id present; fallback is application-level deterministic composite.';

-- =============================================================================
-- Position — holdings at a point in time
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.position (
    position_id             UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    person_id               UUID NOT NULL,
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    trading_account_id      UUID NOT NULL,
    
    -- Position details
    symbol                  VARCHAR(64) NOT NULL,
    quantity                NUMERIC(24, 12) NOT NULL,
    
    -- Optional valuation
    cost_basis              NUMERIC(24, 8),
    market_value            NUMERIC(24, 4),
    unrealized_pnl          NUMERIC(24, 4),
    
    -- Occurred timestamp (point-in-time). Not a calendar date.
    as_of_at                TIMESTAMPTZ NOT NULL,
    recorded_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Provenance
    source                  VARCHAR(200) NOT NULL,
    source_type             VARCHAR(32) NOT NULL DEFAULT 'IMPORT',
    is_observed             BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Provenance only. Not an idempotency key.
    external_position_id    VARCHAR(200),
    
    -- Metadata
    metadata                JSONB NOT NULL DEFAULT '{}'::jsonb,
    
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID,
    
    CONSTRAINT uq_position_account_symbol_as_of UNIQUE (trading_account_id, symbol, as_of_at),
    CONSTRAINT fk_position_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT fk_position_account_tenant
        FOREIGN KEY (trading_account_id, tenant_id)
        REFERENCES pack_personal_intelligence.trading_account (trading_account_id, tenant_id),
    CONSTRAINT ck_position_source_type CHECK (source_type IN ('IMPORT', 'DERIVED', 'RECONCILED'))
);

CREATE INDEX IF NOT EXISTS ix_position_person_tenant_as_of
    ON pack_personal_intelligence.position (person_id, tenant_id, as_of_at);
CREATE INDEX IF NOT EXISTS ix_position_account_symbol_as_of
    ON pack_personal_intelligence.position (trading_account_id, symbol, as_of_at);
CREATE INDEX IF NOT EXISTS ix_position_symbol_as_of
    ON pack_personal_intelligence.position (symbol, as_of_at);

COMMENT ON TABLE pack_personal_intelligence.position IS
    'Holdings at a point in time (point-in-time observation, not daily snapshot). Observed (imported) or derived (calculated from trades). Idempotency: (trading_account_id, symbol, as_of_at). Multiple observations on same calendar day allowed when timestamps differ.';

-- =============================================================================
-- PortfolioSnapshot — complete portfolio state at a point in time
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_personal_intelligence.portfolio_snapshot (
    portfolio_snapshot_id   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    person_id               UUID NOT NULL,
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    
    -- Portfolio state. Person-wide. No trading_account_id.
    total_value             NUMERIC(24, 4),
    base_currency           VARCHAR(32) NOT NULL DEFAULT 'USD',
    cash_balance            NUMERIC(24, 4),
    positions_value         NUMERIC(24, 4),
    unrealized_pnl          NUMERIC(24, 4),
    realized_pnl_inception  NUMERIC(24, 4),
    
    -- Occurred timestamp (point-in-time). Not a calendar date.
    as_of_at                TIMESTAMPTZ NOT NULL,
    recorded_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Provenance
    source                  VARCHAR(200) NOT NULL,
    source_type             VARCHAR(32) NOT NULL DEFAULT 'IMPORT',
    is_observed             BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Provenance only. Not an idempotency key.
    external_snapshot_id    VARCHAR(200),
    
    -- Metadata
    metadata                JSONB NOT NULL DEFAULT '{}'::jsonb,
    
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              UUID,
    
    CONSTRAINT uq_portfolio_snapshot_person_as_of UNIQUE (person_id, as_of_at),
    CONSTRAINT fk_portfolio_snapshot_person_tenant
        FOREIGN KEY (person_id, tenant_id)
        REFERENCES pack_personal_intelligence.person (person_id, tenant_id),
    CONSTRAINT ck_portfolio_source_type CHECK (source_type IN ('IMPORT', 'DERIVED', 'RECONCILED'))
);

CREATE INDEX IF NOT EXISTS ix_portfolio_snapshot_person_tenant_as_of
    ON pack_personal_intelligence.portfolio_snapshot (person_id, tenant_id, as_of_at);

COMMENT ON TABLE pack_personal_intelligence.portfolio_snapshot IS
    'Complete portfolio state at a point in time. Person-wide (not account-specific). Point-in-time observation, not daily. Idempotency: (person_id, as_of_at). Multiple snapshots on same calendar day allowed when timestamps differ.';

-- =============================================================================
-- End of Trading Ledger V1 migration
-- =============================================================================
