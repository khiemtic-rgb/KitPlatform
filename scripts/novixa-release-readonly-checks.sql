-- Novixa release checks. READ ONLY: counts and query plans, no data change, no EXPLAIN ANALYZE.
-- Usage (on the VPS, as an operator):
--   sudo -u postgres psql -d novixa_prod -v tenant_code=NT_XUANHOA -f novixa-release-readonly-checks.sql
-- Default tenant_code is DEMO_PHARMACY. Output is aggregate numbers only.

\set ON_ERROR_STOP on
\if :{?tenant_code}
\else
\set tenant_code DEMO_PHARMACY
\endif

BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '1s';

SELECT id AS tid FROM tenants WHERE tenant_code = :'tenant_code' AND deleted_at IS NULL \gset
\echo 'tenant:' :tenant_code

\echo '--- 1. Data volume'
SELECT
    (SELECT COUNT(*) FROM products WHERE tenant_id = :'tid' AND deleted_at IS NULL) AS products,
    (SELECT COUNT(*) FROM inventory_batches WHERE tenant_id = :'tid') AS batches,
    (SELECT COUNT(*) FROM sales_orders WHERE tenant_id = :'tid') AS sales_orders,
    (SELECT COUNT(*) FROM sales_payments sp JOIN sales_orders o ON o.id = sp.sales_order_id WHERE o.tenant_id = :'tid') AS sales_payments,
    (SELECT COUNT(*) FROM stock_movements WHERE tenant_id = :'tid') AS stock_movements,
    (SELECT COUNT(*) FROM goods_receipts WHERE tenant_id = :'tid') AS goods_receipts;

\echo '--- 2. Report indexes from migration 431 (missing = reports scan line tables per order)'
SELECT want.name, (i.indexname IS NOT NULL) AS present
FROM (VALUES
    ('ix_sales_payments_order'), ('ix_sales_payments_paid_at'), ('ix_sales_order_items_order'),
    ('ix_sales_returns_order'), ('ix_sales_return_items_return'), ('ix_sales_return_items_order_item'),
    ('ix_goods_receipt_items_receipt'), ('ix_supplier_payments_receipt'), ('ix_supplier_payments_supplier')
) AS want(name)
LEFT JOIN pg_indexes i ON i.schemaname = 'public' AND i.indexname = want.name
ORDER BY 1;

\echo '--- 3. INV-01: receipt / PO lines entered in a non-base unit (stock was booked without conversion)'
SELECT 'goods_receipt_items' AS source, COUNT(*) AS lines, COUNT(DISTINCT gri.goods_receipt_id) AS documents,
       MIN(gr.created_at) AS first_at, MAX(gr.created_at) AS last_at
FROM goods_receipt_items gri
JOIN goods_receipts gr ON gr.id = gri.goods_receipt_id
JOIN product_units pu ON pu.id = gri.product_unit_id
WHERE gr.tenant_id = :'tid' AND pu.conversion_factor <> 1
UNION ALL
SELECT 'purchase_order_items', COUNT(*), COUNT(DISTINCT poi.purchase_order_id), NULL, NULL
FROM purchase_order_items poi
JOIN product_units pu ON pu.id = poi.product_unit_id
WHERE poi.tenant_id = :'tid' AND pu.conversion_factor <> 1;

\echo '--- 4. SAL-02: posted customer collections not linked to an order (old screen counted them twice)'
SELECT COUNT(*) AS unlinked_posted, COALESCE(SUM(amount), 0) AS amount, COUNT(DISTINCT customer_id) AS customers
FROM customer_payments
WHERE tenant_id = :'tid' AND status = 2 AND sales_order_id IS NULL AND deleted_at IS NULL;

\echo '--- 5. SAL-03: collections on orders that never belonged to a shift (not in any shift summary)'
SELECT COUNT(*) AS payments, COALESCE(SUM(sp.amount), 0) AS amount
FROM sales_payments sp
JOIN sales_orders o ON o.id = sp.sales_order_id
WHERE o.tenant_id = :'tid' AND o.sales_shift_id IS NULL AND sp.paid_at > o.order_date + interval '1 minute';

\echo '--- 6. Plan only (not executed): revenue-by-period, last 30 days. Look for Seq Scan on sales_payments inside a loop.'
EXPLAIN
SELECT date_trunc('day', timezone('Asia/Ho_Chi_Minh', o.order_date)), COUNT(*), SUM(COALESCE(ck.paid, 0))
FROM sales_orders o
LEFT JOIN LATERAL (
    SELECT SUM(sp.amount) AS paid FROM sales_payments sp WHERE sp.sales_order_id = o.id
) ck ON TRUE
WHERE o.tenant_id = :'tid' AND o.order_date >= now() - interval '30 days'
GROUP BY 1;

ROLLBACK;
