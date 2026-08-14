-- 292: Cấu hình liên thông CSDL dược theo nhà thuốc (tenant)
-- Additive only.

CREATE TABLE IF NOT EXISTS tenant_csdl_duoc_link (
    tenant_id                 UUID PRIMARY KEY REFERENCES tenants(id),
    enabled                   BOOLEAN      NOT NULL DEFAULT FALSE,
    environment               VARCHAR(20)  NOT NULL DEFAULT 'sandbox',
    username                  VARCHAR(100),
    password                  TEXT,
    practice_license_code     VARCHAR(50),
    enable_stock_out_sync     BOOLEAN      NOT NULL DEFAULT FALSE,
    enable_stock_in_sync      BOOLEAN      NOT NULL DEFAULT FALSE,
    status                    VARCHAR(30)  NOT NULL DEFAULT 'NotConfigured',
    last_check_at             TIMESTAMPTZ,
    last_error                TEXT,
    connected_at              TIMESTAMPTZ,
    created_at                TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at                TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_by                UUID,
    CONSTRAINT ck_tenant_csdl_duoc_env
        CHECK (environment IN ('sandbox', 'live')),
    CONSTRAINT ck_tenant_csdl_duoc_status
        CHECK (status IN ('NotConfigured', 'Configured', 'Connected', 'Error', 'Disabled'))
);

COMMENT ON TABLE tenant_csdl_duoc_link IS
    'Per-tenant TTYQG CSDL dược API credentials and linkage status. When Connected, catalog/sync use this account; otherwise fall back to platform sandbox.';

COMMENT ON COLUMN tenant_csdl_duoc_link.password IS
    'Write-only at API layer; stored plaintext like other tenant secrets until DataProtection is wired.';
