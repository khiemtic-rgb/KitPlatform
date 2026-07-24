-- KitPlatform 199: FamilyOS commitment photo evidence
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS evidence_url TEXT,
    ADD COLUMN IF NOT EXISTS evidence_uploaded_at TIMESTAMPTZ;

COMMENT ON COLUMN pack_family.commitment.evidence_url IS
    'Relative upload path e.g. /uploads/family-os/{tenantN}/{file} — photo proof when done.';
COMMENT ON COLUMN pack_family.commitment.evidence_uploaded_at IS
    'When evidence_url was last set.';
