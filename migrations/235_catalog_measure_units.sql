-- Danh mục đơn vị tính (measure units) — quản trị tập trung thay cho danh sách hardcode ở admin.
-- Per-tenant, soft delete, seed các đơn vị phổ biến + đơn vị đã dùng trong product_units.

CREATE TABLE IF NOT EXISTS measure_units (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID         NOT NULL REFERENCES tenants(id),
    unit_name   VARCHAR(50)  NOT NULL,
    sort_order  INT          NOT NULL DEFAULT 0,
    status      SMALLINT     NOT NULL DEFAULT 1,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_measure_units_tenant ON measure_units(tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_measure_units_name
    ON measure_units(tenant_id, lower(unit_name))
    WHERE deleted_at IS NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger WHERE tgname = 'trg_measure_units_updated'
    ) THEN
        CREATE TRIGGER trg_measure_units_updated
            BEFORE UPDATE ON measure_units
            FOR EACH ROW EXECUTE FUNCTION set_updated_at();
    END IF;
END $$;

-- Seed đơn vị phổ biến cho mọi tenant hiện có
INSERT INTO measure_units (tenant_id, unit_name, sort_order)
SELECT t.id, u.unit_name, u.sort_order
FROM tenants t
CROSS JOIN (VALUES
    ('Viên', 1), ('Vỉ', 2), ('Hộp', 3), ('Hộp con', 4), ('Chai', 5),
    ('Lọ', 6), ('Tuýp', 7), ('Gói', 8), ('Ống', 9), ('Túi', 10),
    ('Hũ', 11), ('Cái', 12), ('Bộ', 13), ('Lốc', 14), ('Thùng', 15)
) AS u(unit_name, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM measure_units m
    WHERE m.tenant_id = t.id AND lower(m.unit_name) = lower(u.unit_name) AND m.deleted_at IS NULL
);

-- Bổ sung các đơn vị đã dùng thực tế trong product_units nhưng chưa có trong danh mục.
-- DISTINCT ON theo lower(unit_name) để tránh trùng khi dữ liệu chỉ khác hoa/thường ("Que" vs "que").
INSERT INTO measure_units (tenant_id, unit_name, sort_order)
SELECT DISTINCT ON (pu.tenant_id, lower(pu.unit_name)) pu.tenant_id, pu.unit_name, 100
FROM product_units pu
WHERE pu.status = 1
  AND NOT EXISTS (
      SELECT 1 FROM measure_units m
      WHERE m.tenant_id = pu.tenant_id
        AND lower(m.unit_name) = lower(pu.unit_name)
        AND m.deleted_at IS NULL
  )
ORDER BY pu.tenant_id, lower(pu.unit_name), pu.unit_name;
