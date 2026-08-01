-- KitPlatform 252: FamilyOS Cooperation Score (TP4)
-- Depends on: 231, 251
-- Local / family-os pilot manifest only.
-- Note: do NOT rewrite ck_family_memory_kind here — later RE migs (257/258)
-- widen it; re-applying a narrow CHECK on every pilot deploy would fail.

CREATE TABLE IF NOT EXISTS pack_family.cooperation_score_day (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    score_date       DATE NOT NULL,
    pillars          JSONB NOT NULL DEFAULT '{}'::jsonb,
    total            INT NOT NULL DEFAULT 0,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_coop_score_total CHECK (total >= 0 AND total <= 100),
    CONSTRAINT ux_coop_score_family_day UNIQUE (tenant_id, family_id, score_date)
);

CREATE INDEX IF NOT EXISTS ix_coop_score_family_date
    ON pack_family.cooperation_score_day (family_id, score_date DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_coop_score_day_row_version ON pack_family.cooperation_score_day;
CREATE TRIGGER trg_coop_score_day_row_version
    BEFORE UPDATE ON pack_family.cooperation_score_day
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.cooperation_score_day ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.cooperation_score_day;
CREATE POLICY tenant_isolation ON pack_family.cooperation_score_day
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.cooperation_score_day IS
    'TP4 Cooperation Score daily cache — compute-on-read; optional upsert when API called.';
