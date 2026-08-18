-- KitPlatform 297: persist Facebook OAuth pending (survives API restart)
-- Manifest: deploy/ubuntu/migration-files.content.txt only

CREATE TABLE IF NOT EXISTS pack_content.facebook_oauth_pending (
    id text PRIMARY KEY,
    kind text NOT NULL,
    brand_id uuid NOT NULL REFERENCES pack_content.brand (id) ON DELETE CASCADE,
    redirect_uri text,
    pages_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT facebook_oauth_pending_kind_chk CHECK (kind IN ('state', 'session'))
);

CREATE INDEX IF NOT EXISTS ix_pack_content_fb_oauth_pending_exp
    ON pack_content.facebook_oauth_pending (expires_at);

COMMENT ON TABLE pack_content.facebook_oauth_pending IS
    'Short-lived Facebook Login state/session. Page tokens only until operator picks a Page (15 min).';
