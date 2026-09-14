-- Local DEMO only: dirty lots to test INV-01 "tồn bất thường".
-- Prefix AUD-STK-*. Idempotent. Never touches NT_XUANHOA.
-- Do NOT add to migration-files.prod.txt.

DO $$
DECLARE
  v_tenant uuid := '11111111-1111-1111-1111-111111111101';
  v_code text;
  v_wh uuid := '22222222-2222-2222-2222-222222222201';
  v_p_qty uuid;
  v_p_cost uuid;
  v_p_both uuid;
  v_sup uuid;
BEGIN
  SELECT tenant_code INTO v_code
  FROM public.tenants
  WHERE id = v_tenant AND deleted_at IS NULL;

  IF v_code IS NULL THEN
    RAISE NOTICE 'Skip: DEMO_PHARMACY missing';
    RETURN;
  END IF;
  IF v_code <> 'DEMO_PHARMACY' THEN
    RAISE EXCEPTION 'Safety abort: UUID 1111…1101 is %, expected DEMO_PHARMACY', v_code;
  END IF;
  IF EXISTS (SELECT 1 FROM public.tenants WHERE tenant_code = 'NT_XUANHOA' AND id = v_tenant) THEN
    RAISE EXCEPTION 'Safety abort: DEMO UUID collides with NT_XUANHOA';
  END IF;

  DELETE FROM public.stock_movements sm
  USING public.inventory_batches b
  WHERE sm.batch_id = b.id
    AND b.tenant_id = v_tenant
    AND b.batch_number LIKE 'AUD-STK-%';

  DELETE FROM public.inventory_batches
  WHERE tenant_id = v_tenant
    AND batch_number LIKE 'AUD-STK-%';

  SELECT id INTO v_p_qty
  FROM public.products
  WHERE tenant_id = v_tenant AND deleted_at IS NULL
  ORDER BY product_code
  LIMIT 1;

  SELECT id INTO v_p_cost
  FROM public.products
  WHERE tenant_id = v_tenant AND deleted_at IS NULL AND id <> v_p_qty
  ORDER BY product_code
  LIMIT 1;

  SELECT id INTO v_p_both
  FROM public.products
  WHERE tenant_id = v_tenant AND deleted_at IS NULL AND id NOT IN (v_p_qty, v_p_cost)
  ORDER BY product_code
  LIMIT 1;

  IF v_p_qty IS NULL OR v_p_cost IS NULL OR v_p_both IS NULL THEN
    RAISE EXCEPTION 'Need at least 3 active DEMO products';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM public.warehouses WHERE id = v_wh AND tenant_id = v_tenant AND deleted_at IS NULL) THEN
    SELECT id INTO v_wh
    FROM public.warehouses
    WHERE tenant_id = v_tenant AND deleted_at IS NULL
    ORDER BY is_default DESC
    LIMIT 1;
  END IF;

  SELECT id INTO v_sup
  FROM public.suppliers
  WHERE tenant_id = v_tenant
  ORDER BY supplier_code
  LIMIT 1;

  -- 1) SL quá lớn, giá vốn bình thường (~400đ) → 1.5 triệu viên
  INSERT INTO public.inventory_batches (
    tenant_id, warehouse_id, product_id, batch_number, expiry_date,
    unit_cost, quantity_received, quantity_available, supplier_id, status
  ) VALUES (
    v_tenant, v_wh, v_p_qty, 'AUD-STK-QTY-01', DATE '2029-12-31',
    400, 1500000, 1500000, v_sup, 1
  );

  -- 2) SL thường, giá vốn lô bẩn (80 tỷ/đơn vị) — giống số "xxx tỷ" trên VPS
  INSERT INTO public.inventory_batches (
    tenant_id, warehouse_id, product_id, batch_number, expiry_date,
    unit_cost, quantity_received, quantity_available, supplier_id, status
  ) VALUES (
    v_tenant, v_wh, v_p_cost, 'AUD-STK-COST-01', DATE '2028-06-30',
    80000000000, 80, 80, v_sup, 1
  );

  -- 3) Cả SL lẫn giá vốn lệch
  INSERT INTO public.inventory_batches (
    tenant_id, warehouse_id, product_id, batch_number, expiry_date,
    unit_cost, quantity_received, quantity_available, supplier_id, status
  ) VALUES (
    v_tenant, v_wh, v_p_both, 'AUD-STK-BOTH-01', DATE '2027-03-31',
    20000000, 250000, 250000, v_sup, 1
  );

  INSERT INTO public.stock_movements (
    tenant_id, warehouse_id, batch_id, product_id,
    movement_type, reference_type, reference_id, quantity, unit_cost
  )
  SELECT
    b.tenant_id, b.warehouse_id, b.id, b.product_id,
    1, 'SEED_AUD_STK', b.id, b.quantity_received, b.unit_cost
  FROM public.inventory_batches b
  WHERE b.tenant_id = v_tenant
    AND b.batch_number LIKE 'AUD-STK-%';

  RAISE NOTICE 'Seeded AUD-STK lots on DEMO_PHARMACY';
END $$;
