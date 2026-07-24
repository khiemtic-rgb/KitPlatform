-- KitPlatform 212: FamilyOS child gratitude (praise card "Cảm ơn mẹ!")
-- Depends on: 192
-- One thank-you per child per flow day; parent sees on Today Flow home.

CREATE TABLE IF NOT EXISTS pack_family.child_gratitude (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    from_member_id   UUID NOT NULL REFERENCES pack_family.membership(id),
    to_member_id     UUID REFERENCES pack_family.membership(id),
    flow_date        DATE NOT NULL,
    message_vi       VARCHAR(400) NOT NULL,
    praise_context   VARCHAR(400),
    read_at          TIMESTAMPTZ,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_child_gratitude_child_day
    ON pack_family.child_gratitude (tenant_id, family_id, from_member_id, flow_date)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_child_gratitude_family_date
    ON pack_family.child_gratitude (family_id, flow_date, created_at DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_child_gratitude_row_version ON pack_family.child_gratitude;
CREATE TRIGGER trg_child_gratitude_row_version
    BEFORE UPDATE ON pack_family.child_gratitude
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.child_gratitude ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.child_gratitude;
CREATE POLICY tenant_isolation ON pack_family.child_gratitude
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.child_gratitude IS
    'Child thank-you to parent(s) from praise card — visible on parent Today Flow home.';
