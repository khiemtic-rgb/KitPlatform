-- KAP / Khảo sát RBAC — was ADMIN-role-only; add permission codes for role assignment.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('survey.read', 'Xem KAP — Khảo sát', 'Khảo sát (KAP)'),
    ('survey.write', 'Quản lý KAP — Khảo sát', 'Khảo sát (KAP)')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code IN ('survey.read', 'survey.write')
ON CONFLICT DO NOTHING;
