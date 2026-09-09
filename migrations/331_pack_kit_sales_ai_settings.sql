-- KitPlatform 331: kit_sales Gemini key (isolated from Content Park)
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- Stores the Sales desk restyle key on KIT_SALES. Does not write pack_content.

CREATE TABLE IF NOT EXISTS pack_sales.ai_settings (
    tenant_id           UUID PRIMARY KEY,
    gemini_api_key      TEXT,
    text_model          TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE pack_sales.ai_settings IS
    'KIT Sales — Gemini restyle key. Facts stay locked in code. Not Content Park org_settings.';

INSERT INTO kit_schema_migrations (filename) VALUES ('331_pack_kit_sales_ai_settings.sql')
ON CONFLICT (filename) DO NOTHING;
