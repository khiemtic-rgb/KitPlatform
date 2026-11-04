-- Full content snapshot on each pharmacy-owned knowledge event.
-- Extends 424. Does not alter global knowledge tables.
-- Not in migration-files.prod.txt.

BEGIN;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_event_immutable
    ON pharmacy_owned_knowledge_event;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD COLUMN IF NOT EXISTS body TEXT,
    ADD COLUMN IF NOT EXISTS classification VARCHAR(200);

UPDATE pharmacy_owned_knowledge_event AS event
SET body = version.body,
    classification = version.classification
FROM pharmacy_owned_knowledge_version AS version
WHERE event.body IS NULL
  AND event.knowledge_id = version.knowledge_id
  AND event.tenant_id = version.tenant_id
  AND event.version_no = version.version_no;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pharmacy_owned_knowledge_event
        WHERE body IS NULL
    ) THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge event is missing a content snapshot';
    END IF;
END $$;

ALTER TABLE pharmacy_owned_knowledge_event
    ALTER COLUMN body SET NOT NULL;

ALTER TABLE pharmacy_owned_knowledge_event
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_event_body;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_event_body
    CHECK (char_length(btrim(body)) > 0);

COMMENT ON TABLE pharmacy_owned_knowledge_event IS
    'Append-only content snapshots. Each row is the full text after create or update.';

CREATE TRIGGER trg_pharmacy_owned_knowledge_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_event_immutable();

COMMIT;
