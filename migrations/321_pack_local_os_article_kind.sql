-- Local OS: KIT_MKT web_long → listing kind article (thainguyenlife.vn/tin).
-- Manifest: deploy/ubuntu/migration-files.local-os.txt ONLY
-- Do not re-run 302. Data stays in pack_local.

ALTER TABLE pack_local.listing DROP CONSTRAINT IF EXISTS ck_local_listing_kind;
ALTER TABLE pack_local.listing
    ADD CONSTRAINT ck_local_listing_kind
    CHECK (kind IN ('job', 'event', 'room', 'grant', 'article'));
