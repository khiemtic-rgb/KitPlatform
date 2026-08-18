-- KitPlatform 295: Content Park — Creatomate + ElevenLabs (write-only keys)
-- Manifest: deploy/ubuntu/migration-files.content.txt only

ALTER TABLE pack_content.org_settings
    ADD COLUMN IF NOT EXISTS video_config_json JSONB NOT NULL DEFAULT '{
        "creatomateApiKeySecretRef":"CREATOMATE_API_KEY",
        "elevenLabsApiKeySecretRef":"ELEVENLABS_API_KEY",
        "elevenLabsVoiceId":"",
        "publicMediaBaseUrl":"",
        "creatomateTemplateId":""
    }'::jsonb;

COMMENT ON COLUMN pack_content.org_settings.video_config_json IS
    'Video knobs: Creatomate/ElevenLabs secret refs + optional write-only keys, voice id, publicMediaBaseUrl, creatomateTemplateId. Keys never exposed via API read.';

UPDATE pack_content.org_settings
SET video_config_json = COALESCE(video_config_json, '{}'::jsonb) || '{
    "creatomateApiKeySecretRef":"CREATOMATE_API_KEY",
    "elevenLabsApiKeySecretRef":"ELEVENLABS_API_KEY"
}'::jsonb
WHERE video_config_json = '{}'::jsonb
   OR NOT (video_config_json ? 'creatomateApiKeySecretRef');
