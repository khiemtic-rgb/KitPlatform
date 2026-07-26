-- KitPlatform 229: Prefer Pharmacy credentials as Kit account source of truth.
-- For emails that exist on a pharmacy tenant: kit_accounts.password_hash + display_name
-- come from the pharmacy user (DEMO_PHARMACY first). Then link orphan users and
-- sync their password_hash so email + tenant login share one password.

WITH pharmacy_primary AS (
    SELECT DISTINCT ON (lower(u.email::text))
        lower(u.email::text) AS email_key,
        u.email,
        u.password_hash,
        COALESCE(NULLIF(trim(e.full_name), ''), u.username) AS display_name
    FROM public.users u
    INNER JOIN public.tenants t ON t.id = u.tenant_id
    LEFT JOIN public.employees e ON e.id = u.employee_id
    WHERE u.deleted_at IS NULL
      AND t.deleted_at IS NULL
      AND t.business_vertical = 'pharmacy'
      AND u.email IS NOT NULL
      AND length(trim(u.email::text)) > 0
    ORDER BY
        lower(u.email::text),
        CASE WHEN t.tenant_code = 'DEMO_PHARMACY' THEN 0 ELSE 1 END,
        u.created_at ASC,
        u.id ASC
)
UPDATE public.kit_accounts a
SET password_hash = p.password_hash,
    display_name = p.display_name,
    updated_at = NOW()
FROM pharmacy_primary p
WHERE a.deleted_at IS NULL
  AND lower(a.email::text) = p.email_key
  AND (
      a.password_hash IS DISTINCT FROM p.password_hash
      OR a.display_name IS DISTINCT FROM p.display_name
  );

-- Ensure kit_account row exists for pharmacy emails not yet backfilled
WITH pharmacy_primary AS (
    SELECT DISTINCT ON (lower(u.email::text))
        u.email,
        u.password_hash,
        COALESCE(NULLIF(trim(e.full_name), ''), u.username) AS display_name
    FROM public.users u
    INNER JOIN public.tenants t ON t.id = u.tenant_id
    LEFT JOIN public.employees e ON e.id = u.employee_id
    WHERE u.deleted_at IS NULL
      AND t.deleted_at IS NULL
      AND t.business_vertical = 'pharmacy'
      AND u.email IS NOT NULL
    ORDER BY
        lower(u.email::text),
        CASE WHEN t.tenant_code = 'DEMO_PHARMACY' THEN 0 ELSE 1 END,
        u.created_at ASC,
        u.id ASC
)
INSERT INTO public.kit_accounts (email, password_hash, display_name, status)
SELECT p.email, p.password_hash, p.display_name, 1
FROM pharmacy_primary p
WHERE NOT EXISTS (
    SELECT 1 FROM public.kit_accounts a
    WHERE a.email = p.email AND a.deleted_at IS NULL
)
ON CONFLICT DO NOTHING;

-- Link every active user whose email matches a kit_account (pharmacy-preferred password already on account)
INSERT INTO public.kit_account_memberships (
    account_id, tenant_id, user_id, product_code, is_default, status
)
SELECT
    a.id,
    u.tenant_id,
    u.id,
    CASE lower(COALESCE(t.business_vertical, ''))
        WHEN 'pharmacy' THEN 'pharmacy'
        WHEN 'clinic' THEN 'clinic'
        WHEN 'family' THEN 'family_os'
        WHEN 'hybrid' THEN 'family_os'
        ELSE 'hybrid'
    END,
    FALSE,
    1
FROM public.users u
INNER JOIN public.tenants t ON t.id = u.tenant_id
INNER JOIN public.kit_accounts a
    ON a.email = u.email
   AND a.deleted_at IS NULL
WHERE u.deleted_at IS NULL
  AND t.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM public.kit_account_memberships m WHERE m.user_id = u.id
  )
ON CONFLICT DO NOTHING;

-- One default membership per account (prefer pharmacy workspace)
UPDATE public.kit_account_memberships m
SET is_default = FALSE
WHERE m.is_default = TRUE
  AND EXISTS (
      SELECT 1 FROM public.kit_accounts a
      WHERE a.id = m.account_id AND a.deleted_at IS NULL
  );

UPDATE public.kit_account_memberships m
SET is_default = TRUE
WHERE m.id IN (
    SELECT DISTINCT ON (m2.account_id) m2.id
    FROM public.kit_account_memberships m2
    INNER JOIN public.tenants t ON t.id = m2.tenant_id
    WHERE m2.status = 1
    ORDER BY
        m2.account_id,
        CASE WHEN t.business_vertical = 'pharmacy' THEN 0 ELSE 1 END,
        CASE WHEN t.tenant_code = 'DEMO_PHARMACY' THEN 0 ELSE 1 END,
        m2.created_at ASC,
        m2.id ASC
);

-- Sync password_hash on linked users → Kit account (pharmacy) hash
UPDATE public.users u
SET password_hash = a.password_hash,
    updated_at = NOW()
FROM public.kit_account_memberships m
INNER JOIN public.kit_accounts a ON a.id = m.account_id AND a.deleted_at IS NULL
WHERE m.user_id = u.id
  AND m.status = 1
  AND u.deleted_at IS NULL
  AND u.password_hash IS DISTINCT FROM a.password_hash
  AND EXISTS (
      SELECT 1
      FROM public.users up
      INNER JOIN public.tenants tp ON tp.id = up.tenant_id
      WHERE up.deleted_at IS NULL
        AND tp.deleted_at IS NULL
        AND tp.business_vertical = 'pharmacy'
        AND lower(up.email::text) = lower(a.email::text)
  );
