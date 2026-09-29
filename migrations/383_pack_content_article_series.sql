-- KitPlatform 370: Article Content Series Engine V1
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Does NOT touch series_pilot / series_build / video_story_graph / Famixa video.

CREATE TABLE IF NOT EXISTS pack_content.content_series (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    brand_id                UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
    source_package_id       UUID NOT NULL REFERENCES pack_content.content_package(id) ON DELETE RESTRICT,
    code                    VARCHAR(64) NOT NULL,
    name                    VARCHAR(500) NOT NULL,
    description             TEXT,
    objective               TEXT,
    audience                VARCHAR(400),
    core_message            TEXT,
    status                  VARCHAR(32) NOT NULL DEFAULT 'DRAFT',
    episode_count           INT NOT NULL DEFAULT 10,
    current_episode_no      INT NOT NULL DEFAULT 1,
    start_date              DATE,
    end_date                DATE,
    blueprint_json          JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_series_code UNIQUE (code),
    CONSTRAINT ck_content_series_status CHECK (status IN (
        'DRAFT', 'PLANNED', 'ACTIVE', 'PAUSED', 'COMPLETED', 'CANCELLED'
    )),
    CONSTRAINT ck_content_series_episode_count CHECK (episode_count IN (5, 10, 20, 30, 50, 100)),
    CONSTRAINT ck_content_series_current CHECK (current_episode_no >= 1)
);

CREATE INDEX IF NOT EXISTS ix_content_series_brand_status
    ON pack_content.content_series (brand_id, status, updated_at DESC);

CREATE INDEX IF NOT EXISTS ix_content_series_source
    ON pack_content.content_series (source_package_id, updated_at DESC);

COMMENT ON TABLE pack_content.content_series IS
    'Article/social Content Series (strategy). Not Famixa video series_pilot / series_build.';

CREATE TABLE IF NOT EXISTS pack_content.content_episode (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    series_id               UUID NOT NULL REFERENCES pack_content.content_series(id) ON DELETE CASCADE,
    episode_no              INT NOT NULL,
    code                    VARCHAR(80) NOT NULL,
    title                   VARCHAR(500) NOT NULL,
    objective               TEXT,
    angle                   TEXT,
    key_message             TEXT,
    status                  VARCHAR(32) NOT NULL DEFAULT 'PLANNED',
    planned_at              TIMESTAMPTZ,
    published_at            TIMESTAMPTZ,
    previous_episode_id     UUID REFERENCES pack_content.content_episode(id) ON DELETE SET NULL,
    next_episode_id         UUID REFERENCES pack_content.content_episode(id) ON DELETE SET NULL,
    continuity_json         JSONB NOT NULL DEFAULT '{}'::jsonb,
    content_topic_id        UUID REFERENCES pack_content.topic(id) ON DELETE SET NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_content_episode_series_no UNIQUE (series_id, episode_no),
    CONSTRAINT uq_content_episode_code UNIQUE (code),
    CONSTRAINT uq_content_episode_topic UNIQUE (content_topic_id),
    CONSTRAINT ck_content_episode_no CHECK (episode_no >= 1),
    CONSTRAINT ck_content_episode_status CHECK (status IN (
        'PLANNED', 'BRIEFING', 'READY', 'GENERATING', 'REVIEW',
        'APPROVED', 'SCHEDULED', 'PUBLISHED', 'ANALYZED', 'PAUSED', 'CANCELLED'
    ))
);

CREATE INDEX IF NOT EXISTS ix_content_episode_series_no
    ON pack_content.content_episode (series_id, episode_no);

CREATE INDEX IF NOT EXISTS ix_content_episode_topic
    ON pack_content.content_episode (content_topic_id)
    WHERE content_topic_id IS NOT NULL;

COMMENT ON TABLE pack_content.content_episode IS
    'Article Series episode (plan). Primary topic is optional until generate. Video engine unchanged.';
