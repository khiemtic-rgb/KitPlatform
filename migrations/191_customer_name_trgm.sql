-- Customer name fuzzy duplicate detection (similar-clusters ≥ threshold)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Expression must match CustomerAdminRepository nameNormExpr for `%` to use the index.
CREATE INDEX IF NOT EXISTS ix_customers_full_name_trgm
    ON customers USING gin (
        (lower(trim(regexp_replace(coalesce(full_name, ''), '\s+', ' ', 'g')))) gin_trgm_ops
    )
    WHERE deleted_at IS NULL AND status = 1;
