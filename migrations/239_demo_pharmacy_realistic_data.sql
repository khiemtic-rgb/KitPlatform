-- 239: DEMO_PHARMACY realistic audit data refresh (idempotent)
-- ONLY tenant_code = DEMO_PHARMACY. Never mutates NT_XUANHOA / DEMO_CLINIC.
-- Prefixes: AUD-* sales, AUD-PO / AUD-GRN, AUD-SHIFT, DECK* catalog extension.
-- Safe to re-run: deletes AUD-* transactional rows then recreates.

DO $$
DECLARE
  v_demo uuid;
  v_code text;
BEGIN
  SELECT id, tenant_code INTO v_demo, v_code
  FROM public.tenants
  WHERE id = '11111111-1111-1111-1111-111111111101' AND deleted_at IS NULL;

  IF v_demo IS NULL THEN
    RAISE EXCEPTION 'Safety abort: DEMO tenant UUID missing';
  END IF;
  IF v_code <> 'DEMO_PHARMACY' THEN
    RAISE EXCEPTION 'Safety abort: UUID 1111…1101 is %, expected DEMO_PHARMACY', v_code;
  END IF;
  IF EXISTS (SELECT 1 FROM public.tenants WHERE tenant_code = 'NT_XUANHOA' AND id = v_demo) THEN
    RAISE EXCEPTION 'Safety abort: DEMO UUID collides with NT_XUANHOA';
  END IF;
END $$;

-- ---------------------------------------------------------------------------
-- Branding (audit-safe, keep slim nav feature)
-- ---------------------------------------------------------------------------
UPDATE public.tenants
SET tenant_name = 'Nha Thuoc Demo (Tham dinh)',
    settings = COALESCE(settings, '{}'::jsonb)
      || jsonb_build_object(
        'allow_negative_stock', false,
        'loyalty_enabled', true,
        'batch_mode', 'suggest',
        'receipt', jsonb_build_object(
          'name', 'NHA THUOC DEMO THAM DINH',
          'phone', '0243123456',
          'address', '123 Pho Hue, Hai Ba Trung, Ha Noi',
          'tagline', 'Moi truong test tham dinh — khong phai du lieu that'
        ),
        'contact_hotline', '0243123456',
        'contact_email', 'admin@demo.novixa.vn',
        'contact_address', '123 Pho Hue, Hai Ba Trung, Ha Noi'
      )
WHERE tenant_code = 'DEMO_PHARMACY'
  AND id = '11111111-1111-1111-1111-111111111101';

UPDATE public.branches
SET branch_name = 'Chi nhanh Ha Noi (Demo)',
    address = '123 Pho Hue, Hai Ba Trung, Ha Noi',
    phone = '0243123456',
    updated_at = NOW()
WHERE id = '11111111-1111-1111-1111-111111111201'
  AND tenant_id = '11111111-1111-1111-1111-111111111101';

-- ---------------------------------------------------------------------------
-- Catalog: categories / brands / ingredients
-- ---------------------------------------------------------------------------
INSERT INTO public.product_categories (id, tenant_id, category_code, category_name, sort_order, status)
VALUES
  ('33333333-3333-3333-3333-333333333304', '11111111-1111-1111-1111-111111111101', 'DA_DAY', 'Da day — tieu hoa', 4, 1),
  ('33333333-3333-3333-3333-333333333305', '11111111-1111-1111-1111-111111111101', 'HO_HAP', 'Ho hap — cam cum', 5, 1),
  ('33333333-3333-3333-3333-333333333306', '11111111-1111-1111-1111-111111111101', 'NGAO_DUOC', 'Ngoai dung — dau gio', 6, 1)
ON CONFLICT (tenant_id, category_code) DO NOTHING;

INSERT INTO public.product_brands (id, tenant_id, brand_code, brand_name, status)
VALUES
  ('44444444-4444-4444-4444-444444444403', '11111111-1111-1111-1111-111111111101', 'DOMESCO', 'Domesco', 1),
  ('44444444-4444-4444-4444-444444444404', '11111111-1111-1111-1111-111111111101', 'OPC', 'OPC Pharma', 1),
  ('44444444-4444-4444-4444-444444444405', '11111111-1111-1111-1111-111111111101', 'TRAPHACO', 'Traphaco', 1),
  ('44444444-4444-4444-4444-444444444406', '11111111-1111-1111-1111-111111111101', 'IMEXPHARM', 'Imexpharm', 1)
ON CONFLICT (tenant_id, brand_code) DO NOTHING;

INSERT INTO public.active_ingredients (id, tenant_id, ingredient_code, ingredient_name, status)
SELECT v.id, '11111111-1111-1111-1111-111111111101', v.code, v.name, 1
FROM (VALUES
  ('d239a006-5555-4555-8555-555555555506'::uuid, 'IBUPROFEN', 'Ibuprofen'),
  ('d239a007-5555-4555-8555-555555555507'::uuid, 'OMEPRAZOLE', 'Omeprazole'),
  ('d239a008-5555-4555-8555-555555555508'::uuid, 'DIOSMECTITE', 'Diosmectite'),
  ('d239a009-5555-4555-8555-555555555509'::uuid, 'ACETYLCYSTEINE', 'Acetylcysteine'),
  ('d239a010-5555-4555-8555-555555555510'::uuid, 'CLARITHROMYCIN', 'Clarithromycin')
) AS v(id, code, name)
WHERE NOT EXISTS (
  SELECT 1 FROM public.active_ingredients ai
  WHERE ai.id = v.id
     OR (ai.tenant_id = '11111111-1111-1111-1111-111111111101' AND ai.ingredient_code = v.code)
);

-- Enrich core SKU descriptions
UPDATE public.products SET
  description = CASE product_code
    WHEN 'PARA500' THEN 'Paracetamol 500mg — giam dau ha sot OTC. Hop 10 vi x 10 vien. Khong dung qua 4g/ngay.'
    WHEN 'PARA_EXTRA' THEN 'Paracetamol Extra + caffeine — dau dau, dau rang. Uong sau an.'
    WHEN 'AMOX500' THEN 'Amoxicillin 500mg — khang sinh ke don. Dung du lieu trinh theo bac si.'
    WHEN 'VITC1000' THEN 'Vitamin C 1000mg — bo sung, tang de khang. 1 vien/ngay sau bua sang.'
    ELSE description
  END,
  updated_at = NOW()
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND product_code IN ('PARA500','PARA_EXTRA','AMOX500','VITC1000');

