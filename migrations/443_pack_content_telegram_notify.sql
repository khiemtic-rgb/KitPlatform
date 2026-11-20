-- KitPlatform 443: Telegram notify for Content Park.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Precondition: pack_content.brand, org_settings (281).
-- Không seed tin cũ. Cursor = NOW() để không bắn lịch sử.
-- digest: tin không phải lỗi rơi vào giờ im lặng, gom thành một tin tóm tắt lúc hết giờ im.
-- Chạy lại an toàn.

BEGIN;

ALTER TABLE pack_content.org_settings
    ADD COLUMN IF NOT EXISTS telegram_config_json JSONB NOT NULL DEFAULT '{}'::jsonb;

CREATE TABLE IF NOT EXISTS pack_content.telegram_brand_setting (
    brand_id              UUID PRIMARY KEY REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    enabled               BOOLEAN NOT NULL DEFAULT FALSE,
    chat_id               TEXT,
    notify_success        BOOLEAN NOT NULL DEFAULT TRUE,
    notify_low_stock      BOOLEAN NOT NULL DEFAULT TRUE,
    notify_failure        BOOLEAN NOT NULL DEFAULT TRUE,
    notify_proposal       BOOLEAN NOT NULL DEFAULT TRUE,
    low_stock_remaining   INT NOT NULL DEFAULT 3,
    low_stock_empty_days  INT NOT NULL DEFAULT 2,
    quiet_from_hour       INT NOT NULL DEFAULT 22,
    quiet_to_hour         INT NOT NULL DEFAULT 7,
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_telegram_low_remaining CHECK (low_stock_remaining BETWEEN 1 AND 50),
    CONSTRAINT ck_content_telegram_low_days CHECK (low_stock_empty_days BETWEEN 1 AND 30),
    CONSTRAINT ck_content_telegram_quiet_from CHECK (quiet_from_hour BETWEEN 0 AND 23),
    CONSTRAINT ck_content_telegram_quiet_to CHECK (quiet_to_hour BETWEEN 0 AND 23)
);

CREATE TABLE IF NOT EXISTS pack_content.telegram_outbox (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id         UUID REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    kind             VARCHAR(32) NOT NULL,
    dedupe_key       VARCHAR(200) NOT NULL,
    error_class      VARCHAR(80),
    chat_id          TEXT NOT NULL,
    html             TEXT NOT NULL,
    status           VARCHAR(16) NOT NULL DEFAULT 'Pending',
    attempts         INT NOT NULL DEFAULT 0,
    next_attempt_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_error       TEXT,
    sent_at          TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_telegram_outbox_dedupe UNIQUE (dedupe_key),
    CONSTRAINT ck_content_telegram_outbox_kind CHECK (kind IN ('success', 'failure', 'low_stock', 'proposal')),
    CONSTRAINT ck_content_telegram_outbox_status CHECK (status IN ('Pending', 'Sent', 'Failed', 'Suppressed'))
);

ALTER TABLE pack_content.telegram_outbox
    ADD COLUMN IF NOT EXISTS digest BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS ix_content_telegram_outbox_due
    ON pack_content.telegram_outbox (status, next_attempt_at);

CREATE TABLE IF NOT EXISTS pack_content.telegram_cursor (
    name        TEXT PRIMARY KEY,
    watermark   TIMESTAMPTZ NOT NULL
);

INSERT INTO pack_content.telegram_cursor (name, watermark)
VALUES ('publish_job', NOW()), ('autopilot_run', NOW()), ('schedule_proposal', NOW())
ON CONFLICT (name) DO NOTHING;

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.telegram_brand_setting TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.telegram_outbox TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.telegram_cursor TO %I', r);
    END IF;
  END LOOP;
END $$;

COMMIT;
