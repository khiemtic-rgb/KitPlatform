-- KitPlatform 122: Patient + clinic provider profile fields (Clinic GĐ1)
-- BN: địa chỉ, CCCD, liên hệ khẩn, ghi chú lâm sàng BS: SĐT, email, học vị, ghi chú

ALTER TABLE public.customers
    ADD COLUMN IF NOT EXISTS address_line TEXT,
    ADD COLUMN IF NOT EXISTS id_number VARCHAR(30),
    ADD COLUMN IF NOT EXISTS emergency_contact_name VARCHAR(255),
    ADD COLUMN IF NOT EXISTS emergency_contact_phone VARCHAR(30),
    ADD COLUMN IF NOT EXISTS clinical_notes TEXT;

COMMENT ON COLUMN public.customers.address_line IS 'Địa chỉ liên hệ ngắn (Clinic / CRM).';
COMMENT ON COLUMN public.customers.id_number IS 'CCCD / giấy tờ định danh.';
COMMENT ON COLUMN public.customers.emergency_contact_name IS 'Người liên hệ khẩn cấp.';
COMMENT ON COLUMN public.customers.emergency_contact_phone IS 'SĐT người liên hệ khẩn cấp.';
COMMENT ON COLUMN public.customers.clinical_notes IS 'Ghi chú lâm sàng ngắn: dị ứng, bệnh nền…';

CREATE INDEX IF NOT EXISTS ix_customers_id_number
    ON public.customers (tenant_id, id_number)
    WHERE deleted_at IS NULL AND id_number IS NOT NULL AND BTRIM(id_number) <> '';

ALTER TABLE pack_clinic.clinic_provider
    ADD COLUMN IF NOT EXISTS phone VARCHAR(30),
    ADD COLUMN IF NOT EXISTS email VARCHAR(255),
    ADD COLUMN IF NOT EXISTS title VARCHAR(80),
    ADD COLUMN IF NOT EXISTS notes TEXT;

COMMENT ON COLUMN pack_clinic.clinic_provider.phone IS 'SĐT liên hệ bác sĩ tại PK.';
COMMENT ON COLUMN pack_clinic.clinic_provider.email IS 'Email bác sĩ (tuỳ chọn).';
COMMENT ON COLUMN pack_clinic.clinic_provider.title IS 'Học hàm/học vị: BS, ThS.BS, TS.BS…';
COMMENT ON COLUMN pack_clinic.clinic_provider.notes IS 'Ghi chú nội bộ về bác sĩ.';
