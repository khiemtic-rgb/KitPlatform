-- Local OS: unique source URL so paste-ingest does not duplicate.
-- Isolated park. Do not apply via Pharmacy / Family / Content manifests.

CREATE UNIQUE INDEX IF NOT EXISTS uq_local_listing_source_url
    ON pack_local.listing (source_url)
    WHERE source_url IS NOT NULL AND length(trim(source_url)) > 0;
