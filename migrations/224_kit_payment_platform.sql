-- Kit Payment Platform — shared billing for Famixa / Novixa / KEMS / future SaaS
-- Depends on: public.tenants, kit_uuid_v7(), kit_rls_tenant_match()

CREATE SCHEMA IF NOT EXISTS payment;

-- Catalog of sellable plans (product × plan)
CREATE TABLE IF NOT EXISTS payment.plan (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    product_code    VARCHAR(40) NOT NULL,
    plan_code       VARCHAR(40) NOT NULL,
    display_name    VARCHAR(120) NOT NULL,
    amount_vnd      INT NOT NULL,
    currency        VARCHAR(3) NOT NULL DEFAULT 'VND',
    interval_days   INT NOT NULL DEFAULT 30,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_payment_plan_product_code UNIQUE (product_code, plan_code),
    CONSTRAINT ck_payment_plan_amount CHECK (amount_vnd > 0),
    CONSTRAINT ck_payment_plan_interval CHECK (interval_days > 0)
);

COMMENT ON TABLE payment.plan IS
    'Sellable SaaS plans — product_code scopes Famixa/Novixa/KEMS; shared across tenants.';

-- Core subscription (one row per tenant × product × subject)
CREATE TABLE IF NOT EXISTS payment.subscription (
    id                   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id            UUID NOT NULL REFERENCES public.tenants(id),
    product_code         VARCHAR(40) NOT NULL,
    subject_type         VARCHAR(40) NOT NULL,
    subject_id           UUID NOT NULL,
    plan_code            VARCHAR(40) NOT NULL,
    status               VARCHAR(20) NOT NULL DEFAULT 'trial',
    trial_ends_at        TIMESTAMPTZ,
    current_period_end   TIMESTAMPTZ,
    auto_renew           BOOLEAN NOT NULL DEFAULT FALSE,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_payment_subscription_subject
        UNIQUE (tenant_id, product_code, subject_type, subject_id),
    CONSTRAINT ck_payment_subscription_status CHECK (
        status IN ('trial', 'active', 'past_due', 'expired', 'canceled')
    )
);

CREATE INDEX IF NOT EXISTS ix_payment_subscription_tenant
    ON payment.subscription (tenant_id, product_code);

CREATE INDEX IF NOT EXISTS ix_payment_subscription_period
    ON payment.subscription (status, current_period_end)
    WHERE status IN ('trial', 'active', 'past_due');

ALTER TABLE payment.subscription ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payment.subscription;
CREATE POLICY tenant_isolation ON payment.subscription
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE payment.subscription IS
    'SaaS subscription core — subject_type/subject_id point at product entity (e.g. family).';
COMMENT ON COLUMN payment.subscription.subject_type IS
    'Entity kind: family | tenant | clinic | ...';
COMMENT ON COLUMN payment.subscription.auto_renew IS
    'Reserved for provider auto-debit (MoMo/VNPay token) — Phase 2.';

-- Checkout / payment order
CREATE TABLE IF NOT EXISTS payment.payment_order (
    id                   UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id            UUID NOT NULL REFERENCES public.tenants(id),
    subscription_id      UUID REFERENCES payment.subscription(id),
    product_code         VARCHAR(40) NOT NULL,
    subject_type         VARCHAR(40) NOT NULL,
    subject_id           UUID NOT NULL,
    order_code           BIGINT NOT NULL,
    public_code          VARCHAR(32) NOT NULL,
    plan_code            VARCHAR(40) NOT NULL,
    amount_vnd           INT NOT NULL,
    currency             VARCHAR(3) NOT NULL DEFAULT 'VND',
    status               VARCHAR(20) NOT NULL DEFAULT 'pending',
    provider_code        VARCHAR(40),
    provider_payment_id  TEXT,
    checkout_url         TEXT,
    qr_code              TEXT,
    description          TEXT,
    return_url           TEXT,
    cancel_url           TEXT,
    paid_at              TIMESTAMPTZ,
    raw_webhook          JSONB,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at           TIMESTAMPTZ,
    CONSTRAINT uq_payment_order_code UNIQUE (order_code),
    CONSTRAINT uq_payment_order_public_code UNIQUE (public_code),
    CONSTRAINT ck_payment_order_status CHECK (
        status IN ('pending', 'paid', 'canceled', 'expired')
    ),
    CONSTRAINT ck_payment_order_amount CHECK (amount_vnd > 0)
);

CREATE INDEX IF NOT EXISTS ix_payment_order_subject
    ON payment.payment_order (tenant_id, product_code, subject_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_payment_order_pending
    ON payment.payment_order (status, expires_at)
    WHERE status = 'pending';

ALTER TABLE payment.payment_order ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payment.payment_order;
CREATE POLICY tenant_isolation ON payment.payment_order
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE payment.payment_order IS
    'One checkout attempt — provider-agnostic; fulfillment extends payment.subscription.';
COMMENT ON COLUMN payment.payment_order.public_code IS
    'Human transfer content (e.g. FMX240721A91) — unique per order, not the raw bigint.';
COMMENT ON COLUMN payment.payment_order.order_code IS
    'Provider numeric order id (PayOS orderCode).';

-- Provider transaction / webhook audit
CREATE TABLE IF NOT EXISTS payment.payment_transaction (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id         UUID NOT NULL REFERENCES public.tenants(id),
    order_id          UUID NOT NULL REFERENCES payment.payment_order(id),
    provider_code     VARCHAR(40) NOT NULL,
    provider_txn_id   TEXT,
    amount_vnd        INT,
    status            VARCHAR(20) NOT NULL,
    raw_payload       JSONB,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_payment_txn_status CHECK (
        status IN ('created', 'succeeded', 'failed', 'ignored')
    )
);

CREATE INDEX IF NOT EXISTS ix_payment_txn_order
    ON payment.payment_transaction (order_id, created_at DESC);

ALTER TABLE payment.payment_transaction ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payment.payment_transaction;
CREATE POLICY tenant_isolation ON payment.payment_transaction
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

-- Seed Famixa starter plan
INSERT INTO payment.plan (product_code, plan_code, display_name, amount_vnd, interval_days)
VALUES ('family_os', 'starter_month', 'Famixa Starter (tháng)', 99000, 30)
ON CONFLICT (product_code, plan_code) DO UPDATE
SET display_name = EXCLUDED.display_name,
    amount_vnd = EXCLUDED.amount_vnd,
    interval_days = EXCLUDED.interval_days,
    is_active = TRUE,
    updated_at = NOW();

-- Ops permissions — seed only; do NOT auto-grant to tenant ADMIN
-- (Family OS self-serve parents are ADMIN in their tenant and must not activate orders).
INSERT INTO public.permissions (permission_code, permission_name, module_name)
VALUES
    ('payment.ops.activate', 'Kích hoạt đơn thanh toán (ops)', 'Payment'),
    ('payment.ops.read', 'Xem đơn / subscription (ops)', 'Payment')
ON CONFLICT (permission_code) DO UPDATE SET
    permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Backfill platform subscriptions from Famixa commercial rows
INSERT INTO payment.subscription (
    tenant_id, product_code, subject_type, subject_id,
    plan_code, status, trial_ends_at, current_period_end, created_at, updated_at
)
SELECT
    s.tenant_id,
    'family_os',
    'family',
    s.family_id,
    s.plan_code,
    s.status,
    s.trial_ends_at,
    s.current_period_end,
    COALESCE(s.created_at, NOW()),
    COALESCE(s.updated_at, NOW())
FROM pack_family.family_subscription s
ON CONFLICT (tenant_id, product_code, subject_type, subject_id) DO NOTHING;
