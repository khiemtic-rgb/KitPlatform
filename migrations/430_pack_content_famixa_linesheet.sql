-- KitPlatform 430: Famixa M1 "Dựng tập từ bảng câu" (linesheet → video nháp).
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Do not merge into Pharmacy prod.
-- Precondition: 002 (public.tenants), schema pack_content.
-- Postcondition: 6 bảng pack_content.linesheet_*; mọi bảng có tenant_id NOT NULL (ADR-03, PRODUCTION-TENANT-BOUNDARY-01).
-- Bảng riêng, worker riêng: không dùng work_job, không chạm bảng production_* hay video_* sẵn có.
-- Idempotent. Không có bản hạ cấp.

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.linesheet_production (
    id             UUID PRIMARY KEY,
    tenant_id      UUID NOT NULL REFERENCES public.tenants (id),
    universe_code  VARCHAR(64) NOT NULL,
    episode_code   VARCHAR(32) NOT NULL,
    title          VARCHAR(200) NOT NULL DEFAULT '',
    settings_json  JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_linesheet_production_code UNIQUE (tenant_id, universe_code, episode_code),
    CONSTRAINT uq_linesheet_production_id_tenant UNIQUE (id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_content.linesheet_version (
    id             UUID PRIMARY KEY,
    tenant_id      UUID NOT NULL REFERENCES public.tenants (id),
    production_id  UUID NOT NULL,
    version_no     INT NOT NULL,
    source_sha256  CHAR(64) NOT NULL,
    rows_json      JSONB NOT NULL,
    row_count      INT NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_linesheet_version_production FOREIGN KEY (production_id, tenant_id)
        REFERENCES pack_content.linesheet_production (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT uq_linesheet_version_no UNIQUE (production_id, version_no),
    CONSTRAINT uq_linesheet_version_id_tenant UNIQUE (id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_content.linesheet_file (
    id             UUID PRIMARY KEY,
    tenant_id      UUID NOT NULL REFERENCES public.tenants (id),
    production_id  UUID NOT NULL,
    kind           VARCHAR(24) NOT NULL,
    file_key       VARCHAR(128) NOT NULL,
    storage_key    VARCHAR(400) NOT NULL,
    sha256         CHAR(64) NOT NULL,
    size_bytes     BIGINT NOT NULL,
    duration_ms    INT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_linesheet_file_production FOREIGN KEY (production_id, tenant_id)
        REFERENCES pack_content.linesheet_production (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT uq_linesheet_file_key UNIQUE (production_id, kind, file_key),
    CONSTRAINT ck_linesheet_file_kind CHECK (kind IN ('line_audio', 'cue_music', 'cue_sfx', 'layer_bg', 'layer_mid', 'character'))
);

CREATE TABLE IF NOT EXISTS pack_content.linesheet_timeline (
    id                 UUID PRIMARY KEY,
    tenant_id          UUID NOT NULL REFERENCES public.tenants (id),
    production_id      UUID NOT NULL,
    version_id         UUID NOT NULL,
    audio_set_sha256   CHAR(64) NOT NULL,
    entries_json       JSONB NOT NULL,
    estimated_json     JSONB NOT NULL DEFAULT '[]'::jsonb,
    total_ms           INT NOT NULL,
    audio_storage_key  VARCHAR(400) NULL,
    audio_duration_ms  INT NULL,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_linesheet_timeline_production FOREIGN KEY (production_id, tenant_id)
        REFERENCES pack_content.linesheet_production (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT fk_linesheet_timeline_version FOREIGN KEY (version_id, tenant_id)
        REFERENCES pack_content.linesheet_version (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT uq_linesheet_timeline_id_tenant UNIQUE (id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_content.linesheet_shot_set (
    id             UUID PRIMARY KEY,
    tenant_id      UUID NOT NULL REFERENCES public.tenants (id),
    production_id  UUID NOT NULL,
    version_id     UUID NOT NULL,
    shots_json     JSONB NOT NULL,
    shot_count     INT NOT NULL,
    edited         BOOLEAN NOT NULL DEFAULT FALSE,
    created_by     VARCHAR(120) NOT NULL DEFAULT '',
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_linesheet_shot_set_production FOREIGN KEY (production_id, tenant_id)
        REFERENCES pack_content.linesheet_production (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT fk_linesheet_shot_set_version FOREIGN KEY (version_id, tenant_id)
        REFERENCES pack_content.linesheet_version (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT uq_linesheet_shot_set_id_tenant UNIQUE (id, tenant_id)
);

CREATE TABLE IF NOT EXISTS pack_content.linesheet_render_run (
    id                  UUID PRIMARY KEY,
    tenant_id           UUID NOT NULL REFERENCES public.tenants (id),
    production_id       UUID NOT NULL,
    version_id          UUID NOT NULL,
    timeline_id         UUID NOT NULL,
    shot_set_id         UUID NOT NULL,
    kind                VARCHAR(16) NOT NULL DEFAULT 'episode',
    format              VARCHAR(8) NOT NULL,
    width               INT NOT NULL,
    height              INT NOT NULL,
    fps                 INT NOT NULL,
    range_start_ms      INT NOT NULL,
    range_end_ms        INT NOT NULL,
    text_lang           VARCHAR(4) NOT NULL DEFAULT 'vi',
    burn_subs           VARCHAR(8) NOT NULL DEFAULT 'none',
    status              VARCHAR(16) NOT NULL DEFAULT 'QUEUED',
    next_step           INT NOT NULL DEFAULT 0,
    step_count          INT NOT NULL,
    attempts            INT NOT NULL DEFAULT 0,
    frames_expected     INT NULL,
    frames_actual       INT NULL,
    shot_log_json       JSONB NOT NULL DEFAULT '[]'::jsonb,
    output_storage_key  VARCHAR(400) NULL,
    srt_vi_storage_key  VARCHAR(400) NULL,
    srt_en_storage_key  VARCHAR(400) NULL,
    error               VARCHAR(1000) NULL,
    created_by          VARCHAR(120) NOT NULL DEFAULT '',
    available_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    heartbeat_at        TIMESTAMPTZ NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at          TIMESTAMPTZ NULL,
    completed_at        TIMESTAMPTZ NULL,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_linesheet_run_production FOREIGN KEY (production_id, tenant_id)
        REFERENCES pack_content.linesheet_production (id, tenant_id) ON DELETE CASCADE,
    CONSTRAINT fk_linesheet_run_version FOREIGN KEY (version_id, tenant_id)
        REFERENCES pack_content.linesheet_version (id, tenant_id),
    CONSTRAINT fk_linesheet_run_timeline FOREIGN KEY (timeline_id, tenant_id)
        REFERENCES pack_content.linesheet_timeline (id, tenant_id),
    CONSTRAINT fk_linesheet_run_shot_set FOREIGN KEY (shot_set_id, tenant_id)
        REFERENCES pack_content.linesheet_shot_set (id, tenant_id),
    CONSTRAINT ck_linesheet_run_status CHECK (status IN ('QUEUED', 'RUNNING', 'SUCCEEDED', 'FAILED', 'CANCELLED')),
    CONSTRAINT ck_linesheet_run_format CHECK (format IN ('16x9', '9x16')),
    CONSTRAINT ck_linesheet_run_burn CHECK (burn_subs IN ('none', 'vi', 'en')),
    CONSTRAINT ck_linesheet_run_range CHECK (range_end_ms > range_start_ms)
);

CREATE INDEX IF NOT EXISTS ix_linesheet_version_latest
    ON pack_content.linesheet_version (tenant_id, production_id, version_no DESC);
CREATE INDEX IF NOT EXISTS ix_linesheet_timeline_latest
    ON pack_content.linesheet_timeline (tenant_id, production_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_linesheet_shot_set_latest
    ON pack_content.linesheet_shot_set (tenant_id, production_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_linesheet_run_production
    ON pack_content.linesheet_render_run (tenant_id, production_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_linesheet_run_pending
    ON pack_content.linesheet_render_run (available_at)
    WHERE status IN ('QUEUED', 'RUNNING');

COMMIT;
