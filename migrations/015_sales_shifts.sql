-- PharmaCore: Sales shifts (open/close register, opening cash)

CREATE TABLE sales_shifts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    warehouse_id    UUID          NOT NULL REFERENCES warehouses(id),
    opened_by       UUID          NOT NULL REFERENCES users(id),
    closed_by       UUID          REFERENCES users(id),
    shift_number    VARCHAR(50)   NOT NULL,
    opened_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    closed_at       TIMESTAMPTZ,
    opening_cash    NUMERIC(18,2) NOT NULL DEFAULT 0,
    closing_cash    NUMERIC(18,2),
    expected_cash   NUMERIC(18,2),
    cash_variance   NUMERIC(18,2),
    status          SMALLINT      NOT NULL DEFAULT 1, -- 1 Open 2 Closed
    close_notes     TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_sales_shifts_number UNIQUE (tenant_id, shift_number),
    CONSTRAINT ck_sales_shifts_opening_cash_nonneg CHECK (opening_cash >= 0)
);

CREATE UNIQUE INDEX uq_sales_shifts_open_warehouse
    ON sales_shifts(tenant_id, warehouse_id)
    WHERE status = 1;

CREATE INDEX ix_sales_shifts_tenant_opened ON sales_shifts(tenant_id, opened_at DESC);

CREATE TRIGGER trg_sales_shifts_updated
    BEFORE UPDATE ON sales_shifts
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
