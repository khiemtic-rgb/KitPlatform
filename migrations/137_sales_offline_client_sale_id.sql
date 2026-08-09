-- Offline POS V1: idempotency key for client-queued sales (1 device pilot).
ALTER TABLE public.sales_orders
    ADD COLUMN IF NOT EXISTS client_sale_id VARCHAR(64);

COMMENT ON COLUMN public.sales_orders.client_sale_id IS
    'Offline POS client-generated sale id; unique per tenant for sync idempotency.';

CREATE UNIQUE INDEX IF NOT EXISTS uq_sales_orders_tenant_client_sale_id
    ON public.sales_orders (tenant_id, client_sale_id)
    WHERE client_sale_id IS NOT NULL;
