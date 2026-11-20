-- KIT-PLATFORM-NOVIXA-PRESCRIPTION-ASSIST-DATA-EVIDENCE-V1
-- READ-ONLY QUERIES FOR NT_XUANHOA
-- DO NOT MODIFY DATA

-- =============================================================================
-- PART A: PRESCRIPTION VOLUME
-- =============================================================================

-- Get tenant ID for NT_XUANHOA
SELECT id, tenant_code, tenant_name 
FROM public.tenants 
WHERE tenant_code = 'NT_XUANHOA';

-- Total prescriptions by status
SELECT 
    status,
    COUNT(*) as count
FROM pack_pharmacy.electronic_prescriptions ep
WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
GROUP BY status
ORDER BY count DESC;

-- Overall prescription statistics
SELECT 
    COUNT(*) as total_prescriptions,
    COUNT(*) FILTER (WHERE status = 'verified') as verified_prescriptions,
    COUNT(*) FILTER (WHERE status = 'signed') as signed_prescriptions,
    COUNT(*) FILTER (WHERE status = 'dispensed') as dispensed_prescriptions,
    COUNT(*) FILTER (WHERE status = 'partially_dispensed') as partially_dispensed_prescriptions,
    COUNT(DISTINCT linked_prescriber_id) as unique_prescribers,
    COUNT(DISTINCT customer_id) as unique_customers,
    MIN(created_at) as earliest_prescription,
    MAX(created_at) as latest_prescription,
    EXTRACT(DAY FROM (MAX(created_at) - MIN(created_at))) as days_span
FROM pack_pharmacy.electronic_prescriptions ep
WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND cancelled_at IS NULL;

-- Prescription lines statistics
SELECT 
    COUNT(*) as total_prescription_lines,
    AVG(line_count) as avg_lines_per_prescription,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY line_count) as median_lines_per_prescription,
    COUNT(*) FILTER (WHERE line_count = 1) as prescriptions_with_1_line,
    COUNT(*) FILTER (WHERE line_count >= 2) as prescriptions_with_2plus_lines,
    COUNT(*) FILTER (WHERE line_count >= 3) as prescriptions_with_3plus_lines,
    MAX(line_count) as max_lines_per_prescription
FROM (
    SELECT 
        epl.prescription_id,
        COUNT(*) as line_count
    FROM pack_pharmacy.electronic_prescription_lines epl
    INNER JOIN pack_pharmacy.electronic_prescriptions ep ON epl.prescription_id = ep.id
    WHERE ep.tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
      AND ep.cancelled_at IS NULL
    GROUP BY epl.prescription_id
) line_counts;

-- =============================================================================
-- PART B: DATA QUALITY - PRESCRIPTIONS
-- =============================================================================

-- Prescriptions with valid product_id
SELECT 
    COUNT(*) as total_lines,
    COUNT(p.id) as lines_with_valid_product,
    COUNT(*) - COUNT(p.id) as lines_with_invalid_product,
    ROUND(100.0 * COUNT(p.id) / COUNT(*), 2) as pct_valid_product
FROM pack_pharmacy.electronic_prescription_lines epl
INNER JOIN pack_pharmacy.electronic_prescriptions ep ON epl.prescription_id = ep.id
LEFT JOIN public.products p ON epl.product_id = p.id AND p.deleted_at IS NULL
WHERE ep.tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND ep.cancelled_at IS NULL;

-- Prescriptions with customer_id
SELECT 
    COUNT(*) as total_prescriptions,
    COUNT(customer_id) as prescriptions_with_customer,
    COUNT(*) - COUNT(customer_id) as prescriptions_without_customer,
    ROUND(100.0 * COUNT(customer_id) / COUNT(*), 2) as pct_with_customer
FROM pack_pharmacy.electronic_prescriptions ep
WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND cancelled_at IS NULL;

-- Prescriptions with prescriber
SELECT 
    COUNT(*) as total_prescriptions,
    COUNT(linked_prescriber_id) as prescriptions_with_prescriber,
    ROUND(100.0 * COUNT(linked_prescriber_id) / COUNT(*), 2) as pct_with_prescriber
FROM pack_pharmacy.electronic_prescriptions ep
WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND cancelled_at IS NULL;

-- Prescriptions linked to sales orders
SELECT 
    COUNT(*) as total_prescriptions,
    COUNT(DISTINCT so.id) as prescriptions_linked_to_sales,
    ROUND(100.0 * COUNT(DISTINCT so.id) / COUNT(*), 2) as pct_linked_to_sales
