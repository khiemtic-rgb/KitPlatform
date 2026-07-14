-- KitPlatform 134: Customer groups (CRM) + % discount for POS auto-fill
-- Assign group on customer create/edit; POS applies order % discount when request omits one.

CREATE TABLE IF NOT EXISTS customer_groups (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES tenants(id),
    group_code          VARCHAR(50)  NOT NULL,
    group_name          VARCHAR(255) NOT NULL,
    discount_percent    NUMERIC(5,2) NOT NULL DEFAULT 0
        CONSTRAINT ck_customer_groups_discount CHECK (discount_percent >= 0 AND discount_percent <= 100),
    status              SMALLINT     NOT NULL DEFAULT 1,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT uq_customer_groups_tenant_code UNIQUE (tenant_id, group_code)
);

CREATE INDEX IF NOT EXISTS ix_customer_groups_tenant
    ON customer_groups (tenant_id)
    WHERE deleted_at IS NULL;

ALTER TABLE customers
    ADD COLUMN IF NOT EXISTS customer_group_id UUID REFERENCES customer_groups(id);

CREATE INDEX IF NOT EXISTS ix_customers_customer_group
    ON customers (tenant_id, customer_group_id)
    WHERE deleted_at IS NULL AND customer_group_id IS NOT NULL;

DROP TRIGGER IF EXISTS trg_customer_groups_updated ON customer_groups;
CREATE TRIGGER trg_customer_groups_updated
    BEFORE UPDATE ON customer_groups
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
