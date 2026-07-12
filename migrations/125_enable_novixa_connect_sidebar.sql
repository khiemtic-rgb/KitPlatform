-- KitPlatform 125: Ensure Connect module enabled on pharmacy + clinic pilot tenants
-- Depends on: 108_novixa_connect_org_profiles.sql, 106_novixa_connect_org_links.sql
--
-- Sidebar hides Connect when settings.platform.enabled_modules lacks novixa_connect.

UPDATE public.tenants t
SET
    settings = jsonb_set(
        COALESCE(t.settings, '{}'::jsonb),
        '{platform,enabled_modules}',
        (
            SELECT COALESCE(jsonb_agg(to_jsonb(m)), '[]'::jsonb)
            FROM (
                SELECT DISTINCT m
                FROM jsonb_array_elements_text(
                    COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                    || '["novixa_connect"]'::jsonb
                ) AS m
            ) d
        ),
        true
    ),
    updated_at = NOW()
WHERE t.deleted_at IS NULL
  AND (
        t.tenant_code IN ('NT_XUANHOA', 'DEMO_CLINIC', 'DEMO_PHARMACY')
     OR EXISTS (
            SELECT 1
            FROM pack_connect.org_profiles p
            WHERE p.tenant_id = t.id
        )
     OR EXISTS (
            SELECT 1
            FROM pack_connect.org_links l
            WHERE l.initiator_tenant_id = t.id
               OR l.partner_tenant_id = t.id
        )
  );

SELECT kit_provision_pack_workspace(t.id, 'novixa_connect')
FROM public.tenants t
WHERE t.deleted_at IS NULL
  AND (
        t.tenant_code IN ('NT_XUANHOA', 'DEMO_CLINIC', 'DEMO_PHARMACY')
     OR EXISTS (
            SELECT 1 FROM pack_connect.org_profiles p WHERE p.tenant_id = t.id
        )
  );