FROM pack_pharmacy.electronic_prescriptions ep
LEFT JOIN public.sales_orders so ON ep.id = so.prescription_id
WHERE ep.tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND ep.cancelled_at IS NULL;

-- Sales orders with prescription_id
SELECT 
    COUNT(*) as total_sales_orders,
    COUNT(prescription_id) as sales_with_prescription,
    ROUND(100.0 * COUNT(prescription_id) / COUNT(*), 2) as pct_with_prescription
FROM public.sales_orders so
WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND status = 2; -- Completed

-- =============================================================================
-- PART C: CO-PRESCRIPTION PATTERNS
-- =============================================================================

-- Top 20 product pairs in same prescription
-- Only include prescriptions with 2+ lines
-- Only include valid statuses (verified, signed, dispensed, partially_dispensed)
WITH valid_prescriptions AS (
    SELECT id
    FROM pack_pharmacy.electronic_prescriptions
    WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
      AND status IN ('verified', 'signed', 'dispensed', 'partially_dispensed')
      AND cancelled_at IS NULL
),
prescription_products AS (
    SELECT DISTINCT
        epl.prescription_id,
        epl.product_id,
        p.product_name,
        p.product_code
    FROM pack_pharmacy.electronic_prescription_lines epl
    INNER JOIN valid_prescriptions vp ON epl.prescription_id = vp.id
    INNER JOIN public.products p ON epl.product_id = p.id
    WHERE p.deleted_at IS NULL
),
product_pairs AS (
    SELECT 
        LEAST(pp1.product_id, pp2.product_id) as product_a_id,
        GREATEST(pp1.product_id, pp2.product_id) as product_b_id,
        LEAST(pp1.product_name, pp2.product_name) as product_a_name,
        GREATEST(pp1.product_name, pp2.product_name) as product_b_name,
        LEAST(pp1.product_code, pp2.product_code) as product_a_code,
        GREATEST(pp1.product_code, pp2.product_code) as product_b_code,
        pp1.prescription_id
    FROM prescription_products pp1
    INNER JOIN prescription_products pp2 
        ON pp1.prescription_id = pp2.prescription_id
        AND pp1.product_id < pp2.product_id
),
product_a_counts AS (
    SELECT 
        product_id,
        COUNT(DISTINCT prescription_id) as total_prescriptions_with_a
    FROM prescription_products
    GROUP BY product_id
)
SELECT 
    pp.product_a_code,
    pp.product_a_name,
    pp.product_b_code,
    pp.product_b_name,
    COUNT(DISTINCT pp.prescription_id) as co_prescription_count,
    pac.total_prescriptions_with_a,
    ROUND(100.0 * COUNT(DISTINCT pp.prescription_id) / pac.total_prescriptions_with_a, 2) as pct_when_a_prescribed,
    MIN(ep.created_at)::DATE as earliest_date,
    MAX(ep.created_at)::DATE as latest_date
FROM product_pairs pp
INNER JOIN pack_pharmacy.electronic_prescriptions ep ON pp.prescription_id = ep.id
INNER JOIN product_a_counts pac ON pp.product_a_id = pac.product_id
GROUP BY 
    pp.product_a_id,
    pp.product_a_code,
    pp.product_a_name,
    pp.product_b_id,
    pp.product_b_code,
    pp.product_b_name,
    pac.total_prescriptions_with_a
HAVING COUNT(DISTINCT pp.prescription_id) >= 2 -- At least 2 co-occurrences
ORDER BY co_prescription_count DESC, pct_when_a_prescribed DESC
LIMIT 20;

-- =============================================================================
-- PART D: SALES DATA QUALITY
-- =============================================================================

-- Sales orders statistics
SELECT 
    COUNT(*) as total_sales_orders,
    COUNT(*) FILTER (WHERE status = 2) as completed_orders,
    COUNT(DISTINCT customer_id) as unique_customers,
    MIN(order_date) as earliest_order,
    MAX(order_date) as latest_order,
    EXTRACT(DAY FROM (MAX(order_date) - MIN(order_date))) as days_span
FROM public.sales_orders
WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA');

