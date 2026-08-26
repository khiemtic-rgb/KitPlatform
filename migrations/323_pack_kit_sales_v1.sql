-- KitPlatform 323: KIT Sales — acquisition pipeline (isolated park)
-- Pack/module: kit_sales | Schema: pack_sales | Tenant (324): KIT_SALES
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- NOT Pharmacy Growth Desk (retention). NOT Marketing Park (KIT_MKT).

CREATE SCHEMA IF NOT EXISTS pack_sales;

COMMENT ON SCHEMA pack_sales IS
    'KIT Sales — acquisition & pipeline. Multi-product (novixa, famixa). Isolated from Pharmacy ops.';

-- =============================================================================
-- Platform catalog
-- =============================================================================
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT
    'kit_sales',
    'KIT Sales',
    'Acquisition pipeline — prospect Business/Lead, PHC integration, sales workspace.',
    ARRAY['marketing', 'hybrid'],
    94
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'kit_sales'
);

INSERT INTO kit_tenant.tenant_package (
    package_code, package_name, description, verticals, module_codes, sort_order
)
VALUES (
    'kit_sales',
    'KIT Sales',
    'Novixa/Famixa acquisition pipeline. KIT org workspace — not pharmacy tenant POS.',
    ARRAY['marketing', 'hybrid'],
    ARRAY['kit_sales'],
    94
)
ON CONFLICT (package_code) DO UPDATE SET
    package_name = EXCLUDED.package_name,
    description = EXCLUDED.description,
    verticals = EXCLUDED.verticals,
    module_codes = EXCLUDED.module_codes,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('kit_sales.read', 'KIT Sales — xem pipeline', 'kit_sales'),
    ('kit_sales.write', 'KIT Sales — tạo/sửa lead', 'kit_sales')
ON CONFLICT (permission_code) DO NOTHING;

-- =============================================================================
-- Product profiles (multi-product; Novixa v1 active)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_sales.product (
    code            VARCHAR(32) PRIMARY KEY,
    display_name    TEXT NOT NULL,
    status          VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO pack_sales.product (code, display_name, status)
VALUES
    ('novixa', 'Novixa Pharmacy', 'active'),
    ('famixa', 'Famixa', 'planned')
ON CONFLICT (code) DO NOTHING;

CREATE TABLE IF NOT EXISTS pack_sales.product_profile (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    product_code    VARCHAR(32) NOT NULL REFERENCES pack_sales.product(code),
    version_label   VARCHAR(64) NOT NULL,
    status          VARCHAR(16) NOT NULL DEFAULT 'active',
    icp_json        JSONB NOT NULL DEFAULT '{}'::jsonb,
    pain_json       JSONB NOT NULL DEFAULT '[]'::jsonb,
    offer_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
    funnel_json     JSONB NOT NULL DEFAULT '[]'::jsonb,
    effective_from  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (product_code, version_label)
);

INSERT INTO pack_sales.product_profile (product_code, version_label, status, icp_json, pain_json, offer_json, funnel_json)
SELECT
    'novixa',
    'novixa-sales-v1',
    'active',
    '{"summary":"Nhà thuốc độc lập, chủ/quản lý, quầy phù hợp OTC"}'::jsonb,
    '["staff_turnover","owner_on_counter","inventory","fefo","multi_branch","reporting"]'::jsonb,
    '{"primary":"pharmacy_health_check","conversion":["phc","consultation","demo","pilot_30d","paid"]}'::jsonb,
    '["discover","research","contact","engage","phc","report","qualify","consultation","demo","pilot","value_review","paid"]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM pack_sales.product_profile p
    WHERE p.product_code = 'novixa' AND p.version_label = 'novixa-sales-v1'
);

