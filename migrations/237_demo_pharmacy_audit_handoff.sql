-- 237: DEMO_PHARMACY audit handoff seed (idempotent)
-- ONLY touches tenant_code = DEMO_PHARMACY. Never mutates NT_XUANHOA or DEMO_CLINIC.
-- Staff/pharmacist use a237* UUIDs (11111111-...-302/402/502 collide with DEMO_CLINIC).

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM public.tenants WHERE tenant_code = 'NT_XUANHOA' AND id = '11111111-1111-1111-1111-111111111101') THEN
    RAISE EXCEPTION 'Safety abort: DEMO UUID collides with NT_XUANHOA';
  END IF;
END $$;

INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency, business_vertical, settings, status
)
SELECT
    '11111111-1111-1111-1111-111111111101',
    'DEMO_PHARMACY',
    'Nha Thuoc Demo (Tham dinh)',
    'VN', 'VND', 'pharmacy',
    jsonb_build_object(
        'allow_negative_stock', false,
        'loyalty_enabled', true,
        'batch_mode', 'suggest',
        'receipt', jsonb_build_object(
            'name', 'NHA THUOC DEMO THAM DINH',
            'phone', '0243123456',
            'address', '123 Pho Hue, Ha Noi',
            'tagline', 'Moi truong test - khong phai du lieu that'
        ),
        'platform', jsonb_build_object(
            'schema_version', 1,
            'vertical', 'pharmacy',
            'enabled_modules', jsonb_build_array(
                'inventory', 'procurement', 'sales', 'loyalty', 'customer_app',
                'medication', 'health_wallet', 'reservations', 'reports', 'e_rx'
            ),
            'allowed_modules', jsonb_build_array(
                'inventory', 'procurement', 'sales', 'loyalty', 'customer_app',
                'medication', 'health_wallet', 'reservations', 'reports', 'e_rx',
                'prescriber_network', 'prescriber_portal'
            ),
            'features', jsonb_build_object(
                'batch_tracking', true,
                'national_drug_catalog', true,
                'order_level_repurchase', true,
                'family_members', true,
                'branch_price_overrides', true,
                'branch_product_listings', false
            ),
            'i18n', jsonb_build_object(
                'default_locale', 'vi-VN',
                'supported_locales', jsonb_build_array('vi-VN'),
                'fallback_locale', 'vi-VN',
                'admin_default_locale', 'vi-VN',
                'customer_app_default_locale', 'vi-VN'
            )
        ),
        'rx', jsonb_build_object(
            'enforcement_mode', 'warn',
            'pos_blocked_audit', true,
            'prescription_validity_days', 7
        )
    ),
    1
WHERE NOT EXISTS (
    SELECT 1 FROM public.tenants WHERE tenant_code = 'DEMO_PHARMACY' OR id = '11111111-1111-1111-1111-111111111101'
);

DO $$
DECLARE demo_id uuid;
BEGIN
  SELECT id INTO demo_id FROM public.tenants WHERE tenant_code = 'DEMO_PHARMACY' AND deleted_at IS NULL;
  IF demo_id IS NULL THEN RAISE EXCEPTION 'DEMO_PHARMACY tenant missing after insert'; END IF;
  IF demo_id <> '11111111-1111-1111-1111-111111111101'::uuid THEN
    RAISE EXCEPTION 'DEMO_PHARMACY exists with unexpected id %', demo_id;
  END IF;
END $$;

INSERT INTO public.branches (id, tenant_id, branch_code, branch_name, address, phone, is_head_office, status)
SELECT '11111111-1111-1111-1111-111111111201', '11111111-1111-1111-1111-111111111101',
       'HN01', 'Chi nhanh Ha Noi', '123 Pho Hue, Ha Noi', '0243123456', TRUE, 1
WHERE NOT EXISTS (SELECT 1 FROM public.branches b WHERE b.id = '11111111-1111-1111-1111-111111111201');

INSERT INTO public.warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, is_default, status)
SELECT '22222222-2222-2222-2222-222222222201', '11111111-1111-1111-1111-111111111101',
       '11111111-1111-1111-1111-111111111201', 'WH_MAIN', 'Kho chinh', 1, TRUE, 1
