-- PharmaCore: Procurement module (6 tables)

CREATE TABLE suppliers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    supplier_code   VARCHAR(50)  NOT NULL,
    supplier_name   VARCHAR(255) NOT NULL,
    tax_code        VARCHAR(50),
    contact_name    VARCHAR(255),
    phone           VARCHAR(30),
    email           CITEXT,
    address         TEXT,
    payment_terms   INT          NOT NULL DEFAULT 30,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_suppliers_code UNIQUE (tenant_id, supplier_code)
);

CREATE TABLE purchase_orders (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    po_number       VARCHAR(50)   NOT NULL,
    supplier_id     UUID          NOT NULL REFERENCES suppliers(id),
    warehouse_id    UUID          NOT NULL REFERENCES warehouses(id),
    order_date      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    expected_date   DATE,
    status          SMALLINT      NOT NULL DEFAULT 1,
    currency_code   CHAR(3)       NOT NULL DEFAULT 'VND',
    subtotal        NUMERIC(18,2) NOT NULL DEFAULT 0,
    tax_amount      NUMERIC(18,2) NOT NULL DEFAULT 0,
    total_amount    NUMERIC(18,2) NOT NULL DEFAULT 0,
    notes           TEXT,
    created_by      UUID          REFERENCES users(id),
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_purchase_orders_number UNIQUE (tenant_id, po_number)
);
CREATE INDEX ix_purchase_orders_supplier ON purchase_orders(supplier_id);

CREATE TABLE purchase_order_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    purchase_order_id UUID        NOT NULL REFERENCES purchase_orders(id) ON DELETE CASCADE,
    product_id      UUID          NOT NULL REFERENCES products(id),
    product_unit_id UUID          NOT NULL REFERENCES product_units(id),
    ordered_qty     NUMERIC(18,3) NOT NULL,
    received_qty    NUMERIC(18,3) NOT NULL DEFAULT 0,
    unit_price      NUMERIC(18,2) NOT NULL,
    line_total      NUMERIC(18,2) NOT NULL,
    CONSTRAINT ck_po_items_qty_pos CHECK (ordered_qty > 0)
);

CREATE TABLE goods_receipts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    grn_number      VARCHAR(50)  NOT NULL,
    purchase_order_id UUID       REFERENCES purchase_orders(id),
    supplier_id     UUID         NOT NULL REFERENCES suppliers(id),
    warehouse_id    UUID         NOT NULL REFERENCES warehouses(id),
    receipt_date    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    status          SMALLINT     NOT NULL DEFAULT 1,
    received_by     UUID         REFERENCES users(id),
    notes           TEXT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_goods_receipts_number UNIQUE (tenant_id, grn_number)
);

CREATE TABLE goods_receipt_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    goods_receipt_id    UUID          NOT NULL REFERENCES goods_receipts(id) ON DELETE CASCADE,
    purchase_order_item_id UUID       REFERENCES purchase_order_items(id),
    product_id          UUID          NOT NULL REFERENCES products(id),
    product_unit_id     UUID          NOT NULL REFERENCES product_units(id),
    batch_number        VARCHAR(100)  NOT NULL,
    manufacture_date    DATE,
    expiry_date         DATE          NOT NULL,
    quantity            NUMERIC(18,3) NOT NULL,
    unit_cost           NUMERIC(18,2) NOT NULL,
    line_total          NUMERIC(18,2) NOT NULL,
    CONSTRAINT ck_grn_items_qty_pos CHECK (quantity > 0)
);

CREATE TABLE supplier_payments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    supplier_id     UUID          NOT NULL REFERENCES suppliers(id),
    purchase_order_id UUID          REFERENCES purchase_orders(id),
    goods_receipt_id UUID           REFERENCES goods_receipts(id),
    payment_number  VARCHAR(50)   NOT NULL,
    amount          NUMERIC(18,2) NOT NULL,
    currency_code   CHAR(3)       NOT NULL DEFAULT 'VND',
    payment_method  SMALLINT      NOT NULL DEFAULT 1,
    payment_date    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    notes           TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_supplier_payments_number UNIQUE (tenant_id, payment_number)
);

-- FK deferred from inventory_batches
ALTER TABLE inventory_batches
    ADD CONSTRAINT fk_batches_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers(id);
ALTER TABLE inventory_batches
    ADD CONSTRAINT fk_batches_grn_item FOREIGN KEY (goods_receipt_item_id) REFERENCES goods_receipt_items(id);

CREATE TRIGGER trg_suppliers_updated BEFORE UPDATE ON suppliers FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_purchase_orders_updated BEFORE UPDATE ON purchase_orders FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_goods_receipts_updated BEFORE UPDATE ON goods_receipts FOR EACH ROW EXECUTE FUNCTION set_updated_at();
