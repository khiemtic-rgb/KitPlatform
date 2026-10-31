-- KitPlatform 419: Brand knowledge base (tone_json.knowledgeUnits)
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- knowledgeUnits = kho tri thức của thương hiệu, mỗi phần tử một đơn vị (bản đồ chủ đề, công thức, lỗi cần tránh, quy trình).
-- Hệ thống chọn đơn vị liên quan theo chủ đề khi dựng prompt (ContentBrandKnowledge.SelectKnowledge), nên không giới hạn số đơn vị lưu.
-- Không đổi schema: tone_json đã là JSONB. Chỉ chuyển dữ liệu của brand 'trading' từ ô tạm "competitors" sang knowledgeUnits.

COMMENT ON COLUMN pack_content.brand.tone_json IS
    'Brand Brain JSON. Key knowledgeUnits: brand knowledge base, one unit per array item, selected by relevance at prompt time.';

UPDATE pack_content.brand
SET tone_json = jsonb_set(tone_json, '{knowledgeUnits}', tone_json -> 'competitors', true)
                || jsonb_build_object('competitors', '["Các kênh dạy trade và nhóm kèo trên mạng xã hội"]'::jsonb),
    updated_at = NOW()
WHERE code = 'trading'
  AND jsonb_typeof(tone_json -> 'competitors') = 'array'
  AND jsonb_array_length(tone_json -> 'competitors') > 0
  AND COALESCE(jsonb_typeof(tone_json -> 'knowledgeUnits'), 'null') <> 'array';
