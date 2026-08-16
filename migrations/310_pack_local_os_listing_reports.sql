-- Local OS: độc giả báo tin sai số / hết phòng. Isolated park.
-- Không tự ẩn tin từ một báo cáo ẩn danh.

CREATE TABLE IF NOT EXISTS pack_local.listing_report (
    id          UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    listing_id  UUID NOT NULL REFERENCES pack_local.listing(id) ON DELETE CASCADE,
    reason      VARCHAR(32) NOT NULL,
    note        TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_local_listing_report_reason
        CHECK (reason IN ('wrong_phone', 'gone', 'no_answer', 'other'))
);

CREATE INDEX IF NOT EXISTS ix_local_listing_report_listing
    ON pack_local.listing_report (listing_id, created_at DESC);
