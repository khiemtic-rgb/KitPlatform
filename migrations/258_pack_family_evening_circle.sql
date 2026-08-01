-- KitPlatform 258: FamilyOS evening_circle memory kind (Relationship Engine P1.3)
-- Depends on: 257
-- Local / family-os pilot manifest only — NOT migration-files.prod.txt

ALTER TABLE pack_family.family_memory
    DROP CONSTRAINT IF EXISTS ck_family_memory_kind;

ALTER TABLE pack_family.family_memory
    ADD CONSTRAINT ck_family_memory_kind CHECK (
        kind IN (
            'beautiful_day',
            'streak_milestone',
            'gratitude',
            'photo',
            'team_unlock',
            'reward',
            'first_time',
            'manual',
            'help',
            'team_day',
            'parent_habit',
            'parent_voice',
            'evening_circle'
        )
    );
