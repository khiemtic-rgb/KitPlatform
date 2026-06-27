-- Ngưỡng tồn thấp theo kho (chi nhánh)
ALTER TABLE warehouses
    ADD COLUMN IF NOT EXISTS min_stock_qty NUMERIC(18, 3);

COMMENT ON COLUMN warehouses.min_stock_qty IS 'Ngưỡng tồn thấp riêng cho kho; NULL = kế thừa danh mục / tenant / fallback';
