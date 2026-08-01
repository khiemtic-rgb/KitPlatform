-- A6: attribute completed sale back to repurchase suggestion (Smart Refill)

ALTER TABLE repurchase_suggestions
    ADD COLUMN IF NOT EXISTS converted_sales_order_id UUID REFERENCES sales_orders(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_repurchase_suggestions_converted_sale
    ON repurchase_suggestions (tenant_id, converted_sales_order_id)
    WHERE converted_sales_order_id IS NOT NULL;

COMMENT ON COLUMN repurchase_suggestions.converted_sales_order_id IS
    'Smart Refill: sales order completed from refill reservation';
