-- Link customer reservations to completed POS sales orders

ALTER TABLE customer_reservations
    ADD COLUMN IF NOT EXISTS sales_order_id UUID REFERENCES sales_orders(id);

CREATE INDEX IF NOT EXISTS ix_customer_reservations_sales_order
    ON customer_reservations (sales_order_id)
    WHERE sales_order_id IS NOT NULL;
