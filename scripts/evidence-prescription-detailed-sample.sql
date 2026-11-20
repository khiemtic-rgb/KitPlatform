-- DETAILED SAMPLE QUERIES FOR EVIDENCE
-- Read-only inspection of actual prescription and sales data

-- Get tenant ID
\set tenant_id '36cf73bc-9212-4f88-ae4c-6f0bfdb6ab3a'

\echo '============================================================'
\echo 'SAMPLE PRESCRIPTIONS WITH PRODUCTS'
\echo '============================================================'

SELECT 
    ep.prescription_code,
    ep.status,
    ep.created_at::DATE as date,
    c.full_name as customer,
    p.product_code,
    p.product_name,
    epl.qty_prescribed,
    epl.dosage_instruction
FROM pack_pharmacy.electronic_prescriptions ep
INNER JOIN pack_pharmacy.electronic_prescription_lines epl ON ep.id = epl.prescription_id
LEFT JOIN public.customers c ON ep.customer_id = c.id
INNER JOIN public.products p ON epl.product_id = p.id
WHERE ep.tenant_id = :'tenant_id'
  AND ep.cancelled_at IS NULL
ORDER BY ep.created_at, ep.prescription_code, epl.sort_order;

\echo ''
\echo '============================================================'
\echo 'PRESCRIPTIONS GROUPED BY PRESCRIPTION'
\echo '============================================================'

SELECT 
    ep.prescription_code,
    ep.status,
    ep.created_at::DATE as date,
    COUNT(*) as line_count,
    STRING_AGG(p.product_name, ' + ' ORDER BY epl.sort_order) as products
FROM pack_pharmacy.electronic_prescriptions ep
INNER JOIN pack_pharmacy.electronic_prescription_lines epl ON ep.id = epl.prescription_id
INNER JOIN public.products p ON epl.product_id = p.id
WHERE ep.tenant_id = :'tenant_id'
  AND ep.cancelled_at IS NULL
GROUP BY ep.id, ep.prescription_code, ep.status, ep.created_at
ORDER BY ep.created_at, ep.prescription_code;

\echo ''
\echo '============================================================'
\echo 'SAMPLE SALES ORDERS WITH PRODUCTS'
\echo '============================================================'

SELECT 
    so.order_number,
    so.order_date::DATE as date,
    so.status,
    c.full_name as customer,
    p.product_code,
    p.product_name,
    soi.quantity,
    soi.unit_price
FROM public.sales_orders so
INNER JOIN public.sales_order_items soi ON so.id = soi.sales_order_id
LEFT JOIN public.customers c ON so.customer_id = c.id
INNER JOIN public.products p ON soi.product_id = p.id
WHERE so.tenant_id = :'tenant_id'
  AND so.status = 2 -- Completed
ORDER BY so.order_date DESC, so.order_number
LIMIT 30;

\echo ''
\echo '============================================================'
\echo 'SALES ORDERS GROUPED BY ORDER'
\echo '============================================================'

SELECT 
    so.order_number,
    so.order_date::DATE as date,
    COUNT(*) as item_count,
    STRING_AGG(p.product_name, ' + ') as products
FROM public.sales_orders so
INNER JOIN public.sales_order_items soi ON so.id = soi.sales_order_id
INNER JOIN public.products p ON soi.product_id = p.id
WHERE so.tenant_id = :'tenant_id'
  AND so.status = 2 -- Completed
GROUP BY so.id, so.order_number, so.order_date
ORDER BY so.order_date DESC
LIMIT 20;

\echo ''
\echo '============================================================'
\echo 'PRODUCT FREQUENCY IN PRESCRIPTIONS'
\echo '============================================================'

SELECT 
    p.product_code,
    p.product_name,
    COUNT(DISTINCT epl.prescription_id) as prescription_count,
    SUM(epl.qty_prescribed) as total_qty_prescribed
