-- Epic B3: log staff "Chăm sóc ngay" actions from Growth Desk

CREATE TABLE IF NOT EXISTS growth_care_actions (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID         NOT NULL REFERENCES tenants(id),
    repurchase_suggestion_id    UUID         REFERENCES repurchase_suggestions(id) ON DELETE SET NULL,
    customer_id                 UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    actor_user_id               UUID,
    action_type                 VARCHAR(40)  NOT NULL,
    draft_order_id              UUID         REFERENCES customer_draft_orders(id) ON DELETE SET NULL,
    notes                       TEXT,
    created_at                  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_growth_care_actions_type CHECK (
        action_type IN ('create_draft')
    )
);

CREATE INDEX IF NOT EXISTS ix_growth_care_actions_tenant_created
    ON growth_care_actions (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_growth_care_actions_suggestion
    ON growth_care_actions (tenant_id, repurchase_suggestion_id, created_at DESC)
    WHERE repurchase_suggestion_id IS NOT NULL;

COMMENT ON TABLE growth_care_actions IS 'Growth Desk staff care actions (P0: create_draft from refill suggestion)';
