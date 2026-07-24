-- KitPlatform 219: FamilyOS member mood entry (kid journal mood + parent visibility)
-- Depends on: 218_pack_family_reward_catalog.sql
-- One mood + optional note per child per flow day.

CREATE TABLE IF NOT EXISTS pack_family.member_mood_entry (
    id           UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id    UUID NOT NULL REFERENCES public.tenants(id),
    family_id    UUID NOT NULL REFERENCES pack_family.family(id),
    member_id    UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date    DATE NOT NULL,
    mood_code    VARCHAR(24) NOT NULL,
    note         TEXT,
    row_version  INT NOT NULL DEFAULT 0,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by   UUID,
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by   UUID,
    deleted_at   TIMESTAMPTZ,
    CONSTRAINT uq_member_mood_entry_day UNIQUE (tenant_id, family_id, member_id, flow_date),
    CONSTRAINT ck_member_mood_entry_code CHECK (
        mood_code IN ('mad', 'sad', 'ok', 'happy', 'love')
    )
);

CREATE INDEX IF NOT EXISTS ix_member_mood_entry_family_date
    ON pack_family.member_mood_entry (family_id, flow_date, member_id)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_member_mood_entry_row_version ON pack_family.member_mood_entry;
CREATE TRIGGER trg_member_mood_entry_row_version
    BEFORE UPDATE ON pack_family.member_mood_entry
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.member_mood_entry ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.member_mood_entry;
CREATE POLICY tenant_isolation ON pack_family.member_mood_entry
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.member_mood_entry IS
    'Child self-reported mood + journal note — kid saves from Nhật ký; parent reads on Today Flow.';
