-- Checklist follows a POS sales shift, not one run per branch per day.
-- Not in migration-files.prod.txt.

ALTER TABLE success_shift_checklist_run
    ADD COLUMN IF NOT EXISTS sales_shift_id UUID REFERENCES sales_shifts(id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_success_shift_checklist_run_shift_kind
    ON success_shift_checklist_run (tenant_id, sales_shift_id, kind)
    WHERE sales_shift_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_success_shift_checklist_run_day_legacy
    ON success_shift_checklist_run (tenant_id, branch_id, kind, business_date)
    WHERE sales_shift_id IS NULL;

DROP INDEX IF EXISTS ux_success_shift_checklist_run_day;

COMMENT ON COLUMN success_shift_checklist_run.sales_shift_id IS
    'POS shift this open/close checklist belongs to. Item credit uses checked_by_user_id.';
