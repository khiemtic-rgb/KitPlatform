-- KitPlatform 192a: add business_vertical = family (requires public.tenants owner)
-- Layer: Pack:FamilyOS
-- Local: scripts/seed-family-os-local.ps1 applies this as postgres when -PostgresPassword is set.
-- Production: apply only during off-hours with the rest of FamilyOS deploy — NOT during pharmacy hours.

ALTER TABLE public.tenants DROP CONSTRAINT IF EXISTS ck_tenants_business_vertical;
ALTER TABLE public.tenants
    ADD CONSTRAINT ck_tenants_business_vertical CHECK (
        business_vertical IN (
            'pharmacy', 'pharmacy_chain', 'supplement_store',
            'medical_equipment_store', 'clinic', 'lab', 'medical_spa', 'hybrid',
            'family'
        )
    );

COMMENT ON COLUMN public.tenants.business_vertical IS
    'Loại hình tenant; đồng bộ với settings.platform.vertical. family = FamilyOS consumer.';

-- Promote local demo tenant when present
UPDATE public.tenants
SET
    business_vertical = 'family',
    settings = jsonb_set(
        COALESCE(settings, '{}'::jsonb),
        '{platform,vertical}',
        '"family"'::jsonb,
        true
    ),
    updated_at = NOW()
WHERE tenant_code = 'DEMO_FAMILY'
  AND deleted_at IS NULL
  AND business_vertical <> 'family';
