-- KitPlatform 294: Gemini 2.5-flash retired for new keys → 3.6-flash
-- Manifest: deploy/ubuntu/migration-files.content.txt only

UPDATE pack_content.org_settings
SET
    ai_config_json = jsonb_set(
        COALESCE(ai_config_json, '{}'::jsonb),
        '{textModel}',
        '"gemini-3.6-flash"'
    ),
    updated_at = NOW()
WHERE COALESCE(ai_config_json->>'textModel', '') IN (
    'gemini-2.5-flash',
    'models/gemini-2.5-flash'
);
