-- POS unit-price override ("Sửa giá") + end-of-day catalog sync queue.

ALTER TABLE sales_order_items
    ADD COLUMN IF NOT EXISTS list_unit_price NUMERIC(18, 2) NULL,
    ADD COLUMN IF NOT EXISTS is_price_override BOOLEAN NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN sales_order_items.list_unit_price IS 'Giá catalog (niêm yết) tại thời điểm bán; NULL = chưa snapshot / đơn cũ';
COMMENT ON COLUMN sales_order_items.is_price_override IS 'TRUE khi NV sửa đơn giá khác catalog trên đơn';

CREATE TABLE IF NOT EXISTS sales_price_override_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    sales_order_id UUID NOT NULL REFERENCES sales_orders(id) ON DELETE CASCADE,
    sales_order_item_id UUID NOT NULL REFERENCES sales_order_items(id) ON DELETE CASCADE,
    product_id UUID NOT NULL,
    product_unit_id UUID NOT NULL,
    warehouse_id UUID NOT NULL,
    branch_id UUID NOT NULL,
    order_number TEXT NOT NULL,
    order_date TIMESTAMPTZ NOT NULL,
    list_unit_price NUMERIC(18, 2) NOT NULL,
    sold_unit_price NUMERIC(18, 2) NOT NULL,
    quantity NUMERIC(18, 3) NOT NULL,
    created_by_user_id UUID NOT NULL,
    -- 1=pending, 2=synced to catalog, 3=dismissed
    catalog_sync_status SMALLINT NOT NULL DEFAULT 1,
    catalog_synced_at TIMESTAMPTZ NULL,
    catalog_synced_by_user_id UUID NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_price_override_events_tenant_status_date
    ON sales_price_override_events (tenant_id, catalog_sync_status, order_date DESC);

CREATE INDEX IF NOT EXISTS ix_sales_price_override_events_tenant_product
    ON sales_price_override_events (tenant_id, product_id, product_unit_id, catalog_sync_status);

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('sales.price.override', 'Sửa giá bán trên đơn POS', 'Bán hàng'),
    ('sales.price.manage', 'Tổng hợp & đồng bộ giá lệch về danh mục', 'Bán hàng')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- ADMIN / MANAGER / BRANCH_MANAGER — override + manage (không cấp mặc định cho CASHIER)
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code IN ('sales.price.override', 'sales.price.manage')
ON CONFLICT DO NOTHING;
