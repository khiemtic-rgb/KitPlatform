-- 289: Nhật ký đồng bộ giao dịch CSDL dược (TTYQG API v2) — stock-out từ bán lẻ
-- Additive only.

CREATE TABLE IF NOT EXISTS csdl_duoc_sync_log (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               UUID         NOT NULL REFERENCES tenants(id),
    sales_order_id          UUID         NOT NULL,
    order_number            VARCHAR(50),
    direction               VARCHAR(20)  NOT NULL DEFAULT 'stock-out',
    status                  VARCHAR(30)  NOT NULL,
    remote_transaction_id   VARCHAR(80),
    remote_status           VARCHAR(40),
    line_count              INT          NOT NULL DEFAULT 0,
    skipped_line_count      INT          NOT NULL DEFAULT 0,
    request_json            JSONB,
    response_json           JSONB,
    error_message           TEXT,
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_csdl_duoc_sync_order UNIQUE (tenant_id, sales_order_id, direction)
);

CREATE INDEX IF NOT EXISTS ix_csdl_duoc_sync_log_tenant_created
    ON csdl_duoc_sync_log (tenant_id, created_at DESC);

COMMENT ON TABLE csdl_duoc_sync_log IS
    'Audit / idempotency for TTYQG CSDL dược transaction sync (stock-out, later stock-in/taking).';
