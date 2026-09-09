-- KitPlatform 325: kit_sales source fields allow URL / long refs
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY

ALTER TABLE pack_sales.business
    ALTER COLUMN source TYPE TEXT;

ALTER TABLE pack_sales.lead
    ALTER COLUMN source TYPE TEXT;

INSERT INTO kit_schema_migrations (filename) VALUES ('325_pack_kit_sales_source_text.sql')
ON CONFLICT (filename) DO NOTHING;
