-- Kit Payment: runtime-configurable trial length per plan (Admin → Family OS → Billing)
ALTER TABLE payment.plan
    ADD COLUMN IF NOT EXISTS trial_days INT NOT NULL DEFAULT 30;

ALTER TABLE payment.plan
    DROP CONSTRAINT IF EXISTS ck_payment_plan_trial_days;
ALTER TABLE payment.plan
    ADD CONSTRAINT ck_payment_plan_trial_days CHECK (trial_days >= 0 AND trial_days <= 365);

COMMENT ON COLUMN payment.plan.trial_days IS
    'Self-serve trial length (days) applied at product registration; 0 = no trial.';
