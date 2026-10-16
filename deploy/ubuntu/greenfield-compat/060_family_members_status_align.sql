-- Execution substitute for migrations/060_family_members_status_align.sql.
-- This file is not a ledger filename. Do not add it to migration-files.prod.txt.
-- Production already recorded the historical filename, so the incremental runner skips it.
-- The historical file stays byte-for-byte. PostgreSQL parses is_active even when the
-- EXISTS guard is false, so the backfill is executed only when that column is present.

ALTER TABLE family_members
    ADD COLUMN IF NOT EXISTS status SMALLINT,
    ADD COLUMN IF NOT EXISTS gender SMALLINT;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'family_members'
          AND column_name = 'is_active'
    ) THEN
        EXECUTE $backfill$
            UPDATE family_members
            SET status = CASE
                    WHEN is_active = FALSE THEN 0
                    ELSE 1
                END
            WHERE status IS NULL
        $backfill$;
    END IF;
END $$;

UPDATE family_members
SET status = 1
WHERE status IS NULL;

ALTER TABLE family_members
    ALTER COLUMN status SET DEFAULT 1;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM family_members
        WHERE status IS NULL
    ) THEN
        RAISE NOTICE 'family_members.status still has NULL rows';
    ELSE
        ALTER TABLE family_members
            ALTER COLUMN status SET NOT NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_family_members_account_status
    ON family_members (tenant_id, account_id, status, created_at DESC);
