-- Restore DEMO_PHARMACY full Admin nav after thẩm định (for customer demos).
-- ONLY tenant_code = 'DEMO_PHARMACY'. Never touches NT_XUANHOA.
--
-- Usage (VPS): apply via psql against prod DB, then hard-refresh admin.novixa.vn.
-- Docs: docs/novixa/06-compliance/pharmacy-audit-handoff-demo-pharmacy-v1.md §5.3

UPDATE public.tenants
SET
  settings = jsonb_set(
    jsonb_set(
      jsonb_set(
        COALESCE(settings, '{}'::jsonb),
        '{platform,features,audit_slim_nav}',
        'false'::jsonb,
        true
      ),
      '{platform,enabled_modules}',
      (
        SELECT COALESCE(jsonb_agg(to_jsonb(m) ORDER BY m), '[]'::jsonb)
        FROM (
          SELECT DISTINCT m
          FROM (
            SELECT jsonb_array_elements_text(
              COALESCE(settings->'platform'->'enabled_modules', '[]'::jsonb)
            ) AS m
            UNION ALL
            SELECT unnest(ARRAY['customer_app', 'reservations', 'learning'])
          ) s
        ) d
      ),
      true
    ),
    '{platform,allowed_modules}',
    (
      SELECT COALESCE(jsonb_agg(to_jsonb(m) ORDER BY m), '[]'::jsonb)
      FROM (
        SELECT DISTINCT m
        FROM (
          SELECT jsonb_array_elements_text(
            COALESCE(settings->'platform'->'allowed_modules', '[]'::jsonb)
          ) AS m
          UNION ALL
          SELECT unnest(ARRAY[
            'customer_app', 'reservations', 'learning',
            'prescriber_network', 'prescriber_portal'
          ])
        ) s
      ) d
    ),
    true
  ),
  updated_at = NOW()
WHERE tenant_code = 'DEMO_PHARMACY'
  AND deleted_at IS NULL;

-- Verify
SELECT
  tenant_code,
  settings->'platform'->'features'->'audit_slim_nav' AS audit_slim_nav,
  settings->'platform'->'enabled_modules' AS enabled_modules
FROM public.tenants
WHERE tenant_code = 'DEMO_PHARMACY'
  AND deleted_at IS NULL;
