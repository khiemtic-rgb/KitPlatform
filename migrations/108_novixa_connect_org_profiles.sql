-- KitPlatform 108: Novixa Connect — durable org profiles + DEMO_CLINIC pilot
-- Depends on: 107_novixa_connect_doctor_membership.sql
-- Clinic Lite (pack_clinic / appointments / EMR) is NOT enabled here.

-- =============================================================================
-- Connect org profile (pharmacy | clinic) — durable identity for C1/C2 gates
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_connect.org_profiles (
    tenant_id       UUID PRIMARY KEY REFERENCES public.tenants(id),
    org_kind        VARCHAR(20) NOT NULL,
    display_name    VARCHAR(255),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_connect_org_profiles_kind CHECK (
        org_kind IN ('pharmacy', 'clinic')
    )
);

COMMENT ON TABLE pack_connect.org_profiles IS
    'Connect org identity. Clinic here is network org — not Clinic Lite clinical pack.';

-- Backfill known pharmacy pilots
INSERT INTO pack_connect.org_profiles (tenant_id, org_kind, display_name)
SELECT t.id, 'pharmacy', t.tenant_name
FROM public.tenants t
WHERE t.tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY')
  AND t.deleted_at IS NULL
ON CONFLICT (tenant_id) DO UPDATE SET
    org_kind = EXCLUDED.org_kind,
    display_name = COALESCE(pack_connect.org_profiles.display_name, EXCLUDED.display_name),
    updated_at = NOW();

-- =============================================================================
-- DEMO_CLINIC — real Clinic org for Connect (no Clinic Lite modules)
-- Fixed UUIDs for reproducible local smoke
-- =============================================================================
INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency,
    business_vertical, settings, status
)
VALUES (
    '11111111-1111-1111-1111-111111111102',
    'DEMO_CLINIC',
    'Phòng khám Demo Connect',
    'VN', 'VND',
    'clinic',
    jsonb_build_object(
        'platform', jsonb_build_object(
            'vertical', 'clinic',
            'enabled_modules', jsonb_build_array('novixa_connect')
        )
    ),
    1
)
ON CONFLICT (tenant_code) DO UPDATE SET
    tenant_name = EXCLUDED.tenant_name,
    business_vertical = 'clinic',
    settings = jsonb_set(
        COALESCE(public.tenants.settings, '{}'::jsonb),
        '{platform}',
        COALESCE(public.tenants.settings->'platform', '{}'::jsonb)
            || jsonb_build_object(
                'vertical', 'clinic',
                'enabled_modules', (
                    SELECT jsonb_agg(DISTINCT x)
                    FROM jsonb_array_elements(
                        COALESCE(public.tenants.settings->'platform'->'enabled_modules', '[]'::jsonb)
                        || jsonb_build_array('novixa_connect')
                    ) AS t(x)
                )
            ),
        true
    ),
    updated_at = NOW(),
    deleted_at = NULL;

INSERT INTO public.branches (
    id, tenant_id, branch_code, branch_name, address, phone, is_head_office, status
)
VALUES (
    '11111111-1111-1111-1111-111111111202',
    '11111111-1111-1111-1111-111111111102',
    'PK01',
    'Cơ sở chính',
    '456 Đường Demo, Hà Nội',
    '0243987654',
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
    '11111111-1111-1111-1111-111111111302',
    '11111111-1111-1111-1111-111111111102',
    'EMP001',
    'Admin Phòng khám Demo',
    '0902000001',
    'admin@demo-clinic.novixa.vn',
    1
)
ON CONFLICT (tenant_id, employee_code) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone = EXCLUDED.phone,
    email = EXCLUDED.email,
    deleted_at = NULL,
    updated_at = NOW();

-- Password = Admin@123 (same hash as DEMO_PHARMACY seed)
INSERT INTO public.users (
    id, tenant_id, employee_id, username, email, password_hash, status
)
VALUES (
    '11111111-1111-1111-1111-111111111402',
    '11111111-1111-1111-1111-111111111102',
    '11111111-1111-1111-1111-111111111302',
    'admin',
    'admin@demo-clinic.novixa.vn',
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
    '11111111-1111-1111-1111-111111111502',
    '11111111-1111-1111-1111-111111111102',
    'ADMIN',
    'Quản trị viên'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111402',
    '11111111-1111-1111-1111-111111111502'
)
ON CONFLICT DO NOTHING;

-- Grant existing permissions to clinic ADMIN (login/admin shell)
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111502', p.id
FROM public.permissions p
WHERE NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111502'
      AND rp.permission_id = p.id
);

INSERT INTO pack_connect.org_profiles (tenant_id, org_kind, display_name)
VALUES (
    '11111111-1111-1111-1111-111111111102',
    'clinic',
    'Phòng khám Demo Connect'
)
ON CONFLICT (tenant_id) DO UPDATE SET
    org_kind = 'clinic',
    display_name = EXCLUDED.display_name,
    updated_at = NOW();

INSERT INTO pack_connect.directory_opt_in (tenant_id, discoverable)
VALUES ('11111111-1111-1111-1111-111111111102', TRUE)
ON CONFLICT (tenant_id) DO UPDATE SET
    discoverable = TRUE,
    updated_at = NOW();

-- Workspace for Connect pack
SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111102'::uuid,
    'novixa_connect'
);
