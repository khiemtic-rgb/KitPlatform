-- RBAC template: restore KAP for MANAGER after 151 stripped BRANCH_MANAGER.
-- MANAGER (store-wide ops) needs survey.read/write for event-tenant KAP admin;
-- BRANCH_MANAGER stays without survey.* (151). Idempotent.

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'MANAGER'
  AND p.permission_code IN ('survey.read', 'survey.write')
ON CONFLICT DO NOTHING;
