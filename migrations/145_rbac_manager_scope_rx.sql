-- RBAC follow-up (audit proposals):
-- 1) BRANCH_MANAGER: drop sales.write package; keep specialized sales codes.
-- 2) MANAGER / BRANCH_MANAGER: drop system.write; keep system.read (+ audit for MANAGER).
-- 3) Rx: grant manager-level Rx ops so menu is not proxied via sales.*.

-- 1) BRANCH_MANAGER without sales.write
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code = 'sales.write';

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code IN (
      'sales.read',
      'sales.pos',
      'sales.customers',
      'sales.cancel',
      'sales.settings',
      'sales.discount'
  )
ON CONFLICT DO NOTHING;

-- 2) Only ADMIN keeps system.write
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code IN ('MANAGER', 'BRANCH_MANAGER', 'STAFF', 'CASHIER')
  AND p.permission_code = 'system.write';

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code = 'system.read'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'MANAGER'
  AND p.permission_code = 'system.audit.read'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code IN ('system.read', 'system.write', 'system.audit.read')
ON CONFLICT DO NOTHING;

-- 3) Rx module for managers (ADMIN already has from 096)
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code IN (
      'rx.prescription.read',
      'rx.prescription.create',
      'rx.prescription.verify',
      'rx.prescription.dispense',
      'rx.prescriber.manage',
      'rx.prescriber.link.manage'
  )
ON CONFLICT DO NOTHING;
