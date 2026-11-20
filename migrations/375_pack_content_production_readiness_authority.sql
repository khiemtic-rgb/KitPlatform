-- KitPlatform 375: Production OS readiness authority — AI selection + one-shot permit
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Durable ProductionAiSelection and ProductionExecutionPermit.
-- Does not rewrite series graph_json. Does not invent timing.
-- Does not execute CREATE_VIDEO. Does not call providers.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.production_ai_selection (
    selection_id     UUID PRIMARY KEY,
    build_id         UUID NOT NULL,
    shot_code        VARCHAR(80) NOT NULL,
    episode_code     VARCHAR(32) NOT NULL DEFAULT '',
    scene_code       VARCHAR(32) NOT NULL DEFAULT '',
    stage            VARCHAR(32) NOT NULL,
    provider_id      VARCHAR(64) NOT NULL,
    model_id         VARCHAR(80) NOT NULL,
    selection_mode   VARCHAR(32) NOT NULL,
    selected_by      VARCHAR(120) NOT NULL DEFAULT '',
    selected_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason           TEXT NOT NULL DEFAULT '',
    status           VARCHAR(24) NOT NULL DEFAULT 'ACTIVE',
    selection_hash   VARCHAR(96) NOT NULL,
    CONSTRAINT ck_prod_ai_sel_stage CHECK (stage IN ('VIDEO', 'IMAGE', 'VOICE', 'LIPSYNC', 'MIX')),
    CONSTRAINT ck_prod_ai_sel_mode CHECK (selection_mode IN ('EXPLICIT')),
    CONSTRAINT ck_prod_ai_sel_status CHECK (status IN ('ACTIVE', 'SUPERSEDED'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_ai_sel_active
    ON pack_content.production_ai_selection (build_id, shot_code, stage)
    WHERE status = 'ACTIVE';

CREATE INDEX IF NOT EXISTS ix_prod_ai_sel_scope
    ON pack_content.production_ai_selection (build_id, shot_code, stage);

COMMENT ON TABLE pack_content.production_ai_selection IS
    'Production OS HOW selection. Not graph_json.aiSelection. Not router remap.';

CREATE TABLE IF NOT EXISTS pack_content.production_execution_permit (
    permit_id        UUID PRIMARY KEY,
    build_id         UUID NOT NULL,
    shot_code        VARCHAR(80) NOT NULL,
    episode_code     VARCHAR(32) NOT NULL DEFAULT '',
    scene_code       VARCHAR(32) NOT NULL DEFAULT '',
    action           VARCHAR(40) NOT NULL,
    provider_id      VARCHAR(64) NOT NULL,
    model_id         VARCHAR(80) NOT NULL,
    status           VARCHAR(24) NOT NULL DEFAULT 'ISSUED',
    issued_by        VARCHAR(120) NOT NULL DEFAULT '',
    issued_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    consumed_at      TIMESTAMPTZ,
    reason           TEXT NOT NULL DEFAULT '',
    permit_hash      VARCHAR(96) NOT NULL,
    CONSTRAINT ck_prod_exec_permit_status CHECK (status IN ('ISSUED', 'CONSUMED', 'EXPIRED', 'REVOKED'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_prod_exec_permit_issued
    ON pack_content.production_execution_permit (build_id, shot_code, action, provider_id, model_id)
    WHERE status = 'ISSUED';

CREATE INDEX IF NOT EXISTS ix_prod_exec_permit_scope
    ON pack_content.production_execution_permit (build_id, shot_code, action);

COMMENT ON TABLE pack_content.production_execution_permit IS
    'One-shot official-film paid execution authorization. Evaluate/GET/prepare must not consume.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.production_ai_selection TO %I', r);
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON pack_content.production_execution_permit TO %I', r);
    END IF;
  END LOOP;
END $$;

COMMIT;
