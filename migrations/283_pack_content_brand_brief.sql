-- KitPlatform 283: Content Park — brand operational brief (AI context)
-- Manifest: deploy/ubuntu/migration-files.content.txt only

ALTER TABLE pack_content.brand
    ADD COLUMN IF NOT EXISTS operational_brief TEXT;

COMMENT ON COLUMN pack_content.brand.operational_brief IS
    'Long-form ops brief (voice, themes, forbidden claims, CTA rules) pasted from strategy docs / ChatGPT — injected into AI generate.';
