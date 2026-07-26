-- KitPlatform 226: FamilyOS Parent Progress (opt-in) — P0.1 Family Transformation
-- Depends on: 192_pack_family_os.sql (family, membership)
-- Parents/caregivers set their own light goals + manual daily check-ins.
-- Privacy-first: share_with_family defaults FALSE — children never see parent
-- progress unless the parent explicitly opts in. No screen-time auto-tracking.

-- =============================================================================
-- parent_goal — a guardian/caregiver personal habit goal
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.parent_goal (
    id                   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id            UUID NOT NULL REFERENCES public.tenants(id),
    family_id            UUID NOT NULL REFERENCES pack_family.family(id),
    member_id            UUID NOT NULL REFERENCES pack_family.membership(id),
    title                VARCHAR(200) NOT NULL,
    emoji                VARCHAR(16),
    target_days_per_week SMALLINT NOT NULL DEFAULT 5,
    share_with_family    BOOLEAN NOT NULL DEFAULT FALSE,
    is_active            BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order           INT NOT NULL DEFAULT 0,
    row_version          INT NOT NULL DEFAULT 0,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by           UUID,
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by           UUID,
    deleted_at           TIMESTAMPTZ,
    CONSTRAINT ck_parent_goal_target CHECK (target_days_per_week BETWEEN 1 AND 7)
);

CREATE INDEX IF NOT EXISTS ix_parent_goal_member
    ON pack_family.parent_goal (family_id, member_id, is_active, sort_order)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_parent_goal_shared
    ON pack_family.parent_goal (family_id, share_with_family)
    WHERE deleted_at IS NULL AND is_active AND share_with_family;

DROP TRIGGER IF EXISTS trg_parent_goal_row_version ON pack_family.parent_goal;
CREATE TRIGGER trg_parent_goal_row_version
    BEFORE UPDATE ON pack_family.parent_goal
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.parent_goal ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.parent_goal;
CREATE POLICY tenant_isolation ON pack_family.parent_goal
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.parent_goal IS
    'Parent Progress — guardian/caregiver personal habit goal; opt-in share only.';
COMMENT ON COLUMN pack_family.parent_goal.share_with_family IS
    'When TRUE, this goal + check-ins appear in the family mirror/home; default private.';

-- =============================================================================
-- parent_goal_checkin — manual daily check-in (done / skip)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.parent_goal_checkin (
    id           UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id    UUID NOT NULL REFERENCES public.tenants(id),
    goal_id      UUID NOT NULL REFERENCES pack_family.parent_goal(id),
    member_id    UUID NOT NULL REFERENCES pack_family.membership(id),
    checkin_date DATE NOT NULL,
    status       VARCHAR(12) NOT NULL DEFAULT 'done',
    note         TEXT,
    row_version  INT NOT NULL DEFAULT 0,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by   UUID,
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by   UUID,
    deleted_at   TIMESTAMPTZ,
    CONSTRAINT uq_parent_goal_checkin_day UNIQUE (goal_id, checkin_date),
    CONSTRAINT ck_parent_goal_checkin_status CHECK (status IN ('done', 'skip'))
);

CREATE INDEX IF NOT EXISTS ix_parent_goal_checkin_goal_date
    ON pack_family.parent_goal_checkin (goal_id, checkin_date)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_parent_goal_checkin_row_version ON pack_family.parent_goal_checkin;
CREATE TRIGGER trg_parent_goal_checkin_row_version
    BEFORE UPDATE ON pack_family.parent_goal_checkin
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.parent_goal_checkin ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.parent_goal_checkin;
CREATE POLICY tenant_isolation ON pack_family.parent_goal_checkin
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.parent_goal_checkin IS
    'Manual daily check-in for a parent goal — no automatic screen-time tracking.';

-- App role grants (safe if role missing / already owner)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        EXECUTE 'GRANT ALL ON pack_family.parent_goal TO kitplatform';
        EXECUTE 'GRANT ALL ON pack_family.parent_goal_checkin TO kitplatform';
    END IF;
END $$;
