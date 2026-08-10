-- FamilyOS GTM demo house — schoolSchedule blueprint + viewer login (DEV ONLY)
-- Depends on: 006–011, migration 222 (membership.user_id), 249 (family_blueprint)
-- Idempotent.
--
-- Household already: Mẹ, Bố, Bảo Nhi, Đức Huy + summer/school calendar periods.
-- This file adds:
--   • layers.members.<child>.schoolSchedule (seasonOn=false while summer 2026)
--   • demo / Admin@123 viewer account (read house; server blocks mutate)
--   • durable viewer invite code FAMIXADEM (optional join path)

-- =============================================================================
-- Blueprint: school hours for both kids (season off during summer period)
-- =============================================================================
INSERT INTO pack_family.family_blueprint (
    id, tenant_id, family_id, layers_json, dna_json, schema_version, hydrated_at
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad01',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    jsonb_build_object(
        'members', jsonb_build_object(
            'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', jsonb_build_object(
                'schoolSchedule', jsonb_build_object(
                    'schemaVersion', 1,
                    'seasonOn', false,
                    'mode', 'full',
                    'weekdays', jsonb_build_array(1, 2, 3, 4, 5),
                    'schoolStart', '07:00',
                    'schoolEnd', '16:30',
                    'hasExtraClass', false,
                    'source', 'onboarding_seed',
                    'updatedAt', '2026-08-10T00:00:00.000Z'
                )
            ),
            'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', jsonb_build_object(
                'schoolSchedule', jsonb_build_object(
                    'schemaVersion', 1,
                    'seasonOn', false,
                    'mode', 'full',
                    'weekdays', jsonb_build_array(1, 2, 3, 4, 5),
                    'schoolStart', '07:15',
                    'schoolEnd', '16:00',
                    'hasExtraClass', false,
                    'source', 'onboarding_seed',
                    'updatedAt', '2026-08-10T00:00:00.000Z'
                )
            )
        ),
        'stage', jsonb_build_object(
            'primaryAgeBand', 'primary',
            'labelVi', 'Tiểu học'
        ),
        'focus', jsonb_build_object(
            'codes', jsonb_build_array('rhythm', 'school_season'),
            'labelVi', 'Nhịp nhà · Mùa đi học'
        )
    ),
    jsonb_build_object(
        'stageLabelVi', 'Tiểu học — 2 bé',
        'valuesLabelsVi', jsonb_build_array('Nhịp đều', 'Tự lập'),
        'focusLabelsVi', jsonb_build_array('Mùa hè → tự học', 'Chuẩn bị năm học mới'),
        'nextStepVi', 'Từ 25/08 bật mùa đi học trong Cài đặt để mở quiet giờ học.'
    ),
    1,
    NOW()
)
ON CONFLICT (tenant_id, family_id) DO UPDATE SET
    layers_json = pack_family.family_blueprint.layers_json
        || EXCLUDED.layers_json
        || jsonb_build_object(
            'members',
            COALESCE(pack_family.family_blueprint.layers_json->'members', '{}'::jsonb)
                || (EXCLUDED.layers_json->'members')
        ),
    dna_json = COALESCE(pack_family.family_blueprint.dna_json, '{}'::jsonb) || EXCLUDED.dna_json,
    hydrated_at = COALESCE(pack_family.family_blueprint.hydrated_at, EXCLUDED.hydrated_at),
    updated_at = NOW(),
    deleted_at = NULL;

-- Mark tenant as GTM demo house (client may also use /demo)
UPDATE public.tenants
SET settings = jsonb_set(
        COALESCE(settings, '{}'::jsonb),
        '{platform,features,demoHouse}',
        'true'::jsonb,
        true
    ),
    updated_at = NOW()
WHERE id = '11111111-1111-1111-1111-111111111104'
  AND deleted_at IS NULL;

-- =============================================================================
-- Viewer login: demo / Admin@123 (same bcrypt as DEMO admin — local only)
-- =============================================================================
INSERT INTO public.employees (
    id, tenant_id, employee_code, full_name, phone, email, status
)
VALUES (
    '11111111-1111-1111-1111-111111111314',
    '11111111-1111-1111-1111-111111111104',
    'DEMOVIEW',
    'Khách xem demo',
    '0903000002',
    'demo@demo-family.kittech.vn',
    1
)
ON CONFLICT (tenant_id, employee_code) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    email = EXCLUDED.email,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.users (
    id, tenant_id, employee_id, username, email, password_hash, status
)
VALUES (
    '11111111-1111-1111-1111-111111111414',
    '11111111-1111-1111-1111-111111111104',
    '11111111-1111-1111-1111-111111111314',
    'demo',
    'demo@demo-family.kittech.vn',
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
    '11111111-1111-1111-1111-111111111514',
    '11111111-1111-1111-1111-111111111104',
    'VIEWER',
    'Khách xem'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111414',
    '11111111-1111-1111-1111-111111111514'
)
ON CONFLICT DO NOTHING;

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111514', p.id
FROM public.permissions p
WHERE (p.permission_code LIKE 'system.%' OR p.permission_code LIKE 'family_os.%')
  AND NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111514'
      AND rp.permission_id = p.id
  );

INSERT INTO pack_family.membership (
    id, tenant_id, family_id, display_name, role_code, sort_order, status, user_id
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab05',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Khách xem',
    'viewer',
    90,
    'active',
    '11111111-1111-1111-1111-111111111414'
)
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    role_code = 'viewer',
    user_id = EXCLUDED.user_id,
    status = 'active',
    deleted_at = NULL,
    updated_at = NOW();

-- Durable invite (optional) — viewer, long TTL, many uses
INSERT INTO pack_family.family_invite (
    id, tenant_id, family_id, code, role_code, expires_at, max_uses, used_count
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'FAMIXADEM',
    'viewer',
    TIMESTAMPTZ '2027-12-31 23:59:59+00',
    500,
    0
)
ON CONFLICT (code) DO UPDATE SET
    role_code = 'viewer',
    expires_at = EXCLUDED.expires_at,
    max_uses = GREATEST(pack_family.family_invite.max_uses, EXCLUDED.max_uses),
    revoked_at = NULL;
