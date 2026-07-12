-- KitPlatform 116: ClinicOS GĐ1 CL1.4 — clinic.read / clinic.write permissions
-- Depends on: seed permissions / role_permissions

INSERT INTO public.permissions (permission_code, permission_name, module_name)
VALUES
    ('clinic.read', 'Xem Clinic', 'Clinic'),
    ('clinic.write', 'Sửa Clinic', 'Clinic')
ON CONFLICT (permission_code) DO UPDATE SET
    permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
CROSS JOIN public.permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code IN ('clinic.read', 'clinic.write')
  AND NOT EXISTS (
      SELECT 1 FROM public.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );
