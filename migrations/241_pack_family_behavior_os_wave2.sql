-- KitPlatform 241: Behavior OS Wave 2 — evidence ladder, confidence, retrieval check
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.
-- Depends on: 240

-- =============================================================================
-- Evidence + confidence snapshot on daily commitment
-- =============================================================================
ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS evidence_level INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS confidence_score INT,
    ADD COLUMN IF NOT EXISTS confidence_updated_at TIMESTAMPTZ;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_evidence_level'
    ) THEN
        ALTER TABLE pack_family.commitment
            ADD CONSTRAINT ck_commitment_evidence_level
            CHECK (evidence_level BETWEEN 0 AND 3);
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_confidence_score'
    ) THEN
        ALTER TABLE pack_family.commitment
            ADD CONSTRAINT ck_commitment_confidence_score
            CHECK (confidence_score IS NULL OR confidence_score BETWEEN 0 AND 100);
    END IF;
END $$;

COMMENT ON COLUMN pack_family.commitment.evidence_level IS
    'Behavior OS Wave 2 ladder: 0=self tick, 1=reflection, 2=retrieval check, 3=photo.';
COMMENT ON COLUMN pack_family.commitment.confidence_score IS
    'Completion confidence 0–100 — need more data, not accusing the child.';

-- =============================================================================
-- Extend behavior_event types
-- =============================================================================
ALTER TABLE pack_family.behavior_event
    DROP CONSTRAINT IF EXISTS ck_behavior_event_type;

-- Superset includes later waves + PSE so re-apply stays safe if those rows already exist.
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

CREATE INDEX IF NOT EXISTS ix_commitment_retrieval_family
    ON pack_family.commitment_retrieval_check (family_id, created_at DESC);

ALTER TABLE pack_family.commitment_retrieval_check ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.commitment_retrieval_check;
CREATE POLICY tenant_isolation ON pack_family.commitment_retrieval_check
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.commitment_retrieval_check IS
    'Wave 2 Behavior OS — meta-cognitive retrieval check after learning missions.';
