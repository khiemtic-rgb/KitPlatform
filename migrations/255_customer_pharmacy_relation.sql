-- 255: Customer pharmacy relation (prospect vs member) + acquisition source
-- Additive / backward compatible: existing customers backfilled as member + counter.

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS acquisition_source VARCHAR(32) NOT NULL DEFAULT 'counter',
    ADD COLUMN IF NOT EXISTS pharmacy_relation VARCHAR(32) NOT NULL DEFAULT 'member',
    ADD COLUMN IF NOT EXISTS pharmacy_verified_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS pharmacy_verified_via VARCHAR(32),
    ADD COLUMN IF NOT EXISTS pharmacy_verified_by UUID;

ALTER TABLE customers
    DROP CONSTRAINT IF EXISTS ck_customers_acquisition_source;
ALTER TABLE customers
    ADD CONSTRAINT ck_customers_acquisition_source
    CHECK (acquisition_source IN ('counter', 'app_self', 'qr_claim', 'import', 'admin'));

ALTER TABLE customers
    DROP CONSTRAINT IF EXISTS ck_customers_pharmacy_relation;
ALTER TABLE customers
    ADD CONSTRAINT ck_customers_pharmacy_relation
    CHECK (pharmacy_relation IN ('prospect', 'member', 'revoked'));

ALTER TABLE customers
    DROP CONSTRAINT IF EXISTS ck_customers_pharmacy_verified_via;
ALTER TABLE customers
    ADD CONSTRAINT ck_customers_pharmacy_verified_via
    CHECK (
        pharmacy_verified_via IS NULL
        OR pharmacy_verified_via IN ('staff_mark', 'qr_scan', 'first_sale', 'invite')
    );

-- Existing rows already default to member/counter; stamp verify metadata once.
UPDATE customers
SET
    pharmacy_verified_at = COALESCE(pharmacy_verified_at, created_at),
    pharmacy_verified_via = COALESCE(pharmacy_verified_via, 'staff_mark')
WHERE deleted_at IS NULL
  AND pharmacy_relation = 'member'
  AND pharmacy_verified_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_customers_pharmacy_relation
    ON customers (tenant_id, pharmacy_relation)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_customers_acquisition_source
    ON customers (tenant_id, acquisition_source)
    WHERE deleted_at IS NULL;
