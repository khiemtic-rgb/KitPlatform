-- KitPlatform 167: Staff/Cashier chỉ learning.read — không chấm/gán (learning.write)
-- Fix: NV vào được form Chấm tháng vì STAFF bị gán nhầm learning.write

DELETE FROM public.role_permissions rp
USING public.roles r, public.permissions p
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.role_code IN ('STAFF', 'CASHIER', 'WAREHOUSE', 'UAT_LOSS_CASHIER')
  AND p.permission_code = 'learning.write';

-- Đảm bảo NV vẫn học được
INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
CROSS JOIN public.permissions p
WHERE r.role_code IN ('STAFF', 'CASHIER')
  AND p.permission_code = 'learning.read'
ON CONFLICT DO NOTHING;
