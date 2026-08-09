-- Phase 1 RBAC: finer Sales / Procurement permissions (risk-based, backward-compatible).
-- Compat: existing sales.write / procurement.write roles keep full access via policy OR DB backfill.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('sales.pos', 'Bán hàng POS (tạo / hoàn tất đơn)', 'Bán hàng'),
    ('sales.customers', 'Khách hàng & công nợ (tạo / sửa / thu)', 'Bán hàng'),
    ('sales.settings', 'Cấu hình bán hàng (loyalty / voucher / hóa đơn / Rx)', 'Bán hàng'),
    ('procurement.suppliers', 'Quản lý nhà cung cấp', 'Mua hàng'),
    ('procurement.po', 'Tạo / sửa đơn đặt hàng (PO)', 'Mua hàng'),
    ('procurement.approve', 'Duyệt đơn đặt hàng (PO)', 'Mua hàng'),
    ('procurement.receive', 'Nhập hàng (GRN tạo / hoàn tất / hủy)', 'Mua hàng'),
    ('procurement.pay', 'Thanh toán & công nợ nhà cung cấp', 'Mua hàng')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- ADMIN / MANAGER get all new codes
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER')
  AND p.permission_code IN (
      'sales.pos', 'sales.customers', 'sales.settings', 'sales.cancel',
      'procurement.suppliers', 'procurement.po', 'procurement.approve',
      'procurement.receive', 'procurement.pay'
  )
ON CONFLICT DO NOTHING;

-- Backfill: roles with sales.write → pos + customers + settings (legacy "full sell" kept equivalent)
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_new.id
FROM role_permissions rp
INNER JOIN permissions p_old ON p_old.id = rp.permission_id AND p_old.permission_code = 'sales.write'
CROSS JOIN permissions p_new
WHERE p_new.permission_code IN ('sales.pos', 'sales.customers', 'sales.settings', 'sales.read')
ON CONFLICT DO NOTHING;

-- Backfill: roles with procurement.write → specialized bundle
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_new.id
FROM role_permissions rp
INNER JOIN permissions p_old ON p_old.id = rp.permission_id AND p_old.permission_code = 'procurement.write'
CROSS JOIN permissions p_new
WHERE p_new.permission_code IN (
    'procurement.read',
    'procurement.suppliers',
    'procurement.po',
    'procurement.approve',
    'procurement.receive',
    'procurement.pay'
)
ON CONFLICT DO NOTHING;

-- Template roles per tenant (optional, skip if already present)
INSERT INTO roles (tenant_id, role_code, role_name)
SELECT t.id, v.role_code, v.role_name
FROM tenants t
CROSS JOIN (
    VALUES
        ('CASHIER', 'Thu ngân'),
        ('WAREHOUSE', 'Thủ kho'),
        ('BRANCH_MANAGER', 'Quản lý chi nhánh')
) AS v(role_code, role_name)
WHERE t.deleted_at IS NULL
  AND t.status = 1
  AND NOT EXISTS (
      SELECT 1 FROM roles r
      WHERE r.tenant_id = t.id AND r.role_code = v.role_code
  );

-- CASHIER template permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'CASHIER'
  AND p.permission_code IN (
      'sales.read', 'sales.pos', 'sales.customers', 'sales.discount'
  )
ON CONFLICT DO NOTHING;

-- WAREHOUSE template
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'WAREHOUSE'
  AND p.permission_code IN (
      'procurement.read', 'procurement.po', 'procurement.receive',
      'inventory.read', 'inventory.write'
  )
ON CONFLICT DO NOTHING;

-- BRANCH_MANAGER template
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code IN (
      'sales.read', 'sales.pos', 'sales.customers', 'sales.write', 'sales.discount',
      'sales.cancel', 'sales.settings',
      'procurement.read', 'procurement.suppliers', 'procurement.po',
      'procurement.approve', 'procurement.receive', 'procurement.pay', 'procurement.write',
      'inventory.read', 'inventory.write', 'inventory.approve',
      'catalog.read', 'reports.read'
  )
ON CONFLICT DO NOTHING;
