-- Customer Knowledge notes and Local Prescription Knowledge regimens.
-- Not in migration-files.prod.txt. Do not apply until the schema impact is approved.
-- Does not alter customers, sales_orders, electronic_prescriptions, catalog,
-- consultation safety rules, or the operating-document tables.

BEGIN;

CREATE TABLE IF NOT EXISTS pharmacy_customer_knowledge_entry (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
    customer_id         UUID NOT NULL,
    kind                VARCHAR(32) NOT NULL,
    body                TEXT NOT NULL,
    source_kind         VARCHAR(32) NOT NULL,
    needs_verification  BOOLEAN NOT NULL DEFAULT FALSE,
    recorded_by         TEXT NOT NULL,
    recorded_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at          TIMESTAMPTZ,
    revoked_by          TEXT,
    CONSTRAINT ck_customer_knowledge_kind CHECK (
        kind IN (
            'CHRONIC',
            'ALLERGY_DRUG',
            'ALLERGY_OTHER',
            'ADVERSE_REACTION',
            'MEDICATION_USE',
            'FOLLOW_UP')),
    CONSTRAINT ck_customer_knowledge_source CHECK (
        source_kind IN (
            'SELF_REPORT',
            'PRESCRIPTION',
            'TRANSACTION',
            'PROFESSIONAL',
            'UNVERIFIED')),
    CONSTRAINT ck_customer_knowledge_body CHECK (char_length(btrim(body)) > 0)
);

CREATE INDEX IF NOT EXISTS ix_customer_knowledge_customer
    ON pharmacy_customer_knowledge_entry (tenant_id, customer_id, recorded_at DESC);

COMMENT ON TABLE pharmacy_customer_knowledge_entry IS
    'Explicit health notes for one customer. Not a purchase and not a diagnosis.';

CREATE TABLE IF NOT EXISTS pharmacy_local_regimen (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL,
    created_by  TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_local_regimen_tenant
    ON pharmacy_local_regimen (tenant_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS pharmacy_local_regimen_version (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    regimen_id          UUID NOT NULL REFERENCES pharmacy_local_regimen (id),
    tenant_id           UUID NOT NULL,
    version_no          INTEGER NOT NULL CHECK (version_no >= 1),
    title               VARCHAR(200) NOT NULL,
    condition_group     VARCHAR(200) NOT NULL,
    audience            TEXT NOT NULL,
    symptoms            TEXT NOT NULL,
    applicability       TEXT NOT NULL,
    limits              TEXT NOT NULL,
    lifecycle_status    VARCHAR(32) NOT NULL,
    created_by          TEXT NOT NULL,
    approved_by         TEXT,
    approved_at         TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_local_regimen_version UNIQUE (regimen_id, version_no),
    CONSTRAINT ck_local_regimen_lifecycle CHECK (
        lifecycle_status IN ('DRAFT', 'IN_REVIEW', 'APPROVED')),
    CONSTRAINT ck_local_regimen_title CHECK (char_length(btrim(title)) > 0),
    CONSTRAINT ck_local_regimen_limits CHECK (char_length(btrim(limits)) > 0)
);

CREATE INDEX IF NOT EXISTS ix_local_regimen_version_tenant
    ON pharmacy_local_regimen_version (tenant_id, regimen_id, version_no DESC);

CREATE TABLE IF NOT EXISTS pharmacy_local_regimen_item (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    version_id      UUID NOT NULL REFERENCES pharmacy_local_regimen_version (id),
    tenant_id       UUID NOT NULL,
    sort_order      INTEGER NOT NULL,
    ingredient      VARCHAR(200) NOT NULL,
    strength        VARCHAR(80),
    dosage_form     VARCHAR(80),
    product_id      UUID,
    product_label   VARCHAR(200),
    match_status    VARCHAR(32) NOT NULL,
    CONSTRAINT ck_local_regimen_item_match CHECK (
        match_status IN ('MATCHED', 'UNMATCHED', 'SUBSTITUTE_PROPOSED', 'SUBSTITUTE_CONFIRMED')),
    CONSTRAINT ck_local_regimen_item_ingredient CHECK (char_length(btrim(ingredient)) > 0)
);

CREATE INDEX IF NOT EXISTS ix_local_regimen_item_version
    ON pharmacy_local_regimen_item (tenant_id, version_id, sort_order);

CREATE TABLE IF NOT EXISTS pharmacy_local_regimen_activation (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    regimen_id  UUID NOT NULL,
    version_id  UUID NOT NULL,
    tenant_id   UUID NOT NULL,
    status      VARCHAR(16) NOT NULL,
    note        TEXT,
    acted_by    TEXT NOT NULL,
    acted_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_local_regimen_activation_status CHECK (status IN ('ACTIVE', 'STOPPED'))
);

CREATE INDEX IF NOT EXISTS ix_local_regimen_activation
    ON pharmacy_local_regimen_activation (tenant_id, regimen_id, acted_at DESC);

CREATE TABLE IF NOT EXISTS pharmacy_local_regimen_event (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    regimen_id      UUID NOT NULL,
    version_no      INTEGER NOT NULL,
    action          VARCHAR(16) NOT NULL,
    actor_user_id   TEXT NOT NULL,
    note            TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_local_regimen_event_action CHECK (
        action IN ('CREATED', 'UPDATED', 'SUBMITTED', 'RETURNED', 'APPROVED', 'ACTIVATED', 'STOPPED'))
);

CREATE INDEX IF NOT EXISTS ix_local_regimen_event
    ON pharmacy_local_regimen_event (tenant_id, regimen_id, created_at);

CREATE TABLE IF NOT EXISTS pharmacy_local_regimen_grant (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL,
    user_id     UUID NOT NULL,
    action      VARCHAR(16) NOT NULL,
    granted_by  TEXT NOT NULL,
    granted_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at  TIMESTAMPTZ,
    CONSTRAINT ck_local_regimen_grant_action CHECK (
        action IN ('DRAFT', 'SUBMIT', 'APPROVE', 'ACTIVATE'))
);

CREATE INDEX IF NOT EXISTS ix_local_regimen_grant
    ON pharmacy_local_regimen_grant (tenant_id, user_id, action);

COMMENT ON TABLE pharmacy_local_regimen IS
    'Local reference regimen. Not a prescription and not an operating document.';

COMMIT;
