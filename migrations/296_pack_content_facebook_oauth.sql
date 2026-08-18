-- KitPlatform 296: Content Park — Facebook Login app knobs (write-only secret)
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Page tokens stay on channel_target.config_json (storedSecret). No SocialConnection table.

ALTER TABLE pack_content.org_settings
    ADD COLUMN IF NOT EXISTS facebook_config_json JSONB NOT NULL DEFAULT '{
        "appIdSecretRef":"FACEBOOK_APP_ID",
        "appSecretSecretRef":"FACEBOOK_APP_SECRET",
        "redirectUri":"http://localhost:5173/content/facebook/callback"
    }'::jsonb;

COMMENT ON COLUMN pack_content.org_settings.facebook_config_json IS
    'Meta app for KIT Marketing OAuth: appId / write-only appSecret / secret refs / redirectUri. Secrets never exposed via API read.';
