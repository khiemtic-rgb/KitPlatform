-- P11 / V2.0: Customer Engagement analytics — first_login_at + event log

ALTER TABLE customer_accounts
    ADD COLUMN IF NOT EXISTS first_login_at TIMESTAMPTZ;

UPDATE customer_accounts
SET first_login_at = last_login_at
WHERE first_login_at IS NULL
  AND last_login_at IS NOT NULL;

COMMENT ON COLUMN customer_accounts.first_login_at IS 'Lần đăng nhập app khách đầu tiên (OTP).';

CREATE TABLE IF NOT EXISTS customer_engagement_events (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    account_id      UUID         NOT NULL REFERENCES customer_accounts(id) ON DELETE CASCADE,
    customer_id     UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    event_type      VARCHAR(40)  NOT NULL,
    event_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    metadata        JSONB        NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT ck_customer_engagement_events_type CHECK (
        event_type IN ('ai_ask', 'app_open', 'push_enable')
    )
);

CREATE INDEX IF NOT EXISTS ix_customer_engagement_events_tenant_type_time
    ON customer_engagement_events (tenant_id, event_type, event_at DESC);

CREATE INDEX IF NOT EXISTS ix_customer_engagement_events_account_type_time
    ON customer_engagement_events (account_id, event_type, event_at DESC);

COMMENT ON TABLE customer_engagement_events IS 'Sự kiện hành vi app khách phục vụ Customer Engagement dashboard.';
