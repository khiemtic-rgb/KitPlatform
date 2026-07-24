-- Family OS — pending star delta at child submit; ledger posts on parent approve
-- Depends on: 214_pack_family_star_ledger.sql

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS pending_star_delta INT,
    ADD COLUMN IF NOT EXISTS pending_star_tier VARCHAR(32),
    ADD COLUMN IF NOT EXISTS pending_star_late_minutes INT,
    ADD COLUMN IF NOT EXISTS star_computed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS star_posted_at TIMESTAMPTZ;

COMMENT ON COLUMN pack_family.commitment.pending_star_delta IS
    'Star delta computed when child marks done (submit for parent check).';
COMMENT ON COLUMN pack_family.commitment.pending_star_tier IS
    'Tier label at submit time (on_time, late_half, late_zero, …).';
COMMENT ON COLUMN pack_family.commitment.pending_star_late_minutes IS
    'Minutes late vs window_end + grace at submit time.';
COMMENT ON COLUMN pack_family.commitment.star_computed_at IS
    'When pending_star_* was frozen at child submit.';
COMMENT ON COLUMN pack_family.commitment.star_posted_at IS
    'When pending stars were posted to star_ledger (parent approve or auto-trust).';

-- Backfill: commitments already in ledger = posted
UPDATE pack_family.commitment c
SET pending_star_delta = l.delta,
    pending_star_tier = l.tier,
    pending_star_late_minutes = l.late_minutes,
    star_computed_at = COALESCE(c.completed_at, l.created_at),
    star_posted_at = l.created_at,
    updated_at = NOW()
FROM pack_family.star_ledger l
WHERE l.commitment_id = c.id
  AND l.tenant_id = c.tenant_id
  AND c.deleted_at IS NULL
  AND c.star_posted_at IS NULL;
