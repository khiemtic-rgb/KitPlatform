-- KitPlatform 444: Xuân Hòa — website + fanpage cho Agent đăng bài.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Precondition: pack_content.brand code xuanhoa (293), site_target / channel_target (281).
-- Không ghi token. Không đụng NT_XUANHOA. Không tạo nhóm Facebook.
-- Chạy lại an toàn: giữ storedSecret và khối facebook đã kết nối.

BEGIN;

UPDATE pack_content.brand
SET
    default_cta_url = 'https://xuanhoa.novixa.vn',
    default_cta_label = 'Hỏi dược sĩ',
    operational_brief = $brief$
Nhà thuốc Xuân Hòa là nhà thuốc phố tại Thái Nguyên, phục vụ gia đình và người dân quanh quầy — không phải kênh bán phần mềm.
Quầy chính: số 30 đường Xuân Hòa, phường Phan Đình Phùng. Thêm điểm: số 526 đường Thống Nhất, cùng phường. Mở 07:00–22:00 tất cả các ngày. Điện thoại 0914.960.069.
Website bài viết: https://xuanhoa.novixa.vn/kien-thuc . Fanpage: https://www.facebook.com/xuanhoapharma .
Bài, caption và video nói với khách đến mua thuốc cho nhà: phụ huynh, người lớn tuổi, người cần dược sĩ giải thích cách dùng. Mời ghé quầy hoặc gọi nhà thuốc. Không kêu gọi tải app, không mời chủ nhà thuốc khác học FEFO hay POS, không cam kết khỏi bệnh, không kê đơn thay bác sĩ.
$brief$,
    tone_json = tone_json || jsonb_build_object(
        'positioning', 'Nhà thuốc phố tại Phan Đình Phùng, Thái Nguyên — tư vấn tại quầy, thuốc chính hãng, giao trong phố.',
        'audience', 'Gia đình và người dân quanh phường Phan Đình Phùng, Thái Nguyên: phụ huynh, người lớn tuổi, khách đến quầy Nhà thuốc Xuân Hòa cần dược sĩ tư vấn dùng thuốc đúng.',
        'tone', '["gần dân","dược sĩ","thận trọng","không thổi"]'::jsonb,
        'forbiddenTopics', '["kê đơn online","chữa khỏi","chính trị","bán phần mềm nhà thuốc","kêu gọi tải app","bài cho chủ nhà thuốc","Family OS"]'::jsonb,
        'preferredTerms', '["nhà thuốc","dược sĩ","quầy","hạn dùng","cả nhà","Phan Đình Phùng"]'::jsonb,
        'avoidTerms', '["#1","rẻ nhất","chữa khỏi","tải app","FEFO","POS"]'::jsonb,
        'hashtags', '["#NhaThuocXuanHoa","#ThaiNguyen"]'::jsonb,
        'ctaStyle', 'Mời ghé số 30 đường Xuân Hòa hoặc gọi 0914.960.069. Không kêu gọi tải ứng dụng.',
        'voiceNotes', 'Viết cho khách nhà thuốc. Đồng nghiệp dược và phần mềm Novixa không phải tệp này.',
        'problems', '["Khách tự ý dùng thuốc","Không biết lúc nào nên hỏi dược sĩ","Tủ thuốc gia đình để lẫn hạn"]'::jsonb,
        'needs', '["Tư vấn đúng tại quầy","Nhắc cách dùng cho cả nhà","Biết nhà thuốc mở đến mấy giờ"]'::jsonb,
        'desires', '["Yên tâm khi mua cho người thân","Gặp dược sĩ quen ở phố"]'::jsonb,
        'contentPillars', '["Tư vấn tại quầy","Dùng thuốc an toàn","Sức khỏe gia đình","Tủ thuốc nhà"]'::jsonb,
        'claimsAllowed', '["Hỏi dược sĩ trước khi dùng","Thuốc chính hãng tại quầy","Nhà thuốc mở 07:00–22:00"]'::jsonb,
        'claimsForbidden', '["Chữa khỏi bệnh","Bán thuốc không kê khi pháp luật cấm","Cam kết khỏi sau X ngày","Thay bác sĩ"]'::jsonb,
        'products', '["Thuốc và sản phẩm đang bán tại quầy — không biến bài thành catalogue"]'::jsonb,
        'services', '["Tư vấn dược sĩ tại quầy","Đặt thuốc giao trong phố","Giữ đơn"]'::jsonb,
        'differentiators', '["Quầy thật trong phố Phan Đình Phùng","Dược sĩ tư vấn trước khi khách mang thuốc về"]'::jsonb,
        'proofPoints', '["Địa chỉ và giờ mở cửa công khai trên xuanhoa.novixa.vn","Fanpage Nhà Thuốc Xuân Hòa tại Thái Nguyên"]'::jsonb,
        'competitors', '["Nhà thuốc chuỗi hô khẩu hiệu","Bán online không tư vấn"]'::jsonb,
        'goodExamples', '["Khi nào nên hỏi dược sĩ trước khi tự mua thuốc hạ sốt cho trẻ","Cách để tủ thuốc gia đình không lẫn hạn"]'::jsonb,
        'badExamples', '["Bài FEFO và tiền kẹt cho chủ nhà thuốc","Tải app Novixa","Tạo website nhà thuốc trong 5 phút","Cam kết khỏi bệnh"]'::jsonb
    ),
    updated_at = NOW()
