-- Cấu hình App khách hàng (link/QR/branding) — không cấp mặc định cho STAFF/CASHIER.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('sales.customer_app', 'Cấu hình app khách hàng (link / QR / branding)', 'Bán hàng')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code = 'sales.customer_app'
ON CONFLICT DO NOTHING;
