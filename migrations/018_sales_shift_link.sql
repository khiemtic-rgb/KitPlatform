-- Gắn đơn bán / phiếu trả với ca làm việc
ALTER TABLE sales_orders
    ADD COLUMN IF NOT EXISTS sales_shift_id UUID REFERENCES sales_shifts(id);

ALTER TABLE sales_returns
    ADD COLUMN IF NOT EXISTS sales_shift_id UUID REFERENCES sales_shifts(id);

CREATE INDEX IF NOT EXISTS ix_sales_orders_shift
    ON sales_orders (tenant_id, sales_shift_id)
    WHERE sales_shift_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_sales_returns_shift
    ON sales_returns (tenant_id, sales_shift_id)
    WHERE sales_shift_id IS NOT NULL;
