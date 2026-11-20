-- KitPlatform 421: Content Park — publishing schedule settings per brand and publish plan per topic.
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Do not merge into Pharmacy prod.
-- Precondition: pack_content.brand, topic exist (281); schedule_proposal (410).
-- Postcondition: the agent reads brand_schedule_setting (slots, gap, short/day, long/week per channel)
-- and topic_publish_plan (long/short, which channel types) when it schedules. Nothing is posted by these tables.
-- One transaction. Forward repair: re-run this file (idempotent).

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.brand_schedule_setting (
    brand_id      UUID PRIMARY KEY REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    settings_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_content.topic_publish_plan (
    topic_id    UUID PRIMARY KEY REFERENCES pack_content.topic(id) ON DELETE CASCADE,
    kind        VARCHAR(16) NOT NULL,
    connectors  TEXT[] NOT NULL DEFAULT '{}',
    seq         INT NOT NULL DEFAULT 0,
    source_ref  VARCHAR(200),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_topic_publish_plan_kind CHECK (kind IN ('long', 'short'))
);

COMMENT ON TABLE pack_content.brand_schedule_setting IS
    'Publishing schedule per brand: local slots, min gap, max per day, short per day and long per week per channel.';
COMMENT ON TABLE pack_content.topic_publish_plan IS
    'How a topic is published: long or short video, which channel types, order within a batch (Video Studio).';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.brand_schedule_setting TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.topic_publish_plan TO %I', r);
    END IF;
  END LOOP;
END $$;

COMMIT;
