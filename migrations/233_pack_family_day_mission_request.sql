-- KitPlatform 233: Child proposes today's mission + ad-hoc commitment support
-- Depends on: 232_pack_family_afe_engine.sql
-- day_mission requests carry title/window; approve → insert commitment with template_id NULL.

ALTER TABLE pack_family.child_request
    DROP CONSTRAINT IF EXISTS ck_child_request_kind;

ALTER TABLE pack_family.child_request
    ADD CONSTRAINT ck_child_request_kind CHECK (
        kind IN (
            'screen_minutes',
            'day_mission',
            'pause_routine',
            'movie_night',
            'other'
        )
    );

ALTER TABLE pack_family.child_request
    ADD COLUMN IF NOT EXISTS title_vi VARCHAR(200);

ALTER TABLE pack_family.child_request
    ADD COLUMN IF NOT EXISTS window_start TIME;

ALTER TABLE pack_family.child_request
    ADD COLUMN IF NOT EXISTS window_end TIME;

-- Minutes optional for day_mission (use expected duration or omit).
ALTER TABLE pack_family.child_request
    DROP CONSTRAINT IF EXISTS ck_child_request_minutes;

ALTER TABLE pack_family.child_request
    ALTER COLUMN amount_minutes DROP NOT NULL;

ALTER TABLE pack_family.child_request
    ADD CONSTRAINT ck_child_request_minutes CHECK (
        amount_minutes IS NULL
        OR (amount_minutes > 0 AND amount_minutes <= 240)
    );

COMMENT ON COLUMN pack_family.child_request.title_vi IS
    'Proposed mission title when kind = day_mission.';
