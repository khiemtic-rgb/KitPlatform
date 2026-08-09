-- RBAC template trim (post-clinic SoD / smoke matrix follow-up):
-- 1) STAFF: drop system.read — cashier-tier must not browse Users / Roles /
--    Branches / audit via System module (least privilege).
-- 2) BRANCH_MANAGER: drop survey.read / survey.write — KAP lead PII stays with
--    ADMIN / MANAGER (event-tenant scoped). Branch ops do not need KAP by default.
-- Idempotent DELETEs; ADMIN / MANAGER survey.* and system.read are untouched.

-- 1) STAFF without system.read
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'STAFF'
  AND p.permission_code = 'system.read';

-- 2) BRANCH_MANAGER without survey.*
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code = 'BRANCH_MANAGER'
  AND p.permission_code IN ('survey.read', 'survey.write');
