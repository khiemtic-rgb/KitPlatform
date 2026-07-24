-- KitPlatform 193: FamilyOS F2 — Agreement foundation for Accountability
-- Depends on: 192_pack_family_os.sql
-- Local / pilot only until deploy is explicitly approved
-- Re-runnable after 198: CHECK includes both legacy F2 codes and taxonomy categories.

-- Widen target_type for accountability / reward rules (still Agreement, not punishment UI)
ALTER TABLE pack_family.agreement DROP CONSTRAINT IF EXISTS ck_agreement_target;
ALTER TABLE pack_family.agreement
    ADD CONSTRAINT ck_agreement_target CHECK (
        target_type IN (
            -- F2 legacy
            'routine_change',
            'commitment_change',
            'accountability_rule',
            'reward_rule',
            'other',
            -- Taxonomy (198+) — keep allowed so local seed can re-apply 193 after 198
            'foundation',
            'routine',
            'commitment',
            'reward',
            'accountability',
            'grace',
            'exception',
            'change',
            'value'
        )
    );

-- Structured terms (JSON): trigger, consequence_code, reward_code, notes — engine reads later
ALTER TABLE pack_family.agreement
    ADD COLUMN IF NOT EXISTS terms JSONB NOT NULL DEFAULT '{}'::jsonb;

COMMENT ON COLUMN pack_family.agreement.terms IS
    'Structured Agreement payload for Accountability Lite (F2.5+). Empty {} = free-text proposal_body only.';

COMMENT ON TABLE pack_family.agreement IS
    'Family Agreement — đồng thuận đổi routine/cam kết/luật accountability. Nền cho Accountability Engine; không phải data consent.';
