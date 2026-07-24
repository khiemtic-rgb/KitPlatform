-- KitPlatform 192: Pack:FamilyOS Starter — schema + module + family daily flow
-- Depends on: 079 (kit_provision_pack_workspace), 071+ (kit_uuid_v7, kit_rls, kit_bump_row_version)
-- Layer: Pack:FamilyOS
-- Product: Routine → Commitment → Progress (not Task/chore surveillance)
--
-- Vertical CHECK `family` is optional and owner-only:
--   migrations/192a_pack_family_os_vertical_owner.sql
-- Local demo tenant can use business_vertical=hybrid until that file is applied.

-- =============================================================================
-- Schema
-- =============================================================================
CREATE SCHEMA IF NOT EXISTS pack_family;

COMMENT ON SCHEMA pack_family IS
    'FamilyOS Starter — One Family. One Plan. One Daily Flow. No finance/health/GPS/school.';

-- =============================================================================
-- Platform module + tenant package
-- =============================================================================
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT v.code, v.name, v.description, v.verticals, v.sort_order
FROM (
    VALUES
        (
            'family_os',
            'FamilyOS',
            'Family daily flow — routine, commitment, progress, contextual reminders',
            ARRAY['family', 'hybrid'],
            60
        )
) AS v(code, name, description, verticals, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = v.code
);

INSERT INTO kit_tenant.tenant_package (
    package_code, package_name, description, verticals, module_codes, sort_order
)
VALUES (
    'family_os',
    'FamilyOS Starter',
    'One Family. One Plan. One Daily Flow — nuclear family coordination (ages 4–17)',
    ARRAY['family', 'hybrid'],
    ARRAY['family_os'],
    40
)
ON CONFLICT (package_code) DO UPDATE SET
    package_name = EXCLUDED.package_name,
    description = EXCLUDED.description,
    verticals = EXCLUDED.verticals,
    module_codes = EXCLUDED.module_codes,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

-- =============================================================================
-- family — household unit (aggregate root)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.family (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    workspace_id     UUID REFERENCES kit_workspace.workspace_workspace(id),
    display_name     VARCHAR(120) NOT NULL,
    timezone         VARCHAR(64) NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    status           VARCHAR(20) NOT NULL DEFAULT 'active',
    settings         JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_family_status CHECK (status IN ('active', 'archived'))
);

CREATE INDEX IF NOT EXISTS ix_family_tenant_status
    ON pack_family.family (tenant_id, status)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_family_row_version ON pack_family.family;
CREATE TRIGGER trg_family_row_version
    BEFORE UPDATE ON pack_family.family
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family;
CREATE POLICY tenant_isolation ON pack_family.family
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family IS
    'FamilyOS household unit — not pharmacy family_members dependents list.';

-- =============================================================================
-- membership — people in a family (children may have no account)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.membership (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    display_name     VARCHAR(120) NOT NULL,
    role_code        VARCHAR(20) NOT NULL,
    date_of_birth    DATE,
    account_id       UUID REFERENCES public.customer_accounts(id),
    sort_order       INT NOT NULL DEFAULT 0,
    status           VARCHAR(20) NOT NULL DEFAULT 'active',
    metadata         JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_membership_role CHECK (
        role_code IN ('guardian', 'caregiver', 'child', 'viewer')
    ),
    CONSTRAINT ck_membership_status CHECK (status IN ('active', 'invited', 'archived'))
);

CREATE INDEX IF NOT EXISTS ix_membership_family
    ON pack_family.membership (family_id, status, sort_order)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_membership_tenant_account
    ON pack_family.membership (tenant_id, account_id)
    WHERE deleted_at IS NULL AND account_id IS NOT NULL;

DROP TRIGGER IF EXISTS trg_membership_row_version ON pack_family.membership;
CREATE TRIGGER trg_membership_row_version
    BEFORE UPDATE ON pack_family.membership
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.membership ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.membership;
CREATE POLICY tenant_isolation ON pack_family.membership
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.membership IS
    'Family member profile; account_id optional (children without login).';

COMMENT ON COLUMN pack_family.membership.role_code IS
    'guardian | caregiver | child | viewer — governance roles, not ERP RBAC.';

-- =============================================================================
-- routine — life rhythm template (school day, weekend, …)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.routine (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    code             VARCHAR(40) NOT NULL,
    display_name     VARCHAR(120) NOT NULL,
    kind             VARCHAR(30) NOT NULL DEFAULT 'custom',
    -- ISO weekday bits: 1=Mon … 7=Sun; empty = manual / on-demand
    weekdays         SMALLINT[] NOT NULL DEFAULT '{}',
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order       INT NOT NULL DEFAULT 0,
    settings         JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT uq_routine_family_code UNIQUE (family_id, code),
    CONSTRAINT ck_routine_kind CHECK (
        kind IN ('school_day', 'weekend', 'holiday', 'exam', 'travel', 'custom')
    )
);

CREATE INDEX IF NOT EXISTS ix_routine_family_active
    ON pack_family.routine (family_id, is_active, sort_order)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_routine_row_version ON pack_family.routine;
CREATE TRIGGER trg_routine_row_version
    BEFORE UPDATE ON pack_family.routine
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.routine ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.routine;
CREATE POLICY tenant_isolation ON pack_family.routine
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.routine IS
    'Family life rhythm template — not a flat to-do list.';

