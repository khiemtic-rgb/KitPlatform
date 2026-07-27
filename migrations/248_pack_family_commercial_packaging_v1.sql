-- KitPlatform 248: Family OS Commercial packaging v1 — Free / Plus / Pro / AI+
-- Layer: Pack:FamilyOS + payment.plan catalog
-- Depends on: 224_kit_payment_platform.sql, 225_payment_plan_trial_days.sql
-- Local / Family OS park: family-os migration manifest only.

-- Allow Free catalog row (0đ) for ops listing; checkout still rejects free.
ALTER TABLE payment.plan
    DROP CONSTRAINT IF EXISTS ck_payment_plan_amount;
ALTER TABLE payment.plan
    ADD CONSTRAINT ck_payment_plan_amount CHECK (amount_vnd >= 0);

-- Rename legacy Starter → Plus (same SKU code for checkout compat)
UPDATE payment.plan
SET display_name = 'Famixa Plus (tháng)',
    amount_vnd = 99000,
    interval_days = 30,
    is_active = TRUE,
    updated_at = NOW()
WHERE product_code = 'family_os' AND plan_code = 'starter_month';

INSERT INTO payment.plan (product_code, plan_code, display_name, amount_vnd, interval_days, trial_days, is_active)
VALUES
    ('family_os', 'plus_month', 'Famixa Plus (tháng)', 99000, 30, 30, TRUE),
    ('family_os', 'family_pro_month', 'Family Peace Plan · Pro (tháng)', 199000, 30, 30, TRUE),
    ('family_os', 'family_ai_plus_month', 'Family AI+ (tháng)', 399000, 30, 30, TRUE),
    ('family_os', 'plus_year', 'Famixa Plus (năm)', 990000, 365, 30, TRUE),
    ('family_os', 'family_pro_year', 'Family Peace Plan · Pro (năm)', 1990000, 365, 30, TRUE),
    ('family_os', 'family_ai_plus_year', 'Family AI+ (năm)', 3990000, 365, 30, TRUE),
    ('family_os', 'free', 'Famixa Free', 0, 30, 0, TRUE)
ON CONFLICT (product_code, plan_code) DO UPDATE
SET display_name = EXCLUDED.display_name,
    amount_vnd = EXCLUDED.amount_vnd,
    interval_days = EXCLUDED.interval_days,
    trial_days = EXCLUDED.trial_days,
    is_active = TRUE,
    updated_at = NOW();

COMMENT ON TABLE payment.plan IS
    'Sellable SaaS plans — Famixa packaging v1: free / plus / family_pro / family_ai_plus (+ legacy starter_month=Plus).';
