-- KitPlatform 350: Identity Stress Implementation V1 (10 cases / 004-D).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Extends 349. Does not LOCK CHAR-001. No Runway / Golden / Production Still.

ALTER TABLE pack_content.video_identity_stress_test
    DROP CONSTRAINT IF EXISTS ck_video_identity_stress_status;
ALTER TABLE pack_content.video_identity_stress_test
    ADD CONSTRAINT ck_video_identity_stress_status CHECK (status IN (
        'PENDING', 'NOT_RUN', 'RUNNING', 'PASS', 'CONDITIONAL', 'FAIL', 'BLOCKED', 'INCOMPLETE'
    ));

ALTER TABLE pack_content.video_identity_stress_attempt
    DROP CONSTRAINT IF EXISTS ck_video_identity_stress_group;
ALTER TABLE pack_content.video_identity_stress_attempt
    ADD CONSTRAINT ck_video_identity_stress_group CHECK (test_group IN (
        'ENVIRONMENT', 'LIGHTING', 'CAMERA', 'EMOTION', 'WARDROBE', 'POSE', 'OCCLUSION', 'SCENE'
    ));

CREATE TABLE IF NOT EXISTS pack_content.video_identity_stress_event (
    id                   UUID PRIMARY KEY,
    test_id              UUID NOT NULL REFERENCES pack_content.video_identity_stress_test (id) ON DELETE RESTRICT,
    event_type           VARCHAR(40) NOT NULL,
    test_case            VARCHAR(16),
    payload_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    actor                VARCHAR(120),
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_identity_stress_event_test
    ON pack_content.video_identity_stress_event (test_id, created_at DESC);

COMMENT ON TABLE pack_content.video_identity_stress_event IS
    'Audit for Identity Stress Implementation V1. Not a Master lock log.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_identity_stress_event TO %I', r);
    END IF;
  END LOOP;
END $$;