WHERE NOT EXISTS (SELECT 1 FROM public.warehouses w WHERE w.id = '22222222-2222-2222-2222-222222222201');

INSERT INTO public.employees (id, tenant_id, employee_code, full_name, phone, email, status)
SELECT '11111111-1111-1111-1111-111111111301', '11111111-1111-1111-1111-111111111101',
       'EMP001', 'Nguyen Van Admin', '0901000001', 'admin@demo.novixa.vn', 1
WHERE NOT EXISTS (SELECT 1 FROM public.employees e WHERE e.id = '11111111-1111-1111-1111-111111111301');

INSERT INTO public.users (id, tenant_id, employee_id, username, email, password_hash, status)
SELECT '11111111-1111-1111-1111-111111111401', '11111111-1111-1111-1111-111111111101',
       '11111111-1111-1111-1111-111111111301', 'admin', 'admin@demo.novixa.vn',
       '$2a$11$Oq8dLLVbqREcBk4VBW0ELOuBQneydTDK7VLpR9FcHEiQdWoUTQyJS', 1
WHERE NOT EXISTS (SELECT 1 FROM public.users u WHERE u.id = '11111111-1111-1111-1111-111111111401');

INSERT INTO public.roles (id, tenant_id, role_code, role_name, status)
SELECT '11111111-1111-1111-1111-111111111501', '11111111-1111-1111-1111-111111111101',
       'ADMIN', 'Quan tri vien', 1
WHERE NOT EXISTS (SELECT 1 FROM public.roles r WHERE r.id = '11111111-1111-1111-1111-111111111501');

INSERT INTO public.user_roles (user_id, role_id)
SELECT '11111111-1111-1111-1111-111111111401', '11111111-1111-1111-1111-111111111501'
WHERE NOT EXISTS (
  SELECT 1 FROM public.user_roles ur
  WHERE ur.user_id = '11111111-1111-1111-1111-111111111401'
    AND ur.role_id = '11111111-1111-1111-1111-111111111501'
);

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111501', p.id
FROM public.permissions p
WHERE NOT EXISTS (
  SELECT 1 FROM public.role_permissions rp
  WHERE rp.role_id = '11111111-1111-1111-1111-111111111501' AND rp.permission_id = p.id
);

INSERT INTO public.product_categories (id, tenant_id, category_code, category_name, sort_order, status)
SELECT v.id, '11111111-1111-1111-1111-111111111101', v.code, v.name, v.ord, 1
FROM (VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid, 'GIAM_DAU', 'Giam dau ha sot', 1),
    ('33333333-3333-3333-3333-333333333302'::uuid, 'KHANG_SINH', 'Khang sinh', 2),
    ('33333333-3333-3333-3333-333333333303'::uuid, 'VITAMIN', 'Vitamin & bo sung', 3)
) AS v(id, code, name, ord)
WHERE NOT EXISTS (
  SELECT 1 FROM public.product_categories c WHERE c.id = v.id
     OR (c.tenant_id = '11111111-1111-1111-1111-111111111101' AND c.category_code = v.code)
);

INSERT INTO public.product_brands (id, tenant_id, brand_code, brand_name, status)
SELECT v.id, '11111111-1111-1111-1111-111111111101', v.code, v.name, 1
FROM (VALUES
    ('44444444-4444-4444-4444-444444444401'::uuid, 'DHG', 'DHG Pharma'),
    ('44444444-4444-4444-4444-444444444402'::uuid, 'STADA', 'Stada Vietnam')
) AS v(id, code, name)
WHERE NOT EXISTS (
  SELECT 1 FROM public.product_brands b WHERE b.id = v.id
     OR (b.tenant_id = '11111111-1111-1111-1111-111111111101' AND b.brand_code = v.code)
);



INSERT INTO public.employees (id, tenant_id, employee_code, full_name, phone, email, status)
SELECT 'a237a302-1111-4111-8111-111111111302',
       '11111111-1111-1111-1111-111111111101',
       'EMP002', 'Tran Thi Duoc si', '0901000002', 'pharmacist@demo.novixa.vn', 1
WHERE NOT EXISTS (
  SELECT 1 FROM public.employees e
  WHERE e.id = 'a237a302-1111-4111-8111-111111111302'
     OR (e.tenant_id = '11111111-1111-1111-1111-111111111101' AND e.employee_code = 'EMP002')
);

