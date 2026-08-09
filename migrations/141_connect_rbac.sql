-- Connect RBAC — menu/API were platform-module-only; add assignable permissions.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('connect.read', 'Xem Connect', 'Connect'),
    ('connect.write', 'Quản lý Connect', 'Connect')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Admin / quản lý mặc định có Connect; STAFF không (phải gán tay nếu cần).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code IN ('connect.read', 'connect.write')
ON CONFLICT DO NOTHING;
