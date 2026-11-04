-- Pharmacy-owned knowledge governance. Extends 426.
-- Does not edit 424, 425, or 426. Does not touch global knowledge.
-- Not in migration-files.prod.txt.

BEGIN;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_version_draft_only
    ON pharmacy_owned_knowledge_version;

ALTER TABLE pharmacy_owned_knowledge_version
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_draft;

ALTER TABLE pharmacy_owned_knowledge_version
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_lifecycle;

ALTER TABLE pharmacy_owned_knowledge_version
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_lifecycle
    CHECK (lifecycle_status IN ('DRAFT', 'IN_REVIEW', 'APPROVED'));

CREATE OR REPLACE FUNCTION pharmacy_owned_knowledge_version_governance()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge version cannot be deleted';
    END IF;

    IF TG_OP = 'INSERT' THEN
        IF NEW.lifecycle_status IS DISTINCT FROM 'DRAFT' THEN
            RAISE EXCEPTION 'a new pharmacy-owned knowledge version starts as draft';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
       OR NEW.knowledge_id IS DISTINCT FROM OLD.knowledge_id
       OR NEW.version_no IS DISTINCT FROM OLD.version_no THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge identity is immutable';
    END IF;

    IF OLD.lifecycle_status = 'APPROVED' THEN
        IF NEW.lifecycle_status IS DISTINCT FROM 'APPROVED'
           OR NEW.kind IS DISTINCT FROM OLD.kind
           OR NEW.title IS DISTINCT FROM OLD.title
           OR NEW.body IS DISTINCT FROM OLD.body
           OR NEW.classification IS DISTINCT FROM OLD.classification THEN
            RAISE EXCEPTION 'approved pharmacy-owned knowledge is frozen';
        END IF;
        RETURN NEW;
    END IF;

    IF OLD.lifecycle_status = 'IN_REVIEW' THEN
        IF NEW.kind IS DISTINCT FROM OLD.kind
           OR NEW.title IS DISTINCT FROM OLD.title
           OR NEW.body IS DISTINCT FROM OLD.body
           OR NEW.classification IS DISTINCT FROM OLD.classification THEN
            RAISE EXCEPTION 'pharmacy-owned knowledge in review is frozen';
        END IF;
        IF NEW.lifecycle_status NOT IN ('DRAFT', 'IN_REVIEW', 'APPROVED') THEN
            RAISE EXCEPTION 'invalid pharmacy-owned knowledge review transition';
        END IF;
        RETURN NEW;
    END IF;

    IF OLD.lifecycle_status = 'DRAFT'
       AND NEW.lifecycle_status IN ('DRAFT', 'IN_REVIEW') THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'invalid pharmacy-owned knowledge transition';
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_version_governance
    ON pharmacy_owned_knowledge_version;

CREATE TRIGGER trg_pharmacy_owned_knowledge_version_governance
    BEFORE INSERT OR UPDATE OR DELETE ON pharmacy_owned_knowledge_version
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_version_governance();

CREATE UNIQUE INDEX IF NOT EXISTS uq_pharmacy_owned_knowledge_one_draft
    ON pharmacy_owned_knowledge_version (tenant_id, knowledge_id)
    WHERE lifecycle_status = 'DRAFT';

CREATE UNIQUE INDEX IF NOT EXISTS uq_pharmacy_owned_knowledge_one_review
    ON pharmacy_owned_knowledge_version (tenant_id, knowledge_id)
    WHERE lifecycle_status = 'IN_REVIEW';

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_event_immutable
    ON pharmacy_owned_knowledge_event;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD COLUMN IF NOT EXISTS note VARCHAR(2000);

ALTER TABLE pharmacy_owned_knowledge_event
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_event_action;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_event_action
    CHECK (action IN ('CREATED', 'UPDATED', 'SUBMITTED', 'RETURNED', 'APPROVED'));

ALTER TABLE pharmacy_owned_knowledge_event
    DROP CONSTRAINT IF EXISTS ck_pharmacy_owned_knowledge_event_note;

ALTER TABLE pharmacy_owned_knowledge_event
    ADD CONSTRAINT ck_pharmacy_owned_knowledge_event_note
    CHECK (
        (action = 'RETURNED' AND note IS NOT NULL AND char_length(btrim(note)) > 0)
        OR action <> 'RETURNED'
    );

CREATE TRIGGER trg_pharmacy_owned_knowledge_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_event_immutable();

CREATE TABLE IF NOT EXISTS pharmacy_owned_knowledge_activation (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    knowledge_id    UUID NOT NULL,
    version_id      UUID NOT NULL REFERENCES pharmacy_owned_knowledge_version (id),
    status          VARCHAR(16) NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pharmacy_owned_knowledge_activation UNIQUE (tenant_id, knowledge_id, version_id),
    CONSTRAINT ck_pharmacy_owned_knowledge_activation_status CHECK (status IN ('ACTIVE', 'STOPPED'))
);

CREATE TABLE IF NOT EXISTS pharmacy_owned_knowledge_activation_event (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    knowledge_id    UUID NOT NULL,
    version_id      UUID NOT NULL,
    action          VARCHAR(16) NOT NULL,
    note            VARCHAR(2000),
    actor_user_id   TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_owned_knowledge_activation_event_action
        CHECK (action IN ('ACTIVATED', 'STOPPED', 'RESUMED')),
    CONSTRAINT ck_pharmacy_owned_knowledge_activation_event_note
        CHECK (
            (action IN ('STOPPED', 'RESUMED') AND note IS NOT NULL AND char_length(btrim(note)) > 0)
            OR action = 'ACTIVATED'
        )
);

CREATE OR REPLACE FUNCTION pharmacy_owned_knowledge_activation_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge activation cannot be deleted';
    END IF;

    IF TG_OP = 'UPDATE' AND (
        NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
        OR NEW.knowledge_id IS DISTINCT FROM OLD.knowledge_id
        OR NEW.version_id IS DISTINCT FROM OLD.version_id) THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge activation identity is immutable';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_activation_guard
    ON pharmacy_owned_knowledge_activation;

CREATE TRIGGER trg_pharmacy_owned_knowledge_activation_guard
    BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_activation
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_activation_guard();

CREATE OR REPLACE FUNCTION pharmacy_owned_knowledge_activation_event_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'pharmacy_owned_knowledge_activation_event is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_activation_event_immutable
    ON pharmacy_owned_knowledge_activation_event;

CREATE TRIGGER trg_pharmacy_owned_knowledge_activation_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_activation_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_activation_event_immutable();

CREATE TABLE IF NOT EXISTS pharmacy_owned_knowledge_grant (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    user_id         UUID NOT NULL,
    action          VARCHAR(16) NOT NULL,
    kind            VARCHAR(40) NOT NULL,
    granted_by      TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at      TIMESTAMPTZ,
    CONSTRAINT uq_pharmacy_owned_knowledge_grant UNIQUE (tenant_id, user_id, action, kind),
    CONSTRAINT ck_pharmacy_owned_knowledge_grant_action
        CHECK (action IN ('draft', 'submit', 'approve', 'activate', 'read', 'clinical'))
);

COMMIT;
