-- Phase 12: Identity Admin permissions

INSERT INTO permissions (permission_code, permission_name, module_name) VALUES
('system.read', 'Xem hệ thống', 'System'),
('system.write', 'Quản trị hệ thống', 'System')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111501', p.id
FROM permissions p
WHERE p.permission_code IN ('system.read', 'system.write')
  AND NOT EXISTS (
    SELECT 1 FROM role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111501' AND rp.permission_id = p.id
  );
