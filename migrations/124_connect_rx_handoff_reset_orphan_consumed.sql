-- KitPlatform 124: Reset Connect Rx handoffs wrongly marked consumed by status-event ack
-- Depends on: 121_sales_connect_rx_handoff.sql
--
-- Bug: ConnectStatusEventService.ConsumeAsync previously cascaded handoff_status='consumed'
-- when pharmacy only clicked «Đã nhận tín hiệu». Those handoffs have no sales_orders.link.
-- Re-open them so POS can sell and SALES-05 can attribute revenue.

UPDATE pack_connect.rx_handoffs h
SET
    handoff_status = 'pending_pharmacy',
    consumed_at = NULL,
    consumed_by = NULL,
    updated_at = NOW()
WHERE h.handoff_status = 'consumed'
  AND h.dismissed_at IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.sales_orders o
      WHERE o.connect_rx_handoff_id = h.id
  );
