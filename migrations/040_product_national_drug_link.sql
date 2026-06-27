-- Liên kết sản phẩm nội bộ với bản ghi CSDL Dược QG (QĐ 522)

ALTER TABLE products
    ADD COLUMN IF NOT EXISTS national_drug_id VARCHAR(50),
    ADD COLUMN IF NOT EXISTS national_registration_number VARCHAR(100);

CREATE INDEX IF NOT EXISTS ix_products_national_drug
    ON products (tenant_id, national_drug_id)
    WHERE national_drug_id IS NOT NULL AND deleted_at IS NULL;

COMMENT ON COLUMN products.national_drug_id IS 'Mã thuốc trên CSDL Dược QG (maThuoc)';
COMMENT ON COLUMN products.national_registration_number IS 'Số đăng ký lưu hành (soDangKy)';
