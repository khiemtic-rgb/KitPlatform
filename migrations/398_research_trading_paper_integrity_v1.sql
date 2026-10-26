-- KitPlatform 398: paper integrity columns and research run history.
-- NOT APPLIED. Do not run this file against novixa_prod or the live trading_research database.
-- Forward-only. Adds nullable columns and one empty table. Does not update existing rows.
-- Do not add this file to migration-files.prod.txt.

DO $$
BEGIN
    IF current_database() IN ('novixa_prod', 'trading_research') THEN
        RAISE EXCEPTION '398_research_trading_paper_integrity_v1 must not run on %', current_database();
    END IF;
END $$;

ALTER TABLE research_trading.signal
    ADD COLUMN IF NOT EXISTS strategy_id TEXT,
    ADD COLUMN IF NOT EXISTS strategy_version TEXT,
    ADD COLUMN IF NOT EXISTS timeframe TEXT,
    ADD COLUMN IF NOT EXISTS candle_open_time TIMESTAMPTZ;

ALTER TABLE research_trading.risk_decision
    ADD COLUMN IF NOT EXISTS signal_key TEXT;

ALTER TABLE research_trading.paper_fill
    ADD COLUMN IF NOT EXISTS signal_key TEXT;

ALTER TABLE research_trading.paper_operation
    ADD COLUMN IF NOT EXISTS signal_key TEXT;

CREATE TABLE IF NOT EXISTS research_trading.run_history (
    run_id              TEXT PRIMARY KEY,
    started_at          TIMESTAMPTZ NOT NULL,
    ended_at            TIMESTAMPTZ NOT NULL,
    status              TEXT NOT NULL,
    error               TEXT,
    candles_inserted    INTEGER NOT NULL,
    candles_skipped     INTEGER NOT NULL,
    signals             INTEGER NOT NULL,
    risk_decisions      INTEGER NOT NULL,
    operations          INTEGER NOT NULL,
    fills               INTEGER NOT NULL,
    replayed            BOOLEAN NOT NULL,
    pnl_recomputed      BOOLEAN NOT NULL,
    CONSTRAINT ck_research_run_status CHECK (status IN ('completed', 'failed', 'replayed', 'unknown'))
);

COMMENT ON TABLE research_trading.run_history IS
    'One row per research host run. Not an exchange order and not a closed-trade ledger.';