-- Sales order items statistics
SELECT 
    COUNT(*) as total_order_items,
    AVG(item_count) as avg_items_per_order,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY item_count) as median_items_per_order,
    COUNT(*) FILTER (WHERE item_count = 1) as orders_with_1_item,
    COUNT(*) FILTER (WHERE item_count >= 2) as orders_with_2plus_items,
    COUNT(*) FILTER (WHERE item_count >= 3) as orders_with_3plus_items
FROM (
    SELECT 
        soi.sales_order_id,
        COUNT(*) as item_count
    FROM public.sales_order_items soi
    INNER JOIN public.sales_orders so ON soi.sales_order_id = so.id
    WHERE so.tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
      AND so.status = 2 -- Completed
    GROUP BY soi.sales_order_id
) item_counts;

-- Sales items with valid product_id
SELECT 
    COUNT(*) as total_items,
    COUNT(p.id) as items_with_valid_product,
    COUNT(*) - COUNT(p.id) as items_with_invalid_product,
    ROUND(100.0 * COUNT(p.id) / COUNT(*), 2) as pct_valid_product
FROM public.sales_order_items soi
INNER JOIN public.sales_orders so ON soi.sales_order_id = so.id
LEFT JOIN public.products p ON soi.product_id = p.id AND p.deleted_at IS NULL
WHERE so.tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
  AND so.status = 2; -- Completed

-- =============================================================================
-- PART E: CO-PURCHASE PATTERNS
-- =============================================================================

