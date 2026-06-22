-- Pha 1: batch resolution extensibility — ghi nguồn gán lô trên dòng đơn bán
ALTER TABLE sales_order_items
    ADD COLUMN IF NOT EXISTS batch_source SMALLINT NULL;

COMMENT ON COLUMN sales_order_items.batch_source IS '1=fefo_auto, 2=manual, 3=label_scan';

-- Tenant mặc định gợi ý lô (Pha 1) nếu chưa cấu hình
UPDATE tenants
SET settings = settings || '{"batch_mode": "suggest"}'::jsonb
WHERE settings->>'batch_mode' IS NULL;
