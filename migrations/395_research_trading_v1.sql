-- KitPlatform 395: Trading Research V1 store.
-- Forward-only. Does not alter migrations 385 through 394.
-- No foreign key to public.tenants and no business-tenant column.
-- Do not add this file to migration-files.prod.txt.
-- ADR-008 RLS_DEFERRED_V1: no ROW LEVEL SECURITY and no CREATE POLICY.

DO $$
BEGIN
    IF current_database() = 'novixa_prod' THEN
        RAISE EXCEPTION '395_research_trading_v1 must not run on novixa_prod';
    END IF;
END $$;

CREATE SCHEMA IF NOT EXISTS research_trading;

CREATE TABLE IF NOT EXISTS research_trading.candle (
    source          TEXT NOT NULL,
    symbol          TEXT NOT NULL,
    interval_code   TEXT NOT NULL,
    open_time       TIMESTAMPTZ NOT NULL,
    close_time      TIMESTAMPTZ NOT NULL,
    open_price      NUMERIC(24, 12) NOT NULL,
    high_price      NUMERIC(24, 12) NOT NULL,
    low_price       NUMERIC(24, 12) NOT NULL,
    close_price     NUMERIC(24, 12) NOT NULL,
    raw_payload     TEXT NOT NULL,
    payload_hash    TEXT NOT NULL,
    ingested_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (source, symbol, interval_code, open_time)
);

CREATE TABLE IF NOT EXISTS research_trading.session_result (
    session_key     TEXT PRIMARY KEY,
    payload_hash    TEXT NOT NULL,
    status          TEXT NOT NULL,
    result          TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS research_trading.signal (
    signal_key      TEXT PRIMARY KEY,
    session_key     TEXT NOT NULL,
    symbol          TEXT NOT NULL,
    side            TEXT NOT NULL,
    signal_time     TIMESTAMPTZ NOT NULL,
    fill_time       TIMESTAMPTZ,
    fill_price      NUMERIC(24, 12),
    status          TEXT NOT NULL,
    CONSTRAINT ck_research_signal_side CHECK (side IN ('BUY', 'SELL')),
    CONSTRAINT ck_research_signal_status CHECK (status IN ('ready', 'awaiting_next_open'))
);

CREATE TABLE IF NOT EXISTS research_trading.risk_decision (
    operation_key   TEXT PRIMARY KEY,
    session_key     TEXT NOT NULL,
    allowed         BOOLEAN NOT NULL,
    reasons         TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS research_trading.paper_fill (
    operation_key   TEXT PRIMARY KEY,
    session_key     TEXT NOT NULL,
    side            TEXT NOT NULL,
    tape_price      NUMERIC(24, 12) NOT NULL,
    execution_price NUMERIC(24, 12) NOT NULL,
    quantity        NUMERIC(24, 12) NOT NULL,
    fee             NUMERIC(24, 8) NOT NULL,
    slippage_quote  NUMERIC(24, 8) NOT NULL,
    net_cash        NUMERIC(24, 8) NOT NULL,
    CONSTRAINT ck_research_fill_side CHECK (side IN ('BUY', 'SELL'))
);

CREATE TABLE IF NOT EXISTS research_trading.paper_operation (
    operation_key   TEXT PRIMARY KEY,
    payload_hash    TEXT NOT NULL,
    status          TEXT NOT NULL,
    result          TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS research_trading.health (
    health_id       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    recorded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status          TEXT NOT NULL,
    detail          TEXT NOT NULL
);

COMMENT ON SCHEMA research_trading IS
    'Trading Research V1. Market candles, paper signals, and session results. Not a business tenant and not an exchange order store.';
