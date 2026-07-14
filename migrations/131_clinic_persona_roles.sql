-- KitPlatform 131: Clinic persona roles (CL-GO-02)
-- CLINIC_RECEPTION / CLINIC_PROVIDER — clinic.read + clinic.write
-- ADMIN already granted clinic.* via 116.
-- Depends on: 116_clinic_gd1_permissions.sql

INSERT INTO public.roles (id, tenant_id, role_code, role_name, description)
SELECT
    gen_random_uuid(),
    t.id,
    v.role_code,
    v.role_name,
    v.description
FROM public.tenants t
CROSS JOIN (
    VALUES
        ('CLINIC_RECEPTION', 'Lễ tân PK', 'Đặt lịch, tiếp nhận, hồ sơ BN'),
        ('CLINIC_PROVIDER', 'Bác sĩ PK', 'Khám, kê đơn, gửi NT')
) AS v(role_code, role_name, description)
WHERE t.deleted_at IS NULL
  AND (
      lower(COALESCE(t.settings->'platform'->>'vertical', '')) = 'clinic'
      OR t.tenant_code = 'DEMO_CLINIC'
      OR EXISTS (
          SELECT 1
          FROM jsonb_array_elements_text(
              COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
          ) AS m
          WHERE lower(m) IN ('clinic_appointments', 'clinic_emr_lite')
      )
  )
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name,
    description = COALESCE(EXCLUDED.description, public.roles.description);

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
CROSS JOIN public.permissions p
WHERE r.role_code IN ('CLINIC_RECEPTION', 'CLINIC_PROVIDER')
  AND p.permission_code IN ('clinic.read', 'clinic.write')
  AND NOT EXISTS (
      SELECT 1
      FROM public.role_permissions rp
      WHERE rp.role_id = r.id
        AND rp.permission_id = p.id
  );
