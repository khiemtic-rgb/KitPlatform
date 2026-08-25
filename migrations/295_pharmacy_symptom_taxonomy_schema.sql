-- KitPlatform 295: Pharmacy AI Assistant — symptom taxonomy schema (global catalog)
-- Manifest: deploy/ubuntu/migration-files.prod.txt
-- Depends on: 294_pharmacy_consultation_mvp1.sql

-- Extend session extraction sources used by local-fast path
ALTER TABLE pharmacy_consultation_sessions
    DROP CONSTRAINT IF EXISTS ck_pharmacy_consultation_extraction_source;

ALTER TABLE pharmacy_consultation_sessions
    ADD CONSTRAINT ck_pharmacy_consultation_extraction_source CHECK (
        extraction_source IN (
            'manual', 'quick_only', 'gemini', 'gemini_fallback',
            'local_fast', 'local_fallback'
        )
    );

CREATE TABLE IF NOT EXISTS pharmacy_symptom_category (
    code            VARCHAR(40) PRIMARY KEY,
    label_vi        VARCHAR(120) NOT NULL,
    label_en        VARCHAR(120),
    sort_order      INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pharmacy_symptom (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code                VARCHAR(64) NOT NULL UNIQUE,
    taxonomy_ref        VARCHAR(24),
    name_vi             VARCHAR(200) NOT NULL,
    name_en             VARCHAR(200),
    category_code       VARCHAR(40) NOT NULL REFERENCES pharmacy_symptom_category(code),
    consultation_mode   VARCHAR(24) NOT NULL DEFAULT 'capture_only',
    description         TEXT,
    sort_order          INT NOT NULL DEFAULT 0,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_symptom_mode CHECK (
        consultation_mode IN ('otc_assist', 'capture_only', 'pharmacist_only', 'context_only')
    )
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_symptom_category_active
    ON pharmacy_symptom (category_code, sort_order)
    WHERE is_active = TRUE;

CREATE TABLE IF NOT EXISTS pharmacy_symptom_alias (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    symptom_id  UUID NOT NULL REFERENCES pharmacy_symptom(id) ON DELETE CASCADE,
    alias       VARCHAR(200) NOT NULL,
    source      VARCHAR(40) NOT NULL DEFAULT 'novixa',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pharmacy_symptom_alias UNIQUE (symptom_id, alias)
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_symptom_alias_lookup
    ON pharmacy_symptom_alias (lower(alias));

CREATE TABLE IF NOT EXISTS pharmacy_consultation_risk_flag (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(64) NOT NULL UNIQUE,
    name_vi         VARCHAR(200) NOT NULL,
    severity        VARCHAR(24) NOT NULL,
    action          VARCHAR(32) NOT NULL,
    message_vi      TEXT NOT NULL,
    safety_level    VARCHAR(32) NOT NULL,
    sort_order      INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_risk_severity CHECK (
        severity IN ('emergency', 'urgent', 'pharmacist', 'caution', 'population')
    ),
    CONSTRAINT ck_pharmacy_risk_action CHECK (
        action IN ('stop_sale', 'refer_medical', 'refer_pharmacist', 'caution', 'none')
    ),
    CONSTRAINT ck_pharmacy_risk_safety_level CHECK (
        safety_level IN ('none', 'caution', 'refer_pharmacist', 'refer_medical', 'stop_sale')
    )
);

CREATE TABLE IF NOT EXISTS pharmacy_symptom_risk_rule (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    symptom_id      UUID REFERENCES pharmacy_symptom(id) ON DELETE CASCADE,
    risk_flag_id    UUID NOT NULL REFERENCES pharmacy_consultation_risk_flag(id) ON DELETE CASCADE,
    condition_json  JSONB NOT NULL DEFAULT '{}'::jsonb,
    priority        INT NOT NULL DEFAULT 100,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_symptom_risk_symptom
    ON pharmacy_symptom_risk_rule (symptom_id)
    WHERE is_active = TRUE;

CREATE TABLE IF NOT EXISTS pharmacy_consultation_question (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(64) NOT NULL UNIQUE,
    question_vi     TEXT NOT NULL,
    answer_type     VARCHAR(24) NOT NULL DEFAULT 'boolean',
    options_json    JSONB,
    sort_order      INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_question_answer_type CHECK (
        answer_type IN ('boolean', 'number', 'text', 'select', 'duration_days')
    )
);

CREATE TABLE IF NOT EXISTS pharmacy_symptom_question_rule (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    symptom_id      UUID NOT NULL REFERENCES pharmacy_symptom(id) ON DELETE CASCADE,
    question_id     UUID NOT NULL REFERENCES pharmacy_consultation_question(id) ON DELETE CASCADE,
    required        BOOLEAN NOT NULL DEFAULT FALSE,
    priority        INT NOT NULL DEFAULT 100,
    condition_json  JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (symptom_id, question_id)
);

CREATE TABLE IF NOT EXISTS pharmacy_knowledge_rule (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rule_code       VARCHAR(64) NOT NULL UNIQUE,
    source          VARCHAR(40) NOT NULL DEFAULT 'novixa',
    version         VARCHAR(16) NOT NULL DEFAULT '1',
    effective_from  DATE NOT NULL DEFAULT CURRENT_DATE,
    effective_to    DATE,
    symptom_id      UUID REFERENCES pharmacy_symptom(id) ON DELETE CASCADE,
    category_codes  TEXT[] NOT NULL DEFAULT '{}',
    keywords        TEXT[] NOT NULL DEFAULT '{}',
    exclude_keywords TEXT[] NOT NULL DEFAULT '{}',
    reason_vi       TEXT NOT NULL,
    priority        INT NOT NULL DEFAULT 100,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_knowledge_rule_symptom
    ON pharmacy_knowledge_rule (symptom_id)
    WHERE is_active = TRUE;

CREATE TRIGGER trg_pharmacy_symptom_category_updated
    BEFORE UPDATE ON pharmacy_symptom_category
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_pharmacy_symptom_updated
    BEFORE UPDATE ON pharmacy_symptom
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_pharmacy_consultation_risk_flag_updated
    BEFORE UPDATE ON pharmacy_consultation_risk_flag
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_pharmacy_knowledge_rule_updated
    BEFORE UPDATE ON pharmacy_knowledge_rule
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

COMMENT ON TABLE pharmacy_symptom IS
    'Global Novixa symptom taxonomy V1 — AI Pharmacy Assistant catalog (not tenant-scoped).';

COMMENT ON TABLE pharmacy_knowledge_rule IS
    'OTC product matching rules (category/keyword). Evaluated after risk engine passes.';

INSERT INTO kit_schema_migrations (filename) VALUES ('295_pharmacy_symptom_taxonomy_schema.sql')
ON CONFLICT (filename) DO NOTHING;
