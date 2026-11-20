-- KitPlatform 368: FAMIXA Production OS Work Execution control plane V1
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Authority for Request → Task → Snapshot → Attempt → Artifact → Binding.
-- Does not delete series_build graph or kit video_generation_* tables.
-- Those become LEGACY / COMPATIBILITY. No media generation. No provider calls.

CREATE TABLE IF NOT EXISTS pack_content.production_requests (
    request_id              UUID PRIMARY KEY,
    build_id                UUID NOT NULL REFERENCES pack_content.series_build (id) ON DELETE RESTRICT,
    episode_code            VARCHAR(32) NOT NULL DEFAULT '',
    scene_code              VARCHAR(32) NOT NULL DEFAULT '',
    shot_code               VARCHAR(80) NOT NULL,
    user_action             VARCHAR(32) NOT NULL,
    required_output_type    VARCHAR(32) NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by              VARCHAR(120) NOT NULL DEFAULT '',
    status                  VARCHAR(24) NOT NULL DEFAULT 'SUBMITTED',
    parent_request_id       UUID REFERENCES pack_content.production_requests (request_id) ON DELETE RESTRICT,
    idempotency_key         VARCHAR(180) NOT NULL,
    forbidden_fallbacks     JSONB NOT NULL DEFAULT '[]'::jsonb,
    CONSTRAINT uq_prod_request_idempotency UNIQUE (idempotency_key),
    CONSTRAINT ck_prod_request_action CHECK (user_action IN (
        'CREATE_IMAGE', 'CREATE_VIDEO', 'CREATE_VOICE', 'CREATE_LIPSYNC',
        'MIX_VIDEO', 'ACCEPT', 'BIND', 'REGENERATE'
    )),
    CONSTRAINT ck_prod_request_status CHECK (status IN (
        'DRAFT', 'SUBMITTED', 'IN_PROGRESS', 'COMPLETED', 'FAILED', 'CANCELLED'
    ))
);

CREATE TABLE IF NOT EXISTS pack_content.production_tasks (
    task_id                 UUID PRIMARY KEY,
    request_id              UUID NOT NULL REFERENCES pack_content.production_requests (request_id) ON DELETE RESTRICT,
    parent_task_id          UUID REFERENCES pack_content.production_tasks (task_id) ON DELETE RESTRICT,
    action                  VARCHAR(32) NOT NULL,
    target_type             VARCHAR(24) NOT NULL DEFAULT 'Shot',
    target_id               VARCHAR(80) NOT NULL,
    build_id                UUID NOT NULL REFERENCES pack_content.series_build (id) ON DELETE RESTRICT,
    episode_code            VARCHAR(32) NOT NULL DEFAULT '',
    scene_code              VARCHAR(32) NOT NULL DEFAULT '',
    shot_code               VARCHAR(80) NOT NULL,
    input_snapshot_id       UUID,
    provider_id             VARCHAR(40),
    model_id                VARCHAR(80),
    status                  VARCHAR(24) NOT NULL DEFAULT 'CREATED',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at              TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ,
    failure_code            VARCHAR(48),
    failure_reason          TEXT,
    CONSTRAINT ck_prod_task_action CHECK (action IN (
        'CREATE_IMAGE', 'CREATE_VIDEO', 'CREATE_VOICE', 'CREATE_LIPSYNC',
        'MIX_VIDEO', 'ACCEPT', 'BIND', 'REGENERATE'
    )),
    CONSTRAINT ck_prod_task_status CHECK (status IN (
        'CREATED', 'INPUT_FROZEN', 'PROVIDER_FROZEN', 'RUNNING',
        'SUCCEEDED', 'FAILED_IDENTITY', 'FAILED', 'SUPERSEDED'
    )),
    CONSTRAINT ck_prod_task_no_input_changed CHECK (failure_code IS DISTINCT FROM 'INPUT_CHANGED')
);