-- Top 20 product pairs in same sales order
-- Only include completed orders with 2+ items
WITH completed_orders AS (
    SELECT id
    FROM public.sales_orders
    WHERE tenant_id = (SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA')
      AND status = 2 -- Completed
),
order_products AS (
    SELECT DISTINCT
        soi.sales_order_id,
        soi.product_id,
        p.product_name,
        p.product_code
    FROM public.sales_order_items soi
    INNER JOIN completed_orders co ON soi.sales_order_id = co.id
    INNER JOIN public.products p ON soi.product_id = p.id
    WHERE p.deleted_at IS NULL
),
product_pairs AS (
    SELECT 
        LEAST(op1.product_id, op2.product_id) as product_a_id,
        GREATEST(op1.product_id, op2.product_id) as product_b_id,
        LEAST(op1.product_name, op2.product_name) as product_a_name,
        GREATEST(op1.product_name, op2.product_name) as product_b_name,
        LEAST(op1.product_code, op2.product_code) as product_a_code,
        GREATEST(op1.product_code, op2.product_code) as product_b_code,
        op1.sales_order_id
    FROM order_products op1
    INNER JOIN order_products op2 
        ON op1.sales_order_id = op2.sales_order_id
        AND op1.product_id < op2.product_id
),
product_a_counts AS (
    SELECT 
        product_id,
        COUNT(DISTINCT sales_order_id) as total_orders_with_a
    FROM order_products
    GROUP BY product_id
)
SELECT 
    pp.product_a_code,
    pp.product_a_name,
    pp.product_b_code,
    pp.product_b_name,
    COUNT(DISTINCT pp.sales_order_id) as co_purchase_count,
    pac.total_orders_with_a,
    ROUND(100.0 * COUNT(DISTINCT pp.sales_order_id) / pac.total_orders_with_a, 2) as pct_when_a_purchased,
    MIN(so.order_date)::DATE as earliest_date,
    MAX(so.order_date)::DATE as latest_date
FROM product_pairs pp
INNER JOIN public.sales_orders so ON pp.sales_order_id = so.id
INNER JOIN product_a_counts pac ON pp.product_a_id = pac.product_id
GROUP BY 
    pp.product_a_id,
    pp.product_a_code,
    pp.product_a_name,
    pp.product_b_id,
    pp.product_b_code,
    pp.product_b_name,
    pac.total_orders_with_a
HAVING COUNT(DISTINCT pp.sales_order_id) >= 2 -- At least 2 co-occurrences
ORDER BY co_purchase_count DESC, pct_when_a_purchased DESC
LIMIT 20;

-- =============================================================================
-- PART F: COMPARISON QUERY
-- =============================================================================

-- Products in both prescription and purchase patterns
-- This combines the previous two queries to show overlap
WITH tenant_id_val AS (
    SELECT id FROM public.tenants WHERE tenant_code = 'NT_XUANHOA'
),
valid_prescriptions AS (
    SELECT id, created_at
    FROM pack_pharmacy.electronic_prescriptions
    WHERE tenant_id = (SELECT id FROM tenant_id_val)
      AND status IN ('verified', 'signed', 'dispensed', 'partially_dispensed')
      AND cancelled_at IS NULL
),
prescription_pairs AS (
    SELECT 
        LEAST(pp1.product_id, pp2.product_id) as product_a_id,
        GREATEST(pp1.product_id, pp2.product_id) as product_b_id,
        pp1.prescription_id
    FROM (
        SELECT DISTINCT epl.prescription_id, epl.product_id
        FROM pack_pharmacy.electronic_prescription_lines epl
        INNER JOIN valid_prescriptions vp ON epl.prescription_id = vp.id
        INNER JOIN public.products p ON epl.product_id = p.id AND p.deleted_at IS NULL
    ) pp1
    INNER JOIN (
        SELECT DISTINCT epl.prescription_id, epl.product_id
        FROM pack_pharmacy.electronic_prescription_lines epl
        INNER JOIN valid_prescriptions vp ON epl.prescription_id = vp.id
        INNER JOIN public.products p ON epl.product_id = p.id AND p.deleted_at IS NULL
    ) pp2 ON pp1.prescription_id = pp2.prescription_id AND pp1.product_id < pp2.product_id
),
purchase_pairs AS (
    SELECT 
        LEAST(op1.product_id, op2.product_id) as product_a_id,
        GREATEST(op1.product_id, op2.product_id) as product_b_id,
        op1.sales_order_id
    FROM (
        SELECT DISTINCT soi.sales_order_id, soi.product_id
        FROM public.sales_order_items soi
        INNER JOIN public.sales_orders so ON soi.sales_order_id = so.id
        INNER JOIN public.products p ON soi.product_id = p.id AND p.deleted_at IS NULL
        WHERE so.tenant_id = (SELECT id FROM tenant_id_val)
          AND so.status = 2
    ) op1
    INNER JOIN (
        SELECT DISTINCT soi.sales_order_id, soi.product_id
        FROM public.sales_order_items soi
        INNER JOIN public.sales_orders so ON soi.sales_order_id = so.id
        INNER JOIN public.products p ON soi.product_id = p.id AND p.deleted_at IS NULL
        WHERE so.tenant_id = (SELECT id FROM tenant_id_val)
          AND so.status = 2
    ) op2 ON op1.sales_order_id = op2.sales_order_id AND op1.product_id < op2.product_id
),
rx_counts AS (
    SELECT product_a_id, product_b_id, COUNT(DISTINCT prescription_id) as rx_count
    FROM prescription_pairs
    GROUP BY product_a_id, product_b_id
    HAVING COUNT(DISTINCT prescription_id) >= 2
),
purchase_counts AS (
    SELECT product_a_id, product_b_id, COUNT(DISTINCT sales_order_id) as purchase_count
    FROM purchase_pairs
    GROUP BY product_a_id, product_b_id
    HAVING COUNT(DISTINCT sales_order_id) >= 2
)
SELECT 
    COALESCE(rc.product_a_id, pc.product_a_id) as product_a_id,
    pa.product_code as product_a_code,
    pa.product_name as product_a_name,
    COALESCE(rc.product_b_id, pc.product_b_id) as product_b_id,
    pb.product_code as product_b_code,
    pb.product_name as product_b_name,
    COALESCE(rc.rx_count, 0) as co_prescription_count,
    COALESCE(pc.purchase_count, 0) as co_purchase_count,
    CASE 
        WHEN rc.rx_count IS NOT NULL AND pc.purchase_count IS NOT NULL THEN 'BOTH'
        WHEN rc.rx_count IS NOT NULL THEN 'RX_ONLY'
        WHEN pc.purchase_count IS NOT NULL THEN 'PURCHASE_ONLY'
    END as pattern_type
FROM rx_counts rc
FULL OUTER JOIN purchase_counts pc 
    ON rc.product_a_id = pc.product_a_id 
    AND rc.product_b_id = pc.product_b_id
LEFT JOIN public.products pa ON COALESCE(rc.product_a_id, pc.product_a_id) = pa.id
LEFT JOIN public.products pb ON COALESCE(rc.product_b_id, pc.product_b_id) = pb.id
ORDER BY 
    GREATEST(COALESCE(rc.rx_count, 0), COALESCE(pc.purchase_count, 0)) DESC,
    pattern_type,
    product_a_name,
    product_b_name
LIMIT 30;
