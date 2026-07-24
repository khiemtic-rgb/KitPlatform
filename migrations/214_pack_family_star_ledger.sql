-- Family OS — persistent star ledger + per-task star_reward
-- Depends on: 213_pack_family_allow_early_complete.sql

ALTER TABLE pack_family.commitment_template
    ADD COLUMN IF NOT EXISTS star_reward INT NOT NULL DEFAULT 10;

ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS star_reward INT NOT NULL DEFAULT 10;

COMMENT ON COLUMN pack_family.commitment_template.star_reward IS
    'Base star value for this task template (integer; half tiers use floor(star_reward/2)).';
COMMENT ON COLUMN pack_family.commitment.star_reward IS
    'Snapshot of star_reward at day-flow materialization.';

-- Backfill from legacy FE heuristic (homework 20, brush/sleep 15, default 10)
UPDATE pack_family.commitment_template
SET star_reward = CASE
    WHEN LOWER(title) LIKE '%bài%'
      OR LOWER(title) LIKE '%học%'
      OR LOWER(title) LIKE '%toán%' THEN 20
    WHEN LOWER(title) LIKE '%ngủ%'
      OR LOWER(title) LIKE '%đánh răng%' THEN 15
    ELSE 10
END,
updated_at = NOW()
WHERE deleted_at IS NULL;

UPDATE pack_family.commitment c
SET star_reward = COALESCE(t.star_reward, c.star_reward),
    updated_at = NOW()
FROM pack_family.commitment_template t
WHERE c.template_id = t.id
  AND c.deleted_at IS NULL;

UPDATE pack_family.commitment c
SET star_reward = CASE
    WHEN LOWER(c.title) LIKE '%bài%'
      OR LOWER(c.title) LIKE '%học%'
      OR LOWER(c.title) LIKE '%toán%' THEN 20
    WHEN LOWER(c.title) LIKE '%ngủ%'
      OR LOWER(c.title) LIKE '%đánh răng%' THEN 15
    ELSE 10
END,
updated_at = NOW()
WHERE c.deleted_at IS NULL
  AND c.template_id IS NULL;

CREATE TABLE IF NOT EXISTS pack_family.star_ledger (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL,
    member_id       UUID NOT NULL,
    commitment_id   UUID NOT NULL,
    delta           INT NOT NULL,
    tier            VARCHAR(32) NOT NULL,
    star_reward     INT NOT NULL,
    late_minutes    INT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT star_ledger_commitment_unique UNIQUE (tenant_id, commitment_id),
    CONSTRAINT star_ledger_member_fk FOREIGN KEY (member_id)
        REFERENCES pack_family.membership(id),
    CONSTRAINT star_ledger_commitment_fk FOREIGN KEY (commitment_id)
        REFERENCES pack_family.commitment(id)
);

CREATE INDEX IF NOT EXISTS idx_star_ledger_member
    ON pack_family.star_ledger (tenant_id, family_id, member_id);

COMMENT ON TABLE pack_family.star_ledger IS
    'One row per commitment completion — idempotent star award/penalty ledger.';