CREATE TABLE IF NOT EXISTS pack_content.production_task_input_snapshots (
    snapshot_id             UUID PRIMARY KEY,
    task_id                 UUID NOT NULL REFERENCES pack_content.production_tasks (task_id) ON DELETE RESTRICT,
    story                   TEXT NOT NULL,
    acting_beat             TEXT NOT NULL DEFAULT '',
    visual_contract         TEXT NOT NULL DEFAULT '',
    shot_contract           TEXT NOT NULL DEFAULT '',
    prompt                  TEXT NOT NULL DEFAULT '',
    compiler_output         TEXT NOT NULL DEFAULT '',
    story_hash              VARCHAR(64) NOT NULL,
    acting_hash             VARCHAR(64) NOT NULL,
    visual_hash             VARCHAR(64) NOT NULL,
    compiler_hash           VARCHAR(64) NOT NULL,
    keyframe_hash           VARCHAR(64) NOT NULL DEFAULT '',
    voice_hash              VARCHAR(64) NOT NULL DEFAULT '',
    motion_hash             VARCHAR(64) NOT NULL DEFAULT '',
    lipsync_source_hash     VARCHAR(64) NOT NULL DEFAULT '',
    timing_hash             VARCHAR(64) NOT NULL DEFAULT '',
    provider_id             VARCHAR(40) NOT NULL,
    model_id                VARCHAR(80) NOT NULL,
    snapshot_hash           VARCHAR(64) NOT NULL,
    frozen_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_prod_snapshot_task UNIQUE (task_id)
);

CREATE TABLE IF NOT EXISTS pack_content.production_attempts (
    attempt_id              UUID PRIMARY KEY,
    task_id                 UUID NOT NULL REFERENCES pack_content.production_tasks (task_id) ON DELETE RESTRICT,
    attempt_number          INTEGER NOT NULL,
    provider_id             VARCHAR(40) NOT NULL,
    model_id                VARCHAR(80) NOT NULL,
    provider_job_id         VARCHAR(240),
    status                  VARCHAR(32) NOT NULL DEFAULT 'STARTED',
    started_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at            TIMESTAMPTZ,
    request_payload_hash    VARCHAR(64) NOT NULL DEFAULT '',
    response_payload_hash   VARCHAR(64),
    error_code              VARCHAR(64),
    CONSTRAINT uq_prod_attempt_number UNIQUE (task_id, attempt_number),
    CONSTRAINT ck_prod_attempt_status CHECK (status IN (
        'STARTED', 'PROVIDER_SUBMITTED', 'PROVIDER_SUCCEEDED', 'PROVIDER_FAILED', 'LATE'
    )),
    CONSTRAINT ck_prod_attempt_n CHECK (attempt_number >= 1)
);

CREATE TABLE IF NOT EXISTS pack_content.production_artifacts (
    artifact_id             UUID PRIMARY KEY,
    task_id                 UUID NOT NULL REFERENCES pack_content.production_tasks (task_id) ON DELETE RESTRICT,
    attempt_id              UUID NOT NULL REFERENCES pack_content.production_attempts (attempt_id) ON DELETE RESTRICT,
    role                    VARCHAR(32) NOT NULL,
    mime_type               VARCHAR(80) NOT NULL,
    size_bytes              BIGINT NOT NULL,
    duration_sec            NUMERIC NOT NULL DEFAULT 0,
    byte_sha256             VARCHAR(64) NOT NULL,
    storage_key             TEXT NOT NULL,
    provider_job_id         VARCHAR(240),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    verification_status     VARCHAR(32) NOT NULL DEFAULT 'UNVERIFIED',
    input_snapshot_hash     VARCHAR(64) NOT NULL DEFAULT '',
    CONSTRAINT ck_prod_artifact_verify CHECK (verification_status IN (
        'UNVERIFIED', 'VERIFIED', 'FAILED_VERIFICATION'
    )),
    CONSTRAINT ck_prod_artifact_bytes CHECK (size_bytes >= 0),
    CONSTRAINT ck_prod_artifact_sha CHECK (char_length(byte_sha256) = 64)
);

