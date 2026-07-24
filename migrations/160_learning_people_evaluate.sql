-- KitPlatform 160: People Evaluate — thin monthly review (5 criteria)

CREATE TABLE IF NOT EXISTS pack_learning.evaluation (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id     UUID         NOT NULL REFERENCES public.employees(id),
    period_year     INT          NOT NULL,
    period_month    INT          NOT NULL,
    score_knowledge INT          NOT NULL DEFAULT 0,
    score_attitude  INT          NOT NULL DEFAULT 0,
    score_care      INT          NOT NULL DEFAULT 0,
    score_stock     INT          NOT NULL DEFAULT 0,
    score_discipline INT         NOT NULL DEFAULT 0,
    comment         TEXT,
    reviewed_by_user_id UUID REFERENCES public.users(id),
    reviewed_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_evaluation_period UNIQUE (tenant_id, employee_id, period_year, period_month),
    CONSTRAINT ck_learning_eval_month CHECK (period_month BETWEEN 1 AND 12),
    CONSTRAINT ck_learning_eval_scores CHECK (
        score_knowledge BETWEEN 0 AND 100
        AND score_attitude BETWEEN 0 AND 100
        AND score_care BETWEEN 0 AND 100
        AND score_stock BETWEEN 0 AND 100
        AND score_discipline BETWEEN 0 AND 100
    )
);

CREATE INDEX IF NOT EXISTS ix_learning_evaluation_employee
    ON pack_learning.evaluation (tenant_id, employee_id, period_year DESC, period_month DESC);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.evaluation TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.evaluation TO pharmacore;
    END IF;
END
$$;
