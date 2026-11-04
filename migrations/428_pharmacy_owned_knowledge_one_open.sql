-- One open content version per pharmacy knowledge, and clinical grants by action.
-- Deploy only through migration-files.pharmacy-knowledge-v1.txt, right after 424-427:
-- the grant table is new there, so the 'clinical' DELETE below matches no live rows.
-- Never run it alone on a database where 424-427 were applied earlier.

BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pharmacy_owned_knowledge_version
        WHERE lifecycle_status IN ('DRAFT', 'IN_REVIEW')
        GROUP BY tenant_id, knowledge_id
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge already has more than one open version';
    END IF;
END $$;

DROP INDEX IF EXISTS uq_pharmacy_owned_knowledge_one_draft;
DROP INDEX IF EXISTS uq_pharmacy_owned_knowledge_one_review;

CREATE UNIQUE INDEX IF NOT EXISTS uq_pharmacy_owned_knowledge_one_open
    ON pharmacy_owned_knowledge_version (tenant_id, knowledge_id)
    WHERE lifecycle_status IN ('DRAFT', 'IN_REVIEW');

DELETE FROM pharmacy_owned_knowledge_grant
WHERE action = 'clinical';

ALTER TABLE pharmacy_owned_knowledge_grant
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_grant_action;

ALTER TABLE pharmacy_owned_knowledge_grant
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_grant_action
    CHECK (action IN ('draft', 'submit', 'approve', 'activate', 'read'));

COMMIT;
