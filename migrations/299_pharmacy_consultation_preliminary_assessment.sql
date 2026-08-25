-- KitPlatform 299: Store preliminary assessment on consultation sessions
-- Manifest: deploy/ubuntu/migration-files.prod.txt
-- Depends on: 294_pharmacy_consultation_mvp1.sql

ALTER TABLE pharmacy_consultation_sessions
    ADD COLUMN IF NOT EXISTS preliminary_assessment_json JSONB;

COMMENT ON COLUMN pharmacy_consultation_sessions.preliminary_assessment_json IS
    'Rule-engine preliminary assessment (likely / insufficient / needs_evaluation) — not a diagnosis.';

INSERT INTO kit_schema_migrations (filename) VALUES ('299_pharmacy_consultation_preliminary_assessment.sql')
ON CONFLICT (filename) DO NOTHING;
