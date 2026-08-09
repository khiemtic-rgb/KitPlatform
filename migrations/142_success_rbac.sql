-- Success / Cockpit chủ NT — assignable permission (was role/reports-only, invisible in RBAC UI).

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('success.read', 'Cockpit chủ NT (checklist ca / lệch quỹ)', 'Cockpit chủ NT')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Admin / quản lý mặc định có Cockpit; STAFF / CASHIER không (phải gán tay nếu cần).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code = 'success.read'
ON CONFLICT DO NOTHING;
