-- Family OS PayOS / bank-transfer checkout
-- Depends on: 222_pack_family_commercial_foundation.sql

CREATE TABLE IF NOT EXISTS pack_family.billing_checkout (
    id                   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id            UUID NOT NULL REFERENCES public.tenants(id),
    family_id            UUID NOT NULL REFERENCES pack_family.family(id),
    order_code           BIGINT NOT NULL,
    plan_code            VARCHAR(40) NOT NULL DEFAULT 'starter_month',
    amount_vnd           INT NOT NULL,
    status               VARCHAR(20) NOT NULL DEFAULT 'pending',
    payos_payment_link_id TEXT,
    checkout_url         TEXT,
    qr_code              TEXT,
    description          TEXT,
    paid_at              TIMESTAMPTZ,
    raw_webhook          JSONB,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at           TIMESTAMPTZ,
    CONSTRAINT uq_billing_checkout_order_code UNIQUE (order_code),
    CONSTRAINT ck_billing_checkout_status CHECK (
        status IN ('pending', 'paid', 'canceled', 'expired')
    ),
    CONSTRAINT ck_billing_checkout_amount CHECK (amount_vnd > 0)
);

CREATE INDEX IF NOT EXISTS ix_billing_checkout_family
    ON pack_family.billing_checkout (tenant_id, family_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_billing_checkout_status
    ON pack_family.billing_checkout (status, expires_at)
    WHERE status = 'pending';

ALTER TABLE pack_family.billing_checkout ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.billing_checkout;
CREATE POLICY tenant_isolation ON pack_family.billing_checkout
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.billing_checkout IS
    'Family OS billing checkout — PayOS payment link or manual bank-transfer order code.';
COMMENT ON COLUMN pack_family.billing_checkout.order_code IS
    'Unique PayOS orderCode (bigint); also used as bank-transfer content when PayOS is off.';
COMMENT ON COLUMN pack_family.billing_checkout.description IS
    'Short payment description / transfer content (often the order code).';
