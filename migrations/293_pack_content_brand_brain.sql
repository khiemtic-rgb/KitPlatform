-- KitPlatform 293: Brand Brain seed (tone_json) + 3 acceptance Core Ideas
-- Manifest: deploy/ubuntu/migration-files.content.txt only
-- Brands are rows in pack_content.brand — not ERP tenants. Do not touch NT_XUANHOA.

-- =============================================================================
-- Upsert Brand Brain (merge JSON; keep operator edits on keys they already filled)
-- =============================================================================
INSERT INTO pack_content.brand (
    code, name, default_cta_label, default_cta_url, monthly_ceiling_usd, image_tier,
    pause_when_exceeded, is_active, sort_order, operational_brief, tone_json, visual_kit_json
)
VALUES
(
    'novixa', 'Novixa',
    'Dùng thử Novixa', 'https://novixa.vn',
    40, 'balanced', TRUE, TRUE, 10,
    'Novixa là nền tảng quản trị nhà thuốc: tồn kho, FEFO, dòng tiền, sổ sách. Viết cho chủ nhà thuốc độc lập / chuỗi nhỏ. Giọng thực tế, hiện trường, không phô trương. Không chữa bệnh, không cam kết lãi, không #1 thị trường.',
    $json${
      "positioning": "Nền tảng quản trị nhà thuốc — giúp chủ nhà thuốc kiểm soát tồn, hạn dùng và dòng tiền.",
      "audience": "Chủ nhà thuốc độc lập, chuỗi nhỏ, dược sĩ phụ trách kho",
      "tone": ["thực tế", "hiện trường", "tin cậy", "không phô trương"],
      "forbiddenTopics": ["chữa bệnh", "so sánh giá đối thủ", "chính trị"],
      "preferredTerms": ["nền tảng", "module", "FEFO", "dòng tiền", "tồn kho"],
      "avoidTerms": ["siêu rẻ", "#1 thị trường", "cam kết lãi"],
      "hashtags": ["#Novixa", "#NhaThuoc"],
      "ctaStyle": "Mời dùng thử / xem module — không ép chốt",
      "voiceNotes": "Lấy ví dụ vận hành nhà thuốc. Một bài một vấn đề. Không viết như Famixa hay trà.",
      "problems": ["Mất thuốc hết hạn", "Không biết lãi thật", "Excel lệch sổ", "Nhân viên quên FEFO"],
      "needs": ["FEFO", "Báo cáo tồn", "Đối soát đơn", "Nhắc hạn dùng"],
      "desires": ["Chủ nhà thuốc ngủ ngon", "Kho sạch hạn", "Nhìn dòng tiền trong ngày"],
      "contentPillars": ["Vận hành kho", "Dòng tiền", "Tuân thủ GPP", "Câu chuyện khách"],
      "claimsAllowed": ["Giúp kiểm soát tồn và hạn dùng", "Nhắc FEFO theo lô", "Nhìn đơn và quỹ trong ngày"],
      "claimsForbidden": ["Chữa khỏi bệnh", "Cam kết tăng lãi X%", "#1 thị trường nhà thuốc", "Thay thế dược sĩ"],
      "products": ["POS nhà thuốc", "Tồn kho / lô / hạn", "Sổ quỹ", "Báo cáo vận hành"],
      "services": ["Onboarding hiện trường", "Hỗ trợ go-live"],
      "differentiators": ["Làm trên hiện trường nhà thuốc", "Không demo suông"],
      "proofPoints": ["Pilot DEMO_PHARMACY", "Module tồn + FEFO đã vận hành"],
      "competitors": ["Excel", "Phần mềm kế toán generic", "POS bán lẻ không hiểu thuốc"],
      "goodExamples": ["FEFO 3 bước cho chủ nhà thuốc: nhập lô → xếp hạn → xuất đúng hạn"],
      "badExamples": ["Bài generic «đồng hành cùng bạn» không nói kho / hạn / quỹ"]
    }$json$::jsonb,
    $json${"style":"Modern healthcare, sạch, tin cậy","colors":"Trắng + xanh lá Novixa","imageNotes":"Brand-safe; không chữ chồng ảnh MXH"}$json$::jsonb
),
(
    'famixa', 'Famixa',
    'Tìm hiểu Famixa', 'https://famixa.vn',
    30, 'balanced', TRUE, TRUE, 20,
    'Famixa là Family OS — điều phối việc nhà, lịch, chăm sóc người thân. Không phải phần mềm nhà thuốc. Không viết FEFO, lô thuốc, GPP. Giọng ấm, gần gũi, tôn trọng gia đình Việt.',
    $json${
      "positioning": "Family OS — giúp gia đình điều phối việc nhà và chăm sóc nhau rõ ràng hơn.",
      "audience": "Bố mẹ bận, người chăm sóc ông bà, gia đình nhiều thế hệ",
      "tone": ["ấm", "gần gũi", "tôn trọng", "không sến"],
      "forbiddenTopics": ["bán thuốc", "FEFO", "GPP", "chính trị"],
      "preferredTerms": ["gia đình", "việc nhà", "nhắc việc", "cùng chăm"],
      "avoidTerms": ["nhà thuốc", "lô hạn", "POS", "dòng tiền cửa hàng"],
      "hashtags": ["#Famixa", "#GiaDinh"],
      "ctaStyle": "Mời dùng thử nhẹ — không đe dọa",
      "voiceNotes": "Góc gia đình / chăm sóc. Không mượn ngôn ngữ Novixa hay kho thuốc.",
      "problems": ["Việc nhà rơi vào một người", "Quên lịch khám ông bà", "Anh chị em không đồng bộ"],
      "needs": ["Lịch chung", "Nhắc việc", "Phân vai trong nhà"],
      "desires": ["Gia đình đỡ cãi", "Người già được chăm đều", "Tối về còn thời gian"],
      "contentPillars": ["Điều phối việc nhà", "Chăm người thân", "Lịch gia đình"],
      "claimsAllowed": ["Giúp nhà nhớ việc và lịch", "Giảm sót việc chăm sóc"],
      "claimsForbidden": ["Chữa bệnh", "Thay thế bác sĩ", "Cam kết hạnh phúc gia đình", "Phần mềm nhà thuốc"],
      "products": ["Lịch gia đình", "Việc nhà", "Nhắc chăm sóc"],
      "services": ["Onboarding hộ gia đình"],
      "differentiators": ["Gia đình nhiều vai, không phải to-do cá nhân"],
      "proofPoints": ["Pilot DEMO_FAMILY", "Use-case lịch + việc nhà"],
      "competitors": ["Zalo nhóm gia đình", "Google Calendar cá nhân", "App to-do generic"],
      "goodExamples": ["Chủ nhật: 4 việc nhà không nên để một người gánh"],
      "badExamples": ["Bài FEFO / hạn dùng thuốc / lãi nhà thuốc"]
    }$json$::jsonb,
    $json${"style":"Ấm, gia đình, ánh sáng tự nhiên","colors":"Tím Famixa + kem","imageNotes":"Đời sống gia đình, không kho thuốc"}$json$::jsonb
),
(
    'kittech', 'KIT Tech',
    'Liên hệ KIT', 'https://kittech.vn',
    40, 'balanced', TRUE, TRUE, 5,
    'KIT xây park / nền tảng (Marketing, Local OS, Pharmacy, Family). Viết góc builder, sản phẩm số, vận hành đa brand. Không giả làm nhà thuốc hay trà. Việc làm thành phố chỉ khi nói hạ tầng Local OS — không đăng tin tuyển dụng giả.',
    $json${
      "positioning": "KIT xây các park vận hành (nội dung, thành phố, nhà thuốc, gia đình) trên một nền tảng.",
      "audience": "Founder, đối tác triển khai, đội vận hành số",
      "tone": ["rõ ràng", "builder", "thẳng", "không hô khẩu hiệu"],
      "forbiddenTopics": ["cam kết doanh thu X%", "chữa bệnh"],
      "preferredTerms": ["park", "nền tảng", "brand", "vận hành"],
      "avoidTerms": ["siêu AI", "thay thế hết nhân sự"],
      "hashtags": ["#KITTech"],
      "ctaStyle": "Liên hệ / xem park — không hard-sell",
      "voiceNotes": "Góc nền tảng và cách làm. Không copy bài Novixa hay Famixa.",
      "problems": ["Mỗi brand một giọng loạn", "Nội dung copy-paste giết brand", "Tool rời rạc"],
      "needs": ["Một factory nội dung", "Brand Brain", "Hàng đợi việc"],
      "desires": ["Ra bài đúng brand", "Kiểm soát chi phí AI"],
      "contentPillars": ["Cách làm park", "Đa brand", "Vận hành nội dung"],
      "claimsAllowed": ["Một Core Idea nhiều góc brand", "AI được phép bỏ brand không fit"],
      "claimsForbidden": ["Tự đăng Facebook không duyệt", "Một bài dán 6 brand", "Cam kết viral"],
      "products": ["KIT Marketing Park", "Local OS", "nền tảng đa pack"],
      "services": ["Triển khai park", "Đào tạo vận hành"],
      "differentiators": ["Brand là row, không phải tenant ERP", "Không auto-fanout"],
      "proofPoints": ["Park KIT_MKT đang chạy", "1 idea → N góc, skip được"],
      "competitors": ["Agency copy-paste", "Lịch Facebook thuần"],
      "goodExamples": ["Vì sao không copy một bài sang 6 brand"],
      "badExamples": ["Bài FEFO chi tiết như SOP nhà thuốc"]
    }$json$::jsonb,
    $json${"style":"Sạch, tech, grid","colors":"Xanh KIT + xám","imageNotes":"Không giả pharmacy / trà"}$json$::jsonb
),
(
    'vandinhtra', 'Vân Đỉnh Trà',
    'Xem trà', 'https://vandinhtra.vn',
    20, 'lean', TRUE, TRUE, 30,
    'Thương hiệu trà. Văn hoá uống trà, nguồn cây, ritural. Không phải nhà thuốc, không Family OS, không việc làm thành phố. Cấm claim chữa bệnh / giảm cân thần kỳ.',
    $json${
      "positioning": "Trà Việt — hương vị và ritural uống chậm.",
      "audience": "Người uống trà, quà biếu, khách yêu văn hoá trà",
      "tone": ["chậm", "thơm", "kể chuyện", "không khoa trương"],
      "forbiddenTopics": ["chữa bệnh", "thuốc", "FEFO nhà thuốc", "việc làm IT"],
      "preferredTerms": ["trà", "núi", "mùa hái", "pha"],
      "avoidTerms": ["dược liệu chữa bệnh", "giảm 5kg"],
      "hashtags": ["#VanDinhTra"],
      "ctaStyle": "Mời thưởng thức / xem mùa trà",
      "voiceNotes": "Chỉ nói trà và ritural. Bỏ ý tưởng kho thuốc hoặc tuyển dụng.",
      "problems": ["Trà công nghiệp nhạt", "Không biết mùa hái"],
      "needs": ["Nguồn rõ", "Cách pha", "Quà biếu"],
      "desires": ["Uống chậm", "Món quà tử tế"],
      "contentPillars": ["Mùa hái", "Cách pha", "Câu chuyện vùng"],
      "claimsAllowed": ["Hương vị mùa này", "Cách pha để thơm"],
      "claimsForbidden": ["Chữa bệnh", "Giảm cân thần kỳ", "Thay thuốc", "FEFO nhà thuốc"],
      "products": ["Trà shan", "Trà túi", "Quà biếu"],
      "services": ["Tư vấn pha", "Đặt quà"],
      "differentiators": ["Kể mùa và núi, không bán dược"],
      "proofPoints": ["Mùa hái được ghi trên site", "Hình ảnh vùng trồng"],
      "competitors": ["Trà túi siêu thị", "Trà detox claim"],
      "goodExamples": ["Mùa này nên pha shan thế nào cho khỏi đắng"],
      "badExamples": ["Bài FEFO / việc làm Thái Nguyên / Family OS"]
    }$json$::jsonb,
    $json${"style":"Ấm, lá, ánh sáng chiều","colors":"Nâu vàng trà","imageNotes":"Lá, ấm, bàn gỗ — không kho thuốc"}$json$::jsonb
),
(
    'xuanhoa', 'Nhà thuốc Xuân Hòa',
    'Liên hệ nhà thuốc', NULL,
    15, 'lean', TRUE, TRUE, 40,
    'Brand nội dung của một nhà thuốc thật (không phải tenant ERP). Góc chủ nhà thuốc / dược sĩ quầy. FEFO, tư vấn đúng thuốc, phục vụ dân phố. Không phải Family OS, không đăng việc làm thành phố, không bán trà.',
    $json${
      "positioning": "Nhà thuốc phố — tư vấn đúng thuốc, kho sạch hạn, dân tin.",
      "audience": "Khách phố, chủ nhà thuốc học nghề đồng nghiệp",
      "tone": ["gần dân", "dược sĩ", "thận trọng", "không thổi"],
      "forbiddenTopics": ["kê đơn online", "chữa khỏi", "chính trị"],
      "preferredTerms": ["nhà thuốc", "dược sĩ", "hạn dùng", "tư vấn"],
      "avoidTerms": ["#1", "rẻ nhất", "chữa khỏi"],
      "hashtags": ["#NhaThuocXuanHoa"],
      "ctaStyle": "Mời ghé quầy / hỏi dược sĩ",
      "voiceNotes": "Góc nhà thuốc thật. FEFO và tư vấn là hợp. Việc làm thành phố và Family OS thì bỏ.",
      "problems": ["Thuốc hết hạn trong tủ", "Khách tự ý dùng", "Kho lệch"],
      "needs": ["FEFO", "Tư vấn đúng", "Sổ bán trong ngày"],
      "desires": ["Khách tin", "Kho sạch", "Không bị phạt hạn dùng"],
      "contentPillars": ["An toàn thuốc", "FEFO", "Tư vấn quầy"],
      "claimsAllowed": ["Nhắc hạn dùng", "Hỏi dược sĩ trước khi dùng"],
      "claimsForbidden": ["Chữa khỏi bệnh", "Bán thuốc không kê khi pháp luật cấm", "Cam kết khỏi X ngày"],
      "products": ["Thuốc kê đơn tại quầy", "OTC", "Tư vấn"],
      "services": ["Tư vấn dược sĩ", "Giữ đơn"],
      "differentiators": ["Quầy thật, dân phố", "Không phải demo phần mềm"],
      "proofPoints": ["Vận hành nhà thuốc thật", "Quy trình hạn dùng tại quầy"],
      "competitors": ["Nhà thuốc chuỗi hô khẩu hiệu", "Bán online không tư vấn"],
      "goodExamples": ["Vì sao nhà thuốc phải xuất lô gần hạn trước"],
      "badExamples": ["Bài việc làm Thái Nguyên", "Bài Family OS"]
    }$json$::jsonb,
    $json${"style":"Quầy thuốc sạch, ánh sáng ban ngày","colors":"Xanh dược + trắng","imageNotes":"Quầy / hộp thuốc generic — không nhận diện ERP khác"}$json$::jsonb
),
(
    'tnlife', 'Thái Nguyên Life',
    'Xem việc & sự kiện', 'https://thainguyenlife.vn',
    15, 'lean', TRUE, TRUE, 50,
    'Mặt công khai Local OS — việc làm, sự kiện, phòng trọ Thái Nguyên. Không bán thuốc, không Family OS, không trà. Tin phải có nguồn; không bịa số liệu tuyển dụng.',
    $json${
      "positioning": "Cổng đời sống Thái Nguyên — việc làm, sự kiện, chỗ ở. City first.",
      "audience": "Sinh viên, người tìm việc, người mới đến Thái Nguyên",
      "tone": ["địa phương", "thiết thực", "lịch sự", "không spam"],
      "forbiddenTopics": ["bán thuốc", "FEFO", "Family OS", "chữa bệnh"],
      "preferredTerms": ["Thái Nguyên", "việc làm", "sự kiện", "phòng trọ"],
      "avoidTerms": ["nhà thuốc", "POS", "điều phối việc nhà"],
      "hashtags": ["#ThaiNguyenLife"],
      "ctaStyle": "Xem tin / nộp hồ sơ trên site — không auto-post group",
      "voiceNotes": "Chỉ fit ý tưởng thành phố / việc / sự kiện / phòng. Bỏ kho thuốc và gia đình số.",
      "problems": ["Tin việc rải group ảo", "Sự kiện trễ", "Phòng không rõ"],
      "needs": ["Tin đã duyệt", "Lịch sự kiện", "Phòng có mô tả"],
      "desires": ["Tìm việc gần", "Biết cuối tuần làm gì"],
      "contentPillars": ["Việc làm", "Sự kiện", "Phòng / chỗ ở"],
      "claimsAllowed": ["Tin đã người duyệt mới hiện", "Tập trung Thái Nguyên"],
      "claimsForbidden": ["Cam kết có việc", "Bịa lương", "Chữa bệnh", "FEFO nhà thuốc"],
      "products": ["Tin việc", "Sự kiện", "Phòng trọ"],
      "services": ["Duyệt tin công khai"],
      "differentiators": ["City first Thái Nguyên", "Không crawl Facebook"],
      "proofPoints": ["Park KIT_LOCAL / site Thái Nguyên Life", "Tin ACTIVE sau duyệt"],
      "competitors": ["Group Facebook rác", "Trang rao vặt toàn quốc"],
      "goodExamples": ["5 việc làm Thái Nguyên tuần này — nêu nguồn, không bịa lương"],
      "badExamples": ["Bài FEFO nhà thuốc", "Bài Famixa việc nhà"]
    }$json$::jsonb,
    $json${"style":"Thành phố, trời Thái Nguyên","colors":"Teal + đất","imageNotes":"Phố, sự kiện, không kho thuốc"}$json$::jsonb
)
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    default_cta_label = COALESCE(pack_content.brand.default_cta_label, EXCLUDED.default_cta_label),
    default_cta_url = COALESCE(pack_content.brand.default_cta_url, EXCLUDED.default_cta_url),
    operational_brief = COALESCE(NULLIF(btrim(pack_content.brand.operational_brief), ''), EXCLUDED.operational_brief),
    tone_json = pack_content.brand.tone_json || EXCLUDED.tone_json,
    visual_kit_json = pack_content.brand.visual_kit_json || EXCLUDED.visual_kit_json,
    is_active = TRUE,
    updated_at = NOW();

