-- KitPlatform 092: QĐ 540/QĐ-QLD — integration schema (Bảng 1 export foundation)
-- Depends on: 090_kap_pharmacy_intelligence_seed.sql (or prior pharmacy schema)

ALTER TABLE branches
    ADD COLUMN IF NOT EXISTS retail_facility_code VARCHAR(12);

COMMENT ON COLUMN branches.retail_facility_code IS
    'Ma_co_so_ban_le — mã cơ sở bán lẻ do Cục QLD cấp (QĐ 540).';

ALTER TABLE suppliers
    ADD COLUMN IF NOT EXISTS wholesale_facility_code VARCHAR(12);

COMMENT ON COLUMN suppliers.wholesale_facility_code IS
    'Ma_co_so_ban_buon — mã cơ sở bán buôn NCC do Cục QLD cấp (QĐ 540).';

ALTER TABLE goods_receipts
    ADD COLUMN IF NOT EXISTS supplier_invoice_number VARCHAR(20);

COMMENT ON COLUMN goods_receipts.supplier_invoice_number IS
    'so_hoa_don_mthuoc — số hóa đơn GTGT mua thuốc (QĐ 540).';

ALTER TABLE products
    ADD COLUMN IF NOT EXISTS dosage_form VARCHAR(20),
    ADD COLUMN IF NOT EXISTS packaging VARCHAR(20),
    ADD COLUMN IF NOT EXISTS importer_name VARCHAR(100);

COMMENT ON COLUMN products.dosage_form IS 'dang_bao_che (QĐ 540 Bảng 1).';
COMMENT ON COLUMN products.packaging IS 'quy_cach_dong_goi (QĐ 540 Bảng 1).';
COMMENT ON COLUMN products.importer_name IS 'nha_nhap_khau (QĐ 540 Bảng 1).';

CREATE TABLE IF NOT EXISTS qd540_export_log (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    branch_id       UUID         REFERENCES branches(id),
    exported_from   TIMESTAMPTZ  NOT NULL,
    exported_to     TIMESTAMPTZ  NOT NULL,
    row_count       INT          NOT NULL DEFAULT 0,
    payload_hash    VARCHAR(64),
    status          VARCHAR(20)  NOT NULL DEFAULT 'success',
    error_message   TEXT,
    created_by      UUID         REFERENCES users(id),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_qd540_export_log_tenant
    ON qd540_export_log (tenant_id, created_at DESC);

COMMENT ON TABLE qd540_export_log IS
    'Audit log for QĐ 540 Bảng 1 exports (internal / pre-connector).';