-- =============================================================================
-- Core entities
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_sales.business (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    name                TEXT NOT NULL,
    business_type       VARCHAR(32) NOT NULL DEFAULT 'pharmacy',
    industry            VARCHAR(64),
    description         TEXT,
    address             TEXT,
    province            VARCHAR(64),
    district            VARCHAR(64),
    ward                VARCHAR(64),
    phone               VARCHAR(32),
    email               VARCHAR(256),
    website             TEXT,
    status              VARCHAR(16) NOT NULL DEFAULT 'active',
    source              VARCHAR(64),
    confidence_score    NUMERIC(5,2) NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_business_tenant ON pack_sales.business (tenant_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS pack_sales.business_identity (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    business_id         UUID NOT NULL REFERENCES pack_sales.business(id) ON DELETE CASCADE,
    identity_type       VARCHAR(32) NOT NULL,
    platform            VARCHAR(32),
    url                 TEXT,
    username            VARCHAR(128),
    display_name        TEXT,
    description         TEXT,
    is_public           BOOLEAN NOT NULL DEFAULT TRUE,
    confidence_score    NUMERIC(5,2) NOT NULL DEFAULT 0,
    last_verified_at    TIMESTAMPTZ,
    source              VARCHAR(64),
    status              VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_identity_business ON pack_sales.business_identity (business_id);

CREATE TABLE IF NOT EXISTS pack_sales.contact (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    business_id         UUID NOT NULL REFERENCES pack_sales.business(id) ON DELETE CASCADE,
    name                TEXT NOT NULL,
    role                VARCHAR(32) NOT NULL DEFAULT 'unknown',
    phone               VARCHAR(32),
    email               VARCHAR(256),
    preferred_channel   VARCHAR(32),
    source              VARCHAR(64),
    confidence_score    NUMERIC(5,2) NOT NULL DEFAULT 0,
    is_decision_maker   BOOLEAN NOT NULL DEFAULT FALSE,
    status              VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_contact_business ON pack_sales.contact (business_id);

CREATE TABLE IF NOT EXISTS pack_sales.campaign (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    product_code        VARCHAR(32) NOT NULL REFERENCES pack_sales.product(code),
    name                TEXT NOT NULL,
    objective           TEXT,
    target_profile_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    start_date          DATE,
    end_date            DATE,
    status              VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_campaign_tenant ON pack_sales.campaign (tenant_id, product_code);

CREATE TABLE IF NOT EXISTS pack_sales.lead (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL,
    business_id             UUID NOT NULL REFERENCES pack_sales.business(id) ON DELETE CASCADE,
    contact_id              UUID REFERENCES pack_sales.contact(id) ON DELETE SET NULL,
    product_code            VARCHAR(32) NOT NULL REFERENCES pack_sales.product(code),
    campaign_id             UUID REFERENCES pack_sales.campaign(id) ON DELETE SET NULL,
    source                  VARCHAR(64),
    lead_status             VARCHAR(32) NOT NULL DEFAULT 'discovered',
    lead_temperature        VARCHAR(16) NOT NULL DEFAULT 'cold',
    fit_score               NUMERIC(5,2) NOT NULL DEFAULT 0,
    pain_score              NUMERIC(5,2) NOT NULL DEFAULT 0,
    intent_score            NUMERIC(5,2) NOT NULL DEFAULT 0,
    engagement_score        NUMERIC(5,2) NOT NULL DEFAULT 0,
    readiness_score         NUMERIC(5,2) NOT NULL DEFAULT 0,
    total_score             NUMERIC(5,2) NOT NULL DEFAULT 0,
    owner_user_id           UUID,
    next_action_code        VARCHAR(32),
    next_action_at          TIMESTAMPTZ,
    last_interaction_at     TIMESTAMPTZ,
    assessment_submission_id UUID,
    notes                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_lead_tenant_status ON pack_sales.lead (tenant_id, lead_status, updated_at DESC);
CREATE INDEX IF NOT EXISTS ix_sales_lead_business ON pack_sales.lead (business_id);
CREATE INDEX IF NOT EXISTS ix_sales_lead_next_action ON pack_sales.lead (tenant_id, next_action_at)
    WHERE next_action_at IS NOT NULL;

CREATE TABLE IF NOT EXISTS pack_sales.signal (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    business_id         UUID NOT NULL REFERENCES pack_sales.business(id) ON DELETE CASCADE,
    lead_id             UUID REFERENCES pack_sales.lead(id) ON DELETE SET NULL,
    signal_type         VARCHAR(32) NOT NULL,
    description         TEXT NOT NULL,
    evidence            TEXT,
    source              VARCHAR(64),
    confidence          NUMERIC(5,2) NOT NULL DEFAULT 0,
    kind                VARCHAR(16) NOT NULL DEFAULT 'inference',
    detected_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expired_at          TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_sales_signal_business ON pack_sales.signal (business_id, detected_at DESC);

CREATE TABLE IF NOT EXISTS pack_sales.interaction (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    lead_id             UUID NOT NULL REFERENCES pack_sales.lead(id) ON DELETE CASCADE,
    channel             VARCHAR(32) NOT NULL,
    direction           VARCHAR(16) NOT NULL DEFAULT 'outbound',
    interaction_type    VARCHAR(32) NOT NULL,
    content             TEXT,
    summary             TEXT,
    sent_by_user_id     UUID,
    agent_run_id        UUID,
    outcome             VARCHAR(64),
    occurred_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_interaction_lead ON pack_sales.interaction (lead_id, occurred_at DESC);

CREATE TABLE IF NOT EXISTS pack_sales.task (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    lead_id             UUID NOT NULL REFERENCES pack_sales.lead(id) ON DELETE CASCADE,
    action_code         VARCHAR(32) NOT NULL,
    title               TEXT NOT NULL,
    status              VARCHAR(16) NOT NULL DEFAULT 'open',
    due_at              TIMESTAMPTZ,
    assigned_user_id    UUID,
    created_by_user_id  UUID,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_task_due ON pack_sales.task (tenant_id, status, due_at);

-- AI-ready stubs (no automation V1)
CREATE TABLE IF NOT EXISTS pack_sales.agent_run (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    agent_code          VARCHAR(64) NOT NULL,
    lead_id             UUID REFERENCES pack_sales.lead(id) ON DELETE SET NULL,
    status              VARCHAR(16) NOT NULL DEFAULT 'completed',
    input_json          JSONB NOT NULL DEFAULT '{}'::jsonb,
    output_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    policy_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_by_user_id  UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_sales.agent_task (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL,
    agent_run_id        UUID REFERENCES pack_sales.agent_run(id) ON DELETE SET NULL,
    lead_id             UUID REFERENCES pack_sales.lead(id) ON DELETE SET NULL,
    task_type           VARCHAR(64) NOT NULL,
    status              VARCHAR(16) NOT NULL DEFAULT 'pending',
    payload_json        JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at        TIMESTAMPTZ
);

INSERT INTO kit_schema_migrations (filename) VALUES ('323_pack_kit_sales_v1.sql')
ON CONFLICT (filename) DO NOTHING;
