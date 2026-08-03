-- Pack:FamilyOS
-- P0.5: track when kid starts a commitment for min-duration evidence gate (~70%).

ALTER TABLE pack_family.commitment
  ADD COLUMN IF NOT EXISTS started_at TIMESTAMPTZ;

COMMENT ON COLUMN pack_family.commitment.started_at IS
  'P0.5: first in_progress/done stamp; used for study min-duration before evidence_satisfied.';