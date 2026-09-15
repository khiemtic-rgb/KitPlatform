-- Local OS: rooms stay public (tenants rotate). No display-day clock.
-- Isolated park. Manifest: deploy/ubuntu/migration-files.local-os.txt ONLY.
-- Does not invent listings. Hidden / safety-flagged rooms stay out.

-- Bring back rooms that auto-expired on the old 14-day window.
UPDATE pack_local.listing
SET
    status = 'ACTIVE',
    expires_at = NULL,
    updated_at = NOW()
WHERE kind = 'room'
  AND status = 'EXPIRED'
  AND safety_flag = FALSE;

-- Clear the display clock on remaining rooms (except hidden).
UPDATE pack_local.listing
SET
    expires_at = NULL,
    updated_at = NOW()
WHERE kind = 'room'
  AND status <> 'HIDDEN'
  AND expires_at IS NOT NULL;
