-- FamilyOS Starter — DEV seed only (không chạy trên production)
-- Depends on: 192_pack_family_os.sql
-- Tenant: DEMO_FAMILY · Routine: Ngày đi học (Mon–Fri)

-- =============================================================================
-- DEMO_FAMILY tenant + admin (password = Admin@123 — same hash as DEMO_PHARMACY)
-- =============================================================================
INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency,
    business_vertical, settings, status
)
VALUES (
    '11111111-1111-1111-1111-111111111104',
    'DEMO_FAMILY',
    'Gia đình Demo FamilyOS',
    'VN', 'VND',
    -- hybrid works without owner ALTER; run 192a later to promote to family
    'hybrid',
    jsonb_build_object(
        'platform', jsonb_build_object(
            -- UI vertical = family (admin shell). DB column stays hybrid until 192a owner migration.
            'vertical', 'family',
            'enabled_modules', jsonb_build_array('family_os'),
            'allowed_modules', jsonb_build_array('family_os'),
            'features', jsonb_build_object()
        )
    ),
    1
)
ON CONFLICT (tenant_code) DO UPDATE SET
    tenant_name = EXCLUDED.tenant_name,
    business_vertical = EXCLUDED.business_vertical,
    settings = EXCLUDED.settings,
    updated_at = NOW(),
    deleted_at = NULL;



INSERT INTO public.branches (
    id, tenant_id, branch_code, branch_name, address, phone, is_head_office, status
)
VALUES (
    '11111111-1111-1111-1111-111111111204',
    '11111111-1111-1111-1111-111111111104',
    'HOME',
    'Nhà',
    'Demo FamilyOS — local only',
    '0903000001',
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
    '11111111-1111-1111-1111-111111111304',
    '11111111-1111-1111-1111-111111111104',
    'EMP001',
    'Admin FamilyOS Demo',
    '0903000001',
    'admin@demo-family.kittech.vn',
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
    '11111111-1111-1111-1111-111111111404',
    '11111111-1111-1111-1111-111111111104',
    '11111111-1111-1111-1111-111111111304',
    'admin',
    'admin@demo-family.kittech.vn',
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
    '11111111-1111-1111-1111-111111111504',
    '11111111-1111-1111-1111-111111111104',
    'ADMIN',
    'Quản trị viên'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111404',
    '11111111-1111-1111-1111-111111111504'
)
ON CONFLICT DO NOTHING;

-- FamilyOS ADMIN: chỉ system.* — không gán quyền nhà thuốc/clinic từ catalog dùng chung.
DELETE FROM public.role_permissions rp
USING public.permissions p
WHERE rp.role_id = '11111111-1111-1111-1111-111111111504'
  AND rp.permission_id = p.id
  AND p.permission_code NOT LIKE 'system.%'
  AND p.permission_code NOT LIKE 'family_os.%';

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111504', p.id
FROM public.permissions p
WHERE (p.permission_code LIKE 'system.%' OR p.permission_code LIKE 'family_os.%')
  AND NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111504'
      AND rp.permission_id = p.id
);

SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111104'::uuid,
    'family_os'
);

