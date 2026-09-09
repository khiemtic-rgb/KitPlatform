-- KitPlatform 327: kit_sales Facebook identity (profile vs page) + province fold
-- Manifest: deploy/ubuntu/migration-files.kit-sales.txt ONLY
-- Does not crawl Facebook. Labels URLs already stored on KIT_SALES leads.

UPDATE pack_sales.business
SET province = 'Thái Nguyên'
WHERE tenant_id = '11111111-1111-1111-1111-111111111107'
  AND province IS NOT NULL
  AND (
      province ILIKE '%thái nguyên%'
      OR province ILIKE '%thai nguyen%'
  )
  AND province IS DISTINCT FROM 'Thái Nguyên';

INSERT INTO pack_sales.business_identity (
    tenant_id, business_id, identity_type, platform, url, username, source
)
SELECT
    b.tenant_id,
    b.id,
    'facebook',
    CASE
        WHEN COALESCE(b.source, l.source) ILIKE '%profile.php%' THEN 'profile'
        WHEN COALESCE(b.source, l.source) ILIKE '%/people/%' THEN 'profile'
        WHEN COALESCE(b.source, l.source) ILIKE '%/pages/%' THEN 'page'
        WHEN COALESCE(b.source, l.source) ILIKE '%nha.thuoc%' THEN 'page'
        WHEN COALESCE(b.source, l.source) ILIKE '%nhathuoc%' THEN 'page'
        ELSE 'profile'
    END,
    COALESCE(b.source, l.source),
    NULL,
    'lead_source'
FROM pack_sales.business b
INNER JOIN pack_sales.lead l
    ON l.business_id = b.id AND l.tenant_id = b.tenant_id
WHERE b.tenant_id = '11111111-1111-1111-1111-111111111107'
  AND (
      COALESCE(b.source, l.source) ILIKE '%facebook.com%'
      OR COALESCE(b.source, l.source) ILIKE '%fb.com/%'
  )
  AND NOT EXISTS (
      SELECT 1
      FROM pack_sales.business_identity i
      WHERE i.business_id = b.id
        AND i.tenant_id = b.tenant_id
        AND i.identity_type = 'facebook'
        AND i.platform IN ('profile', 'page')
  );

INSERT INTO kit_schema_migrations (filename) VALUES ('327_pack_kit_sales_facebook_identity.sql')
ON CONFLICT (filename) DO NOTHING;
