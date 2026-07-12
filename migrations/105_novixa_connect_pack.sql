-- KitPlatform 105: Novixa Connect pack — module registry + tenant_package + schema stub
-- Depends on: 079_kit_pack_registry_workspace_party_backfill.sql
-- Layer: Pack:Connect (Healthcare Collaboration — NOT Pharmacy)

-- =============================================================================
-- Schema stub (tables land in C1+)
-- =============================================================================
CREATE SCHEMA IF NOT EXISTS pack_connect;

COMMENT ON SCHEMA pack_connect IS
    'Novixa Connect — org network, referral, booking. No e-Rx issuance / clinical care.';

-- =============================================================================
-- Platform module
-- =============================================================================
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT v.code, v.name, v.description, v.verticals, v.sort_order
FROM (
    VALUES
        (
            'novixa_connect',
            'Novixa Connect',
            'Healthcare collaboration — org network, referral, booking (not clinical issuer)',
            ARRAY['pharmacy', 'pharmacy_chain', 'hybrid', 'clinic'],
            50
        )
) AS v(code, name, description, verticals, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = v.code
);

-- =============================================================================
-- Tenant package (workspace provision via kit_provision_pack_workspace)
-- =============================================================================
INSERT INTO kit_tenant.tenant_package (
    package_code, package_name, description, verticals, module_codes, sort_order
)
VALUES (
    'novixa_connect',
    'Novixa Connect',
    'Healthcare Collaboration Platform — independent of Pharmacy pack',
    ARRAY['pharmacy', 'pharmacy_chain', 'hybrid', 'clinic'],
    ARRAY['novixa_connect'],
    30
)
ON CONFLICT (package_code) DO UPDATE SET
    package_name = EXCLUDED.package_name,
    description = EXCLUDED.description,
    verticals = EXCLUDED.verticals,
    module_codes = EXCLUDED.module_codes,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();
