-- KitPlatform 436: pharmacist-in-charge permission for consultation.
-- Holder (or ADMIN) may view the OTC list for a case that needs a pharmacist
-- (pregnancy, breastfeeding, infant). Not granted to any role here: the owner
-- assigns it to the pharmacist's role. Idempotent; touches no tenant rows.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES (
    'pharmacy.consultation.pharmacist',
    'Dược sĩ phụ trách: xem thuốc cho ca cần dược sĩ',
    'Tư vấn'
)
ON CONFLICT (permission_code) DO NOTHING;