CREATE TABLE IF NOT EXISTS pack_content.production_bindings (
    binding_id              UUID PRIMARY KEY,
    build_id                UUID NOT NULL REFERENCES pack_content.series_build (id) ON DELETE RESTRICT,
    shot_code               VARCHAR(80) NOT NULL,
    role                    VARCHAR(32) NOT NULL,
    artifact_id             UUID NOT NULL REFERENCES pack_content.production_artifacts (artifact_id) ON DELETE RESTRICT,
    bound_by_task_id        UUID NOT NULL REFERENCES pack_content.production_tasks (task_id) ON DELETE RESTRICT,
    bound_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    unbound_at              TIMESTAMPTZ,
    CONSTRAINT ck_prod_binding_role CHECK (role IN (
        'CURRENT', 'ACCEPTED', 'VISIBLE', 'EDITORIAL_SOURCE', 'FINAL'
    ))
);

CREATE TABLE IF NOT EXISTS pack_content.production_task_events (
    event_id                UUID PRIMARY KEY,
    event_type              VARCHAR(48) NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    request_id              UUID REFERENCES pack_content.production_requests (request_id) ON DELETE RESTRICT,
    task_id                 UUID REFERENCES pack_content.production_tasks (task_id) ON DELETE RESTRICT,
    attempt_id              UUID REFERENCES pack_content.production_attempts (attempt_id) ON DELETE RESTRICT,
    artifact_id             UUID REFERENCES pack_content.production_artifacts (artifact_id) ON DELETE RESTRICT,
    shot_code               VARCHAR(80) NOT NULL DEFAULT '',
    action                  VARCHAR(32) NOT NULL DEFAULT '',
    payload_json            JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS pack_content.production_film_assemblies (
    assembly_id             UUID PRIMARY KEY,
    build_id                UUID NOT NULL REFERENCES pack_content.series_build (id) ON DELETE RESTRICT,
    title                   VARCHAR(240) NOT NULL DEFAULT '',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_content.production_film_assembly_shots (
    assembly_shot_id        UUID PRIMARY KEY,
    assembly_id             UUID NOT NULL REFERENCES pack_content.production_film_assemblies (assembly_id) ON DELETE RESTRICT,
    build_id                UUID NOT NULL REFERENCES pack_content.series_build (id) ON DELETE RESTRICT,
    shot_code               VARCHAR(80) NOT NULL,
    artifact_id             UUID NOT NULL REFERENCES pack_content.production_artifacts (artifact_id) ON DELETE RESTRICT,
    sort_order              INTEGER NOT NULL,
    duration_sec            NUMERIC NOT NULL DEFAULT 0,
    transition              VARCHAR(32) NOT NULL DEFAULT 'CUT',
    audio_state             VARCHAR(32) NOT NULL DEFAULT 'BOUND',
    CONSTRAINT uq_prod_assembly_order UNIQUE (assembly_id, sort_order)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_binding_active
    ON pack_content.production_bindings (build_id, shot_code, role)
    WHERE unbound_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_attempt_provider_job
    ON pack_content.production_attempts (provider_id, provider_job_id)
    WHERE provider_job_id IS NOT NULL AND provider_job_id <> '';

CREATE INDEX IF NOT EXISTS ix_prod_request_scope
    ON pack_content.production_requests (build_id, shot_code, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_prod_task_scope
    ON pack_content.production_tasks (build_id, shot_code, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_prod_task_request
    ON pack_content.production_tasks (request_id);

CREATE INDEX IF NOT EXISTS ix_prod_artifact_sha
    ON pack_content.production_artifacts (byte_sha256);

CREATE INDEX IF NOT EXISTS ix_prod_artifact_task
    ON pack_content.production_artifacts (task_id, attempt_id);

CREATE INDEX IF NOT EXISTS ix_prod_event_task
    ON pack_content.production_task_events (task_id, created_at);

CREATE INDEX IF NOT EXISTS ix_prod_event_shot
    ON pack_content.production_task_events (shot_code, created_at DESC);

ALTER TABLE pack_content.production_tasks
    DROP CONSTRAINT IF EXISTS fk_prod_task_snapshot;
ALTER TABLE pack_content.production_tasks
    ADD CONSTRAINT fk_prod_task_snapshot
    FOREIGN KEY (input_snapshot_id)
    REFERENCES pack_content.production_task_input_snapshots (snapshot_id)
    ON DELETE RESTRICT;

CREATE OR REPLACE FUNCTION pack_content.fn_prod_snapshot_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'PRODUCTION_SNAPSHOT_LOCKED: input snapshot is immutable';
  END IF;
  IF NEW.snapshot_hash IS DISTINCT FROM OLD.snapshot_hash
     OR NEW.story IS DISTINCT FROM OLD.story
     OR NEW.compiler_output IS DISTINCT FROM OLD.compiler_output
     OR NEW.keyframe_hash IS DISTINCT FROM OLD.keyframe_hash THEN
    RAISE EXCEPTION 'PRODUCTION_SNAPSHOT_LOCKED: input snapshot is immutable';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_prod_snapshot_immutable ON pack_content.production_task_input_snapshots;
CREATE TRIGGER trg_prod_snapshot_immutable
  BEFORE UPDATE OR DELETE ON pack_content.production_task_input_snapshots
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_prod_snapshot_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_prod_event_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'PRODUCTION_EVENT_LOCKED: audit event is immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_prod_event_immutable ON pack_content.production_task_events;
CREATE TRIGGER trg_prod_event_immutable
  BEFORE UPDATE OR DELETE ON pack_content.production_task_events
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_prod_event_immutable();

CREATE OR REPLACE FUNCTION pack_content.fn_prod_artifact_bytes_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'PRODUCTION_ARTIFACT_LOCKED: physical artifact is immutable';
  END IF;
  IF OLD.verification_status = 'VERIFIED' AND (
        NEW.byte_sha256 IS DISTINCT FROM OLD.byte_sha256
     OR NEW.storage_key IS DISTINCT FROM OLD.storage_key
     OR NEW.size_bytes IS DISTINCT FROM OLD.size_bytes) THEN
    RAISE EXCEPTION 'PRODUCTION_ARTIFACT_LOCKED: verified bytes are immutable';
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_prod_artifact_bytes_immutable ON pack_content.production_artifacts;
CREATE TRIGGER trg_prod_artifact_bytes_immutable
  BEFORE UPDATE OR DELETE ON pack_content.production_artifacts
  FOR EACH ROW EXECUTE FUNCTION pack_content.fn_prod_artifact_bytes_immutable();

COMMENT ON TABLE pack_content.production_requests IS
    'FAMIXA Work Execution V1. User request. Action cannot silently remap.';
COMMENT ON TABLE pack_content.production_tasks IS
    'One executable action. Provider SUCCESS is not production SUCCESS.';
COMMENT ON TABLE pack_content.production_task_input_snapshots IS
    'Frozen story/acting/visual/compiler hashes. Compiler must not drop story.';
COMMENT ON TABLE pack_content.production_attempts IS
    'Provider job identity only. Late results stay on this attempt.';
COMMENT ON TABLE pack_content.production_artifacts IS
    'Physical identity = byte_sha256. FileId/hash(taskId) is not content identity.';
COMMENT ON TABLE pack_content.production_bindings IS
    'CURRENT / ACCEPTED / VISIBLE are separate active roles per build+shot.';
COMMENT ON TABLE pack_content.production_task_events IS
    'Immutable audit trail. Archaeology should not be required.';

DO $$
DECLARE
  r text;
  t text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      FOREACH t IN ARRAY ARRAY[
        'production_requests', 'production_tasks', 'production_task_input_snapshots',
        'production_attempts', 'production_artifacts', 'production_bindings',
        'production_task_events', 'production_film_assemblies', 'production_film_assembly_shots'
      ] LOOP
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.%I TO %I', t, r);
      END LOOP;
    END IF;
  END LOOP;
END $$;
