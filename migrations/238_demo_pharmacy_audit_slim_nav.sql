-- 238: DEMO_PHARMACY audit slim nav (idempotent)
-- ONLY tenant_code = DEMO_PHARMACY. Never mutates NT_XUANHOA.
-- Hides out-of-scope UI for thẩm định via features.audit_slim_nav + drop customer_app.

UPDATE public.tenants
SET
  settings = jsonb_set(
    jsonb_set(
      jsonb_set(
        COALESCE(settings, '{}'::jsonb),
        '{platform,enabled_modules}',
        (
          SELECT COALESCE(jsonb_agg(to_jsonb(x)), '[]'::jsonb)
          FROM jsonb_array_elements_text(
            COALESCE(settings->'platform'->'enabled_modules', '[]'::jsonb)
          ) AS x
          WHERE x NOT IN ('customer_app', 'reservations', 'learning')
        ),
        true
      ),
      '{platform,allowed_modules}',
      (
        SELECT COALESCE(jsonb_agg(to_jsonb(x)), '[]'::jsonb)
        FROM jsonb_array_elements_text(
          COALESCE(settings->'platform'->'allowed_modules', '[]'::jsonb)
        ) AS x
        WHERE x NOT IN ('customer_app', 'reservations', 'learning')
      ),
      true
    ),
    '{platform,features,audit_slim_nav}',
    'true'::jsonb,
    true
  ),
  updated_at = NOW()
WHERE tenant_code = 'DEMO_PHARMACY'
  AND deleted_at IS NULL;
