-- A3 Smart Refill push copy + A4 reservation ← repurchase link

ALTER TABLE customer_reservations
    ADD COLUMN IF NOT EXISTS source_repurchase_suggestion_id UUID
        REFERENCES repurchase_suggestions(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_customer_reservations_source_repurchase
    ON customer_reservations (tenant_id, source_repurchase_suggestion_id)
    WHERE source_repurchase_suggestion_id IS NOT NULL;

COMMENT ON COLUMN customer_reservations.source_repurchase_suggestion_id IS
    'Smart Refill: reservation created from repurchase_suggestions via Đặt lại';

-- Refresh push copy for tenants that already have overrides (code defaults cover the rest).
UPDATE tenant_string_translations
SET translated_value = 'Còn thuốc không?',
    updated_at = NOW()
WHERE translation_key = 'customer.notify.repurchase.title'
  AND locale_code = 'vi-VN';

UPDATE tenant_string_translations
SET translated_value = '{orderLabel} — dự kiến hết khoảng {dateLabel}. Mở app để đặt lại nhanh.',
    updated_at = NOW()
WHERE translation_key = 'customer.notify.repurchase.body'
  AND locale_code = 'vi-VN';

UPDATE tenant_string_translations
SET translated_value = 'Still have enough medicine?',
    updated_at = NOW()
WHERE translation_key = 'customer.notify.repurchase.title'
  AND locale_code = 'en-US';

UPDATE tenant_string_translations
SET translated_value = '{orderLabel} — expected to run out around {dateLabel}. Open the app to reorder.',
    updated_at = NOW()
WHERE translation_key = 'customer.notify.repurchase.body'
  AND locale_code = 'en-US';

-- Ensure DEMO (and any tenant without rows) get the new strings.
INSERT INTO tenant_string_translations (tenant_id, translation_key, locale_code, translated_value)
SELECT t.id, v.key, v.locale, v.value
FROM tenants t
CROSS JOIN (
    VALUES
        ('customer.notify.repurchase.title', 'en-US', 'Still have enough medicine?'),
        ('customer.notify.repurchase.body', 'en-US', '{orderLabel} — expected to run out around {dateLabel}. Open the app to reorder.'),
        ('customer.notify.repurchase.title', 'vi-VN', 'Còn thuốc không?'),
        ('customer.notify.repurchase.body', 'vi-VN', '{orderLabel} — dự kiến hết khoảng {dateLabel}. Mở app để đặt lại nhanh.')
) AS v(key, locale, value)
WHERE t.deleted_at IS NULL
ON CONFLICT (tenant_id, translation_key, locale_code)
DO UPDATE SET translated_value = EXCLUDED.translated_value, updated_at = NOW();
