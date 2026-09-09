-- KitPlatform 328: kit_sales Adaptive Pain Discovery
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- Catalog content lives in code (NovixaPainCatalog) and is upserted at runtime.

CREATE TABLE IF NOT EXISTS pack_sales.pain (
    code                VARCHAR(64) PRIMARY KEY,
    product_code        VARCHAR(32) NOT NULL DEFAULT 'novixa',
    category            VARCHAR(32) NOT NULL,
    name                TEXT NOT NULL,
    description         TEXT NOT NULL,
    payload             JSONB NOT NULL DEFAULT '{}'::jsonb,
    explore_weight      NUMERIC(5,2) NOT NULL DEFAULT 1,
    status              VARCHAR(16) NOT NULL DEFAULT 'active',
    version             INT NOT NULL DEFAULT 1,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_pain_category
    ON pack_sales.pain (category, status);

CREATE TABLE IF NOT EXISTS pack_sales.lead_pain (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    lead_id             UUID NOT NULL REFERENCES pack_sales.lead(id) ON DELETE CASCADE,
    pain_code           VARCHAR(64) NOT NULL,
    category            VARCHAR(32) NOT NULL,
    confidence          NUMERIC(5,2) NOT NULL DEFAULT 0,
    evidence            TEXT,
    source              VARCHAR(32) NOT NULL DEFAULT 'staff',
    response_code       VARCHAR(32),
    approached_count    INT NOT NULL DEFAULT 0,
    response_score      NUMERIC(5,2) NOT NULL DEFAULT 0,
    engagement_score    NUMERIC(5,2) NOT NULL DEFAULT 0,
    confirmed           BOOLEAN NOT NULL DEFAULT FALSE,
    last_approached_at  TIMESTAMPTZ,
    last_response_at    TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, lead_id, pain_code)
);

CREATE INDEX IF NOT EXISTS ix_sales_lead_pain_lead
    ON pack_sales.lead_pain (tenant_id, lead_id, confidence DESC);

CREATE INDEX IF NOT EXISTS ix_sales_lead_pain_market
    ON pack_sales.lead_pain (tenant_id, category, response_code);

INSERT INTO kit_schema_migrations (filename) VALUES ('328_pack_kit_sales_pain_library.sql')
ON CONFLICT (filename) DO NOTHING;
