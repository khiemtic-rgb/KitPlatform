-- Customer merge audit (admin «Gộp khách» on near-duplicate cleanup)

CREATE TABLE IF NOT EXISTS customer_merge_events (
    id                   UUID PRIMARY KEY,
    tenant_id            UUID         NOT NULL REFERENCES tenants(id),
    keeper_customer_id   UUID         NOT NULL,
    source_customer_id   UUID         NOT NULL,
    merged_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    merged_by_user_id    UUID         REFERENCES users(id),
    reason               TEXT,
    meta                 JSONB        NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS ix_customer_merge_events_tenant_merged
    ON customer_merge_events (tenant_id, merged_at DESC);

COMMENT ON TABLE customer_merge_events IS 'Audit log when duplicate customers are merged into a keeper.';
