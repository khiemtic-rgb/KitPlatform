-- KitPlatform 154: KAP event tenant NVX-CS08 (independent from pharmacy NT)
-- Depends on: 108 (tenants/roles patterns), 083/084 (pharmacy_survey), 136 (survey.* perms)
-- Fixed UUIDs for reproducible local/prod smoke. Password = Admin@123 (same as demo seeds).

-- =============================================================================
-- NVX-CS08 — tenant sự kiện KAP (Survey pack only; not pharmacy ops)
-- =============================================================================
INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency,
    business_vertical, settings, status
)
VALUES (
    '11111111-1111-1111-1111-111111111103',
    'NVX-CS08',
    'Novixa KAP — Sự kiện CS08',
    'VN', 'VND',
    'pharmacy',
    jsonb_build_object(
        'platform', jsonb_build_object(
            'vertical', 'pharmacy',
            'enabled_modules', jsonb_build_array('assessment', 'pharmacy_survey'),
            'allowed_modules', jsonb_build_array('assessment', 'pharmacy_survey', 'reports')
        )
    ),
    1
)
ON CONFLICT (tenant_code) DO UPDATE SET
    tenant_name = EXCLUDED.tenant_name,
    business_vertical = 'pharmacy',
    settings = jsonb_set(
        COALESCE(public.tenants.settings, '{}'::jsonb),
        '{platform}',
        COALESCE(public.tenants.settings->'platform', '{}'::jsonb)
            || jsonb_build_object(
                'vertical', 'pharmacy',
                'enabled_modules', (
                    SELECT COALESCE(jsonb_agg(DISTINCT x), '[]'::jsonb)
                    FROM jsonb_array_elements(
                        COALESCE(public.tenants.settings->'platform'->'enabled_modules', '[]'::jsonb)
                        || '["assessment","pharmacy_survey"]'::jsonb
                    ) AS t(x)
                ),
                'allowed_modules', (
                    SELECT COALESCE(jsonb_agg(DISTINCT x), '[]'::jsonb)
                    FROM jsonb_array_elements(
                        COALESCE(public.tenants.settings->'platform'->'allowed_modules', '[]'::jsonb)
                        || '["assessment","pharmacy_survey","reports"]'::jsonb
                    ) AS t(x)
                )
            ),
        true
    ),
    updated_at = NOW(),
    deleted_at = NULL,
    status = 1;

INSERT INTO public.branches (
    id, tenant_id, branch_code, branch_name, address, phone, is_head_office, status
)
VALUES (
    '11111111-1111-1111-1111-111111111203',
    '11111111-1111-1111-1111-111111111103',
    'CS08',
    'Vận hành KAP',
    'Novixa — Survey / KAP',
    '0240000008',
    TRUE,
    1
)
ON CONFLICT (tenant_id, branch_code) DO UPDATE SET
    branch_name = EXCLUDED.branch_name,
    address = EXCLUDED.address,
    phone = EXCLUDED.phone,
    is_head_office = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.employees (
    id, tenant_id, employee_code, full_name, phone, email, status
)
VALUES (
    '11111111-1111-1111-1111-111111111303',
    '11111111-1111-1111-1111-111111111103',
    'EMP001',
    'Admin KAP CS08',
    '0908000008',
    'admin@kap-cs08.novixa.vn',
    1
)
ON CONFLICT (tenant_id, employee_code) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone = EXCLUDED.phone,
    email = EXCLUDED.email,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.users (
    id, tenant_id, employee_id, username, email, password_hash, status
)
VALUES (
    '11111111-1111-1111-1111-111111111403',
    '11111111-1111-1111-1111-111111111103',
    '11111111-1111-1111-1111-111111111303',
    'admin',
    'admin@kap-cs08.novixa.vn',
    '$2a$11$Oq8dLLVbqREcBk4VBW0ELOuBQneydTDK7VLpR9FcHEiQdWoUTQyJS',
    1
)
ON CONFLICT (tenant_id, username) DO UPDATE SET
    employee_id = EXCLUDED.employee_id,
    email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.roles (id, tenant_id, role_code, role_name)
VALUES (
    '11111111-1111-1111-1111-111111111503',
    '11111111-1111-1111-1111-111111111103',
    'ADMIN',
    'Quản trị viên'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111403',
    '11111111-1111-1111-1111-111111111503'
)
ON CONFLICT DO NOTHING;

-- Full permission set for KAP admin shell (login + survey + system)
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111503', p.id
FROM public.permissions p
WHERE NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111503'
      AND rp.permission_id = p.id
);

-- Survey workspace for this event tenant
SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111103'::uuid,
    'pharmacy_survey'
);

-- =============================================================================
-- Keep KAP sidebar off pharmacy/clinic ops tenants (EventTenant gate remains source of truth)
-- =============================================================================
UPDATE public.tenants t
SET
    settings = jsonb_set(
        t.settings,
        '{platform,enabled_modules}',
        COALESCE((
            SELECT jsonb_agg(to_jsonb(m))
            FROM jsonb_array_elements_text(
                COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
            ) AS m
            WHERE m NOT IN ('assessment', 'pharmacy_survey')
        ), '[]'::jsonb),
        true
    ),
    updated_at = NOW()
WHERE t.deleted_at IS NULL
  AND t.tenant_code <> 'NVX-CS08'
  AND t.settings ? 'platform'
  AND t.settings->'platform' ? 'enabled_modules'
  AND (
      t.settings->'platform'->'enabled_modules' ? 'assessment'
      OR t.settings->'platform'->'enabled_modules' ? 'pharmacy_survey'
  );
