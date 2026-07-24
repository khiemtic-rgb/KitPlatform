-- Family OS — per-commitment early lead + on-time grace (migration 216)
-- Depends on: 213_pack_family_allow_early_complete.sql, 214_pack_family_star_ledger.sql

ALTER TABLE pack_family.commitment_template
    ADD COLUMN IF NOT EXISTS early_lead_minutes INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS on_time_grace_minutes INT NOT NULL DEFAULT 0;

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS early_lead_minutes INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS on_time_grace_minutes INT NOT NULL DEFAULT 0;

COMMENT ON COLUMN pack_family.commitment_template.early_lead_minutes IS
    'Minutes before window_start when done is allowed. 0 + allow_early_complete = unlimited early.';
COMMENT ON COLUMN pack_family.commitment_template.on_time_grace_minutes IS
    'Minutes after window_end still counted as on-time full stars; family T1/T2/T3 apply after this.';

-- Flexible tasks: small on-time grace; early_lead stays 0 (unlimited via allow_early_complete)
UPDATE pack_family.commitment_template
SET on_time_grace_minutes = 10,
    updated_at = NOW()
WHERE deleted_at IS NULL
  AND allow_early_complete = TRUE;

-- Homework / study (fixed window, some grace)
UPDATE pack_family.commitment_template
SET on_time_grace_minutes = 10,
    updated_at = NOW()
WHERE deleted_at IS NULL
  AND allow_early_complete = FALSE
  AND (
      LOWER(title) LIKE '%bài%'
      OR LOWER(title) LIKE '%học%'
      OR LOWER(title) LIKE '%toán%'
  );

-- Propagate to materialized day commitments
UPDATE pack_family.commitment c
SET early_lead_minutes = t.early_lead_minutes,
    on_time_grace_minutes = t.on_time_grace_minutes,
    updated_at = NOW()
FROM pack_family.commitment_template t
WHERE c.template_id = t.id
  AND c.deleted_at IS NULL;

-- Orphan commitments: infer grace from title
UPDATE pack_family.commitment c
SET on_time_grace_minutes = CASE
        WHEN allow_early_complete THEN 10
        WHEN LOWER(title) LIKE '%bài%'
          OR LOWER(title) LIKE '%học%'
          OR LOWER(title) LIKE '%toán%' THEN 10
        ELSE 0
    END,
    updated_at = NOW()
WHERE c.deleted_at IS NULL
  AND c.template_id IS NULL;
