-- KitPlatform 200: FamilyOS value persistence (health score / nudges / onboarding)
-- Depends on: 192
-- Local / pilot only until deploy is explicitly approved.

-- =============================================================================
-- Daily Family Health Score (0–100)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.family_health_score (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    score_date       DATE NOT NULL,
    score            INT NOT NULL CHECK (score >= 0 AND score <= 100),
    breakdown        JSONB,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_family_health_score_day
    ON pack_family.family_health_score (tenant_id, family_id, score_date);

CREATE INDEX IF NOT EXISTS ix_family_health_score_family_date
    ON pack_family.family_health_score (family_id, score_date DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_family_health_score_row_version ON pack_family.family_health_score;
CREATE TRIGGER trg_family_health_score_row_version
    BEFORE UPDATE ON pack_family.family_health_score
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_health_score ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_health_score;
CREATE POLICY tenant_isolation ON pack_family.family_health_score
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_health_score IS
    'Persisted Family Health Score by calendar day — multi-device / paid reliability.';

-- =============================================================================
-- Parent nudge counts per day
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.parent_nudge_day (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    nudge_date       DATE NOT NULL,
    nudge_count      INT NOT NULL DEFAULT 0 CHECK (nudge_count >= 0),
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_parent_nudge_day
    ON pack_family.parent_nudge_day (tenant_id, family_id, nudge_date);

CREATE INDEX IF NOT EXISTS ix_parent_nudge_day_family_date
    ON pack_family.parent_nudge_day (family_id, nudge_date DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_parent_nudge_day_row_version ON pack_family.parent_nudge_day;
CREATE TRIGGER trg_parent_nudge_day_row_version
    BEFORE UPDATE ON pack_family.parent_nudge_day
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.parent_nudge_day ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.parent_nudge_day;
CREATE POLICY tenant_isolation ON pack_family.parent_nudge_day
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.parent_nudge_day IS
    'How many soft parent reminders were sent that day — input to Health Score / Coach.';

-- =============================================================================
-- Onboarding profile (one per family)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.family_onboarding (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    payload          JSONB NOT NULL DEFAULT '{}'::jsonb,
    completed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_family_onboarding_family
    ON pack_family.family_onboarding (tenant_id, family_id);

DROP TRIGGER IF EXISTS trg_family_onboarding_row_version ON pack_family.family_onboarding;
CREATE TRIGGER trg_family_onboarding_row_version
    BEFORE UPDATE ON pack_family.family_onboarding
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_onboarding ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_onboarding;
CREATE POLICY tenant_isolation ON pack_family.family_onboarding
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_onboarding IS
    'Foxy onboarding answers + starter mission titles — JSON payload for Coach / Value tab.';
