-- KitPlatform 246: FamilyOS Parent Success P2 — evening 3Q check-in
-- Depends on: 192_pack_family_os.sql (family, membership)
-- Soft recognition for payers; not child Currency badges / not Parent Goal (226).
-- Local / Family OS park: apply only via family-os migration manifest.

CREATE TABLE IF NOT EXISTS pack_family.parent_success_checkin (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    member_id       UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date       DATE NOT NULL,
    q_less_nudge    BOOLEAN NOT NULL,
    q_less_tension  BOOLEAN NOT NULL,
    q_quality_time  BOOLEAN NOT NULL,
    note            TEXT,
    row_version     INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      UUID,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by      UUID,
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_parent_success_checkin_day
        UNIQUE (tenant_id, family_id, member_id, flow_date)
);

CREATE INDEX IF NOT EXISTS ix_parent_success_checkin_family_date
    ON pack_family.parent_success_checkin (family_id, flow_date DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_parent_success_checkin_row_version
    ON pack_family.parent_success_checkin;
CREATE TRIGGER trg_parent_success_checkin_row_version
    BEFORE UPDATE ON pack_family.parent_success_checkin
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.parent_success_checkin ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.parent_success_checkin;
CREATE POLICY tenant_isolation ON pack_family.parent_success_checkin
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.parent_success_checkin IS
    'Parent Success P2 — evening 3Q (less nudge / less tension / quality time). Soft, private to guardians.';
COMMENT ON COLUMN pack_family.parent_success_checkin.q_less_nudge IS
    'PSE Q1: Hôm nay đã phải nhắc con ít hơn chưa?';
COMMENT ON COLUMN pack_family.parent_success_checkin.q_less_tension IS
    'PSE Q2: Hôm nay gia đình có bớt căng thẳng hơn chưa?';
COMMENT ON COLUMN pack_family.parent_success_checkin.q_quality_time IS
    'PSE Q3: Hôm nay có thêm thời gian chất lượng với con chưa?';
