-- Ngưỡng tồn thấp theo danh mục SP

ALTER TABLE product_categories
    ADD COLUMN IF NOT EXISTS min_stock_qty NUMERIC(18,3);
