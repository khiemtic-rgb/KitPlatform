-- KitPlatform 390: register personal_intelligence in the shared platform catalog.
-- Pack/module: personal_intelligence. Schema stays pack_personal_intelligence.
-- Manifest: deploy/ubuntu/migration-files.personal-intelligence.txt
-- Not migration-files.prod.txt. Pharmacy, Family OS, Content, and Local OS deploys do not apply this file.
-- Does not edit migration 051. Same seed shape as later pack registrations (323, 294).

INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT
    'personal_intelligence',
    'Personal Intelligence',
    'Second Brain. Trading is the first domain adapter inside this module.',
    ARRAY['hybrid'],
    210
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'personal_intelligence'
);
