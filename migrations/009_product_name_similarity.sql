-- Product name normalization & fuzzy duplicate detection (per tenant)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

ALTER TABLE products
    ADD COLUMN IF NOT EXISTS product_name_normalized VARCHAR(255);

CREATE INDEX IF NOT EXISTS ix_products_tenant_name_normalized
    ON products (tenant_id, product_name_normalized)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_products_name_normalized_trgm
    ON products USING gin (product_name_normalized gin_trgm_ops);

UPDATE products
SET product_name_normalized = lower(trim(regexp_replace(product_name, '\s+', ' ', 'g')))
WHERE product_name_normalized IS NULL AND deleted_at IS NULL;
