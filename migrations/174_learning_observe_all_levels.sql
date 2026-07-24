-- KitPlatform 174: Bật quan sát tại quầy soft cho toàn lộ trình L0–L5
-- Phân quyền chi tiết / L6: làm sau khi hoàn thiện nội dung.

UPDATE pack_learning.module
SET require_observation = TRUE, updated_at = NOW()
WHERE program_id = '22222222-2222-2222-2222-222222222201'
  AND COALESCE(require_observation, FALSE) = FALSE;

-- Gợi ý trong body L0 welcome (nếu còn bản cũ không nhắc quan sát)
UPDATE pack_learning.module
SET
    body_markdown = CASE
        WHEN body_markdown LIKE '%quan sát tại quầy%' THEN body_markdown
        ELSE body_markdown || E'\n\n## Sau khi đạt quiz\nQuản lý có thể **quan sát tại quầy** (soft) → «Đã áp dụng tại quầy». Không khóa bán — học tiếp bình thường.\n'
    END,
    updated_at = NOW()
WHERE code IN ('l0_welcome', 'l0_noi_quy', 'l2_customer_care', 'l3_stock_grn', 'l4_own_shift', 'l5_solo_ready')
  AND program_id = '22222222-2222-2222-2222-222222222201';
