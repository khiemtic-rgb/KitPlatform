-- W5 platform publication history.
-- Not in migration-files.prod.txt. Apply only on an isolated database after 416.
-- Does not grant platform.knowledge.publish and does not publish any rule.

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.pharmacy_knowledge_rule_version') IS NULL THEN
        RAISE EXCEPTION '417 requires pharmacy_knowledge_rule_version from 416';
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS pharmacy_knowledge_publication_event (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rule_code      VARCHAR(64)  NOT NULL,
    rule_version   VARCHAR(16)  NOT NULL,
    from_status    VARCHAR(32)  NOT NULL,
    to_status      VARCHAR(32)  NOT NULL,
    actor_user_id  TEXT         NOT NULL,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_knowledge_publication_event_rule
    ON pharmacy_knowledge_publication_event (rule_code, rule_version, created_at);

COMMENT ON TABLE pharmacy_knowledge_publication_event IS
    'Append-only global publication history. This is not a pharmacy adoption.';

CREATE OR REPLACE FUNCTION pharmacy_knowledge_publication_event_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'pharmacy_knowledge_publication_event is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_knowledge_publication_event_immutable
    ON pharmacy_knowledge_publication_event;

CREATE TRIGGER trg_pharmacy_knowledge_publication_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_knowledge_publication_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_knowledge_publication_event_immutable();

CREATE OR REPLACE FUNCTION pharmacy_knowledge_rule_version_freeze_published()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.lifecycle_status IN ('PUBLISHED', 'RETIRED') THEN
            RAISE EXCEPTION 'published knowledge version cannot be deleted';
        END IF;
        RETURN OLD;
    END IF;

    IF NEW.lifecycle_status IN ('PUBLISHED', 'RETIRED')
       AND (
            OLD.lifecycle_status IS DISTINCT FROM NEW.lifecycle_status
            OR OLD.lifecycle_status IN ('PUBLISHED', 'RETIRED')
       )
       AND (to_jsonb(NEW) - 'lifecycle_status' - 'updated_at')
           IS DISTINCT FROM
           (to_jsonb(OLD) - 'lifecycle_status' - 'updated_at')
    THEN
        RAISE EXCEPTION 'published knowledge version content is frozen';
    END IF;

    IF OLD.lifecycle_status = 'RETIRED'
       AND NEW.lifecycle_status IS DISTINCT FROM OLD.lifecycle_status THEN
        RAISE EXCEPTION 'retired knowledge version cannot change lifecycle';
    END IF;

    IF OLD.lifecycle_status = 'PUBLISHED'
       AND NEW.lifecycle_status NOT IN ('PUBLISHED', 'RETIRED') THEN
        RAISE EXCEPTION 'published knowledge version cannot return to review';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_knowledge_rule_version_freeze_published
    ON pharmacy_knowledge_rule_version;

CREATE TRIGGER trg_pharmacy_knowledge_rule_version_freeze_published
    BEFORE UPDATE OR DELETE ON pharmacy_knowledge_rule_version
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_knowledge_rule_version_freeze_published();

COMMIT;
