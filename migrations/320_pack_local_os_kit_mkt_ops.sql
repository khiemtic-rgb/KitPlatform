-- KitPlatform 320: KIT_MKT ADMIN may operate Thái Nguyên Life listings
-- Manifest: deploy/ubuntu/migration-files.local-os.txt ONLY
-- Data stays in pack_local. Vertical stays marketing. No Pharmacy / Family.

-- Enable local_os on KIT_MKT (keep kit_content). Do not change vertical.
UPDATE public.tenants t
SET
    settings = jsonb_set(
        jsonb_set(
            t.settings,
            '{platform,enabled_modules}',
            (
                SELECT COALESCE(jsonb_agg(to_jsonb(m)), '[]'::jsonb)
                FROM (
                    SELECT DISTINCT m
                    FROM jsonb_array_elements_text(
                        COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                        || '["local_os"]'::jsonb
                    ) AS m
                ) uniq
            ),
            true
        ),
        '{platform,allowed_modules}',
        (
            SELECT COALESCE(jsonb_agg(to_jsonb(m)), '[]'::jsonb)
            FROM (
                SELECT DISTINCT m
                FROM jsonb_array_elements_text(
                    COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)
                    || '["local_os"]'::jsonb
                ) AS m
            ) uniq
        ),
        true
    ),
    updated_at = NOW()
WHERE t.tenant_code = 'KIT_MKT';

-- KIT_MKT ADMIN: system.* + content.* + local_os.* (still no pharmacy/family)
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.tenants t
JOIN public.roles r ON r.tenant_id = t.id AND r.role_code = 'ADMIN'
JOIN public.permissions p ON p.permission_code LIKE 'local_os.%'
WHERE t.tenant_code = 'KIT_MKT'
  AND NOT EXISTS (
      SELECT 1
      FROM public.role_permissions rp
      WHERE rp.role_id = r.id
        AND rp.permission_id = p.id
  );
