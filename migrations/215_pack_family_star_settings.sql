-- Family OS — per-family late star tier thresholds & award factors
-- Depends on: 214_pack_family_star_ledger.sql

CREATE TABLE IF NOT EXISTS pack_family.family_star_settings (
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    family_id               UUID PRIMARY KEY REFERENCES pack_family.family(id),
    late_t1_minutes         INT NOT NULL DEFAULT 30,
    late_t2_minutes         INT NOT NULL DEFAULT 60,
    late_t3_minutes         INT NOT NULL DEFAULT 90,
    late_half_pct           INT NOT NULL DEFAULT 50,
    late_zero_pct           INT NOT NULL DEFAULT 0,
    late_penalty_half_pct   INT NOT NULL DEFAULT -50,
    late_penalty_full_pct   INT NOT NULL DEFAULT -100,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT family_star_settings_t1_positive CHECK (late_t1_minutes > 0),
    CONSTRAINT family_star_settings_t2_gt_t1 CHECK (late_t2_minutes > late_t1_minutes),
    CONSTRAINT family_star_settings_t3_gt_t2 CHECK (late_t3_minutes > late_t2_minutes),
    CONSTRAINT family_star_settings_half_pct CHECK (late_half_pct BETWEEN 0 AND 100),
    CONSTRAINT family_star_settings_zero_pct CHECK (late_zero_pct = 0),
    CONSTRAINT family_star_settings_penalty_half CHECK (late_penalty_half_pct BETWEEN -100 AND -1),
    CONSTRAINT family_star_settings_penalty_full CHECK (late_penalty_full_pct = -100)
);

CREATE INDEX IF NOT EXISTS idx_family_star_settings_tenant
    ON pack_family.family_star_settings (tenant_id);

COMMENT ON TABLE pack_family.family_star_settings IS
    'Per-family late star tier thresholds (minutes after window_end) and award/penalty percentages of star_reward.';
COMMENT ON COLUMN pack_family.family_star_settings.late_t1_minutes IS
    'Late <= T1: award late_half_pct of star_reward (default 50%%).';
COMMENT ON COLUMN pack_family.family_star_settings.late_t2_minutes IS
    'T1 < late <= T2: award late_zero_pct (default 0).';
COMMENT ON COLUMN pack_family.family_star_settings.late_t3_minutes IS
    'T2 < late <= T3: apply late_penalty_half_pct (default -50%%). Late > T3: late_penalty_full_pct (-100%%).';
