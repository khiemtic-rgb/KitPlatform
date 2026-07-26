-- KitPlatform 227: FamilyOS weekly Family Challenge (P0.3 Transformation)
-- Depends on: 192 (family, membership), 201 (team_unlock_event optional reward hook)
-- One challenge per family per calendar week. Legs for parents, children, household.
-- Completing all legs opens a pending Team Unlock (e.g. Movie Night). No shaming.

CREATE TABLE IF NOT EXISTS pack_family.family_challenge (
    id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id     UUID NOT NULL REFERENCES public.tenants(id),
    family_id     UUID NOT NULL REFERENCES pack_family.family(id),
    week_start    DATE NOT NULL,
    status        VARCHAR(20) NOT NULL DEFAULT 'active',
    title         VARCHAR(200) NOT NULL DEFAULT 'Challenge tuần này',
    reward_code   VARCHAR(80) NOT NULL DEFAULT 'reward_choose_movie_sat',
    reward_label  VARCHAR(120) NOT NULL DEFAULT 'Movie Night',
    accepted_by   UUID REFERENCES pack_family.membership(id),
    completed_at  TIMESTAMPTZ,
    unlock_id     UUID,
    row_version   INT NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by    UUID,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by    UUID,
    deleted_at    TIMESTAMPTZ,
    CONSTRAINT uq_family_challenge_week UNIQUE (tenant_id, family_id, week_start),
    CONSTRAINT ck_family_challenge_status CHECK (
        status IN ('active', 'completed', 'expired', 'canceled')
    )
);

CREATE INDEX IF NOT EXISTS ix_family_challenge_family_week
    ON pack_family.family_challenge (family_id, week_start DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_family_challenge_row_version ON pack_family.family_challenge;
CREATE TRIGGER trg_family_challenge_row_version
    BEFORE UPDATE ON pack_family.family_challenge
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_challenge ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_challenge;
CREATE POLICY tenant_isolation ON pack_family.family_challenge
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_challenge IS
    'Weekly whole-family challenge — parents + kids + shared leg → Movie Night.';

CREATE TABLE IF NOT EXISTS pack_family.family_challenge_leg (
    id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id     UUID NOT NULL REFERENCES public.tenants(id),
    challenge_id  UUID NOT NULL REFERENCES pack_family.family_challenge(id),
    member_id     UUID REFERENCES pack_family.membership(id),
    leg_kind      VARCHAR(20) NOT NULL,
    title         VARCHAR(200) NOT NULL,
    emoji         VARCHAR(16),
    target_days   SMALLINT NOT NULL DEFAULT 5,
    done_days     SMALLINT NOT NULL DEFAULT 0,
    sort_order    INT NOT NULL DEFAULT 0,
    row_version   INT NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by    UUID,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by    UUID,
    deleted_at    TIMESTAMPTZ,
    CONSTRAINT ck_family_challenge_leg_kind CHECK (
        leg_kind IN ('parent', 'child', 'household')
    ),
    CONSTRAINT ck_family_challenge_leg_target CHECK (target_days BETWEEN 1 AND 7),
    CONSTRAINT ck_family_challenge_leg_done CHECK (done_days >= 0 AND done_days <= 7)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_family_challenge_leg_member
    ON pack_family.family_challenge_leg (challenge_id, member_id)
    WHERE deleted_at IS NULL AND member_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_family_challenge_leg_household
    ON pack_family.family_challenge_leg (challenge_id)
    WHERE deleted_at IS NULL AND leg_kind = 'household';

CREATE INDEX IF NOT EXISTS ix_family_challenge_leg_challenge
    ON pack_family.family_challenge_leg (challenge_id, sort_order)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_family_challenge_leg_row_version ON pack_family.family_challenge_leg;
CREATE TRIGGER trg_family_challenge_leg_row_version
    BEFORE UPDATE ON pack_family.family_challenge_leg
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_challenge_leg ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_challenge_leg;
CREATE POLICY tenant_isolation ON pack_family.family_challenge_leg
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_challenge_leg IS
    'One challenge leg per parent/child or a shared household leg.';

-- Daily check-in rows (manual) — allows undo within the day
CREATE TABLE IF NOT EXISTS pack_family.family_challenge_checkin (
    id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id     UUID NOT NULL REFERENCES public.tenants(id),
    leg_id        UUID NOT NULL REFERENCES pack_family.family_challenge_leg(id),
    checkin_date  DATE NOT NULL,
    status        VARCHAR(12) NOT NULL DEFAULT 'done',
    checked_by    UUID REFERENCES pack_family.membership(id),
    row_version   INT NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at    TIMESTAMPTZ,
    CONSTRAINT uq_family_challenge_checkin_day UNIQUE (leg_id, checkin_date),
    CONSTRAINT ck_family_challenge_checkin_status CHECK (status IN ('done', 'skip'))
);

CREATE INDEX IF NOT EXISTS ix_family_challenge_checkin_leg
    ON pack_family.family_challenge_checkin (leg_id, checkin_date)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_family_challenge_checkin_row_version ON pack_family.family_challenge_checkin;
CREATE TRIGGER trg_family_challenge_checkin_row_version
    BEFORE UPDATE ON pack_family.family_challenge_checkin
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_challenge_checkin ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_challenge_checkin;
CREATE POLICY tenant_isolation ON pack_family.family_challenge_checkin
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        EXECUTE 'GRANT ALL ON pack_family.family_challenge TO kitplatform';
        EXECUTE 'GRANT ALL ON pack_family.family_challenge_leg TO kitplatform';
        EXECUTE 'GRANT ALL ON pack_family.family_challenge_checkin TO kitplatform';
    END IF;
END $$;
