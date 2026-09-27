-- Owner marks a SKU×warehouse line as reviewed-clean on INV-01.
-- Hide from the fix list only after this confirm — not by value/qty thresholds.

CREATE TABLE IF NOT EXISTS public.inventory_stock_reviews (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID          NOT NULL REFERENCES public.tenants(id),
    product_id        UUID          NOT NULL REFERENCES public.products(id),
    warehouse_id      UUID          NOT NULL REFERENCES public.warehouses(id),
    confirmed_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    confirmed_by      UUID          REFERENCES public.users(id),
    qty_at_confirm    NUMERIC(18,3) NOT NULL DEFAULT 0,
    value_at_confirm  NUMERIC(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT uq_inventory_stock_reviews UNIQUE (tenant_id, product_id, warehouse_id)
);

CREATE INDEX IF NOT EXISTS ix_inventory_stock_reviews_tenant
    ON public.inventory_stock_reviews (tenant_id);

COMMENT ON TABLE public.inventory_stock_reviews IS
    'INV-01: owner confirmed this on-hand line is clean. Unconfirm deletes the row.';

DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['kitplatform', 'pharmacore']
  LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON public.inventory_stock_reviews TO %I', r);
    END IF;
  END LOOP;
END $$;
