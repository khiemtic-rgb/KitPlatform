-- KitPlatform 438: KIT Sales lead events (funnel history + KPI)
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- Does not alter Pharmacy, Assessment, Payment, or other packs.

ALTER TABLE pack_sales.product_profile
    ADD COLUMN IF NOT EXISTS qualification_json JSONB NOT NULL DEFAULT '{}'::jsonb;

COMMENT ON COLUMN pack_sales.product_profile.qualification_json IS
    'Per-product qualification weights, maturity days, phcTemplateCodes. Empty object uses code defaults. Template allowlist stays empty until the product owner fills it.';

CREATE TABLE IF NOT EXISTS pack_sales.lead_event (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    lead_id             UUID NOT NULL REFERENCES pack_sales.lead(id) ON DELETE CASCADE,
    event_type          VARCHAR(64) NOT NULL,
    from_status         VARCHAR(32),
    to_status           VARCHAR(32),
    payload             JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor_user_id       UUID,
    agent_run_id        UUID,
    occurred_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    idempotency_key     VARCHAR(160) NOT NULL,
    CONSTRAINT uq_sales_lead_event_idem UNIQUE (tenant_id, idempotency_key),
    CONSTRAINT ck_sales_lead_event_payload CHECK (jsonb_typeof(payload) = 'object')
);

CREATE INDEX IF NOT EXISTS ix_sales_lead_event_lead
    ON pack_sales.lead_event (tenant_id, lead_id, occurred_at);

CREATE INDEX IF NOT EXISTS ix_sales_lead_event_type
    ON pack_sales.lead_event (tenant_id, event_type, occurred_at DESC);

COMMENT ON TABLE pack_sales.lead_event IS
    'Append-only KIT Sales funnel history. KPI reads this table. Not platform_events.';

INSERT INTO kit_schema_migrations (filename) VALUES ('438_pack_kit_sales_lead_event.sql')
ON CONFLICT (filename) DO NOTHING;
