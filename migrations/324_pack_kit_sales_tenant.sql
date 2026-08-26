-- KitPlatform 324: KIT Sales dedicated tenant (isolated park)
-- Pack/module/package: kit_sales | Tenant: KIT_SALES | Schema: pack_sales
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- Replaces mistaken "enable on KIT_MKT" approach — Sales is NOT Marketing Park.

-- =============================================================================
-- Undo: strip kit_sales from KIT_MKT (and any non-KIT_SALES tenant)
-- =============================================================================
DELETE FROM public.role_permissions rp
USING public.roles r, public.permissions p, public.tenants t
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.tenant_id = t.id
  AND p.permission_code LIKE 'kit_sales.%'
  AND t.tenant_code <> 'KIT_SALES';

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
                WHERE m <> 'kit_sales'
            ), '[]'::jsonb),
            true
        ),
        '{platform,allowed_modules}',
        COALESCE((
            SELECT jsonb_agg(to_jsonb(m))
            FROM jsonb_array_elements_text(
                COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)
            ) AS m
            WHERE m <> 'kit_sales'
        ), COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)),
        true
    ),
    updated_at = NOW()
WHERE t.tenant_code <> 'KIT_SALES'
  AND (
        COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb) ? 'kit_sales'
     OR COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb) ? 'kit_sales'
  );

-- =============================================================================
-- KIT_SALES — dedicated acquisition pipeline tenant (password = Admin@123)
-- =============================================================================
INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency,
    business_vertical, settings, status
)
VALUES (
    '11111111-1111-1111-1111-111111111107',
    'KIT_SALES',
    'KIT Sales',
    'VN', 'VND',
    'hybrid',
    jsonb_build_object(
        'platform', jsonb_build_object(
            'vertical', 'marketing',
            'enabled_modules', jsonb_build_array('kit_sales'),
            'allowed_modules', jsonb_build_array('kit_sales'),
            'features', jsonb_build_object()
        ),
        'product', jsonb_build_object(
            'package_code', 'kit_sales',
            'org_code', 'KIT_SALES'
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
    '11111111-1111-1111-1111-111111111207',
    '11111111-1111-1111-1111-111111111107',
    'HQ',
    'KIT Sales HQ',
    'KIT Sales — acquisition pipeline (independent park)',
    '0240000007',
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
    '11111111-1111-1111-1111-111111111307',
    '11111111-1111-1111-1111-111111111107',
    'EMP001',
    'Admin KIT Sales',
    '0907000007',
    'admin@kit-sales.kittech.vn',
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
    '11111111-1111-1111-1111-111111111407',
    '11111111-1111-1111-1111-111111111107',
    '11111111-1111-1111-1111-111111111307',
    'admin',
    'admin@kit-sales.kittech.vn',
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
    '11111111-1111-1111-1111-111111111507',
    '11111111-1111-1111-1111-111111111107',
    'ADMIN',
    'Quản trị viên'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111407',
    '11111111-1111-1111-1111-111111111507'
)
ON CONFLICT DO NOTHING;

-- KIT_SALES ADMIN: system.* + kit_sales.* only (no content / local_os / pharmacy)
DELETE FROM public.role_permissions rp
USING public.permissions p
WHERE rp.role_id = '11111111-1111-1111-1111-111111111507'
  AND rp.permission_id = p.id
  AND p.permission_code NOT LIKE 'system.%'
  AND p.permission_code NOT LIKE 'kit_sales.%';

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111507', p.id
FROM public.permissions p
WHERE (p.permission_code LIKE 'system.%' OR p.permission_code LIKE 'kit_sales.%')
  AND NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111507'
      AND rp.permission_id = p.id
  );

SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111107'::uuid,
    'kit_sales'
);

-- Drop obsolete migration stamp if a prior mistaken 324 was applied under the old name
DELETE FROM kit_schema_migrations
WHERE filename = '324_pack_kit_sales_kit_mkt_enable.sql';

INSERT INTO kit_schema_migrations (filename) VALUES ('324_pack_kit_sales_tenant.sql')
ON CONFLICT (filename) DO NOTHING;