-- Extended catalog (20 SKUs) with mock QG map
INSERT INTO public.products (
  id, tenant_id, category_id, brand_id,
  product_code, product_name, generic_name,
  drug_type, product_kind, dispensing_class,
  national_drug_id, national_registration_number,
  dosage_form, packaging, description, status
)
VALUES
  ('a0000000-0000-4000-8000-000000000001', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333301', '44444444-4444-4444-4444-444444444401',
   'DECK001', 'Panadol Extra (H/24v)', 'Paracetamol + Caffeine', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000101', 'VD-1101-24', 'Vien nen', 'Hop 24 vien', 'Giam dau ha sot manh.', 1),
  ('a0000000-0000-4000-8000-000000000002', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333301', '44444444-4444-4444-4444-444444444402',
   'DECK002', 'Efferalgan 500mg (H/16v)', 'Paracetamol', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000102', 'VD-1102-24', 'Vien sui', 'Hop 16 vien', 'Paracetamol dang sui — hap thu nhanh.', 1),
  ('a0000000-0000-4000-8000-000000000003', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333301', '44444444-4444-4444-4444-444444444403',
   'DECK003', 'Brufen 400mg (H/30v)', 'Ibuprofen', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000103', 'VD-1103-23', 'Vien bao phim', 'Hop 30 vien', 'NSAID giam dau khang viem. Uong sau an.', 1),
  ('a0000000-0000-4000-8000-000000000004', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333302', '44444444-4444-4444-4444-444444444404',
   'DECK004', 'Augmentin 625mg (H/14v)', 'Amoxicillin + Clavulanate', 2, 'pharmacy_drug', 'prescription', 'DRUG-VN-000104', 'VD-1104-22', 'Vien nen', 'Hop 14 vien', 'Khang sinh pho rong — ke don.', 1),
  ('a0000000-0000-4000-8000-000000000005', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333302', '44444444-4444-4444-4444-444444444406',
   'DECK005', 'Klacid 500mg (H/14v)', 'Clarithromycin', 2, 'pharmacy_drug', 'prescription', 'DRUG-VN-000105', 'VD-1105-22', 'Vien bao phim', 'Hop 14 vien', 'Macrolide — viem hong, viem xoang.', 1),
  ('a0000000-0000-4000-8000-000000000006', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333303', '44444444-4444-4444-4444-444444444405',
   'DECK006', 'Berocca Performance (H/15v)', 'Vitamin B + C + khoang', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000106', 'VD-1106-24', 'Vien sui', 'Hop 15 vien', 'Da vitamin cho nguoi ban ron.', 1),
  ('a0000000-0000-4000-8000-000000000007', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333303', '44444444-4444-4444-4444-444444444401',
   'DECK007', 'Calcium Corbiere (Chai)', 'Canxi + D3', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000107', 'VD-1107-23', 'Sirol', 'Chai 120ml', 'Bo sung canxi.', 1),
  ('a0000000-0000-4000-8000-000000000008', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333304', '44444444-4444-4444-4444-444444444405',
   'DECK008', 'Gastropulgite (H/20goi)', 'Diosmectite', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000108', 'VD-1108-23', 'Bot goi', 'Hop 20 goi', 'Tri tieu chay cap.', 1),
  ('a0000000-0000-4000-8000-000000000009', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333304', '44444444-4444-4444-4444-444444444403',
   'DECK009', 'Smecta 3g (H/30goi)', 'Diosmectite', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000109', 'VD-1109-23', 'Bot goi', 'Hop 30 goi', 'Roi loan tieu hoa.', 1),
  ('a0000000-0000-4000-8000-000000000010', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333304', '44444444-4444-4444-4444-444444444402',
   'DECK010', 'Omeprazole Stada 20mg (H/28v)', 'Omeprazole', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000110', 'VD-1110-22', 'Vien nang', 'Hop 28 vien', 'PPI — trao nguoc, loet da day.', 1),
  ('a0000000-0000-4000-8000-000000000011', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333305', '44444444-4444-4444-4444-444444444405',
   'DECK011', 'Decolgen Forte (H/24v)', 'Paracetamol + Phenylephrine', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000111', 'VD-1111-24', 'Vien nen', 'Hop 24 vien', 'Cam cum — giam nghe mui, ha sot.', 1),
  ('a0000000-0000-4000-8000-000000000012', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333305', '44444444-4444-4444-4444-444444444401',
   'DECK012', 'Terpin Codeine (Chai 60ml)', 'Terpin + Codeine', 2, 'pharmacy_drug', 'prescription', 'DRUG-VN-000112', 'VD-1112-21', 'Sirol', 'Chai 60ml', 'Thuoc ho ke don.', 1),
  ('a0000000-0000-4000-8000-000000000013', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333305', '44444444-4444-4444-4444-444444444404',
   'DECK013', 'ACC 200mg (H/20goi)', 'Acetylcysteine', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000113', 'VD-1113-23', 'Bot goi', 'Hop 20 goi', 'Long dom, viem phe quan.', 1),
  ('a0000000-0000-4000-8000-000000000014', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333306', '44444444-4444-4444-4444-444444444405',
   'DECK014', 'Dau gio Xanh 24ml', 'Menthol + Eucalyptus', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000114', 'VD-1114-24', 'Dung dich', 'Chai 24ml', 'Xoa bop giam nhuc moi.', 1),
  ('a0000000-0000-4000-8000-000000000015', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333306', '44444444-4444-4444-4444-444444444403',
   'DECK015', 'Salonpas (H/20mieng)', 'Methyl salicylate', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000115', 'VD-1115-24', 'Mieng dan', 'Hop 20 mieng', 'Dan giam dau co.', 1),
  ('a0000000-0000-4000-8000-000000000016', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333301', '44444444-4444-4444-4444-444444444406',
   'DECK016', 'Tatanol 500mg (H/10v)', 'Paracetamol', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000116', 'VD-1116-24', 'Vien nen', 'Hop 10 vien', 'Paracetamol gia binh dan.', 1),
  ('a0000000-0000-4000-8000-000000000017', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333303', '44444444-4444-4444-4444-444444444402',
   'DECK017', 'Redoxon Double Action (H/15v)', 'Vitamin C + Zinc', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000117', 'VD-1117-24', 'Vien sui', 'Hop 15 vien', 'Tang mien dich.', 1),
  ('a0000000-0000-4000-8000-000000000018', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333302', '44444444-4444-4444-4444-444444444401',
   'DECK018', 'Cefixim 200mg (H/10v)', 'Cefixim', 2, 'pharmacy_drug', 'prescription', 'DRUG-VN-000118', 'VD-1118-22', 'Vien nang', 'Hop 10 vien', 'Cephalosporin the he 3.', 1),
  ('a0000000-0000-4000-8000-000000000019', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333304', '44444444-4444-4444-4444-444444444404',
   'DECK019', 'Motilium-M 10mg (H/30v)', 'Domperidone', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000119', 'VD-1119-23', 'Vien nen', 'Hop 30 vien', 'Chong non, tang nhu dong.', 1),
  ('a0000000-0000-4000-8000-000000000020', '11111111-1111-1111-1111-111111111101', '33333333-3333-3333-3333-333333333305', '44444444-4444-4444-4444-444444444403',
   'DECK020', 'Prospan Siro 100ml', 'Ivy leaf extract', 1, 'pharmacy_drug', 'otc', 'DRUG-VN-000120', 'VD-1120-24', 'Sirol', 'Chai 100ml', 'Sirol ho thao duoc.', 1)
ON CONFLICT (tenant_id, product_code) DO UPDATE SET
  product_name = EXCLUDED.product_name,
  generic_name = EXCLUDED.generic_name,
  description = EXCLUDED.description,
  national_drug_id = EXCLUDED.national_drug_id,
  national_registration_number = EXCLUDED.national_registration_number,
  dispensing_class = EXCLUDED.dispensing_class,
  product_kind = EXCLUDED.product_kind,
  dosage_form = EXCLUDED.dosage_form,
  packaging = EXCLUDED.packaging,
  updated_at = NOW();

INSERT INTO public.product_units (id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit)
SELECT
  ('b0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
  '11111111-1111-1111-1111-111111111101',
  ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
  'Hop', 1, TRUE, TRUE
FROM generate_series(1, 20) AS n
WHERE EXISTS (
  SELECT 1 FROM public.products p
  WHERE p.id = ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid
    AND p.tenant_id = '11111111-1111-1111-1111-111111111101'
)
ON CONFLICT (product_id, unit_name) DO NOTHING;

INSERT INTO public.product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
SELECT
  '11111111-1111-1111-1111-111111111101',
  ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
  '8934567891' || lpad(n::text, 3, '0'),
  1, TRUE
FROM generate_series(1, 20) AS n
WHERE EXISTS (
  SELECT 1 FROM public.products p
  WHERE p.id = ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid
)
ON CONFLICT (tenant_id, barcode) DO NOTHING;

INSERT INTO public.product_prices (tenant_id, product_id, product_unit_id, price_type, price)
SELECT
  '11111111-1111-1111-1111-111111111101',
  p.id,
  u.id,
  1,
  (15000 + n * 3500)::numeric(18,2)
FROM generate_series(1, 20) AS n
JOIN public.products p ON p.id = ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid
  AND p.tenant_id = '11111111-1111-1111-1111-111111111101'
JOIN public.product_units u ON u.product_id = p.id AND u.is_base_unit = TRUE
WHERE NOT EXISTS (
  SELECT 1 FROM public.product_prices pp
  WHERE pp.product_id = p.id AND pp.product_unit_id = u.id AND pp.price_type = 1 AND pp.status = 1
);

-- ---------------------------------------------------------------------------
-- Suppliers
-- ---------------------------------------------------------------------------
INSERT INTO public.suppliers (id, tenant_id, supplier_code, supplier_name, tax_code, contact_name, phone, address, payment_terms, status)
VALUES
  ('88888888-8888-8888-8888-888888888801', '11111111-1111-1111-1111-111111111101',
   'NCC001', 'Cong ty Duoc pham ABC', '0123456789', 'Nguyen Van Cung ung', '0283123456',
   '45 Nguyen Trai, Q.1, TP.HCM', 30, 1),
  ('d239a002-8888-4888-8888-888888888802', '11111111-1111-1111-1111-111111111101',
   'NCC002', 'Duoc pham Minh Chau', '0312345678', 'Tran Thi Hang', '0243987654',
   '88 Lang Ha, Dong Da, Ha Noi', 45, 1)
ON CONFLICT (tenant_id, supplier_code) DO UPDATE SET
  supplier_name = EXCLUDED.supplier_name,
  phone = EXCLUDED.phone,
  address = EXCLUDED.address,
  contact_name = EXCLUDED.contact_name,
  updated_at = NOW();

UPDATE public.suppliers
SET contact_name = 'Nguyen Van Cung ung',
    address = '45 Nguyen Trai, Q.1, TP.HCM',
    payment_terms = 30,
    updated_at = NOW()
WHERE tenant_id = '11111111-1111-1111-1111-111111111101' AND supplier_code = 'NCC001';

-- ---------------------------------------------------------------------------
-- Stock: boost core + DECK batches (healthy + near-expiry FEFO)
-- ---------------------------------------------------------------------------
INSERT INTO public.inventory_batches (
  id, tenant_id, warehouse_id, product_id, batch_number, expiry_date,
  unit_cost, quantity_received, quantity_available, supplier_id, status
)
SELECT
  ('c0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
  '11111111-1111-1111-1111-111111111101',
  '22222222-2222-2222-2222-222222222201',
  ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
  'DECK-LOT-' || lpad(n::text, 4, '0'),
  CASE
    WHEN n <= 6 THEN (CURRENT_DATE + (8 + n))::date
    WHEN n <= 12 THEN (CURRENT_DATE + INTERVAL '180 days')::date
    ELSE (CURRENT_DATE + INTERVAL '720 days')::date
  END,
  (8000 + n * 200)::numeric(18,2),
  (120 + n * 20)::numeric(18,3),
  (120 + n * 20)::numeric(18,3),
  '88888888-8888-8888-8888-888888888801',
  1
FROM generate_series(1, 20) AS n
WHERE EXISTS (
  SELECT 1 FROM public.products p
  WHERE p.id = ('a0000000-0000-4000-8000-' || lpad(n::text, 12, '0'))::uuid
)
ON CONFLICT (id) DO UPDATE SET
  expiry_date = EXCLUDED.expiry_date,
  quantity_received = GREATEST(inventory_batches.quantity_received, EXCLUDED.quantity_received),
  quantity_available = GREATEST(inventory_batches.quantity_available, EXCLUDED.quantity_available),
  updated_at = NOW();

-- Near-expiry overlays on core SKUs
INSERT INTO public.inventory_batches (
  id, tenant_id, warehouse_id, product_id, batch_number, expiry_date,
  unit_cost, quantity_received, quantity_available, supplier_id, status
)
VALUES
  ('c0000000-0000-4000-8000-000000000098', '11111111-1111-1111-1111-111111111101',
   '22222222-2222-2222-2222-222222222201', '66666666-6666-6666-6666-666666666603',
   'AMOX-NEAR', (CURRENT_DATE + 18)::date, 1800, 85, 85, '88888888-8888-8888-8888-888888888801', 1),
  ('c0000000-0000-4000-8000-000000000099', '11111111-1111-1111-1111-111111111101',
   '22222222-2222-2222-2222-222222222201', '66666666-6666-6666-6666-666666666604',
   'VITC-NEAR', (CURRENT_DATE + 12)::date, 5500, 120, 120, '88888888-8888-8888-8888-888888888801', 1)
ON CONFLICT (id) DO UPDATE SET
  expiry_date = EXCLUDED.expiry_date,
  quantity_available = GREATEST(inventory_batches.quantity_available, EXCLUDED.quantity_available),
  updated_at = NOW();

UPDATE public.inventory_batches
SET quantity_available = GREATEST(quantity_available, 1800),
    quantity_received = GREATEST(quantity_received, 1800),
    updated_at = NOW()
WHERE id IN (
  '99999999-9999-9999-9999-999999999901',
  '99999999-9999-9999-9999-999999999902',
  '99999999-9999-9999-9999-999999999903',
  '99999999-9999-9999-9999-999999999904',
  '99999999-9999-9999-9999-999999999905'
);

INSERT INTO public.stock_movements (
  tenant_id, warehouse_id, batch_id, product_id, movement_type, reference_type, reference_id, quantity, unit_cost
)
SELECT b.tenant_id, b.warehouse_id, b.id, b.product_id, 1, 'SEED_AUDIT_RICH',
       '11111111-1111-1111-1111-111111111101', b.quantity_received, b.unit_cost
FROM public.inventory_batches b
WHERE b.tenant_id = '11111111-1111-1111-1111-111111111101'
  AND (
    b.batch_number LIKE 'DECK-LOT-%'
    OR b.batch_number IN ('AMOX-NEAR','VITC-NEAR')
  )
  AND NOT EXISTS (
    SELECT 1 FROM public.stock_movements sm
    WHERE sm.batch_id = b.id AND sm.reference_type = 'SEED_AUDIT_RICH'
  );

-- ---------------------------------------------------------------------------
-- Customers + loyalty
-- ---------------------------------------------------------------------------
INSERT INTO public.customers (id, tenant_id, customer_code, full_name, phone, email, date_of_birth, gender, status)
VALUES
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', '11111111-1111-1111-1111-111111111101', 'KH001', 'Tran Thi Mai', '0909123456', 'mai.tran@email.com', '1985-03-15', 2, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02', '11111111-1111-1111-1111-111111111101', 'KH002', 'Nguyen Van Hung', '0909234567', 'hung.nguyen@email.com', '1978-11-22', 1, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03', '11111111-1111-1111-1111-111111111101', 'KH003', 'Le Thi Hong', '0909345678', 'hong.le@email.com', '1990-05-08', 2, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04', '11111111-1111-1111-1111-111111111101', 'KH004', 'Pham Thu Ha', '0909456789', 'ha.pham@email.com', '1992-07-30', 2, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05', '11111111-1111-1111-1111-111111111101', 'KH005', 'Hoang Minh Duc', '0909567890', 'duc.hoang@email.com', '1988-01-14', 1, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06', '11111111-1111-1111-1111-111111111101', 'KH006', 'Vo Thi Lan', '0909678901', 'lan.vo@email.com', '1995-09-03', 2, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa07', '11111111-1111-1111-1111-111111111101', 'KH007', 'Dang Quoc Bao', '0909789012', 'bao.dang@email.com', '1983-12-19', 1, 1),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa08', '11111111-1111-1111-1111-111111111101', 'KH008', 'Bui Thi Yen', '0909890123', 'yen.bui@email.com', '1998-04-27', 2, 1)
ON CONFLICT (tenant_id, customer_code) DO UPDATE SET
  full_name = EXCLUDED.full_name,
  phone = EXCLUDED.phone,
  email = EXCLUDED.email,
  date_of_birth = EXCLUDED.date_of_birth,
  gender = EXCLUDED.gender,
  updated_at = NOW();

INSERT INTO public.loyalty_programs (id, tenant_id, program_code, program_name, points_per_amount, amount_per_point, status)
VALUES (
  'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01',
  '11111111-1111-1111-1111-111111111101',
  'LOYALTY_DEFAULT', 'Tich diem Demo Tham dinh', 10000, 10000, 1
)
ON CONFLICT (tenant_id, program_code) DO UPDATE SET
  program_name = EXCLUDED.program_name,
  status = 1,
  updated_at = NOW();

INSERT INTO public.loyalty_tiers (id, program_id, tier_code, tier_name, min_points, discount_percent, sort_order)
VALUES
  ('cccccccc-cccc-cccc-cccc-cccccccccc01', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'BRONZE', 'Dong', 0, 0, 1),
  ('cccccccc-cccc-cccc-cccc-cccccccccc02', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'SILVER', 'Bac', 500, 2, 2),
  ('cccccccc-cccc-cccc-cccc-cccccccccc03', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', 'GOLD', 'Vang', 2000, 5, 3)
ON CONFLICT (id) DO NOTHING;

INSERT INTO public.customer_loyalty (customer_id, program_id, tier_id, points_balance, lifetime_points)
SELECT c.id, 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01',
       CASE
         WHEN c.customer_code IN ('KH001','KH002') THEN 'cccccccc-cccc-cccc-cccc-cccccccccc02'::uuid
         WHEN c.customer_code = 'KH005' THEN 'cccccccc-cccc-cccc-cccc-cccccccccc03'::uuid
         ELSE 'cccccccc-cccc-cccc-cccc-cccccccccc01'::uuid
       END,
       CASE c.customer_code
         WHEN 'KH001' THEN 680 WHEN 'KH002' THEN 520 WHEN 'KH005' THEN 2450 ELSE 80
       END,
       CASE c.customer_code
         WHEN 'KH001' THEN 1200 WHEN 'KH002' THEN 900 WHEN 'KH005' THEN 3100 ELSE 80
       END
FROM public.customers c
WHERE c.tenant_id = '11111111-1111-1111-1111-111111111101'
ON CONFLICT (customer_id, program_id) DO UPDATE SET
  tier_id = EXCLUDED.tier_id,
  points_balance = EXCLUDED.points_balance,
  lifetime_points = EXCLUDED.lifetime_points;

-- ---------------------------------------------------------------------------
-- Clear previous AUD-* transactional seed (keep RX-DEMO-001)
-- ---------------------------------------------------------------------------
DELETE FROM public.sales_order_batch_allocations
WHERE sales_order_item_id IN (
  SELECT i.id FROM public.sales_order_items i
  JOIN public.sales_orders o ON o.id = i.sales_order_id
  WHERE o.tenant_id = '11111111-1111-1111-1111-111111111101'
    AND (o.order_number LIKE 'AUD-%' OR o.order_number LIKE 'DECK-%')
);

DELETE FROM public.sales_order_items
WHERE sales_order_id IN (
  SELECT id FROM public.sales_orders
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND (order_number LIKE 'AUD-%' OR order_number LIKE 'DECK-%')
);

DELETE FROM public.sales_payments
WHERE sales_order_id IN (
  SELECT id FROM public.sales_orders
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND (order_number LIKE 'AUD-%' OR order_number LIKE 'DECK-%')
);

DELETE FROM public.sales_return_items
WHERE sales_return_id IN (
  SELECT id FROM public.sales_returns
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND return_number LIKE 'AUD-%'
);

DELETE FROM public.sales_returns
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND return_number LIKE 'AUD-%';

DELETE FROM public.sales_orders
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND (order_number LIKE 'AUD-%' OR order_number LIKE 'DECK-%');

DELETE FROM public.stock_movements
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND reference_type IN ('SEED_AUDIT_SALE','SEED_AUDIT_RETURN');

DELETE FROM public.goods_receipt_items
WHERE goods_receipt_id IN (
  SELECT id FROM public.goods_receipts
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND grn_number LIKE 'AUD-%'
);

DELETE FROM public.goods_receipts
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND grn_number LIKE 'AUD-%';

DELETE FROM public.purchase_order_items
WHERE purchase_order_id IN (
  SELECT id FROM public.purchase_orders
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND po_number LIKE 'AUD-%'
);

DELETE FROM public.purchase_orders
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND po_number LIKE 'AUD-%';

-- Detach sales from shifts then refresh AUD shifts
UPDATE public.sales_orders
SET sales_shift_id = NULL
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND sales_shift_id IN (
    SELECT id FROM public.sales_shifts
    WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
      AND shift_number LIKE 'AUD-%'
  );

DELETE FROM public.sales_shifts
WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  AND shift_number LIKE 'AUD-%';

-- ---------------------------------------------------------------------------
-- Shifts: closed yesterday + open today
-- ---------------------------------------------------------------------------
INSERT INTO public.sales_shifts (
  id, tenant_id, warehouse_id, opened_by, closed_by, shift_number,
  opened_at, closed_at, opening_cash, closing_cash, expected_cash, cash_variance, status, close_notes
)
VALUES
(
  'd239f001-ffff-4fff-8fff-ffffffff0001',
  '11111111-1111-1111-1111-111111111101',
  '22222222-2222-2222-2222-222222222201',
  '11111111-1111-1111-1111-111111111401',
  'a237a402-1111-4111-8111-111111111402',
  'AUD-SHIFT-YDAY',
  (date_trunc('day', now() AT TIME ZONE 'Asia/Ho_Chi_Minh') - INTERVAL '1 day' + INTERVAL '7 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh',
  (date_trunc('day', now() AT TIME ZONE 'Asia/Ho_Chi_Minh') - INTERVAL '1 day' + INTERVAL '21 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh',
  500000, 2850000, 2845000, 5000, 2, 'Ca thuoc demo — da chot'
),
(
  'd239f002-ffff-4fff-8fff-ffffffff0002',
  '11111111-1111-1111-1111-111111111101',
  '22222222-2222-2222-2222-222222222201',
  'a237a402-1111-4111-8111-111111111402',
  NULL,
  'AUD-SHIFT-TODAY',
  (date_trunc('day', now() AT TIME ZONE 'Asia/Ho_Chi_Minh') + INTERVAL '7 hours') AT TIME ZONE 'Asia/Ho_Chi_Minh',
  NULL,
  500000, NULL, NULL, NULL, 1, NULL
);

-- ---------------------------------------------------------------------------
-- Procurement: 1 closed PO+GRN, 1 approved PO, 1 draft GRN
-- ---------------------------------------------------------------------------
DO $$
DECLARE
  v_tenant uuid := '11111111-1111-1111-1111-111111111101';
  v_wh uuid := '22222222-2222-2222-2222-222222222201';
  v_user uuid := '11111111-1111-1111-1111-111111111401';
  v_sup1 uuid := '88888888-8888-8888-8888-888888888801';
  v_sup2 uuid := 'd239a002-8888-4888-8888-888888888802';
  v_vat uuid;
  v_po1 uuid := 'd239a101-aaaa-4aaa-8aaa-aaaaaaaaaa01';
  v_po2 uuid := 'd239a102-aaaa-4aaa-8aaa-aaaaaaaaaa02';
  v_poi1 uuid := 'd239a111-aaaa-4aaa-8aaa-aaaaaaaaaa01';
  v_poi2 uuid := 'd239a112-aaaa-4aaa-8aaa-aaaaaaaaaa02';
  v_poi3 uuid := 'd239a113-aaaa-4aaa-8aaa-aaaaaaaaaa03';
  v_grn1 uuid := 'd239a201-bbbb-4bbb-8bbb-bbbbbbbbbb01';
  v_grn2 uuid := 'd239a202-bbbb-4bbb-8bbb-bbbbbbbbbb02';
  v_gri1 uuid := 'd239a211-bbbb-4bbb-8bbb-bbbbbbbbbb01';
  v_gri2 uuid := 'd239a212-bbbb-4bbb-8bbb-bbbbbbbbbb02';
BEGIN
  SELECT id INTO v_vat
  FROM public.procurement_vat_treatments
  WHERE tenant_id = v_tenant AND treatment_code = 'vat_5'
  LIMIT 1;

  IF v_vat IS NULL THEN
    SELECT id INTO v_vat
    FROM public.procurement_vat_treatments
    WHERE tenant_id = v_tenant
    ORDER BY sort_order
    LIMIT 1;
  END IF;

  IF v_vat IS NULL THEN
    RAISE EXCEPTION 'DEMO missing procurement_vat_treatments';
  END IF;

  -- PO1 received+closed (history)
  INSERT INTO public.purchase_orders (
    id, tenant_id, po_number, supplier_id, warehouse_id, order_date, expected_date,
    status, currency_code, subtotal, tax_amount, total_amount, notes, created_by, vat_treatment_id
  ) VALUES (
    v_po1, v_tenant, 'AUD-PO-001', v_sup1, v_wh,
    NOW() - INTERVAL '12 days', (CURRENT_DATE - 10),
    5, 'VND', 2450000, 122500, 2572500,
    'Don nhap hang dinh ky — da nhan du', v_user, v_vat
  );

  INSERT INTO public.purchase_order_items (
    id, tenant_id, purchase_order_id, product_id, product_unit_id, ordered_qty, received_qty, unit_price, line_total
  ) VALUES
    (v_poi1, v_tenant, v_po1, '66666666-6666-6666-6666-666666666601', '77777777-7777-7777-7777-777777777701',
     2000, 2000, 350, 700000),
    (v_poi2, v_tenant, v_po1, '66666666-6666-6666-6666-666666666603', '77777777-7777-7777-7777-777777777704',
     500, 500, 1800, 900000),
    (v_poi3, v_tenant, v_po1, 'a0000000-0000-4000-8000-000000000011', 'b0000000-0000-4000-8000-000000000011',
     40, 40, 21250, 850000);

  INSERT INTO public.goods_receipts (
    id, tenant_id, grn_number, purchase_order_id, supplier_id, warehouse_id,
    receipt_date, status, received_by, notes, vat_treatment_id,
    tax_rate_percent, subtotal_gross, merchandise_net, tax_amount, total_amount
  ) VALUES (
    v_grn1, v_tenant, 'AUD-GRN-001', v_po1, v_sup1, v_wh,
    NOW() - INTERVAL '10 days', 2, v_user, 'Nhap kho day du AUD-PO-001', v_vat,
    5, 2450000, 2450000, 122500, 2572500
  );

  INSERT INTO public.goods_receipt_items (
    id, tenant_id, goods_receipt_id, purchase_order_item_id, product_id, product_unit_id,
    batch_number, expiry_date, quantity, unit_cost, line_total, inventory_unit_cost
  ) VALUES
    (v_gri1, v_tenant, v_grn1, v_poi1, '66666666-6666-6666-6666-666666666601', '77777777-7777-7777-7777-777777777701',
     'LOT2026A', '2028-12-31', 2000, 350, 700000, 350),
    (v_gri2, v_tenant, v_grn1, v_poi2, '66666666-6666-6666-6666-666666666603', '77777777-7777-7777-7777-777777777704',
     'LOTAMOX01', '2028-03-31', 500, 1800, 900000, 1800);

  -- PO2 approved awaiting receive
  INSERT INTO public.purchase_orders (
    id, tenant_id, po_number, supplier_id, warehouse_id, order_date, expected_date,
    status, currency_code, subtotal, tax_amount, total_amount, notes, created_by, vat_treatment_id
  ) VALUES (
    v_po2, v_tenant, 'AUD-PO-002', v_sup2, v_wh,
    NOW() - INTERVAL '2 days', (CURRENT_DATE + 3),
    2, 'VND', 1680000, 84000, 1764000,
    'Cho nhap — vitamin + tieu hoa', v_user, v_vat
  );

  INSERT INTO public.purchase_order_items (
    tenant_id, purchase_order_id, product_id, product_unit_id, ordered_qty, received_qty, unit_price, line_total
  ) VALUES
    (v_tenant, v_po2, '66666666-6666-6666-6666-666666666604', '77777777-7777-7777-7777-777777777705',
     100, 0, 5500, 550000),
    (v_tenant, v_po2, 'a0000000-0000-4000-8000-000000000010', 'b0000000-0000-4000-8000-000000000010',
     50, 0, 22600, 1130000);

  -- Draft GRN without PO (manual receive in progress)
  INSERT INTO public.goods_receipts (
    id, tenant_id, grn_number, purchase_order_id, supplier_id, warehouse_id,
    receipt_date, status, received_by, notes, vat_treatment_id,
    tax_rate_percent, subtotal_gross, merchandise_net, tax_amount, total_amount
  ) VALUES (
    v_grn2, v_tenant, 'AUD-GRN-DRAFT', NULL, v_sup1, v_wh,
    NOW(), 1, v_user, 'Phieu nhap nhap — dang soan', v_vat,
    5, 0, 0, 0, 0
  );
END $$;

-- ---------------------------------------------------------------------------
-- Sales: today + 14-day history + draft + cancelled + AR
-- ---------------------------------------------------------------------------
DO $$
DECLARE
  v_tenant uuid := '11111111-1111-1111-1111-111111111101';
  v_branch uuid := '11111111-1111-1111-1111-111111111201';
  v_wh uuid := '22222222-2222-2222-2222-222222222201';
  v_emp_admin uuid := '11111111-1111-1111-1111-111111111301';
  v_emp_rx uuid := 'a237a302-1111-4111-8111-111111111302';
  v_shift_today uuid := 'd239f002-ffff-4fff-8fff-ffffffff0002';
  v_shift_yday uuid := 'd239f001-ffff-4fff-8fff-ffffffff0001';
  v_customers uuid[] := ARRAY[
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa06'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa07'::uuid,
    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa08'::uuid
  ];
  v_prod uuid; v_unit uuid; v_batch uuid; v_cost numeric(18,2);
  v_order_id uuid; v_item_id uuid;
  v_paid_at timestamptz; v_amount numeric(18,2); v_qty numeric(18,3);
  v_day int; v_seq int; v_cust uuid; v_prod_idx int;
  v_emp uuid; v_shift uuid; v_method smallint;
BEGIN
  -- Today: 18 completed orders (~3.5–6M VND total)
  FOR v_seq IN 1..18 LOOP
    v_prod_idx := 1 + ((v_seq - 1) % 6);
    v_cust := v_customers[1 + ((v_seq - 1) % array_length(v_customers, 1))];
    v_emp := CASE WHEN v_seq % 3 = 0 THEN v_emp_rx ELSE v_emp_admin END;
    v_shift := v_shift_today;
    v_qty := (1 + (v_seq % 3))::numeric;

    SELECT m.pid, m.uid, m.bid, COALESCE(b.unit_cost, 500)
    INTO v_prod, v_unit, v_batch, v_cost
    FROM (VALUES
      (1, '66666666-6666-6666-6666-666666666601'::uuid, '77777777-7777-7777-7777-777777777701'::uuid, '99999999-9999-9999-9999-999999999901'::uuid),
      (2, '66666666-6666-6666-6666-666666666603'::uuid, '77777777-7777-7777-7777-777777777704'::uuid, 'c0000000-0000-4000-8000-000000000098'::uuid),
      (3, '66666666-6666-6666-6666-666666666604'::uuid, '77777777-7777-7777-7777-777777777705'::uuid, 'c0000000-0000-4000-8000-000000000099'::uuid),
      (4, 'a0000000-0000-4000-8000-000000000008'::uuid, 'b0000000-0000-4000-8000-000000000008'::uuid, 'c0000000-0000-4000-8000-000000000008'::uuid),
      (5, 'a0000000-0000-4000-8000-000000000011'::uuid, 'b0000000-0000-4000-8000-000000000011'::uuid, 'c0000000-0000-4000-8000-000000000011'::uuid),
      (6, 'a0000000-0000-4000-8000-000000000016'::uuid, 'b0000000-0000-4000-8000-000000000016'::uuid, 'c0000000-0000-4000-8000-000000000016'::uuid)
    ) AS m(idx, pid, uid, bid)
    JOIN public.inventory_batches b ON b.id = m.bid
    WHERE m.idx = v_prod_idx;

    SELECT COALESCE(pp.price, 25000) * v_qty
    INTO v_amount
    FROM public.product_prices pp
    WHERE pp.product_id = v_prod AND pp.product_unit_id = v_unit AND pp.price_type = 1 AND pp.status = 1
    LIMIT 1;
    IF v_amount IS NULL OR v_amount < 1000 THEN
      v_amount := (45000 + (v_seq % 9) * 12000)::numeric(18,2);
    END IF;

    v_paid_at := (
      date_trunc('day', now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
      + make_interval(hours => 7 + (v_seq % 11), mins => (v_seq * 7) % 60)
    ) AT TIME ZONE 'Asia/Ho_Chi_Minh';
    v_order_id := gen_random_uuid();
    v_item_id := gen_random_uuid();
    v_method := CASE WHEN v_seq % 5 = 0 THEN 2 WHEN v_seq % 7 = 0 THEN 3 ELSE 1 END;

    INSERT INTO public.sales_orders (
      id, tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
      order_date, subtotal, discount_amount, total_amount, status, notes,
      sales_shift_id, amount_paid, outstanding
    ) VALUES (
      v_order_id, v_tenant, 'AUD-T-' || lpad(v_seq::text, 3, '0'),
      v_branch, v_wh, v_cust, v_emp, v_paid_at,
      v_amount, 0, v_amount, 2, 'Don ban demo tham dinh (hom nay)',
      v_shift, v_amount, 0
    );

    INSERT INTO public.sales_order_items (
      id, sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total
    ) VALUES (
      v_item_id, v_order_id, v_prod, v_unit, v_batch, v_qty, (v_amount / v_qty), v_amount
    );

    INSERT INTO public.sales_order_batch_allocations (sales_order_item_id, batch_id, quantity, unit_cost)
    VALUES (v_item_id, v_batch, v_qty, v_cost);

    INSERT INTO public.sales_payments (sales_order_id, payment_method, amount, paid_at)
    VALUES (v_order_id, v_method, v_amount, v_paid_at);

    UPDATE public.inventory_batches
    SET quantity_available = GREATEST(0, quantity_available - v_qty), updated_at = NOW()
    WHERE id = v_batch;

    INSERT INTO public.stock_movements (
      tenant_id, warehouse_id, batch_id, product_id, movement_type,
      reference_type, reference_id, quantity, unit_cost, movement_date
    ) VALUES (
      v_tenant, v_wh, v_batch, v_prod, 2, 'SEED_AUDIT_SALE', v_order_id, v_qty, v_cost, v_paid_at
    );
  END LOOP;

  -- History: 14 days × 8–14 orders
  FOR v_day IN 1..14 LOOP
    FOR v_seq IN 1..(8 + (v_day % 7)) LOOP
      v_prod_idx := 1 + ((v_day + v_seq) % 6);
      v_cust := v_customers[1 + ((v_day + v_seq) % array_length(v_customers, 1))];
      v_emp := CASE WHEN (v_day + v_seq) % 4 = 0 THEN v_emp_rx ELSE v_emp_admin END;
      v_shift := CASE WHEN v_day = 1 THEN v_shift_yday ELSE NULL END;
      v_qty := (1 + ((v_day + v_seq) % 2))::numeric;

      SELECT m.pid, m.uid, m.bid, COALESCE(b.unit_cost, 500)
      INTO v_prod, v_unit, v_batch, v_cost
      FROM (VALUES
        (1, '66666666-6666-6666-6666-666666666601'::uuid, '77777777-7777-7777-7777-777777777701'::uuid, '99999999-9999-9999-9999-999999999901'::uuid),
        (2, '66666666-6666-6666-6666-666666666602'::uuid, '77777777-7777-7777-7777-777777777703'::uuid, '99999999-9999-9999-9999-999999999903'::uuid),
        (3, '66666666-6666-6666-6666-666666666604'::uuid, '77777777-7777-7777-7777-777777777705'::uuid, '99999999-9999-9999-9999-999999999905'::uuid),
        (4, 'a0000000-0000-4000-8000-000000000010'::uuid, 'b0000000-0000-4000-8000-000000000010'::uuid, 'c0000000-0000-4000-8000-000000000010'::uuid),
        (5, 'a0000000-0000-4000-8000-000000000014'::uuid, 'b0000000-0000-4000-8000-000000000014'::uuid, 'c0000000-0000-4000-8000-000000000014'::uuid),
        (6, 'a0000000-0000-4000-8000-000000000020'::uuid, 'b0000000-0000-4000-8000-000000000020'::uuid, 'c0000000-0000-4000-8000-000000000020'::uuid)
      ) AS m(idx, pid, uid, bid)
      JOIN public.inventory_batches b ON b.id = m.bid
      WHERE m.idx = v_prod_idx;

      SELECT COALESCE(pp.price, 28000) * v_qty INTO v_amount
      FROM public.product_prices pp
      WHERE pp.product_id = v_prod AND pp.product_unit_id = v_unit AND pp.price_type = 1 AND pp.status = 1
      LIMIT 1;
      IF v_amount IS NULL OR v_amount < 1000 THEN
        v_amount := (38000 + ((v_day * 17 + v_seq * 11) % 20) * 5000)::numeric(18,2);
      END IF;

      v_paid_at := (
        date_trunc('day', now() AT TIME ZONE 'Asia/Ho_Chi_Minh') - make_interval(days => v_day)
        + make_interval(hours => 8 + (v_seq % 10), mins => (v_seq * 5) % 60)
      ) AT TIME ZONE 'Asia/Ho_Chi_Minh';
      v_order_id := gen_random_uuid();
      v_item_id := gen_random_uuid();

      INSERT INTO public.sales_orders (
        id, tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
        order_date, subtotal, discount_amount, total_amount, status, notes,
        sales_shift_id, amount_paid, outstanding
      ) VALUES (
        v_order_id, v_tenant, 'AUD-H-' || v_day || '-' || lpad(v_seq::text, 2, '0'),
        v_branch, v_wh, v_cust, v_emp, v_paid_at,
        v_amount, 0, v_amount, 2, 'Don ban demo lich su',
        v_shift, v_amount, 0
      );

      INSERT INTO public.sales_order_items (
        id, sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total
      ) VALUES (
        v_item_id, v_order_id, v_prod, v_unit, v_batch, v_qty, (v_amount / v_qty), v_amount
      );

      INSERT INTO public.sales_order_batch_allocations (sales_order_item_id, batch_id, quantity, unit_cost)
      VALUES (v_item_id, v_batch, v_qty, v_cost);

      INSERT INTO public.sales_payments (sales_order_id, payment_method, amount, paid_at)
      VALUES (v_order_id, CASE WHEN v_seq % 4 = 0 THEN 2 ELSE 1 END, v_amount, v_paid_at);

      UPDATE public.inventory_batches
      SET quantity_available = GREATEST(0, quantity_available - v_qty), updated_at = NOW()
      WHERE id = v_batch;

      INSERT INTO public.stock_movements (
        tenant_id, warehouse_id, batch_id, product_id, movement_type,
        reference_type, reference_id, quantity, unit_cost, movement_date
      ) VALUES (
        v_tenant, v_wh, v_batch, v_prod, 2, 'SEED_AUDIT_SALE', v_order_id, v_qty, v_cost, v_paid_at
      );
    END LOOP;
  END LOOP;

  -- Draft sale (POS in progress)
  INSERT INTO public.sales_orders (
    id, tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
    order_date, subtotal, discount_amount, total_amount, status, notes,
    sales_shift_id, amount_paid, outstanding
  ) VALUES (
    'd239d001-dddd-4ddd-8ddd-dddddddddd01', v_tenant, 'AUD-DRAFT-001',
    v_branch, v_wh, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03', v_emp_rx, NOW(),
    50000, 0, 50000, 1, 'Don nhap — chua thanh toan',
    v_shift_today, 0, 50000
  );

  INSERT INTO public.sales_order_items (
    id, sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total
  ) VALUES (
    'd239ad01-dddd-4ddd-8ddd-dddddddddd01',
    'd239d001-dddd-4ddd-8ddd-dddddddddd01',
    '66666666-6666-6666-6666-666666666601',
    '77777777-7777-7777-7777-777777777701',
    '99999999-9999-9999-9999-999999999901',
    100, 500, 50000
  );

  -- Cancelled sale
  INSERT INTO public.sales_orders (
    id, tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
    order_date, subtotal, discount_amount, total_amount, status, notes,
    amount_paid, outstanding
  ) VALUES (
    'd239d002-dddd-4ddd-8ddd-dddddddddd02', v_tenant, 'AUD-CANCEL-001',
    v_branch, v_wh, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04', v_emp_admin,
    NOW() - INTERVAL '3 hours',
    85000, 0, 85000, 3, 'Huy do khach doi y',
    0, 0
  );

  -- AR (cong no): completed with partial pay
  INSERT INTO public.sales_orders (
    id, tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
    order_date, subtotal, discount_amount, total_amount, status, notes,
    sales_shift_id, amount_paid, outstanding
  ) VALUES (
    'd239d003-dddd-4ddd-8ddd-dddddddddd03', v_tenant, 'AUD-AR-001',
    v_branch, v_wh, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa05', v_emp_admin,
    NOW() - INTERVAL '2 days',
    450000, 0, 450000, 2, 'Cong no khach — thanh toan 1 phan',
    NULL, 150000, 300000
  );

  INSERT INTO public.sales_order_items (
    id, sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total
  ) VALUES (
    'd239ad03-dddd-4ddd-8ddd-dddddddddd03',
    'd239d003-dddd-4ddd-8ddd-dddddddddd03',
    'a0000000-0000-4000-8000-000000000006',
    'b0000000-0000-4000-8000-000000000006',
    'c0000000-0000-4000-8000-000000000006',
    12, 37500, 450000
  );

  INSERT INTO public.sales_order_batch_allocations (sales_order_item_id, batch_id, quantity, unit_cost)
  VALUES ('d239ad03-dddd-4ddd-8ddd-dddddddddd03', 'c0000000-0000-4000-8000-000000000006', 12, 9200);

  INSERT INTO public.sales_payments (sales_order_id, payment_method, amount, paid_at)
  VALUES ('d239d003-dddd-4ddd-8ddd-dddddddddd03', 1, 150000, NOW() - INTERVAL '2 days');
END $$;

-- ---------------------------------------------------------------------------
-- Extra Rx (pending) + keep RX-DEMO-001
-- ---------------------------------------------------------------------------
INSERT INTO pack_pharmacy.electronic_prescriptions (
  id, tenant_id, branch_id, prescription_code, linked_prescriber_id,
  customer_id, patient_name, patient_phone, status, source,
  verification_method, verified_at, signed_at, created_by
)
SELECT
  'd239c012-cccc-4ccc-8ccc-cccccccccc12',
  '11111111-1111-1111-1111-111111111101',
  '11111111-1111-1111-1111-111111111201',
  'RX-DEMO-002',
  lp.id,
  c.id,
  'Nguyen Van Hung', '0909234567',
  'draft', 'staff_entry',
  NULL, NULL, NULL,
  'a237a402-1111-4111-8111-111111111402'
FROM pack_pharmacy.linked_prescribers lp
JOIN public.customers c ON c.tenant_id = lp.tenant_id AND c.customer_code = 'KH002'
WHERE lp.tenant_id = '11111111-1111-1111-1111-111111111101' AND lp.license_number = 'BYT-DEMO-001'
  AND NOT EXISTS (
    SELECT 1 FROM pack_pharmacy.electronic_prescriptions ep
    WHERE ep.tenant_id = lp.tenant_id AND ep.prescription_code = 'RX-DEMO-002'
  );

INSERT INTO pack_pharmacy.electronic_prescription_lines (
  id, tenant_id, prescription_id, product_id, product_unit_id,
  line_dispensing_class, qty_prescribed, qty_dispensed, dosage_instruction, sort_order
)
SELECT
  'd239ad12-dddd-4ddd-8ddd-dddddddddd12',
  ep.tenant_id, ep.id,
  'a0000000-0000-4000-8000-000000000004',
  'b0000000-0000-4000-8000-000000000004',
  'prescription', 14, 0, '1 vien x 2 lan/ngay sau an (7 ngay)', 1
FROM pack_pharmacy.electronic_prescriptions ep
WHERE ep.tenant_id = '11111111-1111-1111-1111-111111111101'
  AND ep.prescription_code = 'RX-DEMO-002'
  AND NOT EXISTS (
    SELECT 1 FROM pack_pharmacy.electronic_prescription_lines el WHERE el.id = 'd239ad12-dddd-4ddd-8ddd-dddddddddd12'
  );

-- ---------------------------------------------------------------------------
-- Verify summary (DEMO only)
-- ---------------------------------------------------------------------------
SELECT kind, detail FROM (
  SELECT 1 AS ord, 'tenant' AS kind, tenant_code || ' / ' || tenant_name AS detail
  FROM public.tenants WHERE tenant_code = 'DEMO_PHARMACY'
  UNION ALL
  SELECT 2, 'sku_count', count(*)::text FROM public.products
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101' AND deleted_at IS NULL
  UNION ALL
  SELECT 3, 'batch_count', count(*)::text FROM public.inventory_batches
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  UNION ALL
  SELECT 4, 'customer_count', count(*)::text FROM public.customers
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101' AND deleted_at IS NULL
  UNION ALL
  SELECT 5, 'sales_today', count(*)::text FROM public.sales_orders
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND order_number LIKE 'AUD-T-%' AND status = 2
  UNION ALL
  SELECT 6, 'sales_history', count(*)::text FROM public.sales_orders
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND order_number LIKE 'AUD-H-%'
  UNION ALL
  SELECT 7, 'po', count(*)::text FROM public.purchase_orders
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101' AND po_number LIKE 'AUD-%'
  UNION ALL
  SELECT 8, 'grn', count(*)::text FROM public.goods_receipts
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101' AND grn_number LIKE 'AUD-%'
  UNION ALL
  SELECT 9, 'shifts', count(*)::text FROM public.sales_shifts
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101' AND shift_number LIKE 'AUD-%'
  UNION ALL
  SELECT 10, 'rx', count(*)::text FROM pack_pharmacy.electronic_prescriptions
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
  UNION ALL
  SELECT 11, 'near_expiry', count(*)::text FROM public.inventory_batches
  WHERE tenant_id = '11111111-1111-1111-1111-111111111101'
    AND expiry_date <= CURRENT_DATE + 30
  UNION ALL
  SELECT 12, 'xuanhoa_untouched',
    (SELECT count(*)::text FROM public.sales_orders so
     JOIN public.tenants t ON t.id = so.tenant_id
     WHERE t.tenant_code = 'NT_XUANHOA')
) s
ORDER BY ord;
