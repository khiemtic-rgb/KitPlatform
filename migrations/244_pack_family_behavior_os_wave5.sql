-- KitPlatform 244: Behavior OS Wave 5 — Family Twin + Retirement / Observe-only
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.
-- Depends on: 243

ALTER TABLE pack_family.behavior_event
    DROP CONSTRAINT IF EXISTS ck_behavior_event_type;

ALTER TABLE pack_family.behavior_event
    ADD CONSTRAINT ck_behavior_event_type CHECK (event_type IN (
        'commitment_done',
        'commitment_skipped',
        'reflection_submitted',
        'habit_stage_changed',
        'reminder_suppressed',
        'reminder_fired',
        'parent_nudge',
        'self_start',
        'retrieval_submitted',
        'confidence_scored',
        'evidence_uploaded',
        'motivation_cued',
        'intervention_decided',
        'parent_nudge_blocked',
        'twin_scored',
        'prediction_flagged',
        'retirement_advanced',
        'observe_mode_entered',
        'observe_mode_exited',
        'dependence_warned',
        'parent_coach_acted'
    ));

CREATE TABLE IF NOT EXISTS pack_family.behavior_retirement_policy (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    family_id           UUID NOT NULL REFERENCES pack_family.family(id),
    observe_only        BOOLEAN NOT NULL DEFAULT FALSE,
    retirement_stage    VARCHAR(32),
    parent_nudge_budget INT,
    notes_vi            TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_behavior_retirement_family UNIQUE (family_id),
    CONSTRAINT ck_retirement_stage CHECK (
        retirement_stage IS NULL OR retirement_stage IN (
            'full_support', 'assisted', 'soft', 'observe', 'retired'
        )
    ),
    CONSTRAINT ck_retirement_budget CHECK (
        parent_nudge_budget IS NULL OR parent_nudge_budget BETWEEN 0 AND 20
    )
);

ALTER TABLE pack_family.behavior_retirement_policy ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.behavior_retirement_policy;
CREATE POLICY tenant_isolation ON pack_family.behavior_retirement_policy
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.behavior_retirement_policy IS
    'Wave 5 — Autonomy Gradient / Observe-only policy per family (AI Retirement runtime).';
