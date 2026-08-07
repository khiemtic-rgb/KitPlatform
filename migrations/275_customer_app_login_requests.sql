-- 275: Customer app login — counter PIN / store invite + pending remote approval

CREATE TABLE IF NOT EXISTS tenant_customer_app_auth (
    tenant_id           UUID PRIMARY KEY REFERENCES tenants(id) ON DELETE CASCADE,
    counter_pin_hash    VARCHAR(64),
    invite_code_hash    VARCHAR(64),
    invite_code_hint    VARCHAR(32),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by_user_id  UUID
);

COMMENT ON TABLE tenant_customer_app_auth IS
  'PIN quầy + mã mời app khách (hash SHA-256). Hint chỉ để nhân viên nhớ, không trả public.';

CREATE TABLE IF NOT EXISTS customer_app_login_requests (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            UUID         NOT NULL REFERENCES tenants(id),
    phone                VARCHAR(20)  NOT NULL,
    customer_id          UUID         REFERENCES customers(id),
    channel              VARCHAR(16)  NOT NULL,
    status               VARCHAR(24)  NOT NULL,
    referral_code_used   VARCHAR(32),
    otp_challenge_id     UUID         REFERENCES customer_otp_challenges(id),
    requested_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    reviewed_at          TIMESTAMPTZ,
    reviewed_by_user_id  UUID,
    reject_reason        VARCHAR(500),
    CONSTRAINT ck_customer_app_login_requests_channel
        CHECK (channel IN ('counter', 'remote')),
    CONSTRAINT ck_customer_app_login_requests_status
        CHECK (status IN ('pending', 'approved', 'rejected', 'consumed', 'expired'))
);

CREATE INDEX IF NOT EXISTS ix_customer_app_login_requests_pending
    ON customer_app_login_requests (tenant_id, status, requested_at DESC)
    WHERE status = 'pending';

CREATE INDEX IF NOT EXISTS ix_customer_app_login_requests_phone
    ON customer_app_login_requests (tenant_id, phone, requested_at DESC);

COMMENT ON TABLE customer_app_login_requests IS
  'Yêu cầu đăng nhập app từ xa (chờ duyệt) hoặc audit kênh quầy.';

-- Seed tạm cho NT_XUANHOA (đổi ngay trên Admin): PIN 1357, mã mời XUANHOA
INSERT INTO tenant_customer_app_auth (tenant_id, counter_pin_hash, invite_code_hash, invite_code_hint)
SELECT
    t.id,
    'F3E055913A0B1EB0F07317896F9A1BC466B9A50DB85A7F882F3FFDE9FFB23ACA',
    '2310112B486BA081083DFC4550E76EFB72F3D3ED52A1EE90A27ACFB68D54294D',
    'XUANHOA'
FROM tenants t
WHERE t.tenant_code = 'NT_XUANHOA'
  AND t.deleted_at IS NULL
ON CONFLICT (tenant_id) DO UPDATE
SET
    counter_pin_hash = EXCLUDED.counter_pin_hash,
    invite_code_hash = EXCLUDED.invite_code_hash,
    invite_code_hint = EXCLUDED.invite_code_hint,
    updated_at = NOW();
