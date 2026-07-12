-- KitPlatform 113: ClinicOS GĐ1 — seed DEMO_CLINIC patient for appointments/visits smoke
-- Depends on: 112_clinic_gd1_demo_clinic_modules.sql
-- Inserts customer without party_identifier path (API create blocked by missing workspace_id on party_identifier).

INSERT INTO public.customers (
    id, tenant_id, customer_code, full_name, phone, status
)
VALUES (
    '11111111-1111-1111-1111-111111111802',
    '11111111-1111-1111-1111-111111111102',
    'BN01',
    'BN Demo Clinic',
    '0902000099',
    1
)
ON CONFLICT (tenant_id, customer_code) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone = EXCLUDED.phone,
    status = 1,
    deleted_at = NULL,
    updated_at = NOW();
