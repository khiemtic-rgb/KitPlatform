-- Human approval lifecycle for pharmacy knowledge rules.
-- Existing rows stay PENDING_REVIEW. This script does not approve clinical content.
-- Not in migration-files.prod.txt.

ALTER TABLE pharmacy_knowledge_rule
    ADD COLUMN IF NOT EXISTS approval_status VARCHAR(32) NOT NULL DEFAULT 'PENDING_REVIEW',
    ADD COLUMN IF NOT EXISTS created_by TEXT,
    ADD COLUMN IF NOT EXISTS approved_by TEXT,
    ADD COLUMN IF NOT EXISTS approved_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS reference_source TEXT,
    ADD COLUMN IF NOT EXISTS retired_at TIMESTAMPTZ;

ALTER TABLE pharmacy_knowledge_rule
    ALTER COLUMN approval_status DROP DEFAULT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_pharmacy_knowledge_rule_approval'
    ) THEN
        ALTER TABLE pharmacy_knowledge_rule
            ADD CONSTRAINT ck_pharmacy_knowledge_rule_approval
            CHECK (approval_status IN ('DRAFT', 'PENDING_REVIEW', 'APPROVED', 'RETIRED'));
    END IF;
END $$;

COMMENT ON COLUMN pharmacy_knowledge_rule.approval_status IS
    'Only APPROVED rules may be used by the counter copilot. AI cannot set APPROVED.';
