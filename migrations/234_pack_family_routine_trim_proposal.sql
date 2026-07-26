-- KitPlatform 234: AI proposal kind routine_trim (suggest hiding dense routine templates)
-- Depends on: 232_pack_family_afe_engine.sql

ALTER TABLE pack_family.ai_proposal
    DROP CONSTRAINT IF EXISTS ck_ai_proposal_kind;

ALTER TABLE pack_family.ai_proposal
    ADD CONSTRAINT ck_ai_proposal_kind CHECK (
        kind IN (
            'screen_budget',
            'screen_adjust',
            'family_mode',
            'movie_night',
            'pause_routine',
            'reward_minutes',
            'routine_trim',
            'other'
        )
    );

COMMENT ON CONSTRAINT ck_ai_proposal_kind ON pack_family.ai_proposal IS
    'Includes routine_trim: AI suggests deactivating optional templates (apply from tomorrow).';
