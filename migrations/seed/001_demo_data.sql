-- PharmaCore: Demo seed data for development

-- Fixed UUIDs for reproducible dev references
-- Tenant
INSERT INTO tenants (id, tenant_code, tenant_name, country_code, default_currency, settings)
VALUES (
    '11111111-1111-1111-1111-111111111101',
    'DEMO_PHARMACY',
    'Nhà Thuốc Demo Pharmar',
    'VN', 'VND',
    '{"allow_negative_stock": false, "loyalty_enabled": true, "batch_mode": "suggest"}'::jsonb
);

INSERT INTO branches (id, tenant_id, branch_code, branch_name, address, phone, is_head_office)
VALUES (
    '11111111-1111-1111-1111-111111111201',
    '11111111-1111-1111-1111-111111111101',
    'HN01', 'Chi nhánh Hà Nội', '123 Phố Huế, Hà Nội', '0243123456', TRUE
);

INSERT INTO employees (id, tenant_id, employee_code, full_name, phone, email)
VALUES (
    '11111111-1111-1111-1111-111111111301',
    '11111111-1111-1111-1111-111111111101',
    'EMP001', 'Nguyễn Văn Admin', '0901000001', 'admin@demo.pharmacore.vn'
);

INSERT INTO employee_branches (employee_id, branch_id, is_primary)
VALUES ('11111111-1111-1111-1111-111111111301', '11111111-1111-1111-1111-111111111201', TRUE);

-- Admin user: password = Admin@123
INSERT INTO users (id, tenant_id, employee_id, username, email, password_hash)
VALUES (
    '11111111-1111-1111-1111-111111111401',
    '11111111-1111-1111-1111-111111111101',
    '11111111-1111-1111-1111-111111111301',
    'admin', 'admin@demo.pharmacore.vn',
    '$2a$11$Oq8dLLVbqREcBk4VBW0ELOuBQneydTDK7VLpR9FcHEiQdWoUTQyJS'
);

INSERT INTO roles (id, tenant_id, role_code, role_name)
VALUES ('11111111-1111-1111-1111-111111111501', '11111111-1111-1111-1111-111111111101', 'ADMIN', 'Quản trị viên');

INSERT INTO user_roles (user_id, role_id)
VALUES ('11111111-1111-1111-1111-111111111401', '11111111-1111-1111-1111-111111111501');

INSERT INTO permissions (permission_code, permission_name, module_name) VALUES
('catalog.read', 'Xem danh mục', 'Catalog'),
('catalog.write', 'Sửa danh mục', 'Catalog'),
('inventory.read', 'Xem kho', 'Inventory'),
('inventory.write', 'Sửa kho', 'Inventory'),
('procurement.read', 'Xem mua hàng', 'Procurement'),
('procurement.write', 'Mua hàng', 'Procurement'),
('sales.write', 'Bán hàng', 'Sales'),
('sales.read', 'Xem bán hàng', 'Sales'),
('system.delete_permanent', 'Xóa vĩnh viễn', 'System');

INSERT INTO role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111501', id FROM permissions;

-- Warehouse
INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, is_default)
VALUES (
    '22222222-2222-2222-2222-222222222201',
    '11111111-1111-1111-1111-111111111101',
    '11111111-1111-1111-1111-111111111201',
    'WH_MAIN', 'Kho chính', 1, TRUE
);

-- Categories & brands
INSERT INTO product_categories (id, tenant_id, category_code, category_name, sort_order)
VALUES
('33333333-3333-3333-3333-333333333301', '11111111-1111-1111-1111-111111111101', 'GIAM_DAU', 'Giảm đau hạ sốt', 1),
('33333333-3333-3333-3333-333333333302', '11111111-1111-1111-1111-111111111101', 'KHANG_SINH', 'Kháng sinh', 2),
('33333333-3333-3333-3333-333333333303', '11111111-1111-1111-1111-111111111101', 'VITAMIN', 'Vitamin & bổ sung', 3);

INSERT INTO product_brands (id, tenant_id, brand_code, brand_name)
VALUES
('44444444-4444-4444-4444-444444444401', '11111111-1111-1111-1111-111111111101', 'DHG', 'DHG Pharma'),
('44444444-4444-4444-4444-444444444402', '11111111-1111-1111-1111-111111111101', 'STADA', 'Stada Vietnam');

