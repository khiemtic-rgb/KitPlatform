-- PharmaCore V2: soft delete, workflow audit columns, child tenant_id, purge permission

-- Purchase orders
ALTER TABLE purchase_orders
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS deleted_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS approved_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS approved_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS cancelled_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS cancelled_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS closed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS closed_by UUID REFERENCES users(id);

-- Goods receipts
ALTER TABLE goods_receipts
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS deleted_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS cancelled_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS cancelled_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS completed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS completed_by UUID REFERENCES users(id);

-- Supplier payments
ALTER TABLE supplier_payments
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS deleted_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS created_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS posted_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS cancelled_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS cancelled_by UUID REFERENCES users(id);

-- Child tables: tenant_id for multi-tenant isolation (V2)
ALTER TABLE purchase_order_items ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);
ALTER TABLE goods_receipt_items ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);

UPDATE purchase_order_items poi
SET tenant_id = po.tenant_id
FROM purchase_orders po
WHERE poi.purchase_order_id = po.id AND poi.tenant_id IS NULL;

UPDATE goods_receipt_items gri
SET tenant_id = gr.tenant_id
FROM goods_receipts gr
WHERE gri.goods_receipt_id = gr.id AND gri.tenant_id IS NULL;

ALTER TABLE purchase_order_items ALTER COLUMN tenant_id SET NOT NULL;
ALTER TABLE goods_receipt_items ALTER COLUMN tenant_id SET NOT NULL;

CREATE INDEX IF NOT EXISTS ix_purchase_orders_archived
    ON purchase_orders(tenant_id, deleted_at)
    WHERE deleted_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_goods_receipts_archived
    ON goods_receipts(tenant_id, deleted_at)
    WHERE deleted_at IS NOT NULL;

-- Highest privilege: permanent purge (ADMIN only)
INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES ('system.delete_permanent', 'Xóa vĩnh viễn', 'System')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN' AND p.permission_code = 'system.delete_permanent'
ON CONFLICT DO NOTHING;
