-- KitPlatform 253: FamilyOS rituals (TP culture — dinner / thanks / shared chore)
-- Depends on: 192
-- Local / family-os pilot manifest only.

CREATE TABLE IF NOT EXISTS pack_family.family_ritual (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    code             VARCHAR(48) NOT NULL,
    label_vi         VARCHAR(160) NOT NULL,
    cadence          VARCHAR(24) NOT NULL DEFAULT 'weekly',
    sort_order       INT NOT NULL DEFAULT 0,
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_family_ritual_cadence CHECK (cadence IN ('weekly', 'daily')),
    CONSTRAINT ux_family_ritual_code UNIQUE (tenant_id, family_id, code)
);

CREATE TABLE IF NOT EXISTS pack_family.ritual_checkin (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    ritual_code      VARCHAR(48) NOT NULL,
    period_start     DATE NOT NULL,
    done_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    noted_by         UUID REFERENCES pack_family.membership(id),
    note_vi          VARCHAR(280),
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ux_ritual_checkin_period UNIQUE (tenant_id, family_id, ritual_code, period_start)
);

CREATE INDEX IF NOT EXISTS ix_ritual_checkin_family_period
    ON pack_family.ritual_checkin (family_id, period_start DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_family_ritual_row_version ON pack_family.family_ritual;
CREATE TRIGGER trg_family_ritual_row_version
    BEFORE UPDATE ON pack_family.family_ritual
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

DROP TRIGGER IF EXISTS trg_ritual_checkin_row_version ON pack_family.ritual_checkin;
CREATE TRIGGER trg_ritual_checkin_row_version
    BEFORE UPDATE ON pack_family.ritual_checkin
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_ritual ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_ritual;
CREATE POLICY tenant_isolation ON pack_family.family_ritual
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

ALTER TABLE pack_family.ritual_checkin ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.ritual_checkin;
CREATE POLICY tenant_isolation ON pack_family.ritual_checkin
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_ritual IS
    'Weekly family rituals — dinner_together, thanks_each_other, shared_chore.';
COMMENT ON TABLE pack_family.ritual_checkin IS
    'Ritual done markers per week (period_start = Monday of ISO week).';
