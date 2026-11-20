-- KitPlatform 435: Learning — lịch sử quan sát tại quầy (đạt + chưa đạt).
-- Append-only. pack_learning.module_observation vẫn giữ lần ĐẠT gần nhất (bậc nghề / tiến độ đọc bảng đó).
-- Idempotent; không sửa / backfill dữ liệu cũ. Not in migration-files.prod.txt.

CREATE TABLE IF NOT EXISTS pack_learning.module_observation_history (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             UUID         NOT NULL REFERENCES public.tenants(id),
    enrollment_id         UUID         NOT NULL REFERENCES pack_learning.enrollment(id) ON DELETE CASCADE,
    module_id             UUID         NOT NULL REFERENCES pack_learning.module(id),
    employee_id           UUID         NOT NULL REFERENCES public.employees(id),
    observed_by_user_id   UUID         NOT NULL REFERENCES public.users(id),
    criteria_json         JSONB        NOT NULL DEFAULT '{}'::jsonb,
    note                  TEXT,
    passed                BOOLEAN      NOT NULL,
    score_pct             INT          NOT NULL CHECK (score_pct BETWEEN 0 AND 100),
    observed_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_learning_observation_history_employee_module
    ON pack_learning.module_observation_history (tenant_id, employee_id, module_id, observed_at DESC);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT ON pack_learning.module_observation_history TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT ON pack_learning.module_observation_history TO pharmacore;
    END IF;
END $$;
