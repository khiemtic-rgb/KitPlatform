-- Local DEMO only: staff × product sales for SALES-06/07 + future KPI commission.
-- Prefix AUD-KPI-*. Idempotent. Never touches NT_XUANHOA.
-- Do NOT add to migration-files.prod.txt.

DO $$
DECLARE
  v_tenant uuid := '11111111-1111-1111-1111-111111111101';
  v_code text;
  v_branch uuid := '11111111-1111-1111-1111-111111111201';
  v_wh uuid := '22222222-2222-2222-2222-222222222201';
  v_hien uuid := '33350c09-3d60-4d2a-944c-eb56a925c672';
  v_khiem uuid := '11111111-1111-1111-1111-111111111301';
  v_minh uuid := '41da5589-5b64-4cc8-82cd-909490731c2c';
  v_cust uuid;
  rec record;
  v_i int;
  v_order uuid;
  v_item uuid;
  v_paid timestamptz;
  v_seq int := 0;
  v_month_start date;
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

  -- Restore stock from previous AUD-KPI seed, then delete
  UPDATE public.inventory_batches b
  SET quantity_available = b.quantity_available + x.qty, updated_at = NOW()
  FROM (
    SELECT i.batch_id, SUM(i.quantity) AS qty
    FROM public.sales_order_items i
    JOIN public.sales_orders o ON o.id = i.sales_order_id
    WHERE o.tenant_id = v_tenant AND o.order_number LIKE 'AUD-KPI-%'
    GROUP BY i.batch_id
  ) x
  WHERE b.id = x.batch_id AND b.tenant_id = v_tenant;

  DELETE FROM public.stock_movements sm
  USING public.sales_orders o
  WHERE o.tenant_id = v_tenant
    AND o.order_number LIKE 'AUD-KPI-%'
    AND sm.reference_id = o.id
    AND sm.tenant_id = v_tenant;

  DELETE FROM public.sales_payments sp
  USING public.sales_orders o
  WHERE o.id = sp.sales_order_id
    AND o.tenant_id = v_tenant
    AND o.order_number LIKE 'AUD-KPI-%';

  DELETE FROM public.sales_order_batch_allocations a
  USING public.sales_order_items i
  JOIN public.sales_orders o ON o.id = i.sales_order_id
  WHERE a.sales_order_item_id = i.id
    AND o.tenant_id = v_tenant
    AND o.order_number LIKE 'AUD-KPI-%';

  DELETE FROM public.sales_order_items i
  USING public.sales_orders o
  WHERE i.sales_order_id = o.id
    AND o.tenant_id = v_tenant
    AND o.order_number LIKE 'AUD-KPI-%';

  DELETE FROM public.sales_orders
  WHERE tenant_id = v_tenant AND order_number LIKE 'AUD-KPI-%';

  SELECT id INTO v_cust
  FROM public.customers
  WHERE tenant_id = v_tenant AND deleted_at IS NULL
  ORDER BY created_at
  LIMIT 1;

  -- Ensure DECK lots have room for this month's boxes
  UPDATE public.inventory_batches
  SET quantity_available = GREATEST(quantity_available, 400), updated_at = NOW()
  WHERE tenant_id = v_tenant
    AND id IN (
      'c0000000-0000-4000-8000-000000000006',
      'c0000000-0000-4000-8000-000000000011',
      'c0000000-0000-4000-8000-000000000013'
    );

  v_month_start := date_trunc('month', now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;

  -- Intended KPI (later commission sheet): 3.000đ / hộp vượt
  -- Hiền: Berocca 20 · Decolgen 15 · ACC 10  (sẽ vượt)
  -- Khiêm: 20 / 15 / 10                         (dưới hoặc sát)
  -- Minh:  15 / 12 / 8                          (không vượt)
  FOR rec IN
    SELECT * FROM (VALUES
      (v_hien,  'a0000000-0000-4000-8000-000000000006'::uuid, 'b0000000-0000-4000-8000-000000000006'::uuid, 'c0000000-0000-4000-8000-000000000006'::uuid, 28, 36000::numeric),
      (v_hien,  'a0000000-0000-4000-8000-000000000011'::uuid, 'b0000000-0000-4000-8000-000000000011'::uuid, 'c0000000-0000-4000-8000-000000000011'::uuid, 22, 53500::numeric),
      (v_hien,  'a0000000-0000-4000-8000-000000000013'::uuid, 'b0000000-0000-4000-8000-000000000013'::uuid, 'c0000000-0000-4000-8000-000000000013'::uuid, 14, 60500::numeric),
      (v_khiem, 'a0000000-0000-4000-8000-000000000006'::uuid, 'b0000000-0000-4000-8000-000000000006'::uuid, 'c0000000-0000-4000-8000-000000000006'::uuid, 18, 36000::numeric),
      (v_khiem, 'a0000000-0000-4000-8000-000000000011'::uuid, 'b0000000-0000-4000-8000-000000000011'::uuid, 'c0000000-0000-4000-8000-000000000011'::uuid, 12, 53500::numeric),
      (v_khiem, 'a0000000-0000-4000-8000-000000000013'::uuid, 'b0000000-0000-4000-8000-000000000013'::uuid, 'c0000000-0000-4000-8000-000000000013'::uuid,  8, 60500::numeric),
      (v_minh,  'a0000000-0000-4000-8000-000000000006'::uuid, 'b0000000-0000-4000-8000-000000000006'::uuid, 'c0000000-0000-4000-8000-000000000006'::uuid, 10, 36000::numeric),
      (v_minh,  'a0000000-0000-4000-8000-000000000011'::uuid, 'b0000000-0000-4000-8000-000000000011'::uuid, 'c0000000-0000-4000-8000-000000000011'::uuid,  8, 53500::numeric),
      (v_minh,  'a0000000-0000-4000-8000-000000000013'::uuid, 'b0000000-0000-4000-8000-000000000013'::uuid, 'c0000000-0000-4000-8000-000000000013'::uuid,  5, 60500::numeric)
    ) AS t(emp_id, product_id, unit_id, batch_id, boxes, price)
  LOOP
    FOR v_i IN 1..rec.boxes LOOP
      v_seq := v_seq + 1;
      v_order := gen_random_uuid();
      v_item := gen_random_uuid();
      v_paid := (
        v_month_start
        + ((v_seq % GREATEST(1, EXTRACT(DAY FROM (now() AT TIME ZONE 'Asia/Ho_Chi_Minh'))::int)) || ' days')::interval
        + make_interval(hours => 8 + (v_seq % 10), mins => (v_seq * 3) % 60)
      ) AT TIME ZONE 'Asia/Ho_Chi_Minh';

      INSERT INTO public.sales_orders (
        id, tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
        order_date, subtotal, discount_amount, total_amount, status, notes,
        amount_paid, outstanding
      ) VALUES (
        v_order, v_tenant, 'AUD-KPI-' || to_char(v_month_start, 'YYYYMM') || '-' || lpad(v_seq::text, 3, '0'),
        v_branch, v_wh,
        CASE WHEN v_seq % 4 = 0 THEN v_cust ELSE NULL END,
        rec.emp_id, v_paid,
        rec.price, 0, rec.price, 2, 'Don demo KPI / hoa hong theo SP',
        rec.price, 0
      );

      INSERT INTO public.sales_order_items (
        id, sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total
      ) VALUES (
        v_item, v_order, rec.product_id, rec.unit_id, rec.batch_id, 1, rec.price, rec.price
      );

      INSERT INTO public.sales_order_batch_allocations (sales_order_item_id, batch_id, quantity, unit_cost)
      SELECT v_item, rec.batch_id, 1, COALESCE(b.unit_cost, 10000)
      FROM public.inventory_batches b WHERE b.id = rec.batch_id;

      INSERT INTO public.sales_payments (sales_order_id, payment_method, amount, paid_at)
      VALUES (v_order, 1, rec.price, v_paid);

      UPDATE public.inventory_batches
      SET quantity_available = GREATEST(0, quantity_available - 1), updated_at = NOW()
      WHERE id = rec.batch_id AND tenant_id = v_tenant;

      INSERT INTO public.stock_movements (
        tenant_id, warehouse_id, batch_id, product_id, movement_type,
        reference_type, reference_id, quantity, unit_cost, movement_date
      )
      SELECT v_tenant, v_wh, rec.batch_id, rec.product_id, 2, 'SEED_KPI_SALE', v_order, 1,
             COALESCE(b.unit_cost, 10000), v_paid
      FROM public.inventory_batches b WHERE b.id = rec.batch_id;
    END LOOP;
  END LOOP;

  RAISE NOTICE 'AUD-KPI seed: % orders on DEMO_PHARMACY', v_seq;
END $$;
