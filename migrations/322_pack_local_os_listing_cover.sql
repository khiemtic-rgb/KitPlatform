-- Local OS: article / listing cover from KIT_MKT publish.
-- Manifest: deploy/ubuntu/migration-files.local-os.txt ONLY

ALTER TABLE pack_local.listing
    ADD COLUMN IF NOT EXISTS cover_url TEXT;
