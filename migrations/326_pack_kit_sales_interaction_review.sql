-- KitPlatform 326: kit_sales interaction review (draft / approved / sent) for AI agent
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY

ALTER TABLE pack_sales.interaction
    ADD COLUMN IF NOT EXISTS review_status VARCHAR(16) NOT NULL DEFAULT 'sent',
    ADD COLUMN IF NOT EXISTS ai_generated BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS approved_by_user_id UUID,
    ADD COLUMN IF NOT EXISTS sent_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS classification VARCHAR(64);

UPDATE pack_sales.interaction
SET sent_at = COALESCE(sent_at, occurred_at)
WHERE review_status = 'sent' AND sent_at IS NULL;

ALTER TABLE pack_sales.interaction
    DROP CONSTRAINT IF EXISTS ck_sales_interaction_review;

ALTER TABLE pack_sales.interaction
    ADD CONSTRAINT ck_sales_interaction_review
    CHECK (review_status IN ('draft', 'approved', 'sent'));

CREATE INDEX IF NOT EXISTS ix_sales_interaction_review
    ON pack_sales.interaction (tenant_id, lead_id, review_status, occurred_at DESC);

INSERT INTO kit_schema_migrations (filename) VALUES ('326_pack_kit_sales_interaction_review.sql')
ON CONFLICT (filename) DO NOTHING;
