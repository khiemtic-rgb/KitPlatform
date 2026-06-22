-- Sales POS V2.5: persist discount metadata on draft orders

ALTER TABLE sales_orders
    ADD COLUMN IF NOT EXISTS order_discount_type SMALLINT,
    ADD COLUMN IF NOT EXISTS order_discount_value NUMERIC(18,2) NOT NULL DEFAULT 0;

ALTER TABLE sales_order_items
    ADD COLUMN IF NOT EXISTS discount_type SMALLINT,
    ADD COLUMN IF NOT EXISTS discount_value NUMERIC(18,2) NOT NULL DEFAULT 0;

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('sales.discount', 'Chiết khấu bán hàng (tối đa 10%)', 'Sales'),
    ('sales.discount.unlimited', 'Chiết khấu không giới hạn', 'Sales')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111501', p.id
FROM permissions p
WHERE p.permission_code IN ('sales.discount', 'sales.discount.unlimited')
ON CONFLICT DO NOTHING;
