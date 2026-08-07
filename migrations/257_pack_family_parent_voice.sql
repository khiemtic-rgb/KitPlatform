-- KitPlatform 257: FamilyOS parent_voice + memory kind (Relationship Engine P0)
-- Depends on: 192, 201, 231, 252
-- Local / family-os pilot manifest only — NOT migration-files.prod.txt

-- Widen family_memory.kind for parent → child voice.
ALTER TABLE pack_family.family_memory
    DROP CONSTRAINT IF EXISTS ck_family_memory_kind;

ALTER TABLE pack_family.family_memory
    ADD CONSTRAINT ck_family_memory_kind CHECK (
        kind IN (
            'beautiful_day',
            'streak_milestone',
            'gratitude',
            'photo',
            'team_unlock',
            'reward',
            'first_time',
            'manual',
            'help',
            'team_day',
            'parent_habit',
            'parent_voice',
            'evening_circle',
            -- Include later kinds so replaying this mig after 272 (or pilot data) stays idempotent.
            'kid_moment'
        )
    );

CREATE TABLE IF NOT EXISTS pack_family.parent_voice_message (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    flow_date        DATE NOT NULL,
    from_member_id   UUID NOT NULL REFERENCES pack_family.membership(id),
    to_member_id     UUID NOT NULL REFERENCES pack_family.membership(id),
    template_code    VARCHAR(32) NOT NULL,
    body_vi          VARCHAR(380) NOT NULL,
    status           VARCHAR(24) NOT NULL DEFAULT 'sent',
    sent_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ack_at           TIMESTAMPTZ,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_parent_voice_status CHECK (
        status IN ('sent', 'read', 'thanks')
    ),
    CONSTRAINT ck_parent_voice_template CHECK (
        template_code IN ('praise', 'encourage', 'custom')
    ),
    CONSTRAINT ck_parent_voice_distinct CHECK (from_member_id <> to_member_id)
);

CREATE INDEX IF NOT EXISTS ix_parent_voice_to_member
    ON pack_family.parent_voice_message (to_member_id, flow_date, status)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_parent_voice_from_day
    ON pack_family.parent_voice_message (family_id, from_member_id, flow_date)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_parent_voice_family_date
    ON pack_family.parent_voice_message (family_id, flow_date, status)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_parent_voice_row_version ON pack_family.parent_voice_message;
CREATE TRIGGER trg_parent_voice_row_version
    BEFORE UPDATE ON pack_family.parent_voice_message
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.parent_voice_message ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.parent_voice_message;
CREATE POLICY tenant_isolation ON pack_family.parent_voice_message
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.parent_voice_message IS
    'RE P0 — parent/guardian sends praise/encourage to child; AI never speaks as parent.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['pharmacore', 'kitplatform'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format(
        'GRANT SELECT, INSERT, UPDATE ON TABLE pack_family.parent_voice_message TO %I',
        r);
    END IF;
  END LOOP;
END $$;
