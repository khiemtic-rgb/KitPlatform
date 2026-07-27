-- KitPlatform 247: Parent Success P3 — parent_coach_acted on behavior_event
-- Layer: Pack:FamilyOS
-- Depends on: 244 (ck_behavior_event_type Wave 5), 246 (P2 check-in)
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
        'parent_coach_acted'
    ));

COMMENT ON CONSTRAINT ck_behavior_event_type ON pack_family.behavior_event IS
    'Behavior OS + PSE events; parent_coach_acted = payer tapped Đã thử on Famixa tip (Trust Flywheel).';
