-- KitPlatform 265: reminder_dispatch kind for Relationship Engine push (RE P1.14 lite)
-- Depends on: 232_pack_family_afe_engine, 257_pack_family_parent_voice
-- Local / family-os pilot manifest only.

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
            'relationship_voice'
        )
    );

DROP INDEX IF EXISTS pack_family.ux_reminder_dispatch_family_kind_date;

CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_dispatch_family_kind_date
    ON pack_family.reminder_dispatch (tenant_id, family_id, kind, flow_date)
    WHERE commitment_id IS NULL
      AND kind IN (
          'evening_digest',
          'approval_digest',
          'all_done',
          'beautiful_day',
          'streak_milestone',
          'relationship_voice'
      );

COMMENT ON CONSTRAINT ck_reminder_dispatch_kind ON pack_family.reminder_dispatch IS
    'P1.14 lite: relationship_voice = 1 push/ngày cho lời người↔người chưa đọc.';