INSERT INTO public.users (id, tenant_id, employee_id, username, email, password_hash, status)
SELECT 'a237a402-1111-4111-8111-111111111402',
       '11111111-1111-1111-1111-111111111101',
       e.id,
       'pharmacist', 'pharmacist@demo.novixa.vn',
       '$2a$11$Oq8dLLVbqREcBk4VBW0ELOuBQneydTDK7VLpR9FcHEiQdWoUTQyJS', 1
FROM public.employees e
WHERE e.tenant_id = '11111111-1111-1111-1111-111111111101' AND e.employee_code = 'EMP002'
  AND NOT EXISTS (
    SELECT 1 FROM public.users u
    WHERE u.id = 'a237a402-1111-4111-8111-111111111402'
       OR (u.tenant_id = '11111111-1111-1111-1111-111111111101' AND u.username = 'pharmacist')
  );

INSERT INTO public.employee_branches (employee_id, branch_id)
SELECT e.id, '11111111-1111-1111-1111-111111111201'
FROM public.employees e
WHERE e.tenant_id = '11111111-1111-1111-1111-111111111101' AND e.employee_code = 'EMP002'
  AND NOT EXISTS (
    SELECT 1 FROM public.employee_branches eb
    WHERE eb.employee_id = e.id AND eb.branch_id = '11111111-1111-1111-1111-111111111201'
  );

INSERT INTO public.roles (id, tenant_id, role_code, role_name, status)
SELECT 'a237a502-1111-4111-8111-111111111502',
       '11111111-1111-1111-1111-111111111101',
       'STAFF', 'Duoc si / Nhan vien', 1
WHERE NOT EXISTS (
  SELECT 1 FROM public.roles r
  WHERE r.id = 'a237a502-1111-4111-8111-111111111502'
     OR (r.tenant_id = '11111111-1111-1111-1111-111111111101' AND r.role_code = 'STAFF')
);

INSERT INTO public.user_roles (user_id, role_id)
SELECT u.id, r.id
FROM public.users u
JOIN public.roles r ON r.tenant_id = u.tenant_id AND r.role_code = 'STAFF'
WHERE u.tenant_id = '11111111-1111-1111-1111-111111111101' AND u.username = 'pharmacist'
  AND NOT EXISTS (SELECT 1 FROM public.user_roles ur WHERE ur.user_id = u.id AND ur.role_id = r.id);

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM public.roles r
JOIN public.permissions p ON p.permission_code IN (
    'catalog.read',
    'inventory.read', 'inventory.write',
    'procurement.read', 'procurement.write', 'procurement.receive', 'procurement.suppliers',
    'sales.read', 'sales.write', 'sales.pos', 'sales.customers', 'sales.cancel', 'sales.discount'
)
WHERE r.tenant_id = '11111111-1111-1111-1111-111111111101' AND r.role_code = 'STAFF'
  AND NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );

INSERT INTO public.active_ingredients (id, tenant_id, ingredient_code, ingredient_name, status)
SELECT v.id, '11111111-1111-1111-1111-111111111101', v.code, v.name, 1
FROM (VALUES
    ('a237a001-5555-4555-8555-555555555501'::uuid, 'PARACETAMOL', 'Paracetamol'),
    ('a237a002-5555-4555-8555-555555555502'::uuid, 'IBUPROFEN', 'Ibuprofen'),
    ('a237a003-5555-4555-8555-555555555503'::uuid, 'AMOXICILLIN', 'Amoxicillin'),
    ('a237a004-5555-4555-8555-555555555504'::uuid, 'VITAMIN_C', 'Vitamin C'),
    ('a237a005-5555-4555-8555-555555555505'::uuid, 'CAFFEINE', 'Caffeine')
) AS v(id, code, name)
WHERE NOT EXISTS (
  SELECT 1 FROM public.active_ingredients ai
  WHERE ai.id = v.id
     OR (ai.tenant_id = '11111111-1111-1111-1111-111111111101' AND ai.ingredient_code = v.code)
);

