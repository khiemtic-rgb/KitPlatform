-- K1 source verification on pharmacy_knowledge_rule_version.
-- Not in migration-files.prod.txt. Do not apply on production from this change.
-- Does not grant permissions, does not accept a source, and does not publish any rule.

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.pharmacy_knowledge_rule_version') IS NULL THEN
        RAISE EXCEPTION '423 requires pharmacy_knowledge_rule_version from 416';
    END IF;
END $$;

ALTER TABLE pharmacy_knowledge_rule_version
    ADD COLUMN IF NOT EXISTS reference_source TEXT,
    ADD COLUMN IF NOT EXISTS source_identity TEXT,
    ADD COLUMN IF NOT EXISTS source_url TEXT,
    ADD COLUMN IF NOT EXISTS document_ref TEXT,
    ADD COLUMN IF NOT EXISTS issuing_organization TEXT,
    ADD COLUMN IF NOT EXISTS source_published_on DATE,
    ADD COLUMN IF NOT EXISTS evidence_note TEXT,
    ADD COLUMN IF NOT EXISTS source_verification_status VARCHAR(32) NOT NULL DEFAULT 'UNVERIFIED',
    ADD COLUMN IF NOT EXISTS verified_by TEXT,
    ADD COLUMN IF NOT EXISTS verified_at TIMESTAMPTZ;

COMMENT ON COLUMN pharmacy_knowledge_rule_version.reference_source IS
    'Citation for this version. The catalog label source=novixa is not a citation.';

COMMENT ON COLUMN pharmacy_knowledge_rule_version.source_verification_status IS
    'UNVERIFIED, ACCEPTED, or REJECTED. ACCEPTED is not PUBLISHED.';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_pharmacy_knowledge_version_source_status'
    ) THEN
        ALTER TABLE pharmacy_knowledge_rule_version
            ADD CONSTRAINT ck_pharmacy_knowledge_version_source_status
            CHECK (source_verification_status IN ('UNVERIFIED', 'ACCEPTED', 'REJECTED'));
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_pharmacy_knowledge_version_source_accepted'
    ) THEN
        ALTER TABLE pharmacy_knowledge_rule_version
            ADD CONSTRAINT ck_pharmacy_knowledge_version_source_accepted
            CHECK (
                source_verification_status <> 'ACCEPTED'
                OR (
                    length(btrim(coalesce(reference_source, ''))) > 0
                    AND lower(btrim(reference_source)) <> 'novixa'
                    AND length(btrim(coalesce(source_identity, ''))) > 0
                    AND lower(btrim(source_identity)) <> 'novixa'
                    AND length(btrim(coalesce(issuing_organization, ''))) > 0
                    AND length(btrim(coalesce(evidence_note, ''))) > 0
                    AND length(btrim(coalesce(verified_by, ''))) > 0
                    AND verified_at IS NOT NULL
                    AND (
                        length(btrim(coalesce(source_url, ''))) > 0
                        OR length(btrim(coalesce(document_ref, ''))) > 0
                    )
                )
            );
    END IF;
END $$;

CREATE OR REPLACE FUNCTION pharmacy_knowledge_rule_version_clear_source()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.lifecycle_status IN ('DRAFT', 'IN_REVIEW') THEN
        NEW.source_verification_status := 'UNVERIFIED';
        NEW.verified_by := NULL;
        NEW.verified_at := NULL;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_knowledge_rule_version_clear_source
    ON pharmacy_knowledge_rule_version;

CREATE TRIGGER trg_pharmacy_knowledge_rule_version_clear_source
    BEFORE UPDATE ON pharmacy_knowledge_rule_version
    FOR EACH ROW
    WHEN (
        OLD.keywords IS DISTINCT FROM NEW.keywords
        OR OLD.category_codes IS DISTINCT FROM NEW.category_codes
        OR OLD.exclude_keywords IS DISTINCT FROM NEW.exclude_keywords
        OR OLD.reason_vi IS DISTINCT FROM NEW.reason_vi
        OR OLD.symptom_id IS DISTINCT FROM NEW.symptom_id
    )
    EXECUTE FUNCTION pharmacy_knowledge_rule_version_clear_source();

CREATE OR REPLACE FUNCTION pharmacy_knowledge_rule_version_require_accepted_source()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.lifecycle_status = 'PUBLISHED'
       AND (TG_OP = 'INSERT' OR OLD.lifecycle_status IS DISTINCT FROM 'PUBLISHED') THEN
        IF NEW.rule_code = 'KR_OTHER' THEN
            RAISE EXCEPTION 'KR_OTHER cannot be published';
        END IF;
        IF NEW.source_verification_status IS DISTINCT FROM 'ACCEPTED'
           OR length(btrim(coalesce(NEW.reference_source, ''))) = 0
           OR lower(btrim(NEW.reference_source)) = 'novixa'
           OR length(btrim(coalesce(NEW.source_identity, ''))) = 0
           OR lower(btrim(NEW.source_identity)) = 'novixa'
           OR length(btrim(coalesce(NEW.issuing_organization, ''))) = 0
           OR length(btrim(coalesce(NEW.evidence_note, ''))) = 0
           OR length(btrim(coalesce(NEW.verified_by, ''))) = 0
           OR NEW.verified_at IS NULL
           OR (
                length(btrim(coalesce(NEW.source_url, ''))) = 0
                AND length(btrim(coalesce(NEW.document_ref, ''))) = 0
           ) THEN
            RAISE EXCEPTION 'published knowledge version requires an accepted source';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_knowledge_rule_version_require_accepted_source
    ON pharmacy_knowledge_rule_version;

CREATE TRIGGER trg_pharmacy_knowledge_rule_version_require_accepted_source
    BEFORE INSERT OR UPDATE ON pharmacy_knowledge_rule_version
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_knowledge_rule_version_require_accepted_source();

COMMIT;