-- =============================================================================
-- 3 acceptance Core Ideas (paste/chấm Fit trên Idea Pool — không phải HTTP 200)
-- ACC-01 generic đồng hành → 4 góc khác nhau (Novixa / Xuân Hòa / Famixa / KIT)
-- ACC-02 FEFO → Novixa+Xuân Hòa FIT; Famixa/Vân Đỉnh/TN Life SKIP; KIT maybe/skip
-- ACC-03 việc làm TN → TN Life FIT; KIT maybe; còn lại SKIP
-- =============================================================================
DO $$
DECLARE
    v_brand uuid;
    v_topic uuid;
BEGIN
    SELECT id INTO v_brand
    FROM pack_content.brand
    WHERE lower(code) IN ('kittech', 'kit')
    ORDER BY CASE WHEN lower(code) = 'kittech' THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_brand IS NULL THEN
        SELECT id INTO v_brand FROM pack_content.brand WHERE is_active ORDER BY sort_order LIMIT 1;
    END IF;
    IF v_brand IS NULL THEN
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pack_content.content_package WHERE title = 'ACC-01 Đồng hành cùng khách hàng') THEN
        INSERT INTO pack_content.topic (brand_id, title, pillar, goal, priority, status, body_outline)
        VALUES (
            v_brand,
            'ACC-01 Đồng hành cùng khách hàng',
            'acceptance',
            'traffic',
            'P1',
            'Draft',
            'Ý tưởng generic — mỗi brand phải có góc riêng, không dịch cùng một bài.'
        )
        RETURNING id INTO v_topic;

        INSERT INTO pack_content.content_package (
            brand_id, topic_id, title, angle, audience, content_type, pillar, goal, priority, status, extra_json
        ) VALUES (
            v_brand, v_topic,
            'ACC-01 Đồng hành cùng khách hàng',
            'Generic — chưa gắn brand',
            'Khách hàng nói chung',
            'insight',
            'acceptance',
            'traffic',
            'P1',
            'Draft',
            $json${
              "coreIdea": {
                "insight": "Người ta nhớ thương hiệu khi được đồng hành sau giao dịch, không chỉ lúc chốt.",
                "problem": "Nhiều brand chỉ nói bán hàng, quên giai đoạn sau.",
                "coreMessage": "Đồng hành phải cụ thể theo đời sống brand — không phải khẩu hiệu chung.",
                "keywords": ["đồng hành", "sau bán"],
                "source": "Giả định insight — opinion",
                "sourceType": "opinion",
                "factOrOpinion": "opinion"
              },
              "acceptanceTest": {
                "key": "generic-dong-hanh",
                "expectDistinctAngles": ["novixa", "xuanhoa", "famixa", "kittech"]
              }
            }$json$::jsonb
        );
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pack_content.content_package WHERE title = 'ACC-02 FEFO nhà thuốc') THEN
        INSERT INTO pack_content.topic (brand_id, title, pillar, goal, priority, status, body_outline)
        VALUES (
            v_brand,
            'ACC-02 FEFO nhà thuốc',
            'acceptance',
            'traffic',
            'P0',
            'Draft',
            'First Expired First Out — chỉ brand nhà thuốc. Có nguồn/evidence, không bịa số liệu.'
        )
        RETURNING id INTO v_topic;

        INSERT INTO pack_content.content_package (
            brand_id, topic_id, title, angle, audience, content_type, pillar, goal, priority, status, extra_json
        ) VALUES (
            v_brand, v_topic,
            'ACC-02 FEFO nhà thuốc',
            'Quy tắc xuất lô gần hạn trước',
            'Chủ nhà thuốc / dược sĩ kho',
            'educational',
            'acceptance',
            'traffic',
            'P0',
            'Draft',
            $json${
              "coreIdea": {
                "insight": "Hết hạn trong tủ là lỗ thật, không phải lỗi kế toán.",
                "problem": "Xuất lô mới trước vì dễ lấy — lô cũ chết trong kho.",
                "coreMessage": "FEFO: nhập lô → xếp hạn → xuất đúng hạn.",
                "keywords": ["FEFO", "hạn dùng", "lô"],
                "source": "Thực hành kho nhà thuốc / GPP",
                "sourceUrl": null,
                "sourceType": "ops",
                "evidence": "Lô gần hạn phải ra trước; không bịa % tổn thất.",
                "factOrOpinion": "fact"
              },
              "acceptanceTest": {
                "key": "fefo",
                "expectedVerdict": {
                  "novixa": "fit",
                  "xuanhoa": "fit",
                  "famixa": "skip",
                  "vandinhtra": "skip",
                  "tnlife": "skip",
                  "kittech": "maybe"
                }
              }
            }$json$::jsonb
        );
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pack_content.content_package WHERE title = 'ACC-03 5 việc làm Thái Nguyên') THEN
        INSERT INTO pack_content.topic (brand_id, title, pillar, goal, priority, status, body_outline)
        VALUES (
            v_brand,
            'ACC-03 5 việc làm Thái Nguyên',
            'acceptance',
            'traffic',
            'P1',
            'Draft',
            'Tin địa phương. Cần nguồn. Không bịa lương. Brand nhà thuốc / trà / gia đình phải skip.'
        )
        RETURNING id INTO v_topic;

        INSERT INTO pack_content.content_package (
            brand_id, topic_id, title, angle, audience, content_type, pillar, goal, priority, status, extra_json
        ) VALUES (
            v_brand, v_topic,
            'ACC-03 5 việc làm Thái Nguyên',
            'Việc làm thành phố — chưa gắn tin cụ thể',
            'Người tìm việc tại Thái Nguyên',
            'educational',
            'acceptance',
            'traffic',
            'P1',
            'Draft',
            $json${
              "coreIdea": {
                "insight": "Người tìm việc cần tin địa phương đã lọc, không phải group rác.",
                "problem": "Tin việc rải group, lương bịa, hết hạn không gỡ.",
                "coreMessage": "Năm việc làm Thái Nguyên — nêu nguồn, không bịa số.",
                "keywords": ["việc làm", "Thái Nguyên"],
                "source": "Cần gắn tin Local OS / nhà tuyển dụng — chưa có URL cụ thể",
                "sourceType": "listing",
                "evidence": "Chỉ kể việc khi có nguồn; thiếu nguồn thì không bịa lương.",
                "factOrOpinion": "fact"
              },
              "acceptanceTest": {
                "key": "tn-jobs",
                "expectedVerdict": {
                  "tnlife": "fit",
                  "kittech": "maybe",
                  "novixa": "skip",
                  "xuanhoa": "skip",
                  "famixa": "skip",
                  "vandinhtra": "skip"
                }
              }
            }$json$::jsonb
        );
    END IF;
END $$;
