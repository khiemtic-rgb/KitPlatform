-- KitPlatform 298: Ho / khàn tiếng — OTC assist + knowledge rules
-- Manifest: deploy/ubuntu/migration-files.prod.txt
-- Depends on: 296_pharmacy_symptom_taxonomy_seed.sql

UPDATE pharmacy_symptom
SET consultation_mode = 'otc_assist',
    updated_at = NOW()
WHERE code IN ('voice_hoarseness', 'throat_clearing');

INSERT INTO pharmacy_knowledge_rule (id, rule_code, symptom_id, category_codes, keywords, reason_vi, priority)
SELECT
    'd1e2f3a4-b5c6-7890-d1e2-f3a4b5c67890'::uuid,
    'KR_HOARSENESS',
    s.id,
    ARRAY['HO_HAP', 'GIAM_DAU'],
    ARRAY['prospan', 'acc', 'acetylcysteine', 'decolgen', 'cam cum', 'khan tieng', 'khàn tiếng', 'ho khan', 'dau hong', 'đau họng', 'long dom', 'long đờm'],
    'Khàn tiếng / ho — long đờm / giảm ho OTC',
    145
FROM pharmacy_symptom s
WHERE s.code = 'voice_hoarseness'
ON CONFLICT (rule_code) DO UPDATE SET
    symptom_id = EXCLUDED.symptom_id,
    category_codes = EXCLUDED.category_codes,
    keywords = EXCLUDED.keywords,
    reason_vi = EXCLUDED.reason_vi,
    priority = EXCLUDED.priority,
    is_active = TRUE,
    updated_at = NOW();

INSERT INTO pharmacy_knowledge_rule (id, rule_code, symptom_id, category_codes, keywords, reason_vi, priority)
SELECT
    'e2f3a4b5-c6d7-8901-e2f3-a4b5c6d78901'::uuid,
    'KR_THROAT_CLEARING',
    s.id,
    ARRAY['HO_HAP'],
    ARRAY['prospan', 'decolgen', 'cam cum', 'vuong hong', 'vướng họng', 'khan tieng', 'khàn tiếng', 'dau hong', 'đau họng'],
    'Vướng họng / khản tiếng — giảm ho / cảm cúm OTC',
    146
FROM pharmacy_symptom s
WHERE s.code = 'throat_clearing'
ON CONFLICT (rule_code) DO UPDATE SET
    symptom_id = EXCLUDED.symptom_id,
    category_codes = EXCLUDED.category_codes,
    keywords = EXCLUDED.keywords,
    reason_vi = EXCLUDED.reason_vi,
    priority = EXCLUDED.priority,
    is_active = TRUE,
    updated_at = NOW();

INSERT INTO kit_schema_migrations (filename) VALUES ('298_pharmacy_consultation_ent_otc.sql')
ON CONFLICT (filename) DO NOTHING;
