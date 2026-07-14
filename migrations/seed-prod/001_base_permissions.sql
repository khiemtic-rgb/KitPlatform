-- Production: global permission catalog (no tenant / demo data)
-- Chạy sau schema migrations khi không dùng seed demo.

INSERT INTO permissions (permission_code, permission_name, module_name) VALUES
    ('catalog.read', 'Xem danh mục', 'Danh mục'),
    ('catalog.write', 'Sửa danh mục', 'Danh mục'),
    ('inventory.read', 'Xem kho', 'Kho hàng'),
    ('inventory.write', 'Sửa kho', 'Kho hàng'),
    ('procurement.read', 'Xem mua hàng', 'Mua hàng'),
    ('procurement.write', 'Mua hàng', 'Mua hàng'),
    ('sales.read', 'Xem bán hàng', 'Bán hàng'),
    ('sales.write', 'Bán hàng', 'Bán hàng'),
    ('sales.discount', 'Chiết khấu bán hàng (tối đa 10%)', 'Bán hàng'),
    ('sales.discount.unlimited', 'Chiết khấu không giới hạn', 'Bán hàng'),
    ('sales.cancel', 'Hủy hóa đơn nháp (Loss gate)', 'Bán hàng'),
    ('inventory.approve', 'Duyệt điều chỉnh / xuất nội bộ tồn (Loss gate)', 'Kho'),
    ('system.delete_permanent', 'Xóa vĩnh viễn', 'Hệ thống'),
    ('system.read', 'Xem hệ thống', 'Hệ thống'),
    ('system.write', 'Quản trị hệ thống', 'Hệ thống'),
    ('reports.read', 'Xem báo cáo', 'Báo cáo'),
    ('reports.export', 'Xuất báo cáo', 'Báo cáo'),
    ('system.audit.read', 'Xem nhật ký hệ thống', 'Hệ thống')
ON CONFLICT (permission_code) DO UPDATE
SET permission_name = EXCLUDED.permission_name,
    module_name = EXCLUDED.module_name;
