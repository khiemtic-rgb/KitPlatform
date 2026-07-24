-- KitPlatform 196: FamilyOS parent Web Push + reminder dispatch dedupe
-- Depends on: 192
-- Local / pilot only until deploy is explicitly approved

CREATE TABLE IF NOT EXISTS pack_family.parent_push_subscription (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    membership_id    UUID NOT NULL REFERENCES pack_family.membership(id),
    endpoint         TEXT NOT NULL,
    p256dh           TEXT NOT NULL,
    auth             TEXT NOT NULL,
    user_agent       VARCHAR(240),
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_parent_push_endpoint
    ON pack_family.parent_push_subscription (tenant_id, endpoint);

CREATE INDEX IF NOT EXISTS ix_parent_push_family
    ON pack_family.parent_push_subscription (family_id)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_parent_push_subscription_row_version ON pack_family.parent_push_subscription;
CREATE TRIGGER trg_parent_push_subscription_row_version
    BEFORE UPDATE ON pack_family.parent_push_subscription
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.parent_push_subscription ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.parent_push_subscription;
CREATE POLICY tenant_isolation ON pack_family.parent_push_subscription
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.parent_push_subscription IS
    'Web Push subscriptions for FamilyOS guardians/caregivers (family-app).';

CREATE TABLE IF NOT EXISTS pack_family.reminder_dispatch (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    flow_date        DATE NOT NULL,
    kind             VARCHAR(32) NOT NULL,
    commitment_id    UUID REFERENCES pack_family.commitment(id),
    payload_summary  VARCHAR(400),
    dispatched_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_reminder_dispatch_kind CHECK (
        kind IN ('due_now', 'overdue', 'evening_digest')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_dispatch_commitment
    ON pack_family.reminder_dispatch (tenant_id, commitment_id, kind, flow_date)
    WHERE commitment_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_dispatch_digest
    ON pack_family.reminder_dispatch (tenant_id, family_id, kind, flow_date)
    WHERE kind = 'evening_digest';

CREATE INDEX IF NOT EXISTS ix_reminder_dispatch_family_date
    ON pack_family.reminder_dispatch (family_id, flow_date);

ALTER TABLE pack_family.reminder_dispatch ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.reminder_dispatch;
CREATE POLICY tenant_isolation ON pack_family.reminder_dispatch
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.reminder_dispatch IS
    'Dedupe log for parent due/overdue push and once-daily evening digest.';
