-- KitPlatform 245: Family Currency Economics (C0–C5)
-- SAO như tiền tệ gia đình: ngân sách ngày, phân bổ nhóm, 3 loại sao, duty/decay, badge.
-- Depends on: 244_pack_family_behavior_os_wave5.sql
-- Local / park deploy until explicitly approved.

-- =============================================================================
-- C0 — per-family currency settings (preset + overrides)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.family_currency_settings (
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    family_id               UUID PRIMARY KEY REFERENCES pack_family.family(id),
    enabled                 BOOLEAN NOT NULL DEFAULT TRUE,
    preset_id               VARCHAR(64) NOT NULL DEFAULT 'balanced_v1',
    age_band                VARCHAR(16),
    daily_budget_override   INT,
    config_json             JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_family_currency_age_band CHECK (
        age_band IS NULL OR age_band IN ('6_10', '11_15', '16_18', 'custom')
    ),
    CONSTRAINT ck_family_currency_budget_override CHECK (
        daily_budget_override IS NULL
        OR (daily_budget_override BETWEEN 10 AND 80)
    )
);

CREATE INDEX IF NOT EXISTS idx_family_currency_settings_tenant
    ON pack_family.family_currency_settings (tenant_id);

ALTER TABLE pack_family.family_currency_settings ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_currency_settings;
CREATE POLICY tenant_isolation ON pack_family.family_currency_settings
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_currency_settings IS
    'Family Currency Economics — daily budget, category weights, duty/decay policy (preset balanced_v1).';

-- =============================================================================
-- Template / commitment classification (C1–C2, C4)
-- =============================================================================
ALTER TABLE pack_family.commitment_template
    ADD COLUMN IF NOT EXISTS currency_category VARCHAR(32),
    ADD COLUMN IF NOT EXISTS eligible_for_stars BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS star_kind VARCHAR(24),
    ADD COLUMN IF NOT EXISTS plan_target INT;

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS currency_category VARCHAR(32),
    ADD COLUMN IF NOT EXISTS eligible_for_stars BOOLEAN,
    ADD COLUMN IF NOT EXISTS star_kind VARCHAR(24),
    ADD COLUMN IF NOT EXISTS plan_target INT,
    ADD COLUMN IF NOT EXISTS actual_progress INT,
    ADD COLUMN IF NOT EXISTS allocated_base_stars INT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_template_currency_category'
    ) THEN
        ALTER TABLE pack_family.commitment_template
            ADD CONSTRAINT ck_commitment_template_currency_category
            CHECK (
                currency_category IS NULL
                OR currency_category IN (
                    'growth', 'responsibility', 'kindness', 'cue', 'play', 'duty'
                )
            );
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_template_star_kind'
    ) THEN
        ALTER TABLE pack_family.commitment_template
            ADD CONSTRAINT ck_commitment_template_star_kind
            CHECK (
                star_kind IS NULL
                OR star_kind IN ('growth', 'responsibility', 'kindness')
            );
    END IF;
END $$;

-- =============================================================================
-- C3 — star_kind on ledger
-- =============================================================================
ALTER TABLE pack_family.star_ledger
    ADD COLUMN IF NOT EXISTS star_kind VARCHAR(24) NOT NULL DEFAULT 'growth';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_star_ledger_star_kind'
    ) THEN
        ALTER TABLE pack_family.star_ledger
            ADD CONSTRAINT ck_star_ledger_star_kind
            CHECK (star_kind IN ('growth', 'responsibility', 'kindness'));
    END IF;
END $$;

COMMENT ON COLUMN pack_family.star_ledger.star_kind IS
    'Family Currency kind: growth | responsibility | kindness.';

-- Redeem mix costs (optional; NULL = use flat cost)
ALTER TABLE pack_family.reward_catalog
    ADD COLUMN IF NOT EXISTS cost_growth INT,
    ADD COLUMN IF NOT EXISTS cost_responsibility INT,
    ADD COLUMN IF NOT EXISTS cost_kindness INT;

-- =============================================================================
-- C5 — badges (non-star recognition)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.badge_definition (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID REFERENCES pack_family.family(id),
    code            VARCHAR(64) NOT NULL,
    label_vi        VARCHAR(160) NOT NULL,
    unlock_json     JSONB NOT NULL DEFAULT '[]'::jsonb,
    rule_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    active          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_badge_definition_scope UNIQUE (tenant_id, family_id, code)
);

CREATE INDEX IF NOT EXISTS ix_badge_definition_family
    ON pack_family.badge_definition (tenant_id, family_id)
    WHERE active = TRUE;

ALTER TABLE pack_family.badge_definition ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.badge_definition;
CREATE POLICY tenant_isolation ON pack_family.badge_definition
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

CREATE TABLE IF NOT EXISTS pack_family.member_badge (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    member_id       UUID NOT NULL REFERENCES pack_family.membership(id),
    badge_id        UUID NOT NULL REFERENCES pack_family.badge_definition(id),
    awarded_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    payload_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_member_badge UNIQUE (tenant_id, member_id, badge_id)
);

CREATE INDEX IF NOT EXISTS ix_member_badge_member
    ON pack_family.member_badge (tenant_id, family_id, member_id, awarded_at DESC);

ALTER TABLE pack_family.member_badge ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.member_badge;
CREATE POLICY tenant_isolation ON pack_family.member_badge
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.badge_definition IS
    'Family Currency badges — streak/count recognition without star payout.';
COMMENT ON TABLE pack_family.member_badge IS
    'Badges awarded to a member (idempotent per badge).';
