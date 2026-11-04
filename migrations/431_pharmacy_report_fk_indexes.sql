-- Foreign-key indexes for sales, returns and goods-receipt line tables.
-- Without them, sales reports scan sales_payments / sales_return_items once per order.
-- Index only: no data change. Run outside a transaction (CONCURRENTLY); psql -f autocommits each line.
-- If a CONCURRENTLY build fails it leaves an INVALID index: drop it, then re-run this file.

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sales_payments_order
    ON public.sales_payments (sales_order_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sales_payments_paid_at
    ON public.sales_payments (paid_at);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sales_order_items_order
    ON public.sales_order_items (sales_order_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sales_returns_order
    ON public.sales_returns (sales_order_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sales_return_items_return
    ON public.sales_return_items (sales_return_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_sales_return_items_order_item
    ON public.sales_return_items (sales_order_item_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_goods_receipt_items_receipt
    ON public.goods_receipt_items (goods_receipt_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_supplier_payments_receipt
    ON public.supplier_payments (tenant_id, goods_receipt_id)
    WHERE goods_receipt_id IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_supplier_payments_supplier
    ON public.supplier_payments (tenant_id, supplier_id);

ANALYZE public.sales_payments;
ANALYZE public.sales_order_items;
ANALYZE public.sales_returns;
ANALYZE public.sales_return_items;
ANALYZE public.goods_receipt_items;
ANALYZE public.supplier_payments;
