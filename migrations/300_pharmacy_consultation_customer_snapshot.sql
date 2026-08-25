-- KitPlatform 300: Customer profile snapshot on consultation sessions
-- Manifest: deploy/ubuntu/migration-files.prod.txt
-- Depends on: 294_pharmacy_consultation_mvp1.sql

ALTER TABLE pharmacy_consultation_sessions
    ADD COLUMN IF NOT EXISTS customer_profile_snapshot_json JSONB;

COMMENT ON COLUMN pharmacy_consultation_sessions.customer_profile_snapshot_json IS
    'Point-in-time customer profile (age, gender, allergies notes) at consultation — not live profile ref.';

INSERT INTO kit_schema_migrations (filename) VALUES ('300_pharmacy_consultation_customer_snapshot.sql')
ON CONFLICT (filename) DO NOTHING;