-- Active ingredients (global)
INSERT INTO active_ingredients (id, ingredient_code, ingredient_name) VALUES
('55555555-5555-5555-5555-555555555501', 'PARACETAMOL', 'Paracetamol'),
('55555555-5555-5555-5555-555555555502', 'IBUPROFEN', 'Ibuprofen'),
('55555555-5555-5555-5555-555555555503', 'AMOXICILLIN', 'Amoxicillin'),
('55555555-5555-5555-5555-555555555504', 'VITAMIN_C', 'Vitamin C'),
('55555555-5555-5555-5555-555555555505', 'CAFFEINE', 'Caffeine');

-- Products
INSERT INTO products (id, tenant_id, category_id, brand_id, product_code, product_name, generic_name, drug_type)
VALUES
('66666666-6666-6666-6666-666666666601', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333301', '44444444-4444-4444-4444-444444444401', 'PARA500', 'Paracetamol 500mg', 'Paracetamol', 1),
('66666666-6666-6666-6666-666666666602', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333301', '44444444-4444-4444-4444-444444444402', 'PARA_EXTRA', 'Paracetamol Extra', 'Paracetamol + Caffeine', 1),
('66666666-6666-6666-6666-666666666603', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333302', '44444444-4444-4444-4444-444444444401', 'AMOX500', 'Amoxicillin 500mg', 'Amoxicillin', 2),
('66666666-6666-6666-6666-666666666604', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333303', '44444444-4444-4444-4444-444444444402', 'VITC1000', 'Vitamin C 1000mg', 'Ascorbic Acid', 1);

-- Units
INSERT INTO product_units (id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit)
VALUES
('77777777-7777-7777-7777-777777777701', '11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666601', 'Viên', 1, TRUE, TRUE),
('77777777-7777-7777-7777-777777777702', '11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666601', 'Hộp', 10, FALSE, TRUE),
('77777777-7777-7777-7777-777777777703', '11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666602', 'Viên', 1, TRUE, TRUE),
('77777777-7777-7777-7777-777777777704', '11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666603', 'Viên', 1, TRUE, TRUE),
('77777777-7777-7777-7777-777777777705', '11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666604', 'Viên', 1, TRUE, TRUE);

-- Barcodes
INSERT INTO product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
VALUES
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666601', '8934567890012', 1, TRUE),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666602', '8934567890029', 1, TRUE),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666603', '8934567890036', 1, TRUE),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666604', '8934567890043', 1, TRUE);

-- Prices (Retail)
INSERT INTO product_prices (tenant_id, product_id, product_unit_id, price_type, price)
VALUES
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666601', '77777777-7777-7777-7777-777777777701', 1, 500),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666601', '77777777-7777-7777-7777-777777777702', 1, 4500),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666602', '77777777-7777-7777-7777-777777777703', 1, 1200),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666603', '77777777-7777-7777-7777-777777777704', 1, 2500),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666604', '77777777-7777-7777-7777-777777777705', 1, 8000);

-- Ingredients mapping
INSERT INTO product_ingredients (tenant_id, product_id, ingredient_id, strength_value, strength_unit)
VALUES
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666601', '55555555-5555-5555-5555-555555555501', 500, 'mg'),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666602', '55555555-5555-5555-5555-555555555501', 500, 'mg'),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666602', '55555555-5555-5555-5555-555555555505', 65, 'mg'),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666603', '55555555-5555-5555-5555-555555555503', 500, 'mg'),
('11111111-1111-1111-1111-111111111101', '66666666-6666-6666-6666-666666666604', '55555555-5555-5555-5555-555555555504', 1000, 'mg');

-- Supplier
INSERT INTO suppliers (id, tenant_id, supplier_code, supplier_name, tax_code, phone)
VALUES ('88888888-8888-8888-8888-888888888801', '11111111-1111-1111-1111-111111111101', 'NCC001', 'Công ty Dược phẩm ABC', '0123456789', '0283123456');

