-- M1 pharmacy-owned knowledge drafts.
-- Not in migration-files.prod.txt. Apply only on an isolated local database.
-- Does not publish, pin, grant permissions, or write global knowledge.

BEGIN;

CREATE TABLE IF NOT EXISTS pharmacy_owned_knowledge (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL,
    created_by  TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_owned_knowledge_tenant
    ON pharmacy_owned_knowledge (tenant_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS pharmacy_owned_knowledge_version (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    knowledge_id        UUID NOT NULL REFERENCES pharmacy_owned_knowledge (id),
    tenant_id           UUID NOT NULL,
    version_no          INTEGER NOT NULL CHECK (version_no >= 1),
    kind                VARCHAR(40) NOT NULL,
    title               VARCHAR(200) NOT NULL,
    body                TEXT NOT NULL,
    classification      VARCHAR(200),
    lifecycle_status    VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pharmacy_owned_knowledge_version UNIQUE (knowledge_id, version_no),
    CONSTRAINT ck_pharmacy_owned_knowledge_kind CHECK (
        kind IN (
            'GUIDANCE',
            'OPERATING_PROCEDURE',
            'OPERATIONAL_POLICY',
            'PRODUCT_KNOWLEDGE',
            'CLINICAL_KNOWLEDGE')),
    CONSTRAINT ck_pharmacy_owned_knowledge_draft CHECK (lifecycle_status = 'DRAFT'),
    CONSTRAINT ck_pharmacy_owned_knowledge_title CHECK (char_length(btrim(title)) > 0),
    CONSTRAINT ck_pharmacy_owned_knowledge_body CHECK (char_length(btrim(body)) > 0)
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_owned_knowledge_version_tenant
    ON pharmacy_owned_knowledge_version (tenant_id, knowledge_id, version_no);

CREATE TABLE IF NOT EXISTS pharmacy_owned_knowledge_event (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    knowledge_id    UUID NOT NULL,
    version_no      INTEGER NOT NULL,
    action          VARCHAR(16) NOT NULL,
    kind            VARCHAR(40) NOT NULL,
    title           VARCHAR(200) NOT NULL,
    actor_user_id   TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_owned_knowledge_event_action CHECK (action IN ('CREATED', 'UPDATED'))
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_owned_knowledge_event_tenant
    ON pharmacy_owned_knowledge_event (tenant_id, knowledge_id, created_at);

COMMENT ON TABLE pharmacy_owned_knowledge IS
    'Tenant-authored knowledge head. Not a global rule and not an adoption pin.';

COMMENT ON TABLE pharmacy_owned_knowledge_version IS
    'M1 stores drafts only. Approved versions stay immutable in a later milestone.';

CREATE OR REPLACE FUNCTION pharmacy_owned_knowledge_version_draft_only()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.lifecycle_status IS DISTINCT FROM 'DRAFT' THEN
        RAISE EXCEPTION 'pharmacy-owned knowledge cannot leave draft in this milestone';
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF OLD.lifecycle_status IS DISTINCT FROM 'DRAFT' THEN
            RAISE EXCEPTION 'only draft pharmacy-owned knowledge can be edited';
        END IF;
        IF NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
           OR NEW.knowledge_id IS DISTINCT FROM OLD.knowledge_id
           OR NEW.version_no IS DISTINCT FROM OLD.version_no THEN
            RAISE EXCEPTION 'pharmacy-owned knowledge identity is immutable';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_version_draft_only
    ON pharmacy_owned_knowledge_version;

CREATE TRIGGER trg_pharmacy_owned_knowledge_version_draft_only
    BEFORE INSERT OR UPDATE ON pharmacy_owned_knowledge_version
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_version_draft_only();

CREATE OR REPLACE FUNCTION pharmacy_owned_knowledge_event_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'pharmacy_owned_knowledge_event is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_event_immutable
    ON pharmacy_owned_knowledge_event;

CREATE TRIGGER trg_pharmacy_owned_knowledge_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_owned_knowledge_event_immutable();

COMMIT;
