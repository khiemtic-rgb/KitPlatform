-- Align POS unit-price override with roles that already have unlimited discount.

INSERT INTO role_permissions (role_id, permission_id)
SELECT DISTINCT rp.role_id, p_override.id
FROM role_permissions rp
JOIN permissions p_disc ON p_disc.id = rp.permission_id
  AND p_disc.permission_code = 'sales.discount.unlimited'
JOIN permissions p_override ON p_override.permission_code = 'sales.price.override'
ON CONFLICT DO NOTHING;
