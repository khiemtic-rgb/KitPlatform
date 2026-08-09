-- KitPlatform 153: Ensure MANAGER role exists + KAP survey.* (follow-up to 152).
-- Prod had zero MANAGER roles (only BRANCH_MANAGER / ADMIN / …), so 152 was a no-op.
-- Seed MANAGER for active tenants; grant survey.read/write + lean store-wide ops
-- aligned with prior RBAC intent (not BRANCH_MANAGER breadth).

INSERT INTO roles (tenant_id, role_code, role_name, description)
SELECT t.id, 'MANAGER', 'Quản lý', 'Quản lý cửa hàng (store-wide, không system.write)'
FROM tenants t
WHERE t.deleted_at IS NULL
  AND t.status = 1
  AND NOT EXISTS (
      SELECT 1 FROM roles r
      WHERE r.tenant_id = t.id AND r.role_code = 'MANAGER'
  );

-- KAP for MANAGER (event-tenant scoped in AssessmentAdminService)
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'MANAGER'
  AND p.permission_code IN ('survey.read', 'survey.write')
ON CONFLICT DO NOTHING;

-- Lean store-wide package (idempotent; ADMIN already has broader grants)
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'MANAGER'
  AND p.permission_code IN (
      'system.read',
      'system.audit.read',
      'reports.read',
      'reports.export',
      'success.read',
      'sales.read',
      'sales.pos',
      'sales.customers',
      'sales.cancel',
      'sales.settings',
      'sales.discount',
      'procurement.read',
      'procurement.suppliers',
      'procurement.po',
      'procurement.approve',
      'procurement.receive',
      'procurement.pay',
      'inventory.read',
      'inventory.write',
      'catalog.read',
      'connect.read',
      'connect.write'
  )
ON CONFLICT DO NOTHING;
