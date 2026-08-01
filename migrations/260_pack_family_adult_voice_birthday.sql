-- KitPlatform 260: Adult↔adult voice templates + birthday (RE P1.9–P1.10)
-- Depends on: 257_pack_family_parent_voice
-- Local / family-os pilot manifest only.

ALTER TABLE pack_family.parent_voice_message
    DROP CONSTRAINT IF EXISTS ck_parent_voice_template;

ALTER TABLE pack_family.parent_voice_message
    ADD CONSTRAINT ck_parent_voice_template CHECK (
        template_code IN (
            'praise',
            'encourage',
            'custom',
            'thanks_partner',
            'help_offer',
            'warm_adult',
            'birthday'
        )
    );

COMMENT ON CONSTRAINT ck_parent_voice_template ON pack_family.parent_voice_message IS
    'P1.9–10: adult partner care + birthday wish templates; praise/encourage remain child-facing.';
