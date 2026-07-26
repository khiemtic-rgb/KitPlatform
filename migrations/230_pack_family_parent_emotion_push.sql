-- KitPlatform 230: Parent emotion push kinds — approval digest, gratitude, surprises
-- Depends on: 196_pack_family_parent_push.sql
-- Expands reminder_dispatch.kind for Family Emotion Platform (digest / gratitude / milestones).

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
            'streak_milestone'
        )
    );

-- Family-level kinds (once per day), excluding gratitude (may be multiple children).
DROP INDEX IF EXISTS pack_family.ux_reminder_dispatch_digest;

CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_dispatch_family_kind_date
    ON pack_family.reminder_dispatch (tenant_id, family_id, kind, flow_date)
    WHERE commitment_id IS NULL
      AND kind IN (
          'evening_digest',
          'approval_digest',
          'all_done',
          'beautiful_day',
          'streak_milestone'
      );

-- One gratitude push per gratitude message (payload_summary = gratitude UUID text).
CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_dispatch_gratitude
    ON pack_family.reminder_dispatch (tenant_id, family_id, kind, flow_date, payload_summary)
    WHERE kind = 'gratitude';

COMMENT ON TABLE pack_family.reminder_dispatch IS
    'Dedupe log: hot reminders, evening digest, approval digest, gratitude, positive surprises.';
