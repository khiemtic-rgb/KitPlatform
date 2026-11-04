-- Staff Enablement: job title on employees (e.g. "Dược sĩ", "Thu ngân").
-- Not in migration-files.prod.txt. Code tolerates the column missing on older databases.
-- Idempotent.

ALTER TABLE employees
    ADD COLUMN IF NOT EXISTS job_title TEXT;

COMMENT ON COLUMN employees.job_title IS
    'Free-text job title shown in user/employee admin. Optional.';
