-- Wave 1: Báo cáo — Thống kê permissions
INSERT INTO permissions (permission_code, permission_name, module_name) VALUES
    ('reports.read', 'Xem báo cáo', 'Báo cáo'),
    ('reports.export', 'Xuất báo cáo', 'Báo cáo')
ON CONFLICT (permission_code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.role_code = 'ADMIN'
  AND p.permission_code IN ('reports.read', 'reports.export')
ON CONFLICT DO NOTHING;
