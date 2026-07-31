-- KitPlatform 251: FamilyOS Team Nudge (TP3 Sibling Nudge)
-- Depends on: 192, 201
-- Local / family-os pilot manifest only.

CREATE TABLE IF NOT EXISTS pack_family.team_nudge (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    flow_date        DATE NOT NULL,
    from_member_id   UUID NOT NULL REFERENCES pack_family.membership(id),
    to_member_id     UUID NOT NULL REFERENCES pack_family.membership(id),
    commitment_id    UUID REFERENCES pack_family.commitment(id),
    template_code    VARCHAR(32) NOT NULL,
    message_vi       VARCHAR(280) NOT NULL,
    status           VARCHAR(24) NOT NULL DEFAULT 'draft',
    sent_at          TIMESTAMPTZ,
    ack_at           TIMESTAMPTZ,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_team_nudge_status CHECK (
        status IN ('draft', 'sent', 'seen', 'thanks', 'deferred')
    ),
    CONSTRAINT ck_team_nudge_template CHECK (
        template_code IN ('cheer_up', 'one_left', 'you_got_this')
    ),
    CONSTRAINT ck_team_nudge_distinct CHECK (from_member_id <> to_member_id)
);

CREATE INDEX IF NOT EXISTS ix_team_nudge_family_date
    ON pack_family.team_nudge (family_id, flow_date, status)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_team_nudge_to_member
    ON pack_family.team_nudge (to_member_id, flow_date, status)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_team_nudge_from_day
    ON pack_family.team_nudge (family_id, from_member_id, flow_date)
    WHERE deleted_at IS NULL AND status = 'sent';

DROP TRIGGER IF EXISTS trg_team_nudge_row_version ON pack_family.team_nudge;
CREATE TRIGGER trg_team_nudge_row_version
    BEFORE UPDATE ON pack_family.team_nudge
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.team_nudge ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.team_nudge;
CREATE POLICY tenant_isolation ON pack_family.team_nudge
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.team_nudge IS
    'TP3 Sibling Nudge — child invites child with fixed templates; never approves done.';