INSERT INTO public.products (
    id, tenant_id, category_id, brand_id,
    product_code, product_name, generic_name,
    drug_type, product_kind, dispensing_class,
    national_drug_id, national_registration_number,
    dosage_form, packaging, status
)
SELECT v.id, '11111111-1111-1111-1111-111111111101', v.cat, v.brand, v.code, v.name, v.generic,
       v.drug_type, 'pharmacy_drug', v.disp, v.ndid, v.reg, v.form, v.pack, 1
FROM (VALUES
    ('66666666-6666-6666-6666-666666666601'::uuid, '33333333-3333-3333-3333-333333333301'::uuid, '44444444-4444-4444-4444-444444444401'::uuid,
     'PARA500', 'Paracetamol 500mg', 'Paracetamol', 1::smallint, 'otc', 'DRUG-VN-000001', 'VD-1234-23', 'Viên nén', 'Hộp 10 vỉ x 10 viên'),
    ('66666666-6666-6666-6666-666666666602'::uuid, '33333333-3333-3333-3333-333333333301'::uuid, '44444444-4444-4444-4444-444444444402'::uuid,
     'PARA_EXTRA', 'Paracetamol Extra', 'Paracetamol + Caffeine', 1::smallint, 'otc', 'DRUG-VN-000004', 'VD-3456-24', 'Viên nén bao phim', 'Hộp 2 vỉ x 10 viên'),
    ('66666666-6666-6666-6666-666666666603'::uuid, '33333333-3333-3333-3333-333333333302'::uuid, '44444444-4444-4444-4444-444444444401'::uuid,
     'AMOX500', 'Amoxicillin 500mg', 'Amoxicillin', 2::smallint, 'prescription', 'DRUG-VN-000002', 'VD-5678-22', 'Viên nang cứng', 'Hộp 2 vỉ x 10 viên'),
    ('66666666-6666-6666-6666-666666666604'::uuid, '33333333-3333-3333-3333-333333333303'::uuid, '44444444-4444-4444-4444-444444444402'::uuid,
     'VITC1000', 'Vitamin C 1000mg', 'Ascorbic Acid', 1::smallint, 'otc', 'DRUG-VN-000003', 'VD-9012-21', 'Viên sủi', 'Tuýp 20 viên')
) AS v(id, cat, brand, code, name, generic, drug_type, disp, ndid, reg, form, pack)
WHERE NOT EXISTS (
  SELECT 1 FROM public.products p
  WHERE p.id = v.id OR (p.tenant_id = '11111111-1111-1111-1111-111111111101' AND p.product_code = v.code)
);

UPDATE public.products p
SET national_drug_id = v.ndid,
    national_registration_number = v.reg,
    dispensing_class = v.disp,
    dosage_form = v.form,
    packaging = v.pack,
    updated_at = NOW()
FROM (VALUES
    ('PARA500', 'DRUG-VN-000001', 'VD-1234-23', 'otc', 'Viên nén', 'Hộp 10 vỉ x 10 viên'),
    ('PARA_EXTRA', 'DRUG-VN-000004', 'VD-3456-24', 'otc', 'Viên nén bao phim', 'Hộp 2 vỉ x 10 viên'),
    ('AMOX500', 'DRUG-VN-000002', 'VD-5678-22', 'prescription', 'Viên nang cứng', 'Hộp 2 vỉ x 10 viên'),
    ('VITC1000', 'DRUG-VN-000003', 'VD-9012-21', 'otc', 'Viên sủi', 'Tuýp 20 viên')
) AS v(code, ndid, reg, disp, form, pack)
WHERE p.tenant_id = '11111111-1111-1111-1111-111111111101' AND p.product_code = v.code;

INSERT INTO public.product_units (id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit)
SELECT v.id, '11111111-1111-1111-1111-111111111101', v.product_id, v.unit_name, v.factor, v.is_base, v.is_sale
FROM (VALUES
    ('77777777-7777-7777-7777-777777777701'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, 'Viên', 1::numeric, TRUE, TRUE),
    ('77777777-7777-7777-7777-777777777702'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, 'Hộp', 10::numeric, FALSE, TRUE),
    ('77777777-7777-7777-7777-777777777703'::uuid, '66666666-6666-6666-6666-666666666602'::uuid, 'Viên', 1::numeric, TRUE, TRUE),
    ('77777777-7777-7777-7777-777777777704'::uuid, '66666666-6666-6666-6666-666666666603'::uuid, 'Viên', 1::numeric, TRUE, TRUE),
    ('77777777-7777-7777-7777-777777777705'::uuid, '66666666-6666-6666-6666-666666666604'::uuid, 'Viên', 1::numeric, TRUE, TRUE)
) AS v(id, product_id, unit_name, factor, is_base, is_sale)
WHERE NOT EXISTS (SELECT 1 FROM public.product_units u WHERE u.id = v.id);

