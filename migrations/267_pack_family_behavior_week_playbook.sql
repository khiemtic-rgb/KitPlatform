-- KitPlatform 267: Behavior patterns week playbook + child voice (S1–S4)
-- Layer: Pack:FamilyOS
-- Depends on: 244_pack_family_behavior_os_wave5.sql
-- Local / Family OS park: family-os migration manifest only.

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
        'parent_coach_acted',
        'pattern_detected',
        'tactic_rotated',
        'child_voice_submitted',
        'parent_strategy_tip',
        'commitment_evidence_gate_blocked',
        'commitment_evidence_satisfied',
        'commitment_kind_assigned'
    ));

CREATE TABLE IF NOT EXISTS pack_family.behavior_week_playbook (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
    family_id               UUID NOT NULL REFERENCES pack_family.family(id),
    member_id               UUID REFERENCES pack_family.membership(id),
    week_start              DATE NOT NULL,
    pattern_code            VARCHAR(40),
    tactic_code             VARCHAR(40),
    last_failed_tactic      VARCHAR(40),
    parent_strategy_tip_vi  TEXT,
    child_voice_json        JSONB NOT NULL DEFAULT '{}'::jsonb,
    child_voice_at          TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_week_playbook_pattern CHECK (
        pattern_code IS NULL OR pattern_code IN (
            'evening_fatigue',
            'subject_avoidance',
            'nudge_dependent',
            'social_boost',
            'streak_fragile'
        )
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_behavior_week_playbook_member
    ON pack_family.behavior_week_playbook (
        tenant_id,
        family_id,
        week_start,
        COALESCE(member_id, '00000000-0000-0000-0000-000000000000'::uuid)
    );

CREATE INDEX IF NOT EXISTS ix_behavior_week_playbook_family_week
    ON pack_family.behavior_week_playbook (tenant_id, family_id, week_start DESC);

ALTER TABLE pack_family.behavior_week_playbook ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.behavior_week_playbook;
CREATE POLICY tenant_isolation ON pack_family.behavior_week_playbook
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.behavior_week_playbook IS
    'S1–S4 week playbook: active pattern/tactic, child voice JSON, one parent strategy tip.';
