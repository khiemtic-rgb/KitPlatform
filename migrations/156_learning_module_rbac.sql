-- KitPlatform 156: Learning OS — module registry + RBAC + enable on pharmacy pilots

INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT v.code, v.name, v.description, v.verticals, v.sort_order
FROM (
    VALUES
        (
            'learning',
            'Learning OS',
            'Onboarding / academy engine — shared across Pharmacy, Clinic, SPA packs',
            ARRAY['pharmacy', 'pharmacy_chain', 'hybrid', 'clinic', 'spa'],
            55
        )
) AS v(code, name, description, verticals, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = v.code
);

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('learning.read', 'Xem đào tạo / onboarding', 'Đào tạo'),
    ('learning.write', 'Quản lý đào tạo / gán lộ trình', 'Đào tạo')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Learners (staff) need read; managers need write to assign
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER', 'STAFF', 'CASHIER')
  AND p.permission_code = 'learning.read'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code IN ('ADMIN', 'MANAGER', 'BRANCH_MANAGER')
  AND p.permission_code = 'learning.write'
ON CONFLICT DO NOTHING;

-- Enable on pharmacy pilots (local + prod NT)
UPDATE public.tenants t
SET
    settings = jsonb_set(
        COALESCE(t.settings, '{}'::jsonb),
        '{platform}',
        COALESCE(t.settings->'platform', '{}'::jsonb)
            || jsonb_build_object(
                'enabled_modules', (
                    SELECT COALESCE(jsonb_agg(DISTINCT x), '[]'::jsonb)
                    FROM jsonb_array_elements(
                        COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                        || '["learning"]'::jsonb
                    ) AS t(x)
                ),
                'allowed_modules', (
                    SELECT COALESCE(jsonb_agg(DISTINCT x), '[]'::jsonb)
                    FROM jsonb_array_elements(
                        COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)
                        || '["learning"]'::jsonb
                    ) AS t(x)
                )
            ),
        true
    ),
    updated_at = NOW()
WHERE t.deleted_at IS NULL
  AND t.tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY');
