-- KitPlatform 195: FamilyOS F2.5 L3 — consequence events (parent confirm)
-- Depends on: 192, 193, 194
-- Local / pilot only until deploy is explicitly approved

CREATE TABLE IF NOT EXISTS pack_family.consequence_event (
    id                   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id            UUID NOT NULL REFERENCES public.tenants(id),
    family_id            UUID NOT NULL REFERENCES pack_family.family(id),
    day_flow_id          UUID NOT NULL REFERENCES pack_family.day_flow(id),
    commitment_id        UUID NOT NULL REFERENCES pack_family.commitment(id),
    agreement_id         UUID NOT NULL REFERENCES pack_family.agreement(id),
    member_id            UUID REFERENCES pack_family.membership(id),
    flow_date            DATE NOT NULL,
    consequence_code     VARCHAR(64) NOT NULL,
    label_vi             VARCHAR(160) NOT NULL,
    trigger_skip_reason  VARCHAR(40),
    commitment_title     VARCHAR(200) NOT NULL DEFAULT '',
    status               VARCHAR(20) NOT NULL DEFAULT 'pending_confirm',
    decided_by           UUID REFERENCES pack_family.membership(id),
    decided_at           TIMESTAMPTZ,
    decision_note        TEXT,
    row_version          INT NOT NULL DEFAULT 0,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by           UUID,
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by           UUID,
    deleted_at           TIMESTAMPTZ,
    CONSTRAINT ck_consequence_event_status CHECK (
        status IN ('pending_confirm', 'applied', 'waived')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_consequence_event_commitment_agreement
    ON pack_family.consequence_event (tenant_id, commitment_id, agreement_id)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_consequence_event_family_date
    ON pack_family.consequence_event (family_id, flow_date, status)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_consequence_event_row_version ON pack_family.consequence_event;
CREATE TRIGGER trg_consequence_event_row_version
    BEFORE UPDATE ON pack_family.consequence_event
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.consequence_event ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.consequence_event;
CREATE POLICY tenant_isolation ON pack_family.consequence_event
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.consequence_event IS
    'L3 Family Consequence — suggested from accepted Agreement on skip; parent confirms apply/waive. Never auto-punish.';
