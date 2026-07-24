-- KitPlatform 197: FamilyOS Routine Operating Model lite (R0)
-- Commitment graph fields on template + daily commitment snapshot
-- Depends on: 192
-- Local / pilot only until deploy is explicitly approved

ALTER TABLE pack_family.commitment_template
    ADD COLUMN IF NOT EXISTS priority VARCHAR(20) NOT NULL DEFAULT 'normal',
    ADD COLUMN IF NOT EXISTS expected_duration_minutes INT,
    ADD COLUMN IF NOT EXISTS context_anchor VARCHAR(64),
    ADD COLUMN IF NOT EXISTS depends_on_template_ids UUID[] NOT NULL DEFAULT '{}'::uuid[];

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_commitment_template_priority'
    ) THEN
        ALTER TABLE pack_family.commitment_template
            ADD CONSTRAINT ck_commitment_template_priority
            CHECK (priority IN ('critical', 'normal', 'optional'));
    END IF;
END $$;

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS priority VARCHAR(20) NOT NULL DEFAULT 'normal',
    ADD COLUMN IF NOT EXISTS expected_duration_minutes INT,
    ADD COLUMN IF NOT EXISTS context_anchor VARCHAR(64),
    ADD COLUMN IF NOT EXISTS depends_on_template_ids UUID[] NOT NULL DEFAULT '{}'::uuid[];

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_commitment_priority'
    ) THEN
        ALTER TABLE pack_family.commitment
            ADD CONSTRAINT ck_commitment_priority
            CHECK (priority IN ('critical', 'normal', 'optional'));
    END IF;
END $$;

-- Backfill duration from existing windows (minutes)
UPDATE pack_family.commitment_template
SET expected_duration_minutes = GREATEST(
    1,
    ROUND(EXTRACT(EPOCH FROM (window_end - window_start)) / 60.0)::int
)
WHERE expected_duration_minutes IS NULL
  AND window_start IS NOT NULL
  AND window_end IS NOT NULL
  AND window_end > window_start;

UPDATE pack_family.commitment
SET expected_duration_minutes = GREATEST(
    1,
    ROUND(EXTRACT(EPOCH FROM (window_end - window_start)) / 60.0)::int
)
WHERE expected_duration_minutes IS NULL
  AND window_start IS NOT NULL
  AND window_end IS NOT NULL
  AND window_end > window_start;

-- Demo school-day chain (ids from 006_family_os_demo) — safe if rows missing
UPDATE pack_family.commitment_template SET
    priority = 'critical',
    context_anchor = 'after_wake',
    depends_on_template_ids = '{}'::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad01';

UPDATE pack_family.commitment_template SET
    priority = 'normal',
    context_anchor = 'after_wake',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad01']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad02';

UPDATE pack_family.commitment_template SET
    priority = 'critical',
    context_anchor = 'before_breakfast',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad02']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad03';

UPDATE pack_family.commitment_template SET
    priority = 'normal',
    context_anchor = 'after_breakfast',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad03']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad04';

UPDATE pack_family.commitment_template SET
    priority = 'critical',
    context_anchor = 'before_school',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad04']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad05';

UPDATE pack_family.commitment_template SET
    priority = 'critical',
    context_anchor = 'after_school',
    depends_on_template_ids = '{}'::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06';

UPDATE pack_family.commitment_template SET
    priority = 'normal',
    context_anchor = 'after_school',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad06']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad07';

UPDATE pack_family.commitment_template SET
    priority = 'critical',
    context_anchor = 'before_dinner',
    depends_on_template_ids = '{}'::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad08';

UPDATE pack_family.commitment_template SET
    priority = 'optional',
    context_anchor = 'after_dinner',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad08']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad09';

UPDATE pack_family.commitment_template SET
    priority = 'normal',
    context_anchor = 'before_sleep',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad09']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad10';

UPDATE pack_family.commitment_template SET
    priority = 'critical',
    context_anchor = 'before_sleep',
    depends_on_template_ids = ARRAY['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad10']::uuid[]
WHERE id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaad11';

COMMENT ON COLUMN pack_family.commitment_template.priority IS
    'critical | normal | optional — scheduler keeps critical when time is short';
COMMENT ON COLUMN pack_family.commitment_template.expected_duration_minutes IS
    'Expected duration; AI/scheduler can recompute windows from duration + anchors';
COMMENT ON COLUMN pack_family.commitment_template.context_anchor IS
    'Semantic slot (after_wake, before_breakfast, …) — not wall-clock';
COMMENT ON COLUMN pack_family.commitment_template.depends_on_template_ids IS
    'Templates that should complete before this one (same routine)';
