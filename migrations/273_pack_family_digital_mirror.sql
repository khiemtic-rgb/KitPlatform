-- KitPlatform 273: FamilyOS Daily Digital Mirror M1 (agent heartbeat + usage + parent note)
-- Depends on: 192+
-- Local / family-os pilot manifest only — NOT migration-files.prod.txt

CREATE TABLE IF NOT EXISTS pack_family.mirror_agent_device (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    family_id           UUID NOT NULL REFERENCES pack_family.family(id),
    member_id           UUID NOT NULL REFERENCES pack_family.membership(id),
    device_id           VARCHAR(80) NOT NULL,
    device_label        VARCHAR(120),
    agent_version       VARCHAR(40),
    last_heartbeat_at   TIMESTAMPTZ,
    last_foreground_app VARCHAR(160),
    status              VARCHAR(24) NOT NULL DEFAULT 'online',
    row_version         INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT ck_mirror_agent_device_status CHECK (
        status IN ('online', 'offline')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_mirror_agent_device_family_device
    ON pack_family.mirror_agent_device (tenant_id, family_id, device_id)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_mirror_agent_device_member_heartbeat
    ON pack_family.mirror_agent_device (family_id, member_id, last_heartbeat_at DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_mirror_agent_device_row_version ON pack_family.mirror_agent_device;
CREATE TRIGGER trg_mirror_agent_device_row_version
    BEFORE UPDATE ON pack_family.mirror_agent_device
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.mirror_agent_device ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.mirror_agent_device;
CREATE POLICY tenant_isolation ON pack_family.mirror_agent_device
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.mirror_agent_device IS
    'Windows Famixa Agent heartbeat per child device — no blocking; parent evening mirror only.';

CREATE TABLE IF NOT EXISTS pack_family.mirror_usage_day (
    id          UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id   UUID NOT NULL REFERENCES public.tenants(id),
    family_id   UUID NOT NULL REFERENCES pack_family.family(id),
    member_id   UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date   DATE NOT NULL,
    app_key     VARCHAR(120) NOT NULL,
    app_label   VARCHAR(160),
    kind        VARCHAR(16) NOT NULL DEFAULT 'app',
    seconds     INT NOT NULL DEFAULT 0,
    row_version INT NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by  UUID,
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  UUID,
    deleted_at  TIMESTAMPTZ,
    CONSTRAINT uq_mirror_usage_day_app UNIQUE (
        tenant_id, family_id, member_id, flow_date, app_key, kind
    ),
    CONSTRAINT ck_mirror_usage_day_kind CHECK (
        kind IN ('app', 'web')
    ),
    CONSTRAINT ck_mirror_usage_day_seconds CHECK (seconds >= 0)
);

CREATE INDEX IF NOT EXISTS ix_mirror_usage_day_family_member_date
    ON pack_family.mirror_usage_day (family_id, member_id, flow_date)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_mirror_usage_day_row_version ON pack_family.mirror_usage_day;
CREATE TRIGGER trg_mirror_usage_day_row_version
    BEFORE UPDATE ON pack_family.mirror_usage_day
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.mirror_usage_day ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.mirror_usage_day;
CREATE POLICY tenant_isolation ON pack_family.mirror_usage_day
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.mirror_usage_day IS
    'Aggregated app/web seconds per child per flow day — additive ingest from agent.';

CREATE TABLE IF NOT EXISTS pack_family.mirror_parent_note (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    family_id           UUID NOT NULL REFERENCES pack_family.family(id),
    member_id           UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date           DATE NOT NULL,
    from_membership_id  UUID NOT NULL REFERENCES pack_family.membership(id),
    tone                VARCHAR(24) NOT NULL,
    body_vi             TEXT NOT NULL,
    row_version         INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          UUID,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by          UUID,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT ck_mirror_parent_note_tone CHECK (
        tone IN ('praise', 'soft', 'renegotiate')
    )
);

CREATE INDEX IF NOT EXISTS ix_mirror_parent_note_family_day
    ON pack_family.mirror_parent_note (family_id, member_id, flow_date, created_at DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_mirror_parent_note_row_version ON pack_family.mirror_parent_note;
CREATE TRIGGER trg_mirror_parent_note_row_version
    BEFORE UPDATE ON pack_family.mirror_parent_note
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.mirror_parent_note ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.mirror_parent_note;
CREATE POLICY tenant_isolation ON pack_family.mirror_parent_note
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.mirror_parent_note IS
    'Parent evening mirror note to child — warm tone, max ~5 per parent per child per day (app enforced).';
