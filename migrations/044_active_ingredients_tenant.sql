-- Cô lập hoạt chất theo tenant (tránh tenant A sửa ảnh hưởng tenant B)

ALTER TABLE active_ingredients
    ADD COLUMN IF NOT EXISTS tenant_id UUID REFERENCES tenants(id);

UPDATE active_ingredients
SET tenant_id = (
    SELECT id FROM tenants WHERE deleted_at IS NULL ORDER BY created_at ASC LIMIT 1
)
WHERE tenant_id IS NULL;

INSERT INTO active_ingredients (tenant_id, ingredient_code, ingredient_name, description, status)
SELECT DISTINCT pi.tenant_id, ai.ingredient_code, ai.ingredient_name, ai.description, ai.status
FROM product_ingredients pi
INNER JOIN active_ingredients ai ON ai.id = pi.ingredient_id
WHERE pi.tenant_id <> ai.tenant_id
  AND NOT EXISTS (
      SELECT 1 FROM active_ingredients x
      WHERE x.tenant_id = pi.tenant_id AND x.ingredient_code = ai.ingredient_code
  );

UPDATE product_ingredients pi
SET ingredient_id = mapped.new_id
FROM (
    SELECT pi2.id AS pi_row_id, ai_local.id AS new_id
    FROM product_ingredients pi2
    INNER JOIN active_ingredients ai_old ON ai_old.id = pi2.ingredient_id
    INNER JOIN active_ingredients ai_local
        ON ai_local.tenant_id = pi2.tenant_id
       AND ai_local.ingredient_code = ai_old.ingredient_code
    WHERE ai_old.tenant_id <> pi2.tenant_id
) mapped
WHERE pi.id = mapped.pi_row_id;

ALTER TABLE active_ingredients
    ALTER COLUMN tenant_id SET NOT NULL;

ALTER TABLE active_ingredients DROP CONSTRAINT IF EXISTS uq_active_ingredients_code;
ALTER TABLE active_ingredients
    ADD CONSTRAINT uq_active_ingredients_code UNIQUE (tenant_id, ingredient_code);

CREATE INDEX IF NOT EXISTS ix_active_ingredients_tenant ON active_ingredients(tenant_id);

COMMENT ON COLUMN active_ingredients.tenant_id IS 'Mỗi nhà thuốc quản lý danh mục hoạt chất riêng';
