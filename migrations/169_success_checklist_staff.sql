-- KitPlatform 169: Cho NV/thu ngân tick checklist mở–đóng ca
-- Tách khỏi Cockpit chủ NT (success.read) và success.write (chủ/QL).

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('success.checklist', 'Tick checklist mở/đóng ca (NV)', 'Cockpit chủ NT')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Vai trò quầy mặc định được tick checklist
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER', 'STAFF', 'CASHIER')
  AND p.permission_code = 'success.checklist'
ON CONFLICT DO NOTHING;

-- Mọi role đã có sales.pos cũng được tick (custom POS roles)
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_chk.id
FROM role_permissions rp
INNER JOIN permissions p_pos ON p_pos.id = rp.permission_id
  AND p_pos.permission_code = 'sales.pos'
CROSS JOIN permissions p_chk
WHERE p_chk.permission_code = 'success.checklist'
ON CONFLICT DO NOTHING;

-- Ai đã có success.write thì cũng có checklist (tương thích ngược)
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_chk.id
FROM role_permissions rp
INNER JOIN permissions p_w ON p_w.id = rp.permission_id
  AND p_w.permission_code = 'success.write'
CROSS JOIN permissions p_chk
WHERE p_chk.permission_code = 'success.checklist'
ON CONFLICT DO NOTHING;
