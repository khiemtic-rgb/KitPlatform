-- KitPlatform 268: Evidence P0 — commitment_kind + evidence_policy
-- Layer: Pack:FamilyOS
-- Depends on: 241_pack_family_behavior_os_wave2.sql, 199_pack_family_commitment_evidence.sql
-- Manifest: migration-files.family-os.txt only

ALTER TABLE pack_family.commitment_template
  ADD COLUMN IF NOT EXISTS commitment_kind VARCHAR(32) NOT NULL DEFAULT 'chore';

DO 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_template_kind'
  ) THEN
    ALTER TABLE pack_family.commitment_template
      ADD CONSTRAINT ck_commitment_template_kind
      CHECK (commitment_kind IN ('chore', 'study_focus', 'relation'));
  END IF;
END ;

COMMENT ON COLUMN pack_family.commitment_template.commitment_kind IS
  'Evidence P0: chore | study_focus | relation';

ALTER TABLE pack_family.commitment
  ADD COLUMN IF NOT EXISTS commitment_kind VARCHAR(32) NOT NULL DEFAULT 'chore',
  ADD COLUMN IF NOT EXISTS evidence_policy VARCHAR(32) NOT NULL DEFAULT 'optional',
  ADD COLUMN IF NOT EXISTS evidence_satisfied_at TIMESTAMPTZ,
  ADD COLUMN IF NOT EXISTS evidence_satisfied_by VARCHAR(32);

DO 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_kind'
  ) THEN
    ALTER TABLE pack_family.commitment
      ADD CONSTRAINT ck_commitment_kind
      CHECK (commitment_kind IN ('chore', 'study_focus', 'relation'));
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_evidence_policy'
  ) THEN
    ALTER TABLE pack_family.commitment
      ADD CONSTRAINT ck_commitment_evidence_policy
      CHECK (evidence_policy IN ('optional', 'required_soft', 'required_hard'));
  END IF;
END ;

COMMENT ON COLUMN pack_family.commitment.commitment_kind IS
  'Evidence P0 snapshot from template.';
COMMENT ON COLUMN pack_family.commitment.evidence_policy IS
  'optional | required_soft (tick OK, stars gated) | required_hard (block done).';
COMMENT ON COLUMN pack_family.commitment.evidence_satisfied_at IS
  'When min evidence was met (photo | retrieval | parent_verify).';
COMMENT ON COLUMN pack_family.commitment.evidence_satisfied_by IS
  'photo | retrieval | parent_verify | device_signal';

UPDATE pack_family.commitment_template t
SET commitment_kind = 'study_focus'
WHERE t.deleted_at IS NULL
  AND t.commitment_kind = 'chore'
  AND lower(t.title) ~ '(học|bài tập|ôn|homework|study|toán|văn|tiếng anh)';

UPDATE pack_family.commitment c
SET commitment_kind = 'study_focus',
    evidence_policy = 'required_soft'
WHERE c.deleted_at IS NULL
  AND c.commitment_kind = 'chore'
  AND lower(c.title) ~ '(học|bài tập|ôn|homework|study|toán|văn|tiếng anh)';

UPDATE pack_family.commitment c
SET evidence_policy = 'required_soft'
WHERE c.deleted_at IS NULL
  AND c.commitment_kind = 'study_focus'
  AND c.evidence_policy = 'optional';
