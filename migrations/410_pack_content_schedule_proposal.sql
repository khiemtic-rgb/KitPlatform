-- KitPlatform 410: Content Park — publishing agent schedule proposals (human approval gate).
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Do not merge into Pharmacy prod.
-- Precondition: pack_content.brand, topic, publish_job exist (281, 282).
-- Postcondition: one open proposal (Proposed/Approved) per topic and target; approving creates a publish_job
-- through the normal publish service. The table never posts anything by itself.
-- One transaction. Forward repair: re-run this file (idempotent).

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.schedule_proposal (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id        UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    topic_id        UUID NOT NULL REFERENCES pack_content.topic(id) ON DELETE CASCADE,
    title           VARCHAR(500) NOT NULL DEFAULT '',
    target_key      VARCHAR(80) NOT NULL,
    target_label    VARCHAR(200) NOT NULL DEFAULT '',
    connector_type  VARCHAR(64) NOT NULL,
    site_id         UUID REFERENCES pack_content.site_target(id) ON DELETE SET NULL,
    channel_id      UUID REFERENCES pack_content.channel_target(id) ON DELETE SET NULL,
    publish_at      TIMESTAMPTZ NOT NULL,
    status          VARCHAR(24) NOT NULL DEFAULT 'Proposed',
    reason          TEXT NOT NULL DEFAULT '',
    publish_job_id  UUID REFERENCES pack_content.publish_job(id) ON DELETE SET NULL,
    decided_by      VARCHAR(200),
    decided_at      TIMESTAMPTZ,
    last_error      TEXT,
    created_by      VARCHAR(200),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_schedule_proposal_status CHECK (status IN (
        'Proposed', 'Approved', 'Rejected', 'Cancelled', 'Failed'
    )),
    CONSTRAINT ck_content_schedule_proposal_target CHECK (site_id IS NOT NULL OR channel_id IS NOT NULL)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_content_schedule_proposal_open
    ON pack_content.schedule_proposal (topic_id, target_key)
    WHERE status IN ('Proposed', 'Approved');

CREATE INDEX IF NOT EXISTS ix_content_schedule_proposal_brand
    ON pack_content.schedule_proposal (brand_id, status, publish_at);

COMMENT ON TABLE pack_content.schedule_proposal IS
    'Agent proposals for scheduled publishing. Approved rows link to the publish_job created by the human approval.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.schedule_proposal TO %I', r);
    END IF;
  END LOOP;
END $$;

COMMIT;
