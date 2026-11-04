-- W5 tenant knowledge governance.
-- Not in migration-files.prod.txt. Apply only on an isolated database after 415.
-- Does not grant permissions, does not publish rules, and does not approve safety flags.

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.pharmacy_knowledge_rule') IS NULL THEN
        RAISE EXCEPTION '416 requires pharmacy_knowledge_rule';
    END IF;
    IF to_regclass('public.pharmacy_knowledge_approval_event') IS NULL THEN
        RAISE EXCEPTION '416 requires pharmacy_knowledge_approval_event from 415';
    END IF;
    IF to_regclass('public.pharmacy_consultation_risk_flag') IS NULL THEN
        RAISE EXCEPTION '416 requires pharmacy_consultation_risk_flag';
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS pharmacy_knowledge_rule_version (
    rule_code         VARCHAR(64)  NOT NULL,
    version           VARCHAR(16)  NOT NULL,
    source            VARCHAR(40)  NOT NULL DEFAULT 'novixa',
    effective_from    DATE         NOT NULL DEFAULT CURRENT_DATE,
    effective_to      DATE,
    symptom_id        UUID         REFERENCES pharmacy_symptom(id) ON DELETE CASCADE,
    category_codes    TEXT[]       NOT NULL DEFAULT '{}',
    keywords          TEXT[]       NOT NULL DEFAULT '{}',
    exclude_keywords  TEXT[]       NOT NULL DEFAULT '{}',
    reason_vi         TEXT         NOT NULL,
    priority          INT          NOT NULL DEFAULT 100,
    lifecycle_status  VARCHAR(32)  NOT NULL,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (rule_code, version),
    CONSTRAINT ck_pharmacy_knowledge_rule_version_lifecycle
        CHECK (lifecycle_status IN ('DRAFT', 'IN_REVIEW', 'PUBLISHED', 'RETIRED'))
);

INSERT INTO pharmacy_knowledge_rule_version (
    rule_code, version, source, effective_from, effective_to, symptom_id,
    category_codes, keywords, exclude_keywords, reason_vi, priority, lifecycle_status)
SELECT
    r.rule_code,
    COALESCE(NULLIF(BTRIM(r.version), ''), '1'),
    r.source,
    r.effective_from,
    r.effective_to,
    r.symptom_id,
    r.category_codes,
    r.keywords,
    r.exclude_keywords,
    r.reason_vi,
    r.priority,
    'IN_REVIEW'
FROM pharmacy_knowledge_rule r
ON CONFLICT (rule_code, version) DO NOTHING;

COMMENT ON TABLE pharmacy_knowledge_rule_version IS
    'Global content version. PUBLISHED means a pharmacy may pin it. Copilot does not read this table alone.';

CREATE TABLE IF NOT EXISTS pharmacy_knowledge_adoption (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID         NOT NULL REFERENCES tenants(id),
    rule_code         VARCHAR(64)  NOT NULL,
    rule_version      VARCHAR(16)  NOT NULL,
    status            VARCHAR(32)  NOT NULL,
    approved_by       TEXT,
    approved_at       TIMESTAMPTZ,
    reference_source  TEXT,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pharmacy_knowledge_adoption_tenant_rule UNIQUE (tenant_id, rule_code),
    CONSTRAINT fk_pharmacy_knowledge_adoption_version
        FOREIGN KEY (rule_code, rule_version)
        REFERENCES pharmacy_knowledge_rule_version (rule_code, version),
    CONSTRAINT ck_pharmacy_knowledge_adoption_status
        CHECK (status IN ('APPROVED', 'RETIRED'))
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_knowledge_adoption_tenant
    ON pharmacy_knowledge_adoption (tenant_id, status);

COMMENT ON TABLE pharmacy_knowledge_adoption IS
    'One pin per tenant and rule_code. Copilot reads APPROVED pins only.';

CREATE TABLE IF NOT EXISTS pharmacy_tenant_knowledge_policy (
    id                         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    adoption_id                UUID        NOT NULL UNIQUE REFERENCES pharmacy_knowledge_adoption(id) ON DELETE CASCADE,
    rule_version               VARCHAR(16) NOT NULL,
    product_matching_enabled   BOOLEAN     NOT NULL DEFAULT TRUE,
    extra_exclude_keywords     TEXT[]      NOT NULL DEFAULT '{}',
    priority_override          INT,
    created_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE pharmacy_tenant_knowledge_policy IS
    'Narrows the pinned version. A policy for another version is ignored.';

ALTER TABLE pharmacy_knowledge_approval_event
    ADD COLUMN IF NOT EXISTS tenant_id UUID;

COMMENT ON COLUMN pharmacy_knowledge_approval_event.tenant_id IS
    'Null on history written before tenant pins existed. Those rows are not a tenant approval.';

ALTER TABLE pharmacy_consultation_risk_flag
    ADD COLUMN IF NOT EXISTS publication_status VARCHAR(16);

UPDATE pharmacy_consultation_risk_flag
SET publication_status = 'PUBLISHED'
WHERE publication_status IS NULL
  AND is_active = TRUE;

UPDATE pharmacy_consultation_risk_flag
SET publication_status = 'DRAFT'
WHERE publication_status IS NULL;

ALTER TABLE pharmacy_consultation_risk_flag
    ALTER COLUMN publication_status SET DEFAULT 'DRAFT';

ALTER TABLE pharmacy_consultation_risk_flag
    ALTER COLUMN publication_status SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_pharmacy_risk_publication_status'
    ) THEN
        ALTER TABLE pharmacy_consultation_risk_flag
            ADD CONSTRAINT ck_pharmacy_risk_publication_status
            CHECK (publication_status IN ('DRAFT', 'PUBLISHED', 'RETIRED'));
    END IF;
END $$;

COMMENT ON COLUMN pharmacy_consultation_risk_flag.publication_status IS
    'Runtime source for catalog risk flags. is_active does not change the safety gate.';

COMMIT;
