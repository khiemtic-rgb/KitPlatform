-- KitPlatform 266: relationship_thanks push kind (con cảm ơn lời bố mẹ / anh chị)
-- Depends on: 265_pack_family_relationship_push_kind
-- Local / family-os pilot manifest only.
-- Surprises (like gratitude) — không nằm trong unique 1-alert/ngày.

ALTER TABLE pack_family.reminder_dispatch
    DROP CONSTRAINT IF EXISTS ck_reminder_dispatch_kind;

ALTER TABLE pack_family.reminder_dispatch
    ADD CONSTRAINT ck_reminder_dispatch_kind CHECK (
        kind IN (
            'due_now',
            'overdue',
            'evening_digest',
            'approval_digest',
            'gratitude',
            'all_done',
            'beautiful_day',
            'streak_milestone',
            'child_request',
            'ai_proposal',
            'relationship_voice',
            'relationship_thanks'
        )
    );

COMMENT ON CONSTRAINT ck_reminder_dispatch_kind ON pack_family.reminder_dispatch IS
    'P1.15: relationship_thanks = push khi con cảm ơn lời bố mẹ / anh chị (không ăn slot alert 1/ngày).';
