-- KitPlatform 249: Family Growth Blueprint™ — sparse JSON SoT (Wave A)
-- Depends on: 192_pack_family_os.sql (family)
-- Local / Family OS park: family-os migration manifest only.
-- Blueprint is NOT an 8-layer Settings form — engines read sparse layers;
-- DNA card is the parent-facing snapshot (Stage / Values / Focus / Next).

CREATE TABLE IF NOT EXISTS pack_family.family_blueprint (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    layers_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
    dna_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    schema_version   INT NOT NULL DEFAULT 1,
    hydrated_at      TIMESTAMPTZ,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_family_blueprint_family
    ON pack_family.family_blueprint (tenant_id, family_id);

DROP TRIGGER IF EXISTS trg_family_blueprint_row_version ON pack_family.family_blueprint;
CREATE TRIGGER trg_family_blueprint_row_version
    BEFORE UPDATE ON pack_family.family_blueprint
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_blueprint ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_blueprint;
CREATE POLICY tenant_isolation ON pack_family.family_blueprint
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_blueprint IS
    'Family Growth Blueprint™ — sparse 8-layer context + DNA card snapshot. Wave A: L1/L6/L8 from onboarding.';
COMMENT ON COLUMN pack_family.family_blueprint.layers_json IS
    'Sparse layers: profile, stage, child, context, style, values, resources, goals (+ optional focus).';
COMMENT ON COLUMN pack_family.family_blueprint.dna_json IS
    'Parent-facing DNA card: stageLabelVi, valuesLabelsVi, focusLabelsVi, nextStepVi.';
