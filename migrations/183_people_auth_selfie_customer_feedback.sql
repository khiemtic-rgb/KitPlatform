-- KitPlatform 183: Force đổi mật khẩu lần đầu + selfieie cam kết L0 + phản hồi khách sau bán

-- 1) Auth: bắt đổi mật khẩu khi admin tạo / đặt lại mật khẩu
ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS must_change_password BOOLEAN NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN public.users.must_change_password IS
    'TRUE khi admin tạo/đặt lại mật khẩu — NV phải đổi trước khi dùng hệ thống.';

-- OTP xác nhận danh tính khi đổi mật khẩu lần đầu (nếu có SĐT nhân viên)
CREATE TABLE IF NOT EXISTS public.staff_otp_challenges (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    user_id         UUID         NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    phone           VARCHAR(30)  NOT NULL,
    code_hash       VARCHAR(64)  NOT NULL,
    expires_at      TIMESTAMPTZ  NOT NULL,
    consumed_at     TIMESTAMPTZ,
    attempt_count   INT          NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_staff_otp_user_open
    ON public.staff_otp_challenges (tenant_id, user_id, created_at DESC)
    WHERE consumed_at IS NULL;

-- 2) Learning: selfie tuỳ chọn khi ký cam kết (L0)
ALTER TABLE pack_learning.module_progress
    ADD COLUMN IF NOT EXISTS acknowledge_selfie_url VARCHAR(500);

COMMENT ON COLUMN pack_learning.module_progress.acknowledge_selfie_url IS
    'URL ảnh selfie tuỳ chọn lúc ký cam kết (khuyến nghị L0).';

-- 3) Customer: phản hồi sau bán → People / ghi nhận
CREATE TABLE IF NOT EXISTS pack_learning.customer_sale_feedback (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID         NOT NULL REFERENCES public.tenants(id),
    sales_order_id    UUID         NOT NULL REFERENCES public.sales_orders(id),
    customer_id       UUID         NOT NULL REFERENCES public.customers(id),
    employee_id       UUID         REFERENCES public.employees(id),
    rating            SMALLINT     NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment           TEXT,
    recognition_id    UUID         REFERENCES pack_learning.recognition(id) ON DELETE SET NULL,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_customer_sale_feedback_order UNIQUE (sales_order_id, customer_id)
);

CREATE INDEX IF NOT EXISTS ix_customer_sale_feedback_tenant_created
    ON pack_learning.customer_sale_feedback (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_customer_sale_feedback_employee
    ON pack_learning.customer_sale_feedback (tenant_id, employee_id, created_at DESC)
    WHERE employee_id IS NOT NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.staff_otp_challenges TO kitplatform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.customer_sale_feedback TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.staff_otp_challenges TO pharmacore;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.customer_sale_feedback TO pharmacore;
    END IF;
END $$;
