-- KitPlatform 264: Relationship trigger UI state (RE P1.2 — lời chưa gửi)
-- Depends on: 257_pack_family_parent_voice
-- Local / family-os pilot manifest only — NOT migration-files.prod.txt
-- Persist suggested → opened → sent | dismissed per viewer · day (survives device switch).

CREATE TABLE IF NOT EXISTS pack_family.relationship_trigger_state (
    id                 UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id          UUID NOT NULL REFERENCES public.tenants(id),
    family_id          UUID NOT NULL REFERENCES pack_family.family(id),
    viewer_member_id   UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date          DATE NOT NULL,
    trigger_code       VARCHAR(48) NOT NULL,
    to_member_id       UUID REFERENCES pack_family.membership(id),
    state              VARCHAR(24) NOT NULL,
    draft_body_vi      VARCHAR(380),
    template_code      VARCHAR(32),
    title_vi           VARCHAR(160),
    body_vi            VARCHAR(380),
    row_version        INT NOT NULL DEFAULT 0,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by         UUID,
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by         UUID,
    deleted_at         TIMESTAMPTZ,
    CONSTRAINT ck_rel_trigger_state CHECK (
        state IN ('opened', 'dismissed', 'sent')
    ),
    CONSTRAINT ck_rel_trigger_viewer_to CHECK (
        to_member_id IS NULL OR viewer_member_id <> to_member_id
    )
);

-- Nullable to_member_id: coalesce to zero-uuid for uniqueness.
CREATE UNIQUE INDEX IF NOT EXISTS uq_rel_trigger_state_viewer_day
    ON pack_family.relationship_trigger_state (
        tenant_id,
        family_id,
        viewer_member_id,
        flow_date,
        trigger_code,
        (COALESCE(to_member_id, '00000000-0000-0000-0000-000000000000'::uuid))
    )
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_rel_trigger_state_viewer_day
    ON pack_family.relationship_trigger_state (family_id, viewer_member_id, flow_date)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_rel_trigger_state_row_version
    ON pack_family.relationship_trigger_state;
CREATE TRIGGER trg_rel_trigger_state_row_version
    BEFORE UPDATE ON pack_family.relationship_trigger_state
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.relationship_trigger_state ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.relationship_trigger_state;
CREATE POLICY tenant_isolation ON pack_family.relationship_trigger_state
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.relationship_trigger_state IS
    'RE P1.2 — parent/viewer trigger card state: opened (unsent draft) | dismissed | sent.';
