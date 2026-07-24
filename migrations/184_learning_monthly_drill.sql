-- KitPlatform 184: Ôn nhanh hàng tháng (soft — không khóa POS)

CREATE TABLE IF NOT EXISTS pack_learning.monthly_drill (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id     UUID         NOT NULL REFERENCES public.employees(id),
    period_year     INT          NOT NULL,
    period_month    INT          NOT NULL CHECK (period_month BETWEEN 1 AND 12),
    score_pct       INT          NOT NULL CHECK (score_pct BETWEEN 0 AND 100),
    question_count  INT          NOT NULL DEFAULT 0,
    completed_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_monthly_drill_period UNIQUE (tenant_id, employee_id, period_year, period_month)
);

CREATE INDEX IF NOT EXISTS ix_monthly_drill_tenant_period
    ON pack_learning.monthly_drill (tenant_id, period_year DESC, period_month DESC);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.monthly_drill TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.monthly_drill TO pharmacore;
    END IF;
END $$;
