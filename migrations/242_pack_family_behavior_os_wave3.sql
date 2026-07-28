-- KitPlatform 242: Behavior OS Wave 3 — motivation / intervention lite
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.
-- Depends on: 241

-- Extend behavior_event types for Wave 3
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

COMMENT ON CONSTRAINT ck_behavior_event_type ON pack_family.behavior_event IS
    'Behavior OS event bus through Wave 3 (motivation / intervention lite).';
