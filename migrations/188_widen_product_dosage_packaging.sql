-- Widen product dosage_form / packaging — national catalog values often exceed 20 chars
-- (e.g. "Viên nang cứng tan trong ruột"). QĐ 540 export still truncates to 20.

ALTER TABLE products
    ALTER COLUMN dosage_form TYPE VARCHAR(100),
    ALTER COLUMN packaging TYPE VARCHAR(100);

COMMENT ON COLUMN products.dosage_form IS 'dang_bao_che (QĐ 540 Bảng 1) — lưu đầy đủ; export truncate 20.';
COMMENT ON COLUMN products.packaging IS 'quy_cach_dong_goi (QĐ 540 Bảng 1) — lưu đầy đủ; export truncate 20.';
