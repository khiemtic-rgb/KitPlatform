-- KitPlatform 394: Core Idea may exist before a brand.
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Does not rewrite existing packages. Rows that already have brand_id and topic_id stay as they are.
-- Precondition: every content_package is either (brand_id, topic_id, source_package_id all null)
-- or (brand_id and topic_id both not null). A half-null row aborts this file.
-- Postcondition: brand_id and topic_id are nullable; ck_content_package_scope is present.
-- The content runner applies each file with psql -f and autocommit. This file is one transaction.
-- Forward repair: re-run this file. Rollback of an already committed file is a later migration, not a down script.

BEGIN;

ALTER TABLE pack_content.content_package
    ALTER COLUMN brand_id DROP NOT NULL;

ALTER TABLE pack_content.content_package
    ALTER COLUMN topic_id DROP NOT NULL;

ALTER TABLE pack_content.content_package
    DROP CONSTRAINT IF EXISTS ck_content_package_scope;

ALTER TABLE pack_content.content_package
    ADD CONSTRAINT ck_content_package_scope CHECK (
        (brand_id IS NULL AND topic_id IS NULL AND source_package_id IS NULL)
        OR (brand_id IS NOT NULL AND topic_id IS NOT NULL)
    );

COMMENT ON CONSTRAINT ck_content_package_scope ON pack_content.content_package IS
    'An Idea has no brand and no topic. A Brand Angle or branded package has both.';

COMMIT;
