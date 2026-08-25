-- KitPlatform 294: Pharmacy POS consultation sessions (AI Assistant MVP 1)
-- Manifest: deploy/ubuntu/migration-files.prod.txt
-- Depends on: 006_sales, 098_schema_migrations

CREATE TABLE IF NOT EXISTS pharmacy_consultation_sessions (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID         NOT NULL REFERENCES tenants(id),
    branch_id               UUID         REFERENCES branches(id),
    customer_id             UUID         REFERENCES customers(id),
    staff_user_id           UUID         NOT NULL REFERENCES users(id),
    sales_order_id          UUID         REFERENCES sales_orders(id) ON DELETE SET NULL,
    consultation_level      SMALLINT     NOT NULL DEFAULT 1,
    status                  VARCHAR(20)  NOT NULL DEFAULT 'confirmed',
    quick_symptoms          JSONB        NOT NULL DEFAULT '[]'::jsonb,
    natural_language_input  TEXT,
    extracted_json          JSONB,
    confirmed_facts         JSONB        NOT NULL,
    safety_flags            JSONB        NOT NULL DEFAULT '[]'::jsonb,
    safety_level            VARCHAR(32)  NOT NULL DEFAULT 'none',
    ai_model                VARCHAR(80),
    extraction_source       VARCHAR(24)  NOT NULL DEFAULT 'manual',
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    confirmed_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_consultation_level CHECK (consultation_level BETWEEN 0 AND 2),
    CONSTRAINT ck_pharmacy_consultation_status CHECK (
        status IN ('draft', 'confirmed', 'linked', 'cancelled')
    ),
    CONSTRAINT ck_pharmacy_consultation_safety_level CHECK (
        safety_level IN ('none', 'caution', 'refer_pharmacist', 'refer_medical', 'stop_sale')
    ),
    CONSTRAINT ck_pharmacy_consultation_extraction_source CHECK (
        extraction_source IN ('manual', 'quick_only', 'gemini', 'gemini_fallback')
    )
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_consultation_tenant_customer
    ON pharmacy_consultation_sessions (tenant_id, customer_id, created_at DESC)
    WHERE customer_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_pharmacy_consultation_tenant_created
    ON pharmacy_consultation_sessions (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_pharmacy_consultation_order
    ON pharmacy_consultation_sessions (tenant_id, sales_order_id)
    WHERE sales_order_id IS NOT NULL;

CREATE TRIGGER trg_pharmacy_consultation_sessions_updated
    BEFORE UPDATE ON pharmacy_consultation_sessions
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

COMMENT ON TABLE pharmacy_consultation_sessions IS
    'POS consultation capture (symptoms/facts) — separate from sales order lines. MVP 1: no drug recommendations.';

INSERT INTO kit_schema_migrations (filename) VALUES ('294_pharmacy_consultation_mvp1.sql')
ON CONFLICT (filename) DO NOTHING;
