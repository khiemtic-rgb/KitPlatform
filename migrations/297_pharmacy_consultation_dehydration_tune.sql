-- KitPlatform 297: Dehydration screening — caution + ORS suggest (not hard refer_medical block)
-- Manifest: deploy/ubuntu/migration-files.prod.txt
-- Depends on: 296_pharmacy_symptom_taxonomy_seed.sql

INSERT INTO pharmacy_consultation_risk_flag (id, code, name_vi, severity, action, message_vi, safety_level, sort_order)
VALUES (
    '8f3c2a1b-4d5e-6f70-8192-a3b4c5d6e7f8',
    'dehydration_screening',
    'Dấu hiệu mất nước',
    'caution',
    'caution',
    'Dấu hiệu mất nước — hỏi thêm mức độ; có thể gợi ý bù nước / Oresol OTC nếu nhẹ.',
    'caution',
    52
)
ON CONFLICT (code) DO UPDATE SET
    name_vi = EXCLUDED.name_vi,
    severity = EXCLUDED.severity,
    action = EXCLUDED.action,
    message_vi = EXCLUDED.message_vi,
    safety_level = EXCLUDED.safety_level,
    sort_order = EXCLUDED.sort_order,
    is_active = TRUE,
    updated_at = NOW();

UPDATE pharmacy_symptom_risk_rule sr
SET risk_flag_id = rf_new.id,
    updated_at = NOW()
FROM pharmacy_symptom s,
     pharmacy_consultation_risk_flag rf_new
WHERE sr.symptom_id = s.id
  AND s.code = 'dehydration_signs'
  AND sr.is_active = TRUE
  AND rf_new.code = 'dehydration_screening';

UPDATE pharmacy_symptom
SET consultation_mode = 'otc_assist',
    updated_at = NOW()
WHERE code = 'dehydration_signs';

INSERT INTO pharmacy_knowledge_rule (id, rule_code, symptom_id, category_codes, keywords, reason_vi, priority)
SELECT
    'c4e8f2a0-1b3d-4c5e-9f6a-7b8c9d0e1f2a'::uuid,
    'KR_DEHYDRATION',
    s.id,
    ARRAY['DA_DAY', 'VITAMIN'],
    ARRAY['oresol', 'rehydration', 'ors', 'bù nước', 'dien giai', 'điện giải', 'mat nuoc', 'mất nước', 'oral rehydration'],
    'Mất nước — bù nước / điện giải OTC',
    235
FROM pharmacy_symptom s
WHERE s.code = 'dehydration_signs'
ON CONFLICT (rule_code) DO UPDATE SET
    symptom_id = EXCLUDED.symptom_id,
    category_codes = EXCLUDED.category_codes,
    keywords = EXCLUDED.keywords,
    reason_vi = EXCLUDED.reason_vi,
    priority = EXCLUDED.priority,
    is_active = TRUE,
    updated_at = NOW();

INSERT INTO kit_schema_migrations (filename) VALUES ('297_pharmacy_consultation_dehydration_tune.sql')
ON CONFLICT (filename) DO NOTHING;
