-- POS: allow line unit-price override when permitted (sales.price.override).

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('sales.price.override', 'Sửa giá bán trên đơn POS', 'Bán hàng'),
    ('sales.price.manage', 'Tổng hợp & đồng bộ giá vượt khung', 'Bán hàng')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER')
  AND p.permission_code IN ('sales.price.override', 'sales.price.manage')
ON CONFLICT DO NOTHING;
