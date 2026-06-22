-- PharmaCore: Product images (multi-image ready)

CREATE TABLE product_images (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    product_id      UUID         NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    image_url       TEXT         NOT NULL,
    sort_order      INT          NOT NULL DEFAULT 0,
    is_primary      BOOLEAN      NOT NULL DEFAULT FALSE,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_product_images_product ON product_images(product_id);
CREATE UNIQUE INDEX uq_product_images_one_primary
    ON product_images(product_id)
    WHERE is_primary = TRUE AND status = 1;

CREATE TRIGGER trg_product_images_updated
    BEFORE UPDATE ON product_images
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
