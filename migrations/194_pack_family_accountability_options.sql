-- KitPlatform 194: FamilyOS — configurable consequence / reward options (per family)
-- Depends on: 192_pack_family_os.sql, 193_pack_family_agreement_f2.sql
-- Local / pilot only until deploy is explicitly approved

CREATE TABLE IF NOT EXISTS pack_family.accountability_option (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    kind             VARCHAR(20) NOT NULL,
    code             VARCHAR(64) NOT NULL,
    option_group     VARCHAR(40) NOT NULL,
    label_vi         VARCHAR(160) NOT NULL,
    description_vi   VARCHAR(400) NOT NULL DEFAULT '',
    is_system        BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order       INT NOT NULL DEFAULT 0,
    status           VARCHAR(20) NOT NULL DEFAULT 'active',
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_accountability_option_kind CHECK (kind IN ('consequence', 'reward')),
    CONSTRAINT ck_accountability_option_status CHECK (status IN ('active', 'archived'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_accountability_option_family_kind_code
    ON pack_family.accountability_option (tenant_id, family_id, kind, code)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_accountability_option_family_kind
    ON pack_family.accountability_option (family_id, kind, status, sort_order)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_accountability_option_row_version ON pack_family.accountability_option;
CREATE TRIGGER trg_accountability_option_row_version
    BEFORE UPDATE ON pack_family.accountability_option
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.accountability_option ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.accountability_option;
CREATE POLICY tenant_isolation ON pack_family.accountability_option
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.accountability_option IS
    'Family-scoped safe consequence/reward catalog. System rows seeded; family may add custom rows within safety filter.';