-- Inventory batch (initial stock via GRN simulation)
INSERT INTO inventory_batches (id, tenant_id, warehouse_id, product_id, batch_number, expiry_date, unit_cost, quantity_received, quantity_available, supplier_id)
VALUES
('99999999-9999-9999-9999-999999999901', '11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201', '66666666-6666-6666-6666-666666666601', 'LOT2026A', '2028-12-31', 350, 1000, 1000, '88888888-8888-8888-8888-888888888801'),
('99999999-9999-9999-9999-999999999902', '11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201', '66666666-6666-6666-6666-666666666601', 'LOT2027B', '2029-06-30', 360, 500, 500, '88888888-8888-8888-8888-888888888801'),
('99999999-9999-9999-9999-999999999903', '11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201', '66666666-6666-6666-6666-666666666602', 'LOT2026C', '2028-08-31', 800, 300, 300, '88888888-8888-8888-8888-888888888801');

INSERT INTO stock_movements (tenant_id, warehouse_id, batch_id, product_id, movement_type, reference_type, reference_id, quantity, unit_cost)
VALUES
('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201', '99999999-9999-9999-9999-999999999901', '66666666-6666-6666-6666-666666666601', 1, 'SEED', '11111111-1111-1111-1111-111111111101', 1000, 350),
('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201', '99999999-9999-9999-9999-999999999902', '66666666-6666-6666-6666-666666666601', 1, 'SEED', '11111111-1111-1111-1111-111111111101', 500, 360),
('11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201', '99999999-9999-9999-9999-999999999903', '66666666-6666-6666-6666-666666666602', 1, 'SEED', '11111111-1111-1111-1111-111111111101', 300, 800);

-- Customer + loyalty
INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, email, date_of_birth)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', '11111111-1111-1111-1111-111111111101', 'KH001', 'Trần Thị Mai', '0909123456', 'mai@email.com', '1990-05-15'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02', '11111111-1111-1111-1111-111111111101', 'KH002', 'Nguyễn Văn An', '0909234567', 'an.nguyen@email.com', '1985-03-22'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03', '11111111-1111-1111-1111-111111111101', 'KH003', 'Lê Hoàng Bình', '0909345678', 'binh.le@email.com', '1978-11-08'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04', '11111111-1111-1111-1111-111111111101', 'KH004', 'Phạm Thu Hà', '0909456789', 'ha.pham@email.com', '1992-07-30'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05', '11111111-1111-1111-1111-111111111101', 'KH005', 'Hoàng Minh Đức', '0909567890', 'duc.hoang@email.com', '1988-01-14'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06', '11111111-1111-1111-1111-111111111101', 'KH006', 'Võ Thị Lan', '0909678901', 'lan.vo@email.com', '1995-09-03');

INSERT INTO loyalty_programs (id, tenant_id, program_code, program_name, points_per_amount)
VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', '11111111-1111-1111-1111-111111111101', 'LOYALTY_DEFAULT', 'Tích điểm Pharmar', 10000);

INSERT INTO loyalty_tiers (id, program_id, tier_code, tier_name, min_points, discount_percent, sort_order)
VALUES
('cccccccc-cccc-cccc-cccc-cccccccccc01', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'BRONZE', 'Đồng', 0, 0, 1),
('cccccccc-cccc-cccc-cccc-cccccccccc02', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'SILVER', 'Bạc', 500, 2, 2),
('cccccccc-cccc-cccc-cccc-cccccccccc03', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'GOLD', 'Vàng', 2000, 5, 3);

INSERT INTO customer_loyalty (customer_id, program_id, tier_id, points_balance, lifetime_points)
VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'cccccccc-cccc-cccc-cccc-cccccccccc01', 120, 120);

INSERT INTO customer_accounts (tenant_id, customer_id, phone, is_verified)
VALUES ('11111111-1111-1111-1111-111111111101', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', '0909123456', TRUE);

INSERT INTO medication_reminders (tenant_id, customer_id, product_id, dosage_note, remind_time, next_remind_at)
VALUES ('11111111-1111-1111-1111-111111111101', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', '66666666-6666-6666-6666-666666666604', '1 viên sau ăn sáng', '08:00', NOW() + INTERVAL '1 day');
