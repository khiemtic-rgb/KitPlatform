-- KitPlatform 334: KIT Video Engine Phase 01 — GenerationJob / Attempt / ProviderTask
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Reuses video_project / video_production / video_shot_state (mig 333). No Famixa-only tables.

ALTER TABLE pack_content.video_production
    ADD COLUMN IF NOT EXISTS run_status VARCHAR(32) NOT NULL DEFAULT 'READY';

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'ck_video_production_run_status'
  ) THEN
    ALTER TABLE pack_content.video_production
      ADD CONSTRAINT ck_video_production_run_status CHECK (run_status IN (
        'READY', 'RUNNING', 'SUCCEEDED', 'FAILED', 'BLOCKED', 'CANCELLED', 'PARTIALLY_COMPLETE'
      ));
  END IF;
END $$;

CREATE TABLE IF NOT EXISTS pack_content.video_generation_job (
    id               UUID PRIMARY KEY,
    project_id       UUID NOT NULL REFERENCES pack_content.video_project (id) ON DELETE RESTRICT,
    production_id    UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    scene_code       VARCHAR(32) NOT NULL DEFAULT '',
    shot_code        VARCHAR(32) NOT NULL,
    provider         VARCHAR(32) NOT NULL,
    operation        VARCHAR(32) NOT NULL,
    status           VARCHAR(32) NOT NULL DEFAULT 'PRECHECK',
    idempotency_key  VARCHAR(120) NOT NULL,
    confirmed        BOOLEAN NOT NULL DEFAULT FALSE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at       TIMESTAMPTZ,
    completed_at     TIMESTAMPTZ,
    CONSTRAINT uq_video_generation_job_key UNIQUE (idempotency_key),
    CONSTRAINT ck_video_generation_job_status CHECK (status IN (
        'PRECHECK', 'READY', 'CONFIRM_REQUIRED', 'SUBMITTED', 'RUNNING',
        'SUCCEEDED', 'FAILED', 'REFUND_PENDING', 'BLOCKED'
    )),
    CONSTRAINT ck_video_generation_job_op CHECK (operation IN ('STILL', 'I2V', 'TTS', 'LIPSYNC'))
);

CREATE INDEX IF NOT EXISTS ix_video_generation_job_prod
    ON pack_content.video_generation_job (production_id, created_at DESC);

CREATE TABLE IF NOT EXISTS pack_content.video_generation_attempt (
    id           UUID PRIMARY KEY,
    job_id       UUID NOT NULL REFERENCES pack_content.video_generation_job (id) ON DELETE RESTRICT,
    attempt_no   INTEGER NOT NULL,
    status       VARCHAR(32) NOT NULL,
    error        TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_video_generation_attempt UNIQUE (job_id, attempt_no)
);

CREATE TABLE IF NOT EXISTS pack_content.video_provider_task (
    id                 UUID PRIMARY KEY,
    attempt_id         UUID NOT NULL REFERENCES pack_content.video_generation_attempt (id) ON DELETE RESTRICT,
    provider           VARCHAR(32) NOT NULL,
    provider_task_id   VARCHAR(160) NOT NULL,
    provider_status    VARCHAR(48) NOT NULL,
    failure_code       VARCHAR(80),
    output_url         TEXT,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE pack_content.video_generation_job IS
    'Engine credit-gated job. Duplicate idempotency_key returns the same job. HOLD shots never insert.';
COMMENT ON TABLE pack_content.video_provider_task IS
    'Provider snapshot (Runway task id / status / failure code). HTTP 200 is not success.';

DO $$
DECLARE
  r text;
  t text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      FOREACH t IN ARRAY ARRAY['video_generation_job', 'video_generation_attempt', 'video_provider_task'] LOOP
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.%I TO %I', t, r);
      END LOOP;
    END IF;
  END LOOP;
END $$;