-- =============================================================================
-- commitment_template — commitments inside a routine
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.commitment_template (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    routine_id       UUID NOT NULL REFERENCES pack_family.routine(id),
    member_id        UUID REFERENCES pack_family.membership(id),
    title            VARCHAR(200) NOT NULL,
    description      TEXT,
    -- local time-of-day window within the family timezone
    window_start     TIME,
    window_end       TIME,
    sort_order       INT NOT NULL DEFAULT 0,
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    metadata         JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_commitment_template_routine
    ON pack_family.commitment_template (routine_id, sort_order)
    WHERE deleted_at IS NULL AND is_active;

DROP TRIGGER IF EXISTS trg_commitment_template_row_version ON pack_family.commitment_template;
CREATE TRIGGER trg_commitment_template_row_version
    BEFORE UPDATE ON pack_family.commitment_template
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.commitment_template ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.commitment_template;
CREATE POLICY tenant_isolation ON pack_family.commitment_template
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.commitment_template IS
    'Commitment (not Task) — responsibility/habit inside a routine.';

-- =============================================================================
-- day_flow — daily instance of a routine (the shared Daily Flow)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.day_flow (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    routine_id       UUID NOT NULL REFERENCES pack_family.routine(id),
    flow_date        DATE NOT NULL,
    status           VARCHAR(20) NOT NULL DEFAULT 'open',
    notes            TEXT,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT uq_day_flow_family_date UNIQUE (family_id, flow_date),
    CONSTRAINT ck_day_flow_status CHECK (status IN ('open', 'closed', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS ix_day_flow_tenant_date
    ON pack_family.day_flow (tenant_id, flow_date)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_day_flow_row_version ON pack_family.day_flow;
CREATE TRIGGER trg_day_flow_row_version
    BEFORE UPDATE ON pack_family.day_flow
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.day_flow ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.day_flow;
CREATE POLICY tenant_isolation ON pack_family.day_flow
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.day_flow IS
    'Shared Daily Flow for one family on one calendar day.';

-- =============================================================================
-- commitment — daily instance (progress lives here)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.commitment (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    day_flow_id      UUID NOT NULL REFERENCES pack_family.day_flow(id),
    template_id      UUID REFERENCES pack_family.commitment_template(id),
    member_id        UUID REFERENCES pack_family.membership(id),
    title            VARCHAR(200) NOT NULL,
    description      TEXT,
    window_start     TIME,
    window_end       TIME,
    sort_order       INT NOT NULL DEFAULT 0,
    status           VARCHAR(20) NOT NULL DEFAULT 'pending',
    completed_at     TIMESTAMPTZ,
    completed_by     UUID,
    skip_reason      TEXT,
    metadata         JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_commitment_status CHECK (
        status IN ('pending', 'in_progress', 'done', 'skipped')
    )
);

CREATE INDEX IF NOT EXISTS ix_commitment_day_flow
    ON pack_family.commitment (day_flow_id, sort_order)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_commitment_member_status
    ON pack_family.commitment (member_id, status)
    WHERE deleted_at IS NULL AND member_id IS NOT NULL;

DROP TRIGGER IF EXISTS trg_commitment_row_version ON pack_family.commitment;
CREATE TRIGGER trg_commitment_row_version
    BEFORE UPDATE ON pack_family.commitment
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.commitment ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.commitment;
CREATE POLICY tenant_isolation ON pack_family.commitment
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.commitment IS
    'Daily commitment instance — Progress = status/completed_at on this row.';

-- =============================================================================
-- agreement — Family Agreement stub (F2+; schema ready, APIs later)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.agreement (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    proposed_by      UUID NOT NULL REFERENCES pack_family.membership(id),
    title            VARCHAR(200) NOT NULL,
    proposal_body    TEXT NOT NULL,
    target_type      VARCHAR(40) NOT NULL DEFAULT 'routine_change',
    target_id        UUID,
    status           VARCHAR(20) NOT NULL DEFAULT 'proposed',
    decided_at       TIMESTAMPTZ,
    decided_by       UUID,
    decision_note    TEXT,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_agreement_status CHECK (
        status IN ('proposed', 'discussing', 'accepted', 'rejected', 'withdrawn')
    ),
    CONSTRAINT ck_agreement_target CHECK (
        target_type IN ('routine_change', 'commitment_change', 'other')
    )
);

CREATE INDEX IF NOT EXISTS ix_agreement_family_status
    ON pack_family.agreement (family_id, status)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_agreement_row_version ON pack_family.agreement;
CREATE TRIGGER trg_agreement_row_version
    BEFORE UPDATE ON pack_family.agreement
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.agreement ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.agreement;
CREATE POLICY tenant_isolation ON pack_family.agreement
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.agreement IS
    'Family Agreement (đồng thuận đổi routine) — F2+. Distinct from data/AI consent.';

-- App role grants (safe if role missing / already owner)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        EXECUTE 'GRANT USAGE, CREATE ON SCHEMA pack_family TO kitplatform';
        EXECUTE 'GRANT ALL ON ALL TABLES IN SCHEMA pack_family TO kitplatform';
        EXECUTE 'GRANT ALL ON ALL SEQUENCES IN SCHEMA pack_family TO kitplatform';
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA pack_family GRANT ALL ON TABLES TO kitplatform';
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA pack_family GRANT ALL ON SEQUENCES TO kitplatform';
    END IF;
END $$;
