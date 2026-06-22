-- PharmaCore: Customer App & Retention module (10 tables)

CREATE TABLE customer_accounts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    customer_id     UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    phone           VARCHAR(20)  NOT NULL,
    email           CITEXT,
    password_hash   VARCHAR(255),
    is_verified     BOOLEAN      NOT NULL DEFAULT FALSE,
    last_login_at   TIMESTAMPTZ,
    device_tokens   JSONB        NOT NULL DEFAULT '[]'::jsonb,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_customer_accounts_phone UNIQUE (tenant_id, phone)
);

CREATE TABLE customer_addresses (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    label           VARCHAR(50)  NOT NULL DEFAULT 'Nhà',
    recipient_name  VARCHAR(255),
    phone           VARCHAR(20),
    address_line    TEXT         NOT NULL,
    ward            VARCHAR(100),
    district        VARCHAR(100),
    province        VARCHAR(100),
    is_default      BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE loyalty_programs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID          NOT NULL REFERENCES tenants(id),
    program_code        VARCHAR(50)   NOT NULL,
    program_name        VARCHAR(255)  NOT NULL,
    points_per_amount   NUMERIC(18,2) NOT NULL DEFAULT 10000,
    amount_per_point    NUMERIC(18,2) NOT NULL DEFAULT 1,
    status              SMALLINT      NOT NULL DEFAULT 1,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_loyalty_programs_code UNIQUE (tenant_id, program_code)
);

CREATE TABLE loyalty_tiers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    program_id      UUID          NOT NULL REFERENCES loyalty_programs(id) ON DELETE CASCADE,
    tier_code       VARCHAR(50)   NOT NULL,
    tier_name       VARCHAR(100)  NOT NULL,
    min_points      INT           NOT NULL DEFAULT 0,
    discount_percent NUMERIC(5,2) NOT NULL DEFAULT 0,
    sort_order      INT           NOT NULL DEFAULT 0,
    CONSTRAINT uq_loyalty_tiers_code UNIQUE (program_id, tier_code)
);

CREATE TABLE customer_loyalty (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    program_id      UUID NOT NULL REFERENCES loyalty_programs(id),
    tier_id         UUID REFERENCES loyalty_tiers(id),
    points_balance  INT  NOT NULL DEFAULT 0,
    lifetime_points INT  NOT NULL DEFAULT 0,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_customer_loyalty UNIQUE (customer_id, program_id)
);

CREATE TABLE loyalty_transactions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    customer_id     UUID         NOT NULL REFERENCES customers(id),
    program_id      UUID         NOT NULL REFERENCES loyalty_programs(id),
    transaction_type SMALLINT    NOT NULL, -- 1 Earn 2 Redeem 3 Expire 4 Adjust
    points          INT          NOT NULL,
    sales_order_id  UUID         REFERENCES sales_orders(id),
    notes           TEXT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_loyalty_tx_customer ON loyalty_transactions(customer_id, created_at DESC);

CREATE TABLE vouchers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    voucher_code    VARCHAR(50)   NOT NULL,
    voucher_name    VARCHAR(255)  NOT NULL,
    discount_type   SMALLINT      NOT NULL, -- 1 Percent 2 Fixed
    discount_value  NUMERIC(18,2) NOT NULL,
    min_order_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    max_uses        INT,
    used_count      INT           NOT NULL DEFAULT 0,
    valid_from      TIMESTAMPTZ   NOT NULL,
    valid_to        TIMESTAMPTZ   NOT NULL,
    status          SMALLINT      NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_vouchers_code UNIQUE (tenant_id, voucher_code)
);

CREATE TABLE customer_vouchers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id     UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    voucher_id      UUID NOT NULL REFERENCES vouchers(id),
    issued_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    used_at         TIMESTAMPTZ,
    sales_order_id  UUID REFERENCES sales_orders(id),
    CONSTRAINT uq_customer_vouchers UNIQUE (customer_id, voucher_id)
);

CREATE TABLE medication_reminders (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    customer_id     UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    product_id      UUID         NOT NULL REFERENCES products(id),
    dosage_note     VARCHAR(255),
    remind_time     TIME         NOT NULL,
    days_of_week    SMALLINT[]   NOT NULL DEFAULT ARRAY[1,2,3,4,5,6,7],
    next_remind_at  TIMESTAMPTZ,
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_medication_reminders_next ON medication_reminders(next_remind_at) WHERE is_active = TRUE;

CREATE TABLE customer_notifications (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    customer_id     UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    channel         SMALLINT     NOT NULL, -- 1 Push 2 SMS 3 Email
    title           VARCHAR(255) NOT NULL,
    body            TEXT         NOT NULL,
    payload         JSONB,
    sent_at         TIMESTAMPTZ,
    read_at         TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_customer_notifications_customer ON customer_notifications(customer_id, created_at DESC);

-- FK voucher on sales_orders (deferred)
ALTER TABLE sales_orders
    ADD CONSTRAINT fk_sales_orders_voucher FOREIGN KEY (voucher_id) REFERENCES vouchers(id);

CREATE TRIGGER trg_customer_accounts_updated BEFORE UPDATE ON customer_accounts FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_loyalty_programs_updated BEFORE UPDATE ON loyalty_programs FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_medication_reminders_updated BEFORE UPDATE ON medication_reminders FOR EACH ROW EXECUTE FUNCTION set_updated_at();
