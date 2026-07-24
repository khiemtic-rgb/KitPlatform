-- KitPlatform 162: People Grow — career ladder + promotion audit

CREATE TABLE IF NOT EXISTS pack_learning.career_level (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID         REFERENCES public.tenants(id),
    code                    VARCHAR(40)  NOT NULL,
    title                   VARCHAR(120) NOT NULL,
    summary                 TEXT,
    sort_order              INT          NOT NULL DEFAULT 0,
    min_months_tenure       INT          NOT NULL DEFAULT 0,
    min_avg_evaluate        INT          NOT NULL DEFAULT 0,
    required_competency_codes TEXT[]     NOT NULL DEFAULT '{}',
    is_active               BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_career_level UNIQUE (tenant_id, code),
    CONSTRAINT ck_learning_career_months CHECK (min_months_tenure >= 0),
    CONSTRAINT ck_learning_career_eval CHECK (min_avg_evaluate BETWEEN 0 AND 100)
);

CREATE INDEX IF NOT EXISTS ix_learning_career_level_tenant
    ON pack_learning.career_level (tenant_id, sort_order);

CREATE TABLE IF NOT EXISTS pack_learning.employee_career (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id         UUID         NOT NULL REFERENCES public.employees(id),
    level_id            UUID         NOT NULL REFERENCES pack_learning.career_level(id),
    assigned_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    assigned_by_user_id UUID REFERENCES public.users(id),
    note                TEXT,
    CONSTRAINT uq_learning_employee_career UNIQUE (tenant_id, employee_id)
);

CREATE INDEX IF NOT EXISTS ix_learning_employee_career_level
    ON pack_learning.employee_career (tenant_id, level_id);

CREATE TABLE IF NOT EXISTS pack_learning.career_promotion (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id         UUID         NOT NULL REFERENCES public.employees(id),
    from_level_id       UUID REFERENCES pack_learning.career_level(id),
    to_level_id         UUID         NOT NULL REFERENCES pack_learning.career_level(id),
    status              VARCHAR(20)  NOT NULL DEFAULT 'approved',
    eligibility_ok      BOOLEAN      NOT NULL DEFAULT FALSE,
    missing_reasons     TEXT[]       NOT NULL DEFAULT '{}',
    comment             TEXT,
    decided_by_user_id  UUID REFERENCES public.users(id),
    decided_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_learning_career_promo_status CHECK (
        status IN ('approved', 'rejected', 'pending')
    )
);

CREATE INDEX IF NOT EXISTS ix_learning_career_promo_employee
    ON pack_learning.career_promotion (tenant_id, employee_id, decided_at DESC);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.career_level TO kitplatform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.employee_career TO kitplatform;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.career_promotion TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.career_level TO pharmacore;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.employee_career TO pharmacore;
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.career_promotion TO pharmacore;
    END IF;
END
$$;

-- Seed default ladder for pharmacy tenants that already have learning enabled
INSERT INTO pack_learning.career_level (
    tenant_id, code, title, summary, sort_order,
    min_months_tenure, min_avg_evaluate, required_competency_codes
)
SELECT t.id, v.code, v.title, v.summary, v.sort_order,
       v.min_months, v.min_eval, v.comps
FROM public.tenants t
CROSS JOIN (
    VALUES
        ('newbie', 'Nhân viên mới', 'Onboarding — học L0–L1', 10, 0, 0,
         ARRAY[]::text[]),
        ('staff', 'Nhân viên quầy', 'POS cơ bản ổn định', 20, 1, 70,
         ARRAY['pos_basic', 'tone_of_service']),
        ('senior', 'Nhân viên chính', 'Kho + ca + tư vấn biên', 30, 6, 75,
         ARRAY['pos_basic', 'grn_receive', 'customer_lookup']),
        ('lead', 'Quản lý ca', 'Đóng ca / lệch quỹ / escalate', 40, 12, 80,
         ARRAY['shift_close', 'cash_variance', 'multi_skill_pass'])
) AS v(code, title, summary, sort_order, min_months, min_eval, comps)
WHERE t.deleted_at IS NULL
  AND t.tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY')
ON CONFLICT (tenant_id, code) DO UPDATE SET
    title = EXCLUDED.title,
    summary = EXCLUDED.summary,
    sort_order = EXCLUDED.sort_order,
    min_months_tenure = EXCLUDED.min_months_tenure,
    min_avg_evaluate = EXCLUDED.min_avg_evaluate,
    required_competency_codes = EXCLUDED.required_competency_codes,
    is_active = TRUE;

-- Default current level = newbie when missing
INSERT INTO pack_learning.employee_career (tenant_id, employee_id, level_id, note)
SELECT e.tenant_id, e.id, lv.id, 'Seed mặc định bậc newbie'
FROM public.employees e
INNER JOIN pack_learning.career_level lv
    ON lv.tenant_id = e.tenant_id AND lv.code = 'newbie' AND lv.is_active
WHERE e.deleted_at IS NULL
  AND e.tenant_id IN (SELECT id FROM public.tenants WHERE tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY') AND deleted_at IS NULL)
ON CONFLICT (tenant_id, employee_id) DO NOTHING;
