-- Sales V2: đơn nháp — dòng bán chưa allocate lô (batch gán khi complete)
ALTER TABLE sales_order_items
    ALTER COLUMN batch_id DROP NOT NULL;
