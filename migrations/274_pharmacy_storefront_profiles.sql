-- KitPlatform 274: Pharmacy storefront CMS profiles (white-label landing)
-- Additive: pack_pharmacy.pharmacy_storefront_profiles

CREATE TABLE IF NOT EXISTS pack_pharmacy.pharmacy_storefront_profiles (
    tenant_id     UUID PRIMARY KEY REFERENCES public.tenants(id),
    slug          VARCHAR(80) NOT NULL,
    is_published  BOOLEAN NOT NULL DEFAULT false,
    content       JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by    UUID NULL,
    CONSTRAINT uq_pharmacy_storefront_slug UNIQUE (slug),
    CONSTRAINT ck_pharmacy_storefront_slug_format
        CHECK (slug ~ '^[a-z0-9]([a-z0-9-]{0,78}[a-z0-9])?$')
);

COMMENT ON TABLE pack_pharmacy.pharmacy_storefront_profiles IS
    'White-label pharmacy storefront CMS content (JSON ≈ PharmacyTenantConfig subset).';

CREATE INDEX IF NOT EXISTS idx_pharmacy_storefront_published
    ON pack_pharmacy.pharmacy_storefront_profiles (is_published)
    WHERE is_published = true;
