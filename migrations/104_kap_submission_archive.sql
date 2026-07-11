-- KitPlatform 104: soft-archive KAP assessment submissions (no hard delete)
-- Depends on: 068, 103_kap_partner_portal

ALTER TABLE assessment_submission
    ADD COLUMN IF NOT EXISTS archived_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS ix_assessment_submission_archived
    ON assessment_submission (archived_at DESC NULLS LAST)
    WHERE archived_at IS NOT NULL;

COMMENT ON COLUMN assessment_submission.archived_at IS
    'Soft-archive timestamp. NULL = visible in admin/partner lists; set = hidden from default views.';