-- =============================================================================
-- Household + members
-- =============================================================================
INSERT INTO pack_family.family (
    id, tenant_id, display_name, timezone, status
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    '11111111-1111-1111-1111-111111111104',
    'Nhà Demo',
    'Asia/Ho_Chi_Minh',
    'active'
)
ON CONFLICT (id) DO UPDATE SET
    -- Giữ tên nhà đã đổi trên Admin; seed chỉ khôi phục nếu hàng bị soft-delete
    display_name = CASE
        WHEN pack_family.family.deleted_at IS NOT NULL THEN EXCLUDED.display_name
        ELSE pack_family.family.display_name
    END,
    timezone = EXCLUDED.timezone,
    status = 'active',
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO pack_family.membership (
    id, tenant_id, family_id, display_name, role_code, date_of_birth, sort_order, status
)
VALUES
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab01',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Mẹ',
    'guardian',
    NULL,
    1,
    'active'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab02',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Bố',
    'guardian',
    NULL,
    2,
    'active'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Bảo Nhi',
    'child',
    DATE '2015-03-15',
    3,
    'active'
),
(
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'Đức Huy',
    'child',
    DATE '2018-09-20',
    4,
    'active'
)
ON CONFLICT (id) DO UPDATE SET
    display_name = CASE
        -- Keep custom renames for ab03 if family already personalized (not stock Minh)
        WHEN pack_family.membership.display_name IS DISTINCT FROM 'Minh'
         AND pack_family.membership.id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03'
            THEN pack_family.membership.display_name
        ELSE EXCLUDED.display_name
    END,
    role_code = EXCLUDED.role_code,
    date_of_birth = EXCLUDED.date_of_birth,
    sort_order = EXCLUDED.sort_order,
    status = 'active',
    deleted_at = NULL,
    updated_at = NOW();

-- =============================================================================
-- Routine template: Ngày đi học (Mon–Fri)
-- =============================================================================
INSERT INTO pack_family.routine (
    id, tenant_id, family_id, code, display_name, kind, weekdays, is_active, sort_order
)
VALUES (
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
    '11111111-1111-1111-1111-111111111104',
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01',
    'school_day',
    'Ngày đi học',
    'school_day',
    ARRAY[1, 2, 3, 4, 5]::SMALLINT[],
    TRUE,
    1
)
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    kind = EXCLUDED.kind,
    weekdays = EXCLUDED.weekdays,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- Soft-replace templates for idempotent re-seed
UPDATE pack_family.commitment_template
SET deleted_at = NOW(), updated_at = NOW()
WHERE routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01'
  AND deleted_at IS NULL
  AND id NOT IN (
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad01',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad02',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad03',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad04',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad05',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad07',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad08',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad09',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad10',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad11',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad12'
  );

INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    allow_early_complete
)
VALUES
-- Buổi sáng
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad01', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Dậy', 'Dậy đúng giờ chuẩn bị đi học', TIME '06:00', TIME '06:15', 10, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad02', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng sáng', NULL, TIME '06:15', TIME '06:25', 20, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad01']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad03', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn sáng', NULL, TIME '06:25', TIME '06:45', 30, TRUE,
 'critical', 20, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad02']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad04', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Mặc đồng phục', NULL, TIME '06:45', TIME '06:55', 40, TRUE,
 'normal', 10, 'after_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad03']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad05', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Chuẩn bị cặp', 'Sách vở + hộp cơm', TIME '06:55', TIME '07:05', 50, TRUE,
 'critical', 10, 'before_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad04']::uuid[], FALSE),
-- Buổi chiều
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Làm bài tập', NULL, TIME '16:00', TIME '17:00', 60, TRUE,
 'critical', 60, 'after_school', '{}'::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad07', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi tắm', NULL, TIME '17:00', TIME '17:30', 70, TRUE,
 'normal', 30, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad12', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Tập thể dục', 'Nhảy dây / chạy nhẹ 15 phút', TIME '17:30', TIME '18:00', 75, TRUE,
 'optional', 20, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad07']::uuid[], TRUE),
-- Buổi tối
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad08', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 80, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad09', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đọc sách', 'Khoảng 20 phút', TIME '19:00', TIME '19:30', 90, TRUE,
 'optional', 30, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad08']::uuid[], TRUE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad10', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đánh răng tối', NULL, TIME '20:30', TIME '20:40', 100, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad09']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad11', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab03', 'Đi ngủ', NULL, TIME '21:00', TIME '21:15', 110, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad10']::uuid[], FALSE)
