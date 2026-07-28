-- KitPlatform 243: Behavior OS Wave 4 — twin snapshot + prediction events
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.
-- Depends on: 242

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

CREATE TABLE IF NOT EXISTS pack_family.behavior_twin_snapshot (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    member_id       UUID NOT NULL REFERENCES pack_family.membership(id),
    snapshot_date   DATE NOT NULL,
    overall_score   INT NOT NULL,
    overall_label   VARCHAR(64) NOT NULL,
    dimensions_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    evening_risk_band VARCHAR(16),
    evening_reasons_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    disclaimer_vi   TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_behavior_twin_member_day UNIQUE (family_id, member_id, snapshot_date),
    CONSTRAINT ck_twin_overall CHECK (overall_score BETWEEN 0 AND 100),
    CONSTRAINT ck_twin_evening_band CHECK (
        evening_risk_band IS NULL OR evening_risk_band IN ('low', 'medium', 'high')
    )
);

CREATE INDEX IF NOT EXISTS ix_behavior_twin_family_day
    ON pack_family.behavior_twin_snapshot (family_id, snapshot_date DESC);

ALTER TABLE pack_family.behavior_twin_snapshot ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.behavior_twin_snapshot;
CREATE POLICY tenant_isolation ON pack_family.behavior_twin_snapshot
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.behavior_twin_snapshot IS
    'Wave 4 Behavior Twin — signal model snapshot (not personality judgment).';
