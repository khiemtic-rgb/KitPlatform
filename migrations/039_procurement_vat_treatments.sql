-- Bảng cấu hình thuế GTGT mua hàng (theo tenant) — thêm/sửa khi luật đổi, không hard-code app

CREATE TABLE procurement_vat_treatments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID          NOT NULL REFERENCES tenants(id),
    treatment_code  VARCHAR(30)   NOT NULL,
    treatment_name  VARCHAR(120)  NOT NULL,
    rate_percent    NUMERIC(5, 2) NOT NULL DEFAULT 0,
    is_not_subject  BOOLEAN       NOT NULL DEFAULT false,
    sort_order      INT           NOT NULL DEFAULT 0,
    is_active       BOOLEAN       NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_procurement_vat_treatments_code UNIQUE (tenant_id, treatment_code),
    CONSTRAINT ck_procurement_vat_treatments_rate CHECK (rate_percent >= 0 AND rate_percent <= 100),
    CONSTRAINT ck_procurement_vat_treatments_kct CHECK (
        (is_not_subject = true AND rate_percent = 0)
        OR is_not_subject = false
    )
);

CREATE INDEX ix_procurement_vat_treatments_tenant
    ON procurement_vat_treatments(tenant_id, sort_order)
    WHERE is_active = true;

CREATE TRIGGER trg_procurement_vat_treatments_updated
    BEFORE UPDATE ON procurement_vat_treatments
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

ALTER TABLE purchase_orders
    ADD COLUMN IF NOT EXISTS vat_treatment_id UUID REFERENCES procurement_vat_treatments(id);

-- Demo tenant: KCT tách biệt + các mức thuế suất VN
INSERT INTO procurement_vat_treatments (
    id, tenant_id, treatment_code, treatment_name, rate_percent, is_not_subject, sort_order
)
VALUES
    ('11111111-1111-1111-1111-111111111801', '11111111-1111-1111-1111-111111111101',
     'kct', 'Không chịu thuế GTGT (KCT)', 0, true, 0),
    ('11111111-1111-1111-1111-111111111802', '11111111-1111-1111-1111-111111111101',
     'vat_0', 'Thuế suất 0%', 0, false, 1),
    ('11111111-1111-1111-1111-111111111803', '11111111-1111-1111-1111-111111111101',
     'vat_5', 'Thuế suất 5%', 5, false, 2),
    ('11111111-1111-1111-1111-111111111804', '11111111-1111-1111-1111-111111111101',
     'vat_8', 'Thuế suất 8%', 8, false, 3),
    ('11111111-1111-1111-1111-111111111805', '11111111-1111-1111-1111-111111111101',
     'vat_10', 'Thuế suất 10%', 10, false, 4)
ON CONFLICT (tenant_id, treatment_code) DO NOTHING;

INSERT INTO procurement_vat_treatments (tenant_id, treatment_code, treatment_name, rate_percent, is_not_subject, sort_order)
SELECT t.id, v.code, v.name, v.rate, v.kct, v.ord
FROM tenants t
CROSS JOIN (
    VALUES
        ('kct', 'Không chịu thuế GTGT (KCT)', 0::numeric, true, 0),
        ('vat_0', 'Thuế suất 0%', 0::numeric, false, 1),
        ('vat_5', 'Thuế suất 5%', 5::numeric, false, 2),
        ('vat_8', 'Thuế suất 8%', 8::numeric, false, 3),
        ('vat_10', 'Thuế suất 10%', 10::numeric, false, 4)
) AS v(code, name, rate, kct, ord)
WHERE NOT EXISTS (
    SELECT 1 FROM procurement_vat_treatments pvt WHERE pvt.tenant_id = t.id
);

UPDATE purchase_orders po
SET vat_treatment_id = t.id
FROM procurement_vat_treatments t
WHERE po.vat_treatment_id IS NULL
  AND po.tenant_id = t.tenant_id
  AND t.treatment_code = CASE po.tax_rate_percent
      WHEN 5 THEN 'vat_5'
      WHEN 8 THEN 'vat_8'
      WHEN 10 THEN 'vat_10'
      ELSE 'vat_0'
  END;

UPDATE purchase_orders po
SET vat_treatment_id = t.id
FROM procurement_vat_treatments t
WHERE po.vat_treatment_id IS NULL
  AND po.tenant_id = t.tenant_id
  AND t.treatment_code = 'vat_0';

ALTER TABLE purchase_orders
    DROP CONSTRAINT IF EXISTS ck_purchase_orders_tax_rate_percent;

ALTER TABLE purchase_orders
    ALTER COLUMN vat_treatment_id SET NOT NULL;

CREATE INDEX ix_purchase_orders_vat_treatment ON purchase_orders(vat_treatment_id);
