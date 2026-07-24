-- KitPlatform 201: FamilyOS Team Unlock (TP2)
-- Depends on: 192
-- Local / pilot only until deploy is explicitly approved.

CREATE TABLE IF NOT EXISTS pack_family.team_unlock_event (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    day_flow_id      UUID NOT NULL REFERENCES pack_family.day_flow(id),
    flow_date        DATE NOT NULL,
    reward_code      VARCHAR(64) NOT NULL,
    label_vi         VARCHAR(160) NOT NULL,
    agreement_id     UUID REFERENCES pack_family.agreement(id),
    team_done        INT NOT NULL DEFAULT 0,
    team_total       INT NOT NULL DEFAULT 0,
    team_percent     INT NOT NULL DEFAULT 0,
    status           VARCHAR(24) NOT NULL DEFAULT 'pending_confirm',
    confirmed_by     UUID REFERENCES pack_family.membership(id),
    confirmed_at     TIMESTAMPTZ,
    decision_note    TEXT,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_team_unlock_status CHECK (
        status IN ('pending_confirm', 'confirmed', 'deferred')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_team_unlock_family_day_reward
    ON pack_family.team_unlock_event (tenant_id, family_id, flow_date, reward_code);

CREATE INDEX IF NOT EXISTS ix_team_unlock_family_date
    ON pack_family.team_unlock_event (family_id, flow_date, status)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_team_unlock_event_row_version ON pack_family.team_unlock_event;
CREATE TRIGGER trg_team_unlock_event_row_version
    BEFORE UPDATE ON pack_family.team_unlock_event
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.team_unlock_event ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.team_unlock_event;
CREATE POLICY tenant_isolation ON pack_family.team_unlock_event
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.team_unlock_event IS
    'TP2 Team Unlock — family reward when child Missions complete; parent must confirm. Never auto-grant.';