FROM pack_pharmacy.electronic_prescription_lines epl
INNER JOIN pack_pharmacy.electronic_prescriptions ep ON epl.prescription_id = ep.id
INNER JOIN public.products p ON epl.product_id = p.id
WHERE ep.tenant_id = :'tenant_id'
  AND ep.cancelled_at IS NULL
  AND ep.status IN ('verified', 'signed', 'dispensed', 'partially_dispensed')
GROUP BY p.id, p.product_code, p.product_name
ORDER BY prescription_count DESC, p.product_name
LIMIT 20;

\echo ''
\echo '============================================================'
\echo 'PRODUCT FREQUENCY IN SALES'
\echo '============================================================'

SELECT 
    p.product_code,
    p.product_name,
    COUNT(DISTINCT soi.sales_order_id) as order_count,
    SUM(soi.quantity) as total_qty_sold
FROM public.sales_order_items soi
INNER JOIN public.sales_orders so ON soi.sales_order_id = so.id
INNER JOIN public.products p ON soi.product_id = p.id
WHERE so.tenant_id = :'tenant_id'
  AND so.status = 2 -- Completed
GROUP BY p.id, p.product_code, p.product_name
ORDER BY order_count DESC, p.product_name
LIMIT 20;

\echo ''
\echo '============================================================'
\echo 'DATA COMPLETENESS CHECK'
\echo '============================================================'

SELECT 
    'Prescriptions' as data_type,
    COUNT(*) as total,
    COUNT(*) FILTER (WHERE customer_id IS NOT NULL) as with_customer,
    COUNT(*) FILTER (WHERE linked_prescriber_id IS NOT NULL) as with_prescriber,
    COUNT(*) FILTER (WHERE patient_name IS NOT NULL AND patient_name != '') as with_patient_name,
    COUNT(*) FILTER (WHERE notes IS NOT NULL AND notes != '') as with_notes
FROM pack_pharmacy.electronic_prescriptions
WHERE tenant_id = :'tenant_id' AND cancelled_at IS NULL

UNION ALL

SELECT 
    'Sales Orders' as data_type,
    COUNT(*) as total,
    COUNT(*) FILTER (WHERE customer_id IS NOT NULL) as with_customer,
    COUNT(*) FILTER (WHERE employee_id IS NOT NULL) as with_employee,
    NULL as with_patient_name,
    COUNT(*) FILTER (WHERE notes IS NOT NULL AND notes != '') as with_notes
FROM public.sales_orders
WHERE tenant_id = :'tenant_id' AND status = 2;

\echo ''
\echo '============================================================'
\echo 'MULTI-ITEM PRESCRIPTIONS (if any)'
\echo '============================================================'

SELECT 
    ep.prescription_code,
    ep.created_at::DATE as date,
    COUNT(*) as line_count,
    STRING_AGG(
        p.product_name || ' (' || epl.qty_prescribed || ')', 
        '; ' 
        ORDER BY epl.sort_order
    ) as prescription_detail
FROM pack_pharmacy.electronic_prescriptions ep
INNER JOIN pack_pharmacy.electronic_prescription_lines epl ON ep.id = epl.prescription_id
INNER JOIN public.products p ON epl.product_id = p.id
WHERE ep.tenant_id = :'tenant_id'
  AND ep.cancelled_at IS NULL
GROUP BY ep.id, ep.prescription_code, ep.created_at
HAVING COUNT(*) >= 2
ORDER BY line_count DESC, ep.created_at;

\echo ''
\echo '============================================================'
\echo 'MULTI-ITEM SALES ORDERS (if any)'
\echo '============================================================'

SELECT 
    so.order_number,
    so.order_date::DATE as date,
    COUNT(*) as item_count,
    STRING_AGG(
        p.product_name || ' (' || soi.quantity || ')', 
        '; '
    ) as order_detail
FROM public.sales_orders so
INNER JOIN public.sales_order_items soi ON so.id = soi.sales_order_id
INNER JOIN public.products p ON soi.product_id = p.id
WHERE so.tenant_id = :'tenant_id'
  AND so.status = 2
GROUP BY so.id, so.order_number, so.order_date
HAVING COUNT(*) >= 2
ORDER BY item_count DESC, so.order_date DESC;
