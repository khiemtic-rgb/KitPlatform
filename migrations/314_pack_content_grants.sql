-- KitPlatform 314: GRANT pack_content to API roles
-- Tables created via sudo -u postgres are owned by postgres; kitplatform cannot SELECT.
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Do not merge into Pharmacy prod.

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT USAGE ON SCHEMA pack_content TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA pack_content TO %I', r);
      EXECUTE format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA pack_content TO %I', r);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA pack_content GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
        r);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA pack_content GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO %I',
        r);
    END IF;
  END LOOP;
END $$;
