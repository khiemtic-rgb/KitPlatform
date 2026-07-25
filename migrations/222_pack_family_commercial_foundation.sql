-- Family OS commercial foundation
-- Auth: membership.user_id + family_invite + parent_pin_hash
-- Billing: family_subscription (trial/active/expired)
-- Harden: star_ledger RLS + family FK
-- Depends on: 221_pack_family_calendar_period.sql, 218_pack_family_reward_catalog.sql

-- =============================================================================
-- 1) Link platform user ↔ guardian membership
-- =============================================================================
ALTER TABLE pack_family.membership
    ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES public.users(id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_membership_user
    ON pack_family.membership (tenant_id, user_id)
    WHERE deleted_at IS NULL AND user_id IS NOT NULL;

COMMENT ON COLUMN pack_family.membership.user_id IS
    'Platform staff/parent user linked to this membership (guardians). Children stay NULL.';

-- =============================================================================
-- 2) Parent PIN (server-side hash; device PIN still allowed as cache)
-- =============================================================================
ALTER TABLE pack_family.family
    ADD COLUMN IF NOT EXISTS parent_pin_hash TEXT;

COMMENT ON COLUMN pack_family.family.parent_pin_hash IS
    'BCrypt hash of 4-digit parent PIN. NULL = not set (device-local PIN only).';

-- =============================================================================
-- 3) Co-parent invite codes
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.family_invite (
    id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id     UUID NOT NULL REFERENCES public.tenants(id),
    family_id     UUID NOT NULL REFERENCES pack_family.family(id),
    code          VARCHAR(16) NOT NULL,
    role_code     VARCHAR(20) NOT NULL DEFAULT 'guardian',
    expires_at    TIMESTAMPTZ NOT NULL,
    max_uses      INT NOT NULL DEFAULT 3,
    used_count    INT NOT NULL DEFAULT 0,
    created_by    UUID,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at    TIMESTAMPTZ,
    CONSTRAINT ck_family_invite_role CHECK (role_code IN ('guardian', 'caregiver', 'viewer')),
    CONSTRAINT ck_family_invite_uses CHECK (max_uses >= 1 AND used_count >= 0),
    CONSTRAINT uq_family_invite_code UNIQUE (code)
);

CREATE INDEX IF NOT EXISTS ix_family_invite_family
    ON pack_family.family_invite (family_id)
    WHERE revoked_at IS NULL;

ALTER TABLE pack_family.family_invite ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_invite;
CREATE POLICY tenant_isolation ON pack_family.family_invite
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

COMMENT ON TABLE pack_family.family_invite IS
    'Short-lived codes for co-parent join (accept → user + guardian membership).';

-- =============================================================================
-- 4) Billing / entitlement (trial → active → expired)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.family_subscription (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    plan_code       VARCHAR(40) NOT NULL DEFAULT 'starter_trial',
    status          VARCHAR(20) NOT NULL DEFAULT 'trial',
    trial_ends_at   TIMESTAMPTZ,
    current_period_end TIMESTAMPTZ,
    notes           TEXT,
    settings        JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_family_subscription_family UNIQUE (family_id),
    CONSTRAINT ck_family_subscription_status CHECK (
        status IN ('trial', 'active', 'past_due', 'expired', 'canceled')
    )
);

ALTER TABLE pack_family.family_subscription ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_subscription;
CREATE POLICY tenant_isolation ON pack_family.family_subscription
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

COMMENT ON TABLE pack_family.family_subscription IS
    'Family OS entitlement — trial on register; payment gateway wires status later.';

-- =============================================================================
-- 5) Harden star_ledger isolation
-- =============================================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'star_ledger_family_fk'
    ) THEN
        ALTER TABLE pack_family.star_ledger
            ADD CONSTRAINT star_ledger_family_fk
            FOREIGN KEY (family_id) REFERENCES pack_family.family(id);
    END IF;
END $$;

ALTER TABLE pack_family.star_ledger ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.star_ledger;
CREATE POLICY tenant_isolation ON pack_family.star_ledger
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_star_ledger_tenant_family
    ON pack_family.star_ledger (tenant_id, family_id);
