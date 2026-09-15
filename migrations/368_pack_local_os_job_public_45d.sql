-- Local OS: jobs stay public ~45 days so the homepage has enough listings.
-- Isolated park. Manifest: deploy/ubuntu/migration-files.local-os.txt ONLY.
-- Does not invent job data. Rooms stay 14 days. Events stay 30 days.

-- Extend ACTIVE jobs that still use the old 14-day clock.
UPDATE pack_local.listing
SET
    expires_at = COALESCE(published_at, last_checked_at, created_at) + INTERVAL '45 days',
    updated_at = NOW()
WHERE kind = 'job'
  AND status = 'ACTIVE'
  AND safety_flag = FALSE
  AND expires_at IS NOT NULL
  AND expires_at < COALESCE(published_at, last_checked_at, created_at) + INTERVAL '45 days';

-- Bring back jobs that auto-expired on the short window but are still within 45 days of publish.
UPDATE pack_local.listing
SET
    status = 'ACTIVE',
    expires_at = COALESCE(published_at, last_checked_at, created_at) + INTERVAL '45 days',
    updated_at = NOW()
WHERE kind = 'job'
  AND status = 'EXPIRED'
  AND safety_flag = FALSE
  AND expires_at IS NOT NULL
  AND expires_at <= NOW()
  AND COALESCE(published_at, last_checked_at, created_at) + INTERVAL '45 days' > NOW();
