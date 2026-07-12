-- KitPlatform 121: Persist Connect Rx handoff on sales orders (clinic/doctor sales report)
-- Depends on: 006_sales.sql, 115_clinic_gd1_rx_handoff.sql

ALTER TABLE public.sales_orders
    ADD COLUMN IF NOT EXISTS connect_rx_handoff_id UUID;

COMMENT ON COLUMN public.sales_orders.connect_rx_handoff_id IS
    'Connect pack_connect.rx_handoffs.id when POS sale originated from a clinic Rx handoff.';

CREATE INDEX IF NOT EXISTS ix_sales_orders_connect_rx_handoff
    ON public.sales_orders (tenant_id, connect_rx_handoff_id)
    WHERE connect_rx_handoff_id IS NOT NULL;
