-- Pilot stability: tồn tối thiểu SP + quyền xem nhật ký

ALTER TABLE products
    ADD COLUMN IF NOT EXISTS min_stock_qty NUMERIC(18,3);

INSERT INTO permissions (permission_code, permission_name, module_name) VALUES
    ('system.audit.read', 'Xem nhật ký hệ thống', 'Hệ thống')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code = 'system.audit.read'
ON CONFLICT DO NOTHING;
