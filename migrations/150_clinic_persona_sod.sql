-- KitPlatform 150: Clinic persona SoD + connect.read for CLINIC_*
-- Splits clinic.write into:
--   clinic.appointments — lịch / BN / CRM leads
--   clinic.emr          — khám / đơn / ký / gửi NT
--   clinic.settings     — cấu hình PK + quản lý bác sĩ nội bộ
-- CLINIC_RECEPTION keeps appointments only; CLINIC_PROVIDER keeps EMR only.
-- Both personas get connect.read for partner catalog / membership list.
-- Holders of clinic.write keep the full package (backward compatible).

INSERT INTO permissions (permission_code, permission_name, module_name) VALUES
    ('clinic.appointments', 'Clinic — lịch / bệnh nhân', 'Clinic'),
    ('clinic.emr', 'Clinic — khám / đơn thuốc', 'Clinic'),
    ('clinic.settings', 'Clinic — cấu hình / bác sĩ', 'Clinic')
ON CONFLICT (permission_code) DO UPDATE SET
    permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;

-- Expand clinic.write holders with specialized codes (package semantics).
INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_spec.id
FROM role_permissions rp
INNER JOIN permissions p_write
    ON p_write.id = rp.permission_id
   AND p_write.permission_code = 'clinic.write'
CROSS JOIN permissions p_spec
WHERE p_spec.permission_code IN (
    'clinic.appointments', 'clinic.emr', 'clinic.settings', 'clinic.read'
)
ON CONFLICT DO NOTHING;

-- Strip clinic.write from personas (SoD) — they keep specialized grants below.
DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code IN ('CLINIC_RECEPTION', 'CLINIC_PROVIDER')
  AND p.permission_code IN ('clinic.write', 'clinic.emr', 'clinic.settings', 'clinic.appointments');

-- Reception: appointments + read + connect.read
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'CLINIC_RECEPTION'
  AND p.permission_code IN ('clinic.read', 'clinic.appointments', 'connect.read')
ON CONFLICT DO NOTHING;

-- Provider: EMR + read + connect.read
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'CLINIC_PROVIDER'
  AND p.permission_code IN ('clinic.read', 'clinic.emr', 'connect.read')
ON CONFLICT DO NOTHING;

-- Ensure ADMIN still has full clinic + connect (idempotent).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code IN (
      'clinic.read', 'clinic.write',
      'clinic.appointments', 'clinic.emr', 'clinic.settings',
      'connect.read', 'connect.write'
  )
ON CONFLICT DO NOTHING;
