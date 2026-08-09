-- RBAC follow-up (staff-facing consistency):
-- CASHIER is a POS-only till role. Customer relationship surfaces
-- (receivables, customer payments, customer chat) require sales.customers,
-- which CASHIER should not hold. Strip it; keep POS + read.

DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'CASHIER'
  AND p.permission_code = 'sales.customers';
