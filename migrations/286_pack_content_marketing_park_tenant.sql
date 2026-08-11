-- KitPlatform 286: Marketing Park product isolation
-- tenant_code KIT_MKT + tenant_package marketing_park
-- Independent of Novixa Pharmacy / Famixa FamilyOS / KAP tenants.
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Depends on: 281 (pack_content), 079 (kit_provision_pack_workspace)

-- =============================================================================
-- Platform module + tenant package
-- =============================================================================
UPDATE platform_module_registry
SET
    module_name = 'KIT Marketing Park',
    description = 'Independent content & marketing factory (multi-brand). Not Pharmacy/Family ERP.',
    verticals = ARRAY['marketing', 'hybrid']
WHERE module_code = 'kit_content';

INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT
    'kit_content',
    'KIT Marketing Park',
    'Independent content & marketing factory (multi-brand). Not Pharmacy/Family ERP.',
    ARRAY['marketing', 'hybrid'],
    90
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'kit_content'
);

INSERT INTO kit_tenant.tenant_package (
    package_code, package_name, description, verticals, module_codes, sort_order
)
VALUES (
    'marketing_park',
    'KIT Marketing Park',
    'AI content & marketing workspace — brands, topics, generate, publish. Own org KIT_MKT.',
    ARRAY['marketing', 'hybrid'],
    ARRAY['kit_content'],
    95
)
ON CONFLICT (package_code) DO UPDATE SET
    package_name = EXCLUDED.package_name,
    description = EXCLUDED.description,
    verticals = EXCLUDED.verticals,
    module_codes = EXCLUDED.module_codes,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

-- =============================================================================
-- KIT_MKT — dedicated Marketing Park tenant (password = Admin@123)
-- =============================================================================
INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency,
    business_vertical, settings, status
)
VALUES (
    '11111111-1111-1111-1111-111111111105',
    'KIT_MKT',
    'KIT Marketing Park',
    'VN', 'VND',
    'hybrid',
    jsonb_build_object(
        'platform', jsonb_build_object(
            'vertical', 'marketing',
            'enabled_modules', jsonb_build_array('kit_content'),
            'allowed_modules', jsonb_build_array('kit_content'),
            'features', jsonb_build_object()
        ),
        'product', jsonb_build_object(
            'package_code', 'marketing_park',
            'org_code', 'KIT_MKT'
        )
    ),
    1
)
ON CONFLICT (tenant_code) DO UPDATE SET
    tenant_name = EXCLUDED.tenant_name,
    business_vertical = EXCLUDED.business_vertical,
    settings = EXCLUDED.settings,
    updated_at = NOW(),
    deleted_at = NULL,
    status = 1;

INSERT INTO public.branches (
    id, tenant_id, branch_code, branch_name, address, phone, is_head_office, status
)
VALUES (
    '11111111-1111-1111-1111-111111111205',
    '11111111-1111-1111-1111-111111111105',
    'HQ',
    'Marketing HQ',
    'KIT Marketing Park — independent product',
    '0240000005',
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
    '11111111-1111-1111-1111-111111111305',
    '11111111-1111-1111-1111-111111111105',
    'EMP001',
    'Admin Marketing Park',
    '0905000005',
    'admin@kit-mkt.kittech.vn',
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
    '11111111-1111-1111-1111-111111111405',
    '11111111-1111-1111-1111-111111111105',
    '11111111-1111-1111-1111-111111111305',
    'admin',
    'admin@kit-mkt.kittech.vn',
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
    '11111111-1111-1111-1111-111111111505',
    '11111111-1111-1111-1111-111111111105',
    'ADMIN',
    'Quản trị viên'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111405',
    '11111111-1111-1111-1111-111111111505'
)
ON CONFLICT DO NOTHING;

-- KIT_MKT ADMIN: system.* + content.* only (no pharmacy/clinic/family packs)
DELETE FROM public.role_permissions rp
USING public.permissions p
WHERE rp.role_id = '11111111-1111-1111-1111-111111111505'
  AND rp.permission_id = p.id
  AND p.permission_code NOT LIKE 'system.%'
  AND p.permission_code NOT LIKE 'content.%';

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111505', p.id
FROM public.permissions p
WHERE (p.permission_code LIKE 'system.%' OR p.permission_code LIKE 'content.%')
  AND NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111505'
      AND rp.permission_id = p.id
  );

SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111105'::uuid,
    'marketing_park'
);

-- =============================================================================
-- Unbundle Marketing from Novixa / Famixa / other ops tenants
-- =============================================================================

-- Stop granting content.* to every ADMIN (wave0 did that for all tenants)
DELETE FROM public.role_permissions rp
USING public.roles r, public.permissions p, public.tenants t
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.tenant_id = t.id
  AND r.role_code = 'ADMIN'
  AND p.permission_code LIKE 'content.%'
  AND t.tenant_code <> 'KIT_MKT';

-- Remove kit_content from enabled/allowed modules on non-marketing tenants
UPDATE public.tenants t
SET
    settings = jsonb_set(
        jsonb_set(
            t.settings,
            '{platform,enabled_modules}',
            COALESCE((
                SELECT jsonb_agg(to_jsonb(m))
                FROM jsonb_array_elements_text(
                    COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                ) AS m
                WHERE m <> 'kit_content'
            ), '[]'::jsonb),
            true
        ),
        '{platform,allowed_modules}',
        COALESCE((
            SELECT jsonb_agg(to_jsonb(m))
            FROM jsonb_array_elements_text(
                COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)
            ) AS m
            WHERE m <> 'kit_content'
        ), COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)),
        true
    ),
    updated_at = NOW()
WHERE t.deleted_at IS NULL
  AND t.tenant_code <> 'KIT_MKT'
  AND t.settings ? 'platform'
  AND (
      COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb) ? 'kit_content'
      OR COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb) ? 'kit_content'
  );
