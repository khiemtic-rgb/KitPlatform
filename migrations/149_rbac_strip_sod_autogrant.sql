-- RBAC follow-up (post-148): migration 148 §3 blanket-granted the SoD gates
-- (approve / receive / pay) to EVERY role holding procurement.write, to avoid
-- breaking prod when the policies stopped accepting write. That defeats SoD.
--
-- 149 re-strips those elevated gates from procurement.write holders, except:
--   * ADMIN   — bypasses policies anyway.
--   * MANAGER — template intent: managers approve / receive / pay.
-- BRANCH_MANAGER keeps its explicit specialized grants from 146 (it does not
-- hold procurement.write, so it is not touched here).

DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code NOT IN ('ADMIN', 'MANAGER')
  AND p.permission_code IN ('procurement.approve', 'procurement.receive', 'procurement.pay')
  AND EXISTS (
      SELECT 1
      FROM role_permissions rp2
      INNER JOIN permissions p2 ON p2.id = rp2.permission_id
      WHERE rp2.role_id = r.id
        AND p2.permission_code = 'procurement.write'
  );

-- Keep the benign part of the write package (read + suppliers + po) — already
-- granted by 148 and matches the current WRITE_IMPLIES_PACKAGE semantics.
