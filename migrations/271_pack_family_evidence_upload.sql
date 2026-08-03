-- Pack:FamilyOS
-- P0.6: evidence upload fingerprints for duplicate / thin-image gates.

CREATE TABLE IF NOT EXISTS pack_family.evidence_upload (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id UUID NOT NULL,
  family_id UUID NOT NULL,
  member_id UUID NULL,
  content_sha256 CHAR(64) NOT NULL,
  byte_size INT NOT NULL,
  width INT NULL,
  height INT NULL,
  url TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_evidence_upload_sha
  ON pack_family.evidence_upload (tenant_id, family_id, content_sha256, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_evidence_upload_family_day
  ON pack_family.evidence_upload (tenant_id, family_id, created_at DESC);

COMMENT ON TABLE pack_family.evidence_upload IS
  'P0.6 fingerprints of uploaded study/chore evidence photos.';
