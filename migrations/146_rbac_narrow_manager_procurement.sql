-- Narrow role templates (account-money + RBAC follow-up):
-- 1) MANAGER: drop sales.write package; keep specialized sales codes.
-- 2) BRANCH_MANAGER: drop procurement.write package; keep specialized procurement.
-- 3) BRANCH_MANAGER: drop inventory.approve (approve = elevated loss gate).
-- 4) STAFF/CASHIER: strip residual sales.write.

-- MANAGER without sales.write
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'MANAGER'
  AND p.permission_code = 'sales.write';

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'MANAGER'
  AND p.permission_code IN (
      'sales.read',
      'sales.pos',
      'sales.customers',
      'sales.cancel',
      'sales.settings',
      'sales.discount'
  )
ON CONFLICT DO NOTHING;

-- BRANCH_MANAGER without procurement.write package
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code = 'procurement.write';

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code IN (
      'procurement.read',
      'procurement.suppliers',
      'procurement.po',
      'procurement.approve',
      'procurement.receive',
      'procurement.pay'
  )
ON CONFLICT DO NOTHING;

-- BRANCH_MANAGER: inventory ops without approve gate
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code = 'inventory.approve';

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code IN ('inventory.read', 'inventory.write')
ON CONFLICT DO NOTHING;

-- Cashiers / staff: never keep sales.write residual
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code IN ('STAFF', 'CASHIER')
  AND p.permission_code = 'sales.write';
