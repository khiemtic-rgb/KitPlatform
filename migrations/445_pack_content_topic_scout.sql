-- KitPlatform 445: phiếu chủ đề do agent quét YouTube (phần Việt và phần Anh).
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Không gộp Pharmacy / Family / Local OS / Sales.
-- Chỉ lưu phiếu chờ người chọn. Không viết bài, không dựng phim, không đăng.
-- Precondition: pack_content.content_package (287).
-- Chạy lại an toàn (IF NOT EXISTS).

BEGIN;

CREATE TABLE IF NOT EXISTS pack_content.topic_scout_proposal (
    id             UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    lane           VARCHAR(2) NOT NULL,
    cluster_key    VARCHAR(160) NOT NULL,
    title          VARCHAR(200) NOT NULL,
    brief          TEXT NOT NULL,
    evidence_json  JSONB NOT NULL DEFAULT '[]'::jsonb,
    status         VARCHAR(16) NOT NULL DEFAULT 'pending',
    package_id     UUID REFERENCES pack_content.content_package(id) ON DELETE SET NULL,
    scanned_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_content_topic_scout_lane CHECK (lane IN ('vi', 'en')),
    CONSTRAINT ck_content_topic_scout_status CHECK (status IN ('pending', 'accepted', 'dismissed')),
    CONSTRAINT uq_content_topic_scout_cluster UNIQUE (lane, cluster_key)
);

CREATE INDEX IF NOT EXISTS ix_content_topic_scout_status
    ON pack_content.topic_scout_proposal (status, scanned_at DESC);

COMMENT ON TABLE pack_content.topic_scout_proposal IS
    'Phiếu chủ đề từ bảng video thịnh hành. Người đưa vào pool thì mới thành ý tưởng. Agent không tự viết và không tự dựng.';

COMMIT;
