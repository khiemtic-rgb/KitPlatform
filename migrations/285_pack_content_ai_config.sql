-- KitPlatform 285: Content Park — AI write/image config (models + secret_ref / write-only key)
-- Manifest: deploy/ubuntu/migration-files.content.txt only

ALTER TABLE pack_content.org_settings
    ADD COLUMN IF NOT EXISTS ai_config_json JSONB NOT NULL DEFAULT '{
        "provider":"gemini",
        "textModel":"gemini-flash-latest",
        "imageModel":"",
        "imagesEnabled":true,
        "geminiApiKeySecretRef":"GEMINI_API_KEY"
    }'::jsonb;

COMMENT ON COLUMN pack_content.org_settings.ai_config_json IS
    'Content AI knobs: models, imagesEnabled, geminiApiKeySecretRef; optional geminiApiKey (write-only, never expose via API read).';

UPDATE pack_content.org_settings
SET ai_config_json = COALESCE(ai_config_json, '{}'::jsonb) || '{
    "provider":"gemini",
    "textModel":"gemini-flash-latest",
    "imageModel":"",
    "imagesEnabled":true,
    "geminiApiKeySecretRef":"GEMINI_API_KEY"
}'::jsonb
WHERE ai_config_json = '{}'::jsonb
   OR NOT (ai_config_json ? 'textModel');
