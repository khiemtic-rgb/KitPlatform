-- KitPlatform 112: ClinicOS GĐ1 — enable Clinic modules on DEMO_CLINIC + seed provider
-- Depends on: 111_novixa_connect_status_events.sql, 108_novixa_connect_org_profiles.sql, 078_pack_clinic_crm.sql

UPDATE public.tenants t
SET
    settings = jsonb_set(
        COALESCE(t.settings, '{}'::jsonb),
        '{platform}',
        COALESCE(t.settings->'platform', '{}'::jsonb)
            || jsonb_build_object(
                'vertical', 'clinic',
                'enabled_modules', (
                    SELECT COALESCE(jsonb_agg(to_jsonb(m)), '[]'::jsonb)
                    FROM (
                        SELECT DISTINCT m
                        FROM jsonb_array_elements_text(
                            COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                            || '["novixa_connect","clinic_appointments","clinic_emr_lite","sales"]'::jsonb
                        ) AS m
                    ) d
                )
            ),
        true
    ),
    updated_at = NOW()
WHERE t.tenant_code = 'DEMO_CLINIC'
  AND t.deleted_at IS NULL;

SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111102'::uuid,
    'clinic_crm'
);

INSERT INTO pack_clinic.clinic_provider (
    id, tenant_id, provider_code, display_name, specialty, license_no, status
)
VALUES (
    '11111111-1111-1111-1111-111111111702',
    '11111111-1111-1111-1111-111111111102',
    'BS01',
    'BS Demo Clinic',
    'Đa khoa',
    'CCHN-DEMO-001',
    1
)
ON CONFLICT (tenant_id, provider_code) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    specialty = EXCLUDED.specialty,
    license_no = EXCLUDED.license_no,
    status = 1,
    deleted_at = NULL,
    updated_at = NOW();