WHERE code = 'xuanhoa';

UPDATE pack_content.brand
SET tone_json = jsonb_set(
        tone_json,
        '{knowledgeUnits}',
        CASE
            WHEN jsonb_typeof(tone_json -> 'knowledgeUnits') = 'array' THEN tone_json -> 'knowledgeUnits'
            ELSE '[]'::jsonb
        END || jsonb_build_array(
            'Quầy Nhà thuốc Xuân Hòa: số 30 đường Xuân Hòa và số 526 đường Thống Nhất, phường Phan Đình Phùng, tỉnh Thái Nguyên. Mở 07:00–22:00 mọi ngày. Điện thoại 0914.960.069. Bài đăng lên https://xuanhoa.novixa.vn/kien-thuc và fanpage https://www.facebook.com/xuanhoapharma . Khách là gia đình trong phố, không phải chủ nhà thuốc khác.'
        ),
        true
    ),
    updated_at = NOW()
WHERE code = 'xuanhoa'
  AND COALESCE(tone_json ->> 'knowledgeUnits', '') NOT LIKE '%facebook.com/xuanhoapharma%';

INSERT INTO pack_content.site_target (
    brand_id, code, name, connector_type, base_url, config_json, is_active, sort_order
)
SELECT
    b.id,
    'xuanhoa-web',
    'xuanhoa.novixa.vn',
    'astro_git',
    'https://xuanhoa.novixa.vn',
    jsonb_build_object(
        'owner', 'khiemtic-rgb',
        'repo', 'KitPlatform',
        'branch', 'main',
        'contentPath', 'client/pharmacy-storefront/src/content/kien-thuc',
        'imagePath', 'client/pharmacy-storefront/public/images/kien-thuc',
        'contentFormat', 'pharmacy',
        'newsCategory', 'Kiến thức sức khỏe',
        'publicUrl', 'https://xuanhoa.novixa.vn/kien-thuc'
    ),
    TRUE,
    10
FROM pack_content.brand b
WHERE b.code = 'xuanhoa'
ON CONFLICT (brand_id, code) DO UPDATE SET
    name = EXCLUDED.name,
    connector_type = EXCLUDED.connector_type,
    base_url = EXCLUDED.base_url,
    config_json = pack_content.site_target.config_json
        || EXCLUDED.config_json
        || jsonb_strip_nulls(jsonb_build_object(
            'storedSecret', pack_content.site_target.config_json -> 'storedSecret'
        )),
    is_active = TRUE,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

INSERT INTO pack_content.channel_target (
    brand_id, code, name, channel_type, external_id, config_json, is_active, sort_order
)
SELECT
    b.id,
    'xuanhoa-fanpage',
    'Nhà Thuốc Xuân Hòa',
    'facebook_page',
    NULL,
    jsonb_build_object(
        'accepts', jsonb_build_array('post', 'video'),
        'pageUrl', 'https://www.facebook.com/xuanhoapharma'
    ),
    TRUE,
    10
FROM pack_content.brand b
WHERE b.code = 'xuanhoa'
ON CONFLICT (brand_id, code) DO UPDATE SET
    name = EXCLUDED.name,
    channel_type = EXCLUDED.channel_type,
    external_id = COALESCE(pack_content.channel_target.external_id, EXCLUDED.external_id),
    config_json = pack_content.channel_target.config_json
        || EXCLUDED.config_json
        || jsonb_strip_nulls(jsonb_build_object(
            'storedSecret', pack_content.channel_target.config_json -> 'storedSecret',
            'facebook', pack_content.channel_target.config_json -> 'facebook'
        )),
    is_active = TRUE,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

COMMIT;
