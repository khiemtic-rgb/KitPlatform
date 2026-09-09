-- KitPlatform 340: KIT Video Engine Phase 06 — controlled Runway I2V + Video QA
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Golden shot only: SH01-01. Do not open SH01-02+ / LipSync / Film.

CREATE TABLE IF NOT EXISTS pack_content.video_runway_take (
    id                      UUID PRIMARY KEY,
    production_id           UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_code               VARCHAR(32) NOT NULL,
    keyframe_attempt_id     UUID NOT NULL REFERENCES pack_content.video_keyframe_attempt (id) ON DELETE RESTRICT,
    attempt_no              INTEGER NOT NULL,
    status                  VARCHAR(32) NOT NULL DEFAULT 'READY',
    model                   VARCHAR(40) NOT NULL DEFAULT 'gen4_turbo',
    duration_sec            INTEGER NOT NULL,
    ratio                   VARCHAR(16) NOT NULL DEFAULT '1280:720',
    motion_prompt           TEXT NOT NULL DEFAULT '',
    fingerprint             VARCHAR(64) NOT NULL DEFAULT '',
    source_artifact_sha256  VARCHAR(64) NOT NULL DEFAULT '',
    video_sha256            VARCHAR(64) NOT NULL DEFAULT '',
    runway_task_id          VARCHAR(160) NOT NULL DEFAULT '',
    output_url              TEXT,
    video_path              TEXT,
    failure_class           VARCHAR(80) NOT NULL DEFAULT '',
    failure_code            VARCHAR(80) NOT NULL DEFAULT '',
    retry_reason            VARCHAR(40) NOT NULL DEFAULT '',
    credit_state            VARCHAR(24) NOT NULL DEFAULT 'NONE',
    persist_status          VARCHAR(32) NOT NULL DEFAULT 'OK',
    idempotency_key         VARCHAR(160),
    qa_json                 JSONB NOT NULL DEFAULT '{}'::jsonb,
    motion_json             JSONB NOT NULL DEFAULT '{}'::jsonb,
    provider_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    metadata_json           JSONB NOT NULL DEFAULT '{}'::jsonb,
    confirmed               BOOLEAN NOT NULL DEFAULT FALSE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_runway_take_shot_attempt UNIQUE (production_id, shot_code, attempt_no),
    CONSTRAINT ck_video_runway_take_shot CHECK (shot_code = 'SH01-01'),
    CONSTRAINT ck_video_runway_take_duration CHECK (duration_sec IN (5, 10)),
    CONSTRAINT ck_video_runway_take_model CHECK (model = 'gen4_turbo'),
    CONSTRAINT ck_video_runway_take_status CHECK (status IN (
        'READY', 'SUBMITTING', 'SUBMITTED', 'PROCESSING',
        'SUCCEEDED', 'DOWNLOAD', 'VERIFY', 'VIDEO_READY',
        'VIDEO_QA_FAIL', 'APPROVED_TAKE',
        'FAILED', 'DIAGNOSE'
    )),
    CONSTRAINT ck_video_runway_take_retry CHECK (retry_reason IN (
        '', 'MOTION_PROMPT_REVISION', 'KEYFRAME_REPAIR'
    )),
    CONSTRAINT ck_video_runway_take_credit CHECK (credit_state IN (
        'NONE', 'PENDING', 'ACTUAL', 'REFUND_PENDING'
    ))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_video_runway_take_idem
    ON pack_content.video_runway_take (idempotency_key)
    WHERE idempotency_key IS NOT NULL AND length(idempotency_key) > 0;

CREATE INDEX IF NOT EXISTS ix_video_runway_take_kf
    ON pack_content.video_runway_take (keyframe_attempt_id, attempt_no);

COMMENT ON TABLE pack_content.video_runway_take IS
    'Phase 06 golden I2V. HTTP 200 ≠ VIDEO_READY. Same fingerprint after FAIL = no blind retry.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.video_runway_take TO %I', r);
    END IF;
  END LOOP;
END $$;
