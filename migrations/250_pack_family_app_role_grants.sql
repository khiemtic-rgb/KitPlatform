-- KitPlatform 250: Grant pack_family to app DB roles (pharmacore / kitplatform).
-- Needed after Behavior OS / currency tables created as postgres (peer-auth migs).
-- Without this, API gets 42501 permission denied on new tables (e.g. commitment_reflection).

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['pharmacore', 'kitplatform'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT USAGE, CREATE ON SCHEMA pack_family TO %I', r);
      EXECUTE format('GRANT ALL ON ALL TABLES IN SCHEMA pack_family TO %I', r);
      EXECUTE format('GRANT ALL ON ALL SEQUENCES IN SCHEMA pack_family TO %I', r);
      -- Future tables created by postgres (VPS peer-auth apply) stay usable by the app role.
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA pack_family GRANT ALL ON TABLES TO %I',
        r);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA pack_family GRANT ALL ON SEQUENCES TO %I',
        r);
    END IF;
  END LOOP;
END $$;
