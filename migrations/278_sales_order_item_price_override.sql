-- Persist list price + flag when POS line sells at overridden unit price.

ALTER TABLE sales_order_items
    ADD COLUMN IF NOT EXISTS list_unit_price NUMERIC(18,2),
    ADD COLUMN IF NOT EXISTS is_price_override BOOLEAN NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN sales_order_items.list_unit_price IS 'Catalog unit price at sale time (when overridden)';
COMMENT ON COLUMN sales_order_items.is_price_override IS 'True when unit_price differs from catalog at sale';

CREATE INDEX IF NOT EXISTS ix_sales_order_items_price_override
    ON sales_order_items (sales_order_id)
    WHERE is_price_override = TRUE;
