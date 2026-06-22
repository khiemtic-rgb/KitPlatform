-- PharmaCore: Catalog module (8 tables)

CREATE TABLE product_categories (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    parent_id       UUID         REFERENCES product_categories(id),
    category_code   VARCHAR(50)  NOT NULL,
    category_name   VARCHAR(255) NOT NULL,
    description     TEXT,
    sort_order      INT          NOT NULL DEFAULT 0,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_product_categories_code UNIQUE (tenant_id, category_code)
);
CREATE INDEX ix_product_categories_tenant ON product_categories(tenant_id);

CREATE TABLE product_brands (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    brand_code      VARCHAR(50)  NOT NULL,
    brand_name      VARCHAR(255) NOT NULL,
    country_code    CHAR(2),
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_product_brands_code UNIQUE (tenant_id, brand_code)
);

CREATE TABLE products (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    category_id     UUID         REFERENCES product_categories(id),
    brand_id        UUID         REFERENCES product_brands(id),
    product_code    VARCHAR(50)  NOT NULL,
    product_name    VARCHAR(255) NOT NULL,
    generic_name    VARCHAR(255),
    drug_type       SMALLINT     NOT NULL DEFAULT 1, -- 1 OTC 2 Rx 3 Controlled
    description     TEXT,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_products_code UNIQUE (tenant_id, product_code)
);
CREATE INDEX ix_products_tenant ON products(tenant_id);
CREATE INDEX ix_products_category ON products(category_id);

CREATE TABLE product_units (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID         NOT NULL REFERENCES tenants(id),
    product_id          UUID         NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    unit_name           VARCHAR(50)  NOT NULL,
    conversion_factor   NUMERIC(18,6) NOT NULL DEFAULT 1,
    is_base_unit        BOOLEAN      NOT NULL DEFAULT FALSE,
    is_sale_unit        BOOLEAN      NOT NULL DEFAULT FALSE,
    status              SMALLINT     NOT NULL DEFAULT 1,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_product_units_name UNIQUE (product_id, unit_name)
);
CREATE INDEX ix_product_units_product ON product_units(product_id);

CREATE TABLE product_barcodes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    product_id      UUID         NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    barcode         VARCHAR(100) NOT NULL,
    barcode_type    SMALLINT     NOT NULL, -- 1 Mfr 2 Internal 3 QR 4 GS1
    is_primary      BOOLEAN      NOT NULL DEFAULT FALSE,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_product_barcodes_tenant_barcode UNIQUE (tenant_id, barcode)
);
CREATE INDEX ix_product_barcodes_product ON product_barcodes(product_id);

CREATE TABLE product_prices (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    product_id      UUID          NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    product_unit_id UUID          NOT NULL REFERENCES product_units(id),
    price_type      SMALLINT      NOT NULL, -- 1 Retail 2 Wholesale 3 VIP 4 Insurance 5 Online
    currency_code   CHAR(3)       NOT NULL DEFAULT 'VND',
    price           NUMERIC(18,2) NOT NULL,
    effective_from  TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    effective_to    TIMESTAMPTZ,
    status          SMALLINT      NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_product_prices_lookup ON product_prices(product_id, price_type, effective_from DESC);

CREATE TABLE active_ingredients (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingredient_code VARCHAR(50)  NOT NULL,
    ingredient_name VARCHAR(255) NOT NULL,
    description     TEXT,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_active_ingredients_code UNIQUE (ingredient_code)
);

CREATE TABLE product_ingredients (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    product_id      UUID          NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    ingredient_id   UUID          NOT NULL REFERENCES active_ingredients(id),
    strength_value  NUMERIC(18,4),
    strength_unit   VARCHAR(50),
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_product_ingredients UNIQUE (product_id, ingredient_id)
);
CREATE INDEX ix_product_ingredients_product ON product_ingredients(product_id);

CREATE TRIGGER trg_product_categories_updated BEFORE UPDATE ON product_categories FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_product_brands_updated BEFORE UPDATE ON product_brands FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_products_updated BEFORE UPDATE ON products FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_product_units_updated BEFORE UPDATE ON product_units FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_product_barcodes_updated BEFORE UPDATE ON product_barcodes FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_product_prices_updated BEFORE UPDATE ON product_prices FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_active_ingredients_updated BEFORE UPDATE ON active_ingredients FOR EACH ROW EXECUTE FUNCTION set_updated_at();
