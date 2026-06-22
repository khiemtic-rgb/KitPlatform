-- PharmaCore: Inventory module (7 tables)

CREATE TABLE warehouses (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    branch_id       UUID         NOT NULL REFERENCES branches(id),
    warehouse_code  VARCHAR(50)  NOT NULL,
    warehouse_name  VARCHAR(255) NOT NULL,
    warehouse_type  SMALLINT     NOT NULL, -- 1 Main 2 Retail 3 Rx 4 Cold 5 Returned
    is_default      BOOLEAN      NOT NULL DEFAULT FALSE,
    address         TEXT,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_warehouses_code UNIQUE (tenant_id, warehouse_code)
);
CREATE INDEX ix_warehouses_branch ON warehouses(branch_id);

CREATE TABLE inventory_batches (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID          NOT NULL REFERENCES tenants(id),
    warehouse_id            UUID          NOT NULL REFERENCES warehouses(id),
    product_id              UUID          NOT NULL REFERENCES products(id),
    batch_number            VARCHAR(100)  NOT NULL,
    manufacture_date        DATE,
    expiry_date             DATE,
    unit_cost               NUMERIC(18,2) NOT NULL,
    quantity_received       NUMERIC(18,3) NOT NULL,
    quantity_available      NUMERIC(18,3) NOT NULL,
    supplier_id             UUID,
    goods_receipt_item_id   UUID,
    status                  SMALLINT      NOT NULL DEFAULT 1,
    created_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_inventory_batches UNIQUE (tenant_id, warehouse_id, product_id, batch_number),
    CONSTRAINT ck_inventory_batches_qty_nonneg CHECK (quantity_available >= 0 AND quantity_received >= 0)
);
CREATE INDEX ix_batches_fefo ON inventory_batches(product_id, expiry_date);
CREATE INDEX ix_batches_product_batch ON inventory_batches(product_id, batch_number);
CREATE INDEX ix_batches_warehouse_product ON inventory_batches(warehouse_id, product_id);

CREATE TABLE stock_movements (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    warehouse_id    UUID          NOT NULL REFERENCES warehouses(id),
    batch_id        UUID          NOT NULL REFERENCES inventory_batches(id),
    product_id      UUID          NOT NULL REFERENCES products(id),
    movement_type   SMALLINT      NOT NULL,
    reference_type  VARCHAR(50)   NOT NULL,
    reference_id    UUID          NOT NULL,
    quantity        NUMERIC(18,3) NOT NULL,
    unit_cost       NUMERIC(18,2),
    movement_date   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    notes           TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_stock_movements_product_date ON stock_movements(product_id, movement_date);
CREATE INDEX ix_stock_movements_batch_date ON stock_movements(batch_id, movement_date);
CREATE INDEX ix_stock_movements_warehouse_date ON stock_movements(warehouse_id, movement_date);
CREATE INDEX ix_stock_movements_reference ON stock_movements(reference_type, reference_id);

CREATE TABLE inventory_adjustments (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES tenants(id),
    warehouse_id        UUID         NOT NULL REFERENCES warehouses(id),
    adjustment_number   VARCHAR(50)  NOT NULL,
    adjustment_date     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    reason              TEXT,
    status              SMALLINT     NOT NULL DEFAULT 1, -- 1 Draft 2 Counting 3 Approved 4 Cancelled
    approved_by         UUID         REFERENCES users(id),
    approved_at         TIMESTAMPTZ,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_inventory_adjustments_number UNIQUE (tenant_id, adjustment_number)
);

CREATE TABLE inventory_adjustment_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    adjustment_id       UUID          NOT NULL REFERENCES inventory_adjustments(id) ON DELETE CASCADE,
    product_id          UUID          NOT NULL REFERENCES products(id),
    batch_id            UUID          NOT NULL REFERENCES inventory_batches(id),
    system_quantity     NUMERIC(18,3) NOT NULL,
    actual_quantity     NUMERIC(18,3) NOT NULL,
    difference_quantity NUMERIC(18,3) NOT NULL,
    note                TEXT
);

CREATE TABLE inventory_transfers (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES tenants(id),
    transfer_number     VARCHAR(50)  NOT NULL,
    from_warehouse_id   UUID         NOT NULL REFERENCES warehouses(id),
    to_warehouse_id     UUID         NOT NULL REFERENCES warehouses(id),
    transfer_date       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    status              SMALLINT     NOT NULL DEFAULT 1, -- 1 Draft 2 Pending 3 Completed 4 Cancelled
    approved_by         UUID         REFERENCES users(id),
    approved_at         TIMESTAMPTZ,
    notes               TEXT,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_inventory_transfers_number UNIQUE (tenant_id, transfer_number),
    CONSTRAINT ck_inventory_transfers_diff_wh CHECK (from_warehouse_id <> to_warehouse_id)
);

CREATE TABLE inventory_transfer_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transfer_id     UUID          NOT NULL REFERENCES inventory_transfers(id) ON DELETE CASCADE,
    batch_id        UUID          NOT NULL REFERENCES inventory_batches(id),
    product_id      UUID          NOT NULL REFERENCES products(id),
    quantity        NUMERIC(18,3) NOT NULL,
    CONSTRAINT ck_transfer_items_qty_pos CHECK (quantity > 0)
);

CREATE TRIGGER trg_warehouses_updated BEFORE UPDATE ON warehouses FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_inventory_batches_updated BEFORE UPDATE ON inventory_batches FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_inventory_adjustments_updated BEFORE UPDATE ON inventory_adjustments FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_inventory_transfers_updated BEFORE UPDATE ON inventory_transfers FOR EACH ROW EXECUTE FUNCTION set_updated_at();
