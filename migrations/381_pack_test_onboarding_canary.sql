-- KIT Platform Pack Onboarding Canary.
-- Schema pack_test and module code test_pack.
-- Not a product pack. Not added to migration-files.prod.txt.
-- Manifest: deploy/ubuntu/migration-files.test-pack.txt

CREATE SCHEMA IF NOT EXISTS pack_test;

COMMENT ON SCHEMA pack_test IS
    'KIT Platform Pack Onboarding Canary. Not a business product.';

INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT
    'test_pack',
    'KIT Test Pack',
    'Pack onboarding canary. Not a business product.',
    ARRAY['hybrid'],
    200
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'test_pack'
);

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES ('test_pack.read', 'Test Pack — đọc canary', 'test_pack')
ON CONFLICT (permission_code) DO NOTHING;

CREATE TABLE IF NOT EXISTS pack_test.marker (
    id          UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id   UUID NOT NULL,
    marker      TEXT NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pack_test_marker_tenant UNIQUE (tenant_id)
);

COMMENT ON TABLE pack_test.marker IS
    'One canary marker per tenant. Reads filter tenant_id from the caller context.';

COMMENT ON COLUMN pack_test.marker.tenant_id IS
    'Caller tenant. Non-null. Unique so a tenant has one current marker.';
