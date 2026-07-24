-- KitPlatform 218: FamilyOS reward catalog + redemptions + star ledger redeem support
-- Depends on: 217_pack_family_pending_stars.sql

-- Allow star_ledger rows without commitment (redeem deductions)
ALTER TABLE pack_family.star_ledger
    ALTER COLUMN commitment_id DROP NOT NULL;

ALTER TABLE pack_family.star_ledger
    ADD COLUMN IF NOT EXISTS reason VARCHAR(128);

ALTER TABLE pack_family.star_ledger
    DROP CONSTRAINT IF EXISTS star_ledger_commitment_unique;

CREATE UNIQUE INDEX IF NOT EXISTS ux_star_ledger_commitment
    ON pack_family.star_ledger (tenant_id, commitment_id)
    WHERE commitment_id IS NOT NULL;

COMMENT ON COLUMN pack_family.star_ledger.reason IS
    'Optional ledger reason — e.g. redeem:{catalogId} for reward redemptions.';

-- Per-family reward catalog (kid-visible redeem items)
CREATE TABLE IF NOT EXISTS pack_family.reward_catalog (
    id           UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id    UUID NOT NULL REFERENCES public.tenants(id),
    family_id    UUID NOT NULL REFERENCES pack_family.family(id),
    title        VARCHAR(160) NOT NULL,
    icon         VARCHAR(16) NOT NULL DEFAULT '🎁',
    cost         INT,
    sort_order   INT NOT NULL DEFAULT 0,
    tone         VARCHAR(16),
    is_special   BOOLEAN NOT NULL DEFAULT FALSE,
    active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_reward_catalog_cost CHECK (cost IS NULL OR cost > 0)
);

CREATE INDEX IF NOT EXISTS ix_reward_catalog_family
    ON pack_family.reward_catalog (tenant_id, family_id, sort_order)
    WHERE active = TRUE;

ALTER TABLE pack_family.reward_catalog ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.reward_catalog;
CREATE POLICY tenant_isolation ON pack_family.reward_catalog
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.reward_catalog IS
    'Kid treasure redeem catalog — per family; cost NULL = parent-pick special item.';

-- Redemption requests (pending parent confirm by default)
CREATE TABLE IF NOT EXISTS pack_family.reward_redemption (
    id            UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id     UUID NOT NULL REFERENCES public.tenants(id),
    family_id     UUID NOT NULL REFERENCES pack_family.family(id),
    member_id     UUID NOT NULL REFERENCES pack_family.membership(id),
    catalog_id    UUID NOT NULL REFERENCES pack_family.reward_catalog(id),
    star_cost     INT NOT NULL DEFAULT 0,
    status        VARCHAR(24) NOT NULL DEFAULT 'pending',
    ledger_id     UUID REFERENCES pack_family.star_ledger(id),
    fulfilled_by  UUID REFERENCES pack_family.membership(id),
    fulfilled_at  TIMESTAMPTZ,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_reward_redemption_status CHECK (
        status IN ('pending', 'fulfilled', 'cancelled')
    ),
    CONSTRAINT ck_reward_redemption_cost CHECK (star_cost >= 0)
);

CREATE INDEX IF NOT EXISTS ix_reward_redemption_member
    ON pack_family.reward_redemption (tenant_id, family_id, member_id, created_at DESC);

ALTER TABLE pack_family.reward_redemption ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.reward_redemption;
CREATE POLICY tenant_isolation ON pack_family.reward_redemption
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.reward_redemption IS
    'Star reward redemption — stars deducted at request; parent marks fulfilled on Parent Board.';

-- DEMO_FAMILY default catalog (matches kid UI mock)
INSERT INTO pack_family.reward_catalog (
    id, tenant_id, family_id, title, icon, cost, sort_order, tone, is_special, active
)
VALUES
(
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Kem yêu thích', '🍦', 100, 1, 'pink', FALSE, TRUE
),
(
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Đồ chơi nhỏ', '🧸', 500, 2, 'lemon', FALSE, TRUE
),
(
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Sách mới', '📖', 800, 3, 'sky', FALSE, TRUE
),
(
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0004',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    '30 phút chơi game', '🎮', 300, 4, 'mint', FALSE, TRUE
),
(
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0005',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Bố mẹ chọn', '🎁', NULL, 5, 'lilac', TRUE, TRUE
)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    icon = EXCLUDED.icon,
    cost = EXCLUDED.cost,
    sort_order = EXCLUDED.sort_order,
    tone = EXCLUDED.tone,
    is_special = EXCLUDED.is_special,
    active = EXCLUDED.active;
