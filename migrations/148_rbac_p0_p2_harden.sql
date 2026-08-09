-- RBAC hardening P0–P2 follow-up:
-- 1) success.write for checklist mutations (no longer mutate under success.read).
-- 2) Ensure rx.* permission catalog rows exist; grant dispense to POS roles.
-- 3) After SoD: procurement.write no longer implies approve/receive/pay —
--    expand any role that still holds write with the specialized codes it needs.
-- 4) sales.settings / sales.cancel no longer unlock SalesRead alone — ensure read.

-- 1) success.write
INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('success.write', 'Ghi checklist ca / thao tác Cockpit', 'Cockpit chủ NT')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER')
  AND p.permission_code = 'success.write'
ON CONFLICT DO NOTHING;

-- Anyone who already had success.read (BRANCH_MANAGER etc.) keeps read;
-- grant write only to ADMIN/MANAGER by default (above). BRANCH_MANAGER may
-- still view cockpit without ticking checklist.

-- 2) Rx permission catalog + dispense for POS operators
INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('rx.prescription.read', 'Xem đơn thuốc', 'Đơn thuốc'),
    ('rx.prescription.create', 'Tạo / sửa đơn thuốc', 'Đơn thuốc'),
    ('rx.prescription.verify', 'Duyệt đơn thuốc', 'Đơn thuốc'),
    ('rx.prescription.dispense', 'Cấp phát đơn thuốc (POS)', 'Đơn thuốc'),
    ('rx.prescriber.manage', 'Quản lý bác sĩ kê đơn', 'Đơn thuốc'),
    ('rx.prescriber.link.manage', 'Duyệt liên kết bác sĩ', 'Đơn thuốc')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- POS roles that sell must hold dispense after RxDispense policy is enforced.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER', 'STAFF', 'CASHIER')
  AND p.permission_code = 'rx.prescription.dispense'
ON CONFLICT DO NOTHING;

-- Also grant dispense to any custom role that already has sales.pos.
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_disp.id
FROM role_permissions rp
INNER JOIN permissions p_pos ON p_pos.id = rp.permission_id
  AND p_pos.permission_code = 'sales.pos'
CROSS JOIN permissions p_disp
WHERE p_disp.permission_code = 'rx.prescription.dispense'
ON CONFLICT DO NOTHING;

-- 3) Expand procurement.write holders with SoD specialized codes
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_spec.id
FROM role_permissions rp
INNER JOIN permissions p_write ON p_write.id = rp.permission_id
  AND p_write.permission_code = 'procurement.write'
CROSS JOIN permissions p_spec
WHERE p_spec.permission_code IN (
    'procurement.approve',
    'procurement.receive',
    'procurement.pay',
    'procurement.suppliers',
    'procurement.po',
    'procurement.read'
)
ON CONFLICT DO NOTHING;

-- 4) sales.settings / cancel holders get sales.read (SalesRead no longer accepts them alone)
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_read.id
FROM role_permissions rp
INNER JOIN permissions p_src ON p_src.id = rp.permission_id
  AND p_src.permission_code IN ('sales.settings', 'sales.cancel')
CROSS JOIN permissions p_read
WHERE p_read.permission_code = 'sales.read'
ON CONFLICT DO NOTHING;
