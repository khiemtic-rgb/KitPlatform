-- KitPlatform 259: Sibling chemistry — thanks_back nudge template (RE P1.6)
-- Depends on: 251_pack_family_team_nudge
-- Local / family-os pilot manifest only.

ALTER TABLE pack_family.team_nudge
    DROP CONSTRAINT IF EXISTS ck_team_nudge_template;

ALTER TABLE pack_family.team_nudge
    ADD CONSTRAINT ck_team_nudge_template CHECK (
        template_code IN ('cheer_up', 'one_left', 'you_got_this', 'thanks_back')
    );

COMMENT ON CONSTRAINT ck_team_nudge_template ON pack_family.team_nudge IS
    'P1.6: thanks_back = em gửi cảm ơn anh/chị sau khi ack nudge = thanks.';
