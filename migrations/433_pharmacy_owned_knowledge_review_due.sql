-- Review due date on pharmacy-owned knowledge and local regimens, plus a small usage/feedback log.
-- Not in migration-files.prod.txt. Ships with migration-files.pharmacy-knowledge-v1.txt only.
-- Idempotent. Existing approved rows keep review_due_on NULL (not overdue) until the next approval.

BEGIN;

ALTER TABLE pharmacy_owned_knowledge_version
    ADD COLUMN IF NOT EXISTS review_due_on DATE;

ALTER TABLE pharmacy_local_regimen_version
    ADD COLUMN IF NOT EXISTS review_due_on DATE;

CREATE TABLE IF NOT EXISTS pharmacy_knowledge_feedback (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    knowledge_id    UUID,
    kind            VARCHAR(16) NOT NULL,
    query_text      VARCHAR(500),
    user_id         UUID NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_knowledge_feedback_kind
        CHECK (kind IN ('open', 'missing', 'not_helpful')),
    CONSTRAINT ck_pharmacy_knowledge_feedback_target
        CHECK (kind = 'missing' OR knowledge_id IS NOT NULL),
    CONSTRAINT ck_pharmacy_knowledge_feedback_query
        CHECK (kind <> 'missing' OR (query_text IS NOT NULL AND char_length(btrim(query_text)) > 0))
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_knowledge_feedback_item
    ON pharmacy_knowledge_feedback (tenant_id, knowledge_id, kind);

CREATE INDEX IF NOT EXISTS ix_pharmacy_knowledge_feedback_missing
    ON pharmacy_knowledge_feedback (tenant_id, created_at DESC)
    WHERE kind = 'missing';

COMMENT ON TABLE pharmacy_knowledge_feedback IS
    'Usage and feedback on tenant knowledge. Not a sale, not a consultation record.';

COMMIT;
