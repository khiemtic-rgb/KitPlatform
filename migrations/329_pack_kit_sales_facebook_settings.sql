-- KitPlatform 329: kit_sales Facebook Page settings + messenger PSID uniqueness
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- Stores Novixa Page token on KIT_SALES. Does not crawl Facebook or reuse KIT_MKT OAuth.

CREATE TABLE IF NOT EXISTS pack_sales.facebook_settings (
    tenant_id           UUID PRIMARY KEY,
    enabled             BOOLEAN NOT NULL DEFAULT FALSE,
    page_id             TEXT,
    page_access_token   TEXT,
    verify_token        TEXT,
    app_secret          TEXT,
    me_link             TEXT,
    page_url            TEXT,
    phc_url             TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE pack_sales.facebook_settings IS
    'KIT Sales — Novixa Fanpage token/webhook. One row per tenant. Not staff personal Facebook.';

CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_identity_messenger_psid
    ON pack_sales.business_identity (tenant_id, username)
    WHERE identity_type = 'facebook'
      AND platform = 'messenger'
      AND username IS NOT NULL
      AND btrim(username) <> '';

INSERT INTO kit_schema_migrations (filename) VALUES ('329_pack_kit_sales_facebook_settings.sql')
ON CONFLICT (filename) DO NOTHING;