INSERT INTO public.product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
SELECT v.tenant_id, v.product_id, v.barcode, v.barcode_type, v.is_primary
FROM (VALUES
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, '8934567890012', 1::smallint, TRUE),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666602'::uuid, '8934567890029', 1::smallint, TRUE),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666603'::uuid, '8934567890036', 1::smallint, TRUE),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666604'::uuid, '8934567890043', 1::smallint, TRUE)
) AS v(tenant_id, product_id, barcode, barcode_type, is_primary)
WHERE NOT EXISTS (
  SELECT 1 FROM public.product_barcodes b WHERE b.tenant_id = v.tenant_id AND b.barcode = v.barcode
);

INSERT INTO public.product_prices (tenant_id, product_id, product_unit_id, price_type, price)
SELECT v.tenant_id, v.product_id, v.product_unit_id, v.price_type, v.price
FROM (VALUES
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, '77777777-7777-7777-7777-777777777701'::uuid, 1::smallint, 500::numeric),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, '77777777-7777-7777-7777-777777777702'::uuid, 1::smallint, 4500::numeric),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666602'::uuid, '77777777-7777-7777-7777-777777777703'::uuid, 1::smallint, 1200::numeric),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666603'::uuid, '77777777-7777-7777-7777-777777777704'::uuid, 1::smallint, 2500::numeric),
    ('11111111-1111-1111-1111-111111111101'::uuid, '66666666-6666-6666-6666-666666666604'::uuid, '77777777-7777-7777-7777-777777777705'::uuid, 1::smallint, 8000::numeric)
) AS v(tenant_id, product_id, product_unit_id, price_type, price)
WHERE NOT EXISTS (
  SELECT 1 FROM public.product_prices pp
  WHERE pp.tenant_id = v.tenant_id AND pp.product_id = v.product_id
    AND pp.product_unit_id = v.product_unit_id AND pp.price_type = v.price_type AND pp.status = 1
);

INSERT INTO public.product_ingredients (tenant_id, product_id, ingredient_id, strength_value, strength_unit)
SELECT '11111111-1111-1111-1111-111111111101', v.product_id, ai.id, v.strength, v.unit
FROM (VALUES
    ('66666666-6666-6666-6666-666666666601'::uuid, 'PARACETAMOL', 500::numeric, 'mg'),
    ('66666666-6666-6666-6666-666666666602'::uuid, 'PARACETAMOL', 500::numeric, 'mg'),
    ('66666666-6666-6666-6666-666666666602'::uuid, 'CAFFEINE', 65::numeric, 'mg'),
    ('66666666-6666-6666-6666-666666666603'::uuid, 'AMOXICILLIN', 500::numeric, 'mg'),
    ('66666666-6666-6666-6666-666666666604'::uuid, 'VITAMIN_C', 1000::numeric, 'mg')
) AS v(product_id, ingredient_code, strength, unit)
JOIN public.active_ingredients ai
  ON ai.tenant_id = '11111111-1111-1111-1111-111111111101' AND ai.ingredient_code = v.ingredient_code
WHERE NOT EXISTS (
  SELECT 1 FROM public.product_ingredients pi WHERE pi.product_id = v.product_id AND pi.ingredient_id = ai.id
);

INSERT INTO public.suppliers (id, tenant_id, supplier_code, supplier_name, tax_code, phone, status)
SELECT '88888888-8888-8888-8888-888888888801', '11111111-1111-1111-1111-111111111101',
       'NCC001', 'Cong ty Duoc pham ABC', '0123456789', '0283123456', 1
WHERE NOT EXISTS (
  SELECT 1 FROM public.suppliers s
  WHERE s.id = '88888888-8888-8888-8888-888888888801'
     OR (s.tenant_id = '11111111-1111-1111-1111-111111111101' AND s.supplier_code = 'NCC001')
);

