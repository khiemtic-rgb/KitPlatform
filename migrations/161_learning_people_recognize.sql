-- KitPlatform 161: People Recognize — feed ghi nhận + badge nội bộ (local P3)

CREATE TABLE IF NOT EXISTS pack_learning.recognition (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id         UUID         NOT NULL REFERENCES public.employees(id),
    kind                VARCHAR(40)  NOT NULL,
    title               VARCHAR(200) NOT NULL,
    body                TEXT,
    badge_code          VARCHAR(80),
    created_by_user_id  UUID REFERENCES public.users(id),
    is_public           BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_learning_recognition_kind CHECK (
        kind IN (
            'course_complete',
            'module_complete',
            'birthday',
            'work_anniversary',
            'customer_praise',
            'custom',
            'badge_award'
        )
    )
);

CREATE INDEX IF NOT EXISTS ix_learning_recognition_tenant_created
    ON pack_learning.recognition (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_learning_recognition_employee
    ON pack_learning.recognition (tenant_id, employee_id, created_at DESC);

CREATE TABLE IF NOT EXISTS pack_learning.badge (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id     UUID         NOT NULL REFERENCES public.employees(id),
    badge_code      VARCHAR(80)  NOT NULL,
    title           VARCHAR(200) NOT NULL,
    source_recognition_id UUID REFERENCES pack_learning.recognition(id) ON DELETE SET NULL,
    earned_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_badge UNIQUE (tenant_id, employee_id, badge_code)
);

CREATE INDEX IF NOT EXISTS ix_learning_badge_employee
    ON pack_learning.badge (tenant_id, employee_id);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.recognition TO kitplatform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.badge TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.recognition TO pharmacore;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.badge TO pharmacore;
    END IF;
END
$$;