ON CONFLICT (id) DO UPDATE SET
    member_id = EXCLUDED.member_id,
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    window_start = EXCLUDED.window_start,
    window_end = EXCLUDED.window_end,
    sort_order = EXCLUDED.sort_order,
    priority = EXCLUDED.priority,
    expected_duration_minutes = EXCLUDED.expected_duration_minutes,
    context_anchor = EXCLUDED.context_anchor,
    depends_on_template_ids = EXCLUDED.depends_on_template_ids,
    allow_early_complete = EXCLUDED.allow_early_complete,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- Soft-replace Đức Huy templates (Team Play — second child)
UPDATE pack_family.commitment_template
SET deleted_at = NOW(), updated_at = NOW()
WHERE routine_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01'
  AND deleted_at IS NULL
  AND member_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04'
  AND id NOT IN (
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae02',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae03',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae04',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae05',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae06',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae07',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae08',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae09',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae10',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae11',
      'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae12'
  );

INSERT INTO pack_family.commitment_template (
    id, tenant_id, routine_id, member_id, title, description,
    window_start, window_end, sort_order, is_active,
    priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
    allow_early_complete
)
VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Dậy', 'Dậy cùng chị / đúng giờ', TIME '06:10', TIME '06:25', 210, TRUE,
 'critical', 15, 'after_wake', '{}'::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae02', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng sáng', NULL, TIME '06:25', TIME '06:35', 220, TRUE,
 'normal', 10, 'after_wake', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae01']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae03', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn sáng', NULL, TIME '06:35', TIME '06:55', 230, TRUE,
 'critical', 20, 'before_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae02']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae04', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Mặc đồng phục', NULL, TIME '06:55', TIME '07:05', 240, TRUE,
 'normal', 10, 'after_breakfast', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae03']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae05', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Chuẩn bị cặp', 'Sách vở nhỏ + hộp cơm', TIME '07:05', TIME '07:15', 250, TRUE,
 'critical', 10, 'before_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae04']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae06', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Làm bài tập', 'Bài ngắn / ôn với chị', TIME '16:00', TIME '16:40', 260, TRUE,
 'critical', 40, 'after_school', '{}'::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae07', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi tắm', NULL, TIME '16:40', TIME '17:10', 270, TRUE,
 'normal', 30, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae06']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae12', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Tập thể dục', 'Nhảy dây nhẹ cùng chị', TIME '17:10', TIME '17:40', 275, TRUE,
 'optional', 15, 'after_school', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae07']::uuid[], TRUE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae08', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Ăn cơm', NULL, TIME '18:00', TIME '18:45', 280, TRUE,
 'critical', 45, 'before_dinner', '{}'::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae09', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đọc sách', 'Khoảng 15 phút', TIME '19:00', TIME '19:20', 290, TRUE,
 'optional', 20, 'after_dinner', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae08']::uuid[], TRUE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae10', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đánh răng tối', NULL, TIME '20:20', TIME '20:30', 300, TRUE,
 'normal', 10, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae09']::uuid[], FALSE),
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae11', '11111111-1111-1111-1111-111111111104', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaac01',
 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaab04', 'Đi ngủ', NULL, TIME '20:45', TIME '21:00', 310, TRUE,
 'critical', 15, 'before_sleep', ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaae10']::uuid[], FALSE)
ON CONFLICT (id) DO UPDATE SET
    member_id = EXCLUDED.member_id,
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    window_start = EXCLUDED.window_start,
    window_end = EXCLUDED.window_end,
    sort_order = EXCLUDED.sort_order,
    priority = EXCLUDED.priority,
    expected_duration_minutes = EXCLUDED.expected_duration_minutes,
    context_anchor = EXCLUDED.context_anchor,
    depends_on_template_ids = EXCLUDED.depends_on_template_ids,
    allow_early_complete = EXCLUDED.allow_early_complete,
    is_active = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

-- =============================================================================
-- Agreements: FamilyOS Default Family Constitution v1.0
-- See migrations/seed/007_family_constitution_v1.sql (soft-delete old + full set)
-- =============================================================================