INSERT INTO public.procurement_vat_treatments (tenant_id, treatment_code, treatment_name, rate_percent, is_not_subject, sort_order)
SELECT '11111111-1111-1111-1111-111111111101', v.code, v.name, v.rate, v.kct, v.ord
FROM (VALUES
    ('kct', 'Không chịu thuế GTGT (KCT)', 0::numeric, true, 0),
    ('vat_0', 'Thuế suất 0%', 0::numeric, false, 1),
    ('vat_5', 'Thuế suất 5%', 5::numeric, false, 2),
    ('vat_8', 'Thuế suất 8%', 8::numeric, false, 3),
    ('vat_10', 'Thuế suất 10%', 10::numeric, false, 4)
) AS v(code, name, rate, kct, ord)
WHERE NOT EXISTS (
  SELECT 1 FROM public.procurement_vat_treatments pvt
  WHERE pvt.tenant_id = '11111111-1111-1111-1111-111111111101' AND pvt.treatment_code = v.code
);

INSERT INTO public.inventory_batches (
    id, tenant_id, warehouse_id, product_id, batch_number, expiry_date,
    unit_cost, quantity_received, quantity_available, supplier_id, status
)
SELECT v.id, '11111111-1111-1111-1111-111111111101', '22222222-2222-2222-2222-222222222201',
       v.product_id, v.batch, v.expiry::date, v.cost, v.qty, v.qty, s.id, 1
FROM (VALUES
    ('99999999-9999-9999-9999-999999999901'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, 'LOT2026A', '2028-12-31', 350::numeric, 1000::numeric),
    ('99999999-9999-9999-9999-999999999902'::uuid, '66666666-6666-6666-6666-666666666601'::uuid, 'LOT2027B', '2029-06-30', 360::numeric, 500::numeric),
    ('99999999-9999-9999-9999-999999999903'::uuid, '66666666-6666-6666-6666-666666666602'::uuid, 'LOT2026C', '2028-08-31', 800::numeric, 300::numeric),
    ('99999999-9999-9999-9999-999999999904'::uuid, '66666666-6666-6666-6666-666666666603'::uuid, 'LOTAMOX01', '2028-03-31', 1800::numeric, 400::numeric),
    ('99999999-9999-9999-9999-999999999905'::uuid, '66666666-6666-6666-6666-666666666604'::uuid, 'LOTVITC01', '2029-01-31', 5500::numeric, 200::numeric)
) AS v(id, product_id, batch, expiry, cost, qty)
JOIN public.suppliers s ON s.tenant_id = '11111111-1111-1111-1111-111111111101' AND s.supplier_code = 'NCC001'
WHERE NOT EXISTS (
  SELECT 1 FROM public.inventory_batches b
  WHERE b.id = v.id OR (
    b.tenant_id = '11111111-1111-1111-1111-111111111101'
    AND b.warehouse_id = '22222222-2222-2222-2222-222222222201'
    AND b.product_id = v.product_id AND b.batch_number = v.batch
  )
);

INSERT INTO public.stock_movements (
    tenant_id, warehouse_id, batch_id, product_id, movement_type, reference_type, reference_id, quantity, unit_cost
)
SELECT b.tenant_id, b.warehouse_id, b.id, b.product_id, 1, 'SEED_AUDIT', '11111111-1111-1111-1111-111111111101', b.quantity_received, b.unit_cost
FROM public.inventory_batches b
WHERE b.tenant_id = '11111111-1111-1111-1111-111111111101'
  AND b.batch_number IN ('LOT2026A','LOT2027B','LOT2026C','LOTAMOX01','LOTVITC01')
  AND NOT EXISTS (
    SELECT 1 FROM public.stock_movements sm
    WHERE sm.batch_id = b.id AND sm.reference_type = 'SEED_AUDIT'
  );

INSERT INTO public.customers (id, tenant_id, customer_code, full_name, phone, status)
SELECT 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', '11111111-1111-1111-1111-111111111101',
       'KH001', 'Trần Thị Mai', '0909123456', 1
WHERE NOT EXISTS (
  SELECT 1 FROM public.customers c
  WHERE c.id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
     OR (c.tenant_id = '11111111-1111-1111-1111-111111111101' AND c.customer_code = 'KH001')
);

