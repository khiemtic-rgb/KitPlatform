-- Family OS — time-anchored vs flexible early completion
-- Depends on: 192_pack_family_os.sql

ALTER TABLE pack_family.commitment_template
    ADD COLUMN IF NOT EXISTS allow_early_complete BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS allow_early_complete BOOLEAN NOT NULL DEFAULT FALSE;

COMMENT ON COLUMN pack_family.commitment_template.allow_early_complete IS
    'When false, done is rejected before window_start (meals, hygiene, sleep, dress, school prep).';
COMMENT ON COLUMN pack_family.commitment.allow_early_complete IS
    'Snapshot from template at day-flow materialization; governs early done gate.';

-- Flexible: reading, exercise, optional enrichment
UPDATE pack_family.commitment_template
SET allow_early_complete = TRUE,
    updated_at = NOW()
WHERE deleted_at IS NULL
  AND (
      LOWER(title) LIKE '%đọc%'
      OR LOWER(title) LIKE '%sách%'
      OR LOWER(title) LIKE '%kể chuyện%'
      OR LOWER(title) LIKE '%thể dục%'
      OR LOWER(title) LIKE '%vận động%'
      OR LOWER(title) LIKE '%chạy bộ%'
      OR LOWER(title) LIKE '%bơi%'
  );

-- Propagate to materialized day commitments (via template link)
UPDATE pack_family.commitment c
SET allow_early_complete = t.allow_early_complete,
    updated_at = NOW()
FROM pack_family.commitment_template t
WHERE c.template_id = t.id
  AND c.deleted_at IS NULL;

-- Orphan / manual commitments: infer from title
UPDATE pack_family.commitment c
SET allow_early_complete = TRUE,
    updated_at = NOW()
WHERE c.deleted_at IS NULL
  AND c.template_id IS NULL
  AND (
      LOWER(c.title) LIKE '%đọc%'
      OR LOWER(c.title) LIKE '%sách%'
      OR LOWER(c.title) LIKE '%kể chuyện%'
      OR LOWER(c.title) LIKE '%thể dục%'
      OR LOWER(c.title) LIKE '%vận động%'
      OR LOWER(c.title) LIKE '%chạy bộ%'
      OR LOWER(c.title) LIKE '%bơi%'
  );
