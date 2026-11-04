-- Mark events that predate full snapshots. Extends 425.
-- Version 1 was updated in place, so text copied by 425 is not historical evidence.
-- Existing event rows stay. Inferred body and classification are cleared.
-- Not in migration-files.prod.txt.

BEGIN;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_event_immutable
    ON pharmacy_owned_knowledge_event;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD COLUMN IF NOT EXISTS snapshot_fidelity VARCHAR(32);

ALTER TABLE pharmacy_owned_knowledge_event
    ALTER COLUMN body DROP NOT NULL;

ALTER TABLE pharmacy_owned_knowledge_event
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_event_body;

UPDATE pharmacy_owned_knowledge_event
SET snapshot_fidelity = 'LEGACY_UNAVAILABLE',
    body = NULL,
    classification = NULL
WHERE snapshot_fidelity IS NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pharmacy_owned_knowledge_event
        WHERE snapshot_fidelity IS NULL
           OR snapshot_fidelity <> 'LEGACY_UNAVAILABLE'
           OR body IS NOT NULL
           OR classification IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'pre-fidelity pharmacy-owned knowledge events must be marked unavailable without inferred text';
    END IF;
END $$;

ALTER TABLE pharmacy_owned_knowledge_event
    ALTER COLUMN snapshot_fidelity SET NOT NULL;

ALTER TABLE pharmacy_owned_knowledge_event
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_event_fidelity;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_event_fidelity
    CHECK (snapshot_fidelity IN ('EXACT', 'LEGACY_UNAVAILABLE'));

ALTER TABLE pharmacy_owned_knowledge_event
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_event_snapshot;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_event_snapshot
    CHECK (
        (
            snapshot_fidelity = 'EXACT'
            AND body IS NOT NULL
            AND char_length(btrim(body)) > 0
        )
        OR (
            snapshot_fidelity = 'LEGACY_UNAVAILABLE'
            AND body IS NULL
            AND classification IS NULL
        )
    );

CREATE INDEX IF NOT EXISTS ix_pharmacy_owned_knowledge_event_history
    ON pharmacy_owned_knowledge_event (tenant_id, knowledge_id, created_at, id);

COMMENT ON COLUMN pharmacy_owned_knowledge_event.snapshot_fidelity IS
    'EXACT: full content stored when the event was written. LEGACY_UNAVAILABLE: event predates full snapshots; original body cannot be recovered.';

CREATE TRIGGER trg_pharmacy_owned_knowledge_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_event_immutable();

COMMIT;
