-- KitPlatform 269: Evidence P0 behavior_event types
-- Layer: Pack:FamilyOS
-- Depends on: 267_pack_family_behavior_week_playbook.sql, 268_pack_family_commitment_kind.sql
-- Manifest: migration-files.family-os.txt only

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

COMMENT ON CONSTRAINT ck_behavior_event_type ON pack_family.behavior_event IS
    'Behavior OS events + Evidence P0 gate/satisfied/kind_assigned.';