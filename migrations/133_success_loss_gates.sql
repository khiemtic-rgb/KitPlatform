-- EP03 AC5 — thin permission gates for cancel draft sale + approve inventory adjustments
-- Does not invent Soft-CKS. ADMIN gets both codes; assign sales.cancel / inventory.approve to trusted roles as needed.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('sales.cancel', 'Hủy hóa đơn nháp (Loss gate)', 'Bán hàng'),
    ('inventory.approve', 'Duyệt điều chỉnh / xuất nội bộ tồn (Loss gate)', 'Kho')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code IN ('sales.cancel', 'inventory.approve')
ON CONFLICT DO NOTHING;

-- Optional MANAGER role (Rx / clinic seeds) — elevate similarly when present
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'MANAGER'
  AND p.permission_code IN ('sales.cancel', 'inventory.approve')
ON CONFLICT DO NOTHING;
