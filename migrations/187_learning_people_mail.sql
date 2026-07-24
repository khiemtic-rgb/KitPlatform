-- KitPlatform 187: Hộp thư riêng QL ↔ NV (1–1), tuỳ chọn gắn sự kiện

CREATE TABLE IF NOT EXISTS pack_learning.mail_thread (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID         NOT NULL REFERENCES public.tenants(id),
    subject                 VARCHAR(200) NOT NULL,
    recipient_employee_id   UUID         NOT NULL REFERENCES public.employees(id),
    created_by_user_id      UUID         NOT NULL REFERENCES public.users(id),
    related_recognition_id  UUID         REFERENCES pack_learning.recognition(id) ON DELETE SET NULL,
    related_feedback_id     UUID         REFERENCES pack_learning.customer_sale_feedback(id) ON DELETE SET NULL,
    related_evaluation_id   UUID         REFERENCES pack_learning.evaluation(id) ON DELETE SET NULL,
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_mail_thread_recipient
    ON pack_learning.mail_thread (tenant_id, recipient_employee_id, updated_at DESC);

CREATE INDEX IF NOT EXISTS ix_mail_thread_creator
    ON pack_learning.mail_thread (tenant_id, created_by_user_id, updated_at DESC);

CREATE TABLE IF NOT EXISTS pack_learning.mail_message (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    thread_id       UUID         NOT NULL REFERENCES pack_learning.mail_thread(id) ON DELETE CASCADE,
    sender_user_id  UUID         NOT NULL REFERENCES public.users(id),
    body            TEXT         NOT NULL,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_mail_message_thread
    ON pack_learning.mail_message (thread_id, created_at ASC);

CREATE TABLE IF NOT EXISTS pack_learning.mail_read (
    tenant_id     UUID         NOT NULL REFERENCES public.tenants(id),
    thread_id     UUID         NOT NULL REFERENCES pack_learning.mail_thread(id) ON DELETE CASCADE,
    user_id       UUID         NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    last_read_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (thread_id, user_id)
);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.mail_thread TO kitplatform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.mail_message TO kitplatform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.mail_read TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.mail_thread TO pharmacore;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.mail_message TO pharmacore;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.mail_read TO pharmacore;
    END IF;
END $$;
