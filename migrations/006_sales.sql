-- PharmaCore: Sales / POS module (7 tables)

CREATE TABLE customers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    customer_code   VARCHAR(50)  NOT NULL,
    full_name       VARCHAR(255) NOT NULL,
    phone           VARCHAR(20)  NOT NULL,
    email           CITEXT,
    date_of_birth   DATE,
    gender          SMALLINT,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_customers_code UNIQUE (tenant_id, customer_code),
    CONSTRAINT uq_customers_phone UNIQUE (tenant_id, phone)
);
CREATE INDEX ix_customers_tenant ON customers(tenant_id);

CREATE TABLE sales_orders (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    order_number    VARCHAR(50)   NOT NULL,
    branch_id       UUID          NOT NULL REFERENCES branches(id),
    warehouse_id    UUID          NOT NULL REFERENCES warehouses(id),
    customer_id     UUID          REFERENCES customers(id),
    employee_id     UUID          REFERENCES employees(id),
    order_date      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    price_type      SMALLINT      NOT NULL DEFAULT 1,
    subtotal        NUMERIC(18,2) NOT NULL DEFAULT 0,
    discount_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_amount    NUMERIC(18,2) NOT NULL DEFAULT 0,
    currency_code   CHAR(3)       NOT NULL DEFAULT 'VND',
    voucher_id      UUID,
    status          SMALLINT      NOT NULL DEFAULT 1, -- 1 Draft 2 Completed 3 Cancelled 4 Refunded
    notes           TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_sales_orders_number UNIQUE (tenant_id, order_number)
);
CREATE INDEX ix_sales_orders_date ON sales_orders(tenant_id, order_date DESC);
CREATE INDEX ix_sales_orders_customer ON sales_orders(customer_id);

CREATE TABLE sales_order_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sales_order_id  UUID          NOT NULL REFERENCES sales_orders(id) ON DELETE CASCADE,
    product_id      UUID          NOT NULL REFERENCES products(id),
    product_unit_id UUID          NOT NULL REFERENCES product_units(id),
    batch_id        UUID          NOT NULL REFERENCES inventory_batches(id),
    quantity        NUMERIC(18,3) NOT NULL,
    unit_price      NUMERIC(18,2) NOT NULL,
    discount_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    line_total      NUMERIC(18,2) NOT NULL,
    CONSTRAINT ck_sales_items_qty_pos CHECK (quantity > 0)
);

CREATE TABLE sales_order_batch_allocations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sales_order_item_id UUID          NOT NULL REFERENCES sales_order_items(id) ON DELETE CASCADE,
    batch_id            UUID          NOT NULL REFERENCES inventory_batches(id),
    quantity            NUMERIC(18,3) NOT NULL,
    unit_cost           NUMERIC(18,2) NOT NULL
);

CREATE TABLE sales_payments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sales_order_id  UUID          NOT NULL REFERENCES sales_orders(id) ON DELETE CASCADE,
    payment_method  SMALLINT      NOT NULL, -- 1 Cash 2 Card 3 Transfer 4 E-wallet
    amount          NUMERIC(18,2) NOT NULL,
    paid_at         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    reference_no    VARCHAR(100)
);

CREATE TABLE sales_returns (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    return_number   VARCHAR(50)  NOT NULL,
    sales_order_id  UUID         NOT NULL REFERENCES sales_orders(id),
    return_date     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    reason          TEXT,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_sales_returns_number UNIQUE (tenant_id, return_number)
);

CREATE TABLE sales_return_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sales_return_id     UUID          NOT NULL REFERENCES sales_returns(id) ON DELETE CASCADE,
    sales_order_item_id UUID          NOT NULL REFERENCES sales_order_items(id),
    batch_id            UUID          NOT NULL REFERENCES inventory_batches(id),
    quantity            NUMERIC(18,3) NOT NULL,
    refund_amount       NUMERIC(18,2) NOT NULL,
    CONSTRAINT ck_return_items_qty_pos CHECK (quantity > 0)
);

CREATE TRIGGER trg_customers_updated BEFORE UPDATE ON customers FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_sales_orders_updated BEFORE UPDATE ON sales_orders FOR EACH ROW EXECUTE FUNCTION set_updated_at();
