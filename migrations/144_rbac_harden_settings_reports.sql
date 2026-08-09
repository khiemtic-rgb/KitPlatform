-- Harden RBAC after audit:
-- 1) sales.settings is no longer implied by sales.write — strip from STAFF/CASHIER.
-- 2) Ensure managers keep sales.settings + reports.read (explicit).
-- 3) Ensure reports.read exists for ADMIN / MANAGER / BRANCH_MANAGER.

-- Strip sales.settings from cashier-style roles (keep on managers via grant below).
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code IN ('STAFF', 'CASHIER')
  AND p.permission_code = 'sales.settings';

-- Managers: explicit sales.settings (may already exist).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code = 'sales.settings'
ON CONFLICT DO NOTHING;

-- Ensure reports permissions catalog rows exist.
INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('reports.read', 'Xem báo cáo', 'Báo cáo'),
    ('reports.export', 'Xuất báo cáo', 'Báo cáo')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Managers + admin: reports.read (store-wide sales reports for elevated roles).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code = 'reports.read'
ON CONFLICT DO NOTHING;

-- Optional export for admin/manager only (not BRANCH_MANAGER by default).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER')
  AND p.permission_code = 'reports.export'
ON CONFLICT DO NOTHING;

-- Ensure system.audit.read exists for picker / optional grants.
INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('system.audit.read', 'Xem nhật ký audit', 'Hệ thống')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER')
  AND p.permission_code = 'system.audit.read'
ON CONFLICT DO NOTHING;
