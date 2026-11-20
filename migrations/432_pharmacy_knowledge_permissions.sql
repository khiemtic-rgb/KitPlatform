-- Tenant-level pharmacy knowledge permission so the role editor can assign it.
-- Not in migration-files.prod.txt. Ships with migration-files.pharmacy-knowledge-v1.txt only.
-- Idempotent. Touches only permissions and role_permissions (insert if missing).
--
-- platform.knowledge.publish, pharmacy.knowledge.review and pharmacy.knowledge.verify-source are
-- deliberately NOT inserted: they act on the shared catalog for every tenant, while new tenants copy
-- every row of permissions into their ADMIN role and tenant admins may assign any listed code.
-- Grant them per platform operator by hand.

BEGIN;

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES ('pharmacy.knowledge.approve', 'Ghim quy tắc tri thức cho nhà thuốc', 'Tri thức nhà thuốc')
ON CONFLICT (permission_code) DO NOTHING;

-- ADMIN already passes the approve policy by role; this row only makes the grant visible.
-- NT_XUANHOA is skipped while the Pharmacy audit freeze is in force.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
JOIN tenants t ON t.id = r.tenant_id
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND t.tenant_code <> 'NT_XUANHOA'
  AND p.permission_code = 'pharmacy.knowledge.approve'
  AND NOT EXISTS (
      SELECT 1 FROM role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id);

COMMIT;