INSERT INTO pack_pharmacy.linked_prescribers (
    id, tenant_id, full_name, license_number, phone, specialty, status
)
SELECT 'a237b011-bbbb-4bbb-8bbb-bbbbbbbbbb11', '11111111-1111-1111-1111-111111111101',
       'BS. Nguyen Van Ke don', 'BYT-DEMO-001', '0902000001', 'Đa khoa', 1
WHERE NOT EXISTS (
  SELECT 1 FROM pack_pharmacy.linked_prescribers lp
  WHERE lp.id = 'a237b011-bbbb-4bbb-8bbb-bbbbbbbbbb11'
     OR (lp.tenant_id = '11111111-1111-1111-1111-111111111101' AND lp.license_number = 'BYT-DEMO-001')
);

INSERT INTO pack_pharmacy.electronic_prescriptions (
    id, tenant_id, branch_id, prescription_code, linked_prescriber_id,
    customer_id, patient_name, patient_phone, status, source,
    verification_method, verified_at, signed_at, created_by
)
SELECT
    'a237c011-cccc-4ccc-8ccc-cccccccccc11',
    '11111111-1111-1111-1111-111111111101',
    '11111111-1111-1111-1111-111111111201',
    'RX-DEMO-001',
    lp.id,
    c.id,
    'Trần Thị Mai', '0909123456',
    'verified', 'staff_entry',
    'manual_check', NOW(), NOW(),
    '11111111-1111-1111-1111-111111111401'
FROM pack_pharmacy.linked_prescribers lp
JOIN public.customers c ON c.tenant_id = lp.tenant_id AND c.customer_code = 'KH001'
WHERE lp.tenant_id = '11111111-1111-1111-1111-111111111101' AND lp.license_number = 'BYT-DEMO-001'
  AND NOT EXISTS (
    SELECT 1 FROM pack_pharmacy.electronic_prescriptions ep
    WHERE ep.id = 'a237c011-cccc-4ccc-8ccc-cccccccccc11'
       OR (ep.tenant_id = lp.tenant_id AND ep.prescription_code = 'RX-DEMO-001')
  );

INSERT INTO pack_pharmacy.electronic_prescription_lines (
    id, tenant_id, prescription_id, product_id, product_unit_id,
    line_dispensing_class, qty_prescribed, qty_dispensed, dosage_instruction, sort_order
)
SELECT
    'a237d011-dddd-4ddd-8ddd-dddddddddd11',
    ep.tenant_id,
    ep.id,
    '66666666-6666-6666-6666-666666666603',
    '77777777-7777-7777-7777-777777777704',
    'prescription', 20, 0, '1 vien x 2 lan/ngay sau an', 1
FROM pack_pharmacy.electronic_prescriptions ep
WHERE ep.tenant_id = '11111111-1111-1111-1111-111111111101' AND ep.prescription_code = 'RX-DEMO-001'
  AND NOT EXISTS (
    SELECT 1 FROM pack_pharmacy.electronic_prescription_lines el WHERE el.id = 'a237d011-dddd-4ddd-8ddd-dddddddddd11'
  );

-- Verify summary
SELECT 'user' AS kind, u.username || '/' || r.role_code AS detail
FROM users u
JOIN tenants t ON t.id = u.tenant_id
JOIN user_roles ur ON ur.user_id = u.id
JOIN roles r ON r.id = ur.role_id
WHERE t.tenant_code = 'DEMO_PHARMACY'
UNION ALL
SELECT 'sku', p.product_code || '=>' || COALESCE(p.national_drug_id, '')
FROM products p JOIN tenants t ON t.id = p.tenant_id WHERE t.tenant_code = 'DEMO_PHARMACY'
UNION ALL
SELECT 'stock', p.product_code || '/' || b.batch_number || '=' || b.quantity_available::text
FROM inventory_batches b
JOIN products p ON p.id = b.product_id
JOIN tenants t ON t.id = b.tenant_id
WHERE t.tenant_code = 'DEMO_PHARMACY'
UNION ALL
SELECT 'rx', ep.prescription_code || '/' || ep.status
FROM pack_pharmacy.electronic_prescriptions ep
JOIN tenants t ON t.id = ep.tenant_id
WHERE t.tenant_code = 'DEMO_PHARMACY'
ORDER BY 1, 2;
