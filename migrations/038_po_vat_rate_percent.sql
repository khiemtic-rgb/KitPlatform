-- PO VAT: lưu mức thuế chuẩn (0/5/8/10%), không nhập số tiền thuế tùy ý
ALTER TABLE purchase_orders
    ADD COLUMN IF NOT EXISTS tax_rate_percent SMALLINT NOT NULL DEFAULT 0;

ALTER TABLE purchase_orders
    DROP CONSTRAINT IF EXISTS ck_purchase_orders_tax_rate_percent;

ALTER TABLE purchase_orders
    ADD CONSTRAINT ck_purchase_orders_tax_rate_percent
    CHECK (tax_rate_percent IN (0, 5, 8, 10));

UPDATE purchase_orders
SET tax_rate_percent = CASE
    WHEN subtotal <= 0 OR tax_amount <= 0 THEN 0
    WHEN ABS(tax_amount - ROUND(subtotal * 0.05, 2)) < 0.01 THEN 5
    WHEN ABS(tax_amount - ROUND(subtotal * 0.08, 2)) < 0.01 THEN 8
    WHEN ABS(tax_amount - ROUND(subtotal * 0.10, 2)) < 0.01 THEN 10
    ELSE 0
END
WHERE tax_rate_percent = 0 AND tax_amount > 0;

UPDATE purchase_orders
SET tax_amount = ROUND(subtotal * tax_rate_percent / 100.0, 2),
    total_amount = subtotal + ROUND(subtotal * tax_rate_percent / 100.0, 2)
WHERE tax_rate_percent > 0;
