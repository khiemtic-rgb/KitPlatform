-- KitPlatform 386: permission content.canon.administer
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Grants only the ADMIN role of tenant KIT_MKT, resolved through public.tenants.
-- Does not use a content.% wildcard and does not change existing marketing permissions.
-- Does not alter canon tables, add tenant_id, enable RLS, or backfill canon data.

INSERT INTO public.permissions (permission_code, permission_name, module_name)
VALUES (
    'content.canon.administer',
    'Content — quản trị canon',
    'content'
)
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Drop this permission from every role that is not the live KIT_MKT ADMIN.
-- Keeps a replay of the older content.% grant from leaving it on another tenant.
DELETE FROM public.role_permissions rp
USING public.roles r, public.tenants t, public.permissions p
WHERE rp.role_id = r.id
  AND r.tenant_id = t.id
  AND rp.permission_id = p.id
  AND p.permission_code = 'content.canon.administer'
  AND NOT (
      t.tenant_code = 'KIT_MKT'
      AND t.deleted_at IS NULL
      AND r.role_code = 'ADMIN'
  );

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.tenants t
JOIN public.roles r
    ON r.tenant_id = t.id
   AND r.role_code = 'ADMIN'
JOIN public.permissions p
    ON p.permission_code = 'content.canon.administer'
WHERE t.tenant_code = 'KIT_MKT'
  AND t.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.role_permissions existing
      WHERE existing.role_id = r.id
        AND existing.permission_id = p.id
  );
