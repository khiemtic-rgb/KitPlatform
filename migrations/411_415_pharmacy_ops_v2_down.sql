-- Recovery for pharmacy ops v2. Not in any deploy manifest. Do not run on a live database.
-- Run with: psql -v ON_ERROR_STOP=1 -f 411_415_pharmacy_ops_v2_down.sql
-- The destructive work is one DO statement. If several checklists share a branch and day,
-- the statement raises before any drop, and PostgreSQL rolls that statement back.
-- A successful run removes v2 rows. Take a backup first. Apply only on an isolated database.

BEGIN;

DO $down$
DECLARE
    extra_runs integer;
BEGIN
    IF to_regclass('public.success_shift_checklist_run') IS NULL THEN
        RAISE EXCEPTION 'Không hoàn nguyên 412: thiếu success_shift_checklist_run. Không xóa đối tượng nào.';
    END IF;

    SELECT COUNT(*) INTO extra_runs
    FROM (
        SELECT 1
        FROM success_shift_checklist_run
        GROUP BY tenant_id, branch_id, kind, business_date
        HAVING COUNT(*) > 1
    ) duplicated;

    IF extra_runs > 0 THEN
        RAISE EXCEPTION 'Không hoàn nguyên 412: nhiều checklist cùng chi nhánh và ngày. Gộp các run theo ca trước khi chạy down.';
    END IF;

    IF to_regclass('public.pharmacy_knowledge_approval_event') IS NOT NULL THEN
        EXECUTE 'DROP TRIGGER IF EXISTS trg_pharmacy_knowledge_approval_event_immutable ON pharmacy_knowledge_approval_event';
        EXECUTE 'DROP TABLE IF EXISTS pharmacy_knowledge_approval_event';
    END IF;

    EXECUTE 'DROP FUNCTION IF EXISTS pharmacy_knowledge_approval_event_immutable()';
    EXECUTE 'DROP TABLE IF EXISTS pharmacy_safety_flag_review';

    EXECUTE 'ALTER TABLE pharmacy_knowledge_rule DROP CONSTRAINT IF EXISTS ck_pharmacy_knowledge_rule_approval';
    EXECUTE 'ALTER TABLE pharmacy_knowledge_rule
        DROP COLUMN IF EXISTS retired_at,
        DROP COLUMN IF EXISTS reference_source,
        DROP COLUMN IF EXISTS approved_at,
        DROP COLUMN IF EXISTS approved_by,
        DROP COLUMN IF EXISTS created_by,
        DROP COLUMN IF EXISTS approval_status';

    IF to_regclass('pack_learning.operational_task') IS NOT NULL THEN
        EXECUTE 'DROP TABLE IF EXISTS pack_learning.operational_task';
    END IF;

    EXECUTE 'DROP INDEX IF EXISTS ux_success_shift_checklist_run_shift_kind';
    EXECUTE 'DROP INDEX IF EXISTS ux_success_shift_checklist_run_day_legacy';
    EXECUTE 'ALTER TABLE success_shift_checklist_run DROP COLUMN IF EXISTS sales_shift_id';
    EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS ux_success_shift_checklist_run_day
        ON success_shift_checklist_run (tenant_id, branch_id, kind, business_date)';

    EXECUTE 'DROP INDEX IF EXISTS uq_sales_returns_tenant_client_return_id';
    EXECUTE 'ALTER TABLE sales_returns DROP COLUMN IF EXISTS client_return_id';
END
$down$;

COMMIT;
