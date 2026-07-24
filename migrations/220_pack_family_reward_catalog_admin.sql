-- KitPlatform 220: reward_catalog description + admin upsert support
-- Depends on: 219_pack_family_member_mood_entry.sql

ALTER TABLE pack_family.reward_catalog
    ADD COLUMN IF NOT EXISTS description VARCHAR(280);

COMMENT ON COLUMN pack_family.reward_catalog.description IS
    'Kid-facing subtitle on treasure cards; optional admin-editable copy.';

UPDATE pack_family.reward_catalog
SET description = CASE title
    WHEN 'Kem yêu thích' THEN '1 ly kem tùy chọn'
    WHEN 'Đồ chơi nhỏ' THEN 'Gấu bông hoặc đồ chơi nhỏ'
    WHEN 'Sách mới' THEN '1 cuốn sách mới'
    WHEN '30 phút chơi game' THEN '30 phút chơi game'
    WHEN 'Bố mẹ chọn' THEN 'Phần thưởng do bố mẹ lựa chọn cho cả gia đình'
    ELSE description
END
WHERE description IS NULL;
