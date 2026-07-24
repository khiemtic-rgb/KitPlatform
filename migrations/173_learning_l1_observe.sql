-- KitPlatform 173: L1 nội dung rút gọn + quan sát tại quầy (soft)

-- ── Schema ──────────────────────────────────────────────────────────────────
ALTER TABLE pack_learning.module
    ADD COLUMN IF NOT EXISTS require_observation BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS pack_learning.module_observation (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             UUID         NOT NULL,
    enrollment_id         UUID         NOT NULL REFERENCES pack_learning.enrollment(id) ON DELETE CASCADE,
    module_id             UUID         NOT NULL REFERENCES pack_learning.module(id),
    employee_id           UUID         NOT NULL REFERENCES public.employees(id),
    observed_by_user_id   UUID         NOT NULL REFERENCES public.users(id),
    criteria_json         JSONB        NOT NULL DEFAULT '{}'::jsonb,
    note                  TEXT,
    observed_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_module_observation UNIQUE (tenant_id, enrollment_id, module_id)
);

CREATE INDEX IF NOT EXISTS ix_learning_observation_tenant_pending
    ON pack_learning.module_observation (tenant_id, observed_at DESC);

CREATE INDEX IF NOT EXISTS ix_learning_observation_employee
    ON pack_learning.module_observation (tenant_id, employee_id);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON pack_learning.module_observation TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON pack_learning.module_observation TO pharmacore;
    END IF;
END $$;

-- L1 bật quan sát soft (không khóa L2 / POS)
UPDATE pack_learning.module
SET require_observation = TRUE, updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222212'
   OR code = 'l1_pos_basic';

-- ── Nội dung L1 rút gọn ─────────────────────────────────────────────────────
UPDATE pack_learning.module
SET
    title = 'L1 — Tiếp đón khách & bán hàng trên POS',
    summary = 'Chào → lắng nghe → tư vấn → bán POS (FEFO) → thanh toán → cảm ơn. ~12 phút + quiz + áp dụng trên ca.',
    body_markdown = $md$## Mục tiêu

Sau bài này bạn có thể:
- Tiếp đón và lắng nghe nhu cầu đúng cách.
- Tạo hóa đơn trên POS Novixa.
- Chọn / tuân thủ gợi ý lô gần hạn trước (FEFO).
- Thanh toán và kết thúc giao dịch lịch sự.

## Vì sao quan trọng?

Quầy là nơi khách gặp nhà thuốc lần đầu. Trải nghiệm tốt → tin tưởng, quay lại, ít sai sót.
Bạn không chỉ bán thuốc — bạn đại diện hình ảnh nhà thuốc.

## Quy trình chuẩn (một giao dịch)

Khách đến → **Chào** → **Lắng nghe** → **Hỏi / tư vấn** → **Lập hóa đơn POS** → **Thanh toán** → **Cảm ơn** → (nhắc dùng / gắn khách nếu phù hợp)

### 1. Chào khách
Mỉm cười, giao tiếp bằng mắt, chào ngắn: «Em chào anh/chị — em hỗ trợ gì ạ?»
Không: nhìn điện thoại, để khách chờ lâu, không chào.

### 2. Lắng nghe nhu cầu
Hỏi trước khi bán: đang gặp gì? bao lâu? đã dùng thuốc gì? đang điều trị bệnh khác?
**Hiểu đúng nhu cầu** trước khi tư vấn.

### 3. Tư vấn
Dễ hiểu, đúng chuyên môn, không hù dọa, không ép mua.
Giải thích ngắn: công dụng · liều · cách dùng · lưu ý (trong quyền).
Không chắc → **mời dược sĩ phụ trách**.

### 4. Tạo hóa đơn trên POS
1. Tìm / tạo khách (nếu được và khách đồng ý).
2. Quét barcode hoặc tìm tên sản phẩm.
3. Kiểm tra số lượng.
4. **FEFO:** ưu tiên lô hạn dùng gần hơn — không tự đổi lô khi hệ thống đã gợi ý.
5. Kiểm tra giá / khuyến mãi trong quyền → thu tiền → in / đưa bill.

### 5. Kết thúc
Cảm ơn, chúc sức khỏe. Hướng dẫn dùng ngắn nếu biết (theo nhãn). Không tranh cãi.

## FEFO (nhận biết)

**First Expired First Out** — bán trước lô hết hạn sớm hơn (còn chất lượng).
Ví dụ: Lô B HSD 05/2026 trước Lô A HSD 12/2026.

## Không được

- Bán quá hạn / sai thuốc / sai hàm lượng / sai số lượng.
- Bỏ qua cảnh báo của Novixa.
- Tư vấn vượt quyền.

## Checklist nhớ (trên ca)

**Trước:** chào · hỏi nhu cầu · tư vấn trong quyền.
**Trong:** đúng khách · đúng thuốc · FEFO · đúng SL · thanh toán.
**Sau:** cảm ơn · gắn khách/điểm khi phù hợp · hướng dẫn dùng.

## Tình huống

1. Khách mua lại thuốc cảm → **hỏi tình trạng hiện tại**, không bán giống lần trước ngay.
2. POS gợi ý lô gần hạn → **chọn theo FEFO**.
3. Khách không cho SĐT → **giải thích lợi ích, tôn trọng quyết định** (không ép).

## Sau bài này

1. Ký xác nhận đã đọc (nếu được yêu cầu).
2. Làm quiz (≥ 80%).
3. **Áp dụng trên ca** (bán vài đơn đúng quy trình).
4. Quản lý có thể **quan sát tại quầy** (soft) → trạng thái «Đã áp dụng tại quầy».
Không khóa bán / không chặn học L2.
$md$,
    duration_minutes = 12,
    pass_score_pct = 80,
    require_ack = TRUE,
    level_code = 'L1',
    updated_at = NOW()
WHERE id = '22222222-2222-2222-2222-222222222212'
   OR (program_id = '22222222-2222-2222-2222-222222222201' AND code = 'l1_pos_basic');

-- Quiz L1 (6 câu)
DELETE FROM pack_learning.quiz_question
WHERE module_id = '22222222-2222-2222-2222-222222222212';

INSERT INTO pack_learning.quiz_question (module_id, sort_order, prompt, options_json, correct_option_index)
VALUES
(
    '22222222-2222-2222-2222-222222222212', 1,
    'FEFO nghĩa là gì?',
    '["Bán lô mới nhất trước","Ưu tiên bán lô hết hạn sớm hơn (còn chất lượng)","Chỉ kiểm FEFO khi kiểm kê tháng"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 2,
    'Bước đầu khi khách vào nhà thuốc?',
    '["In bill trước","Chào hỏi và hỏi nhu cầu","Nhờ khách tự lấy hàng rồi tính tiền"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 3,
    'Khách quay lại mua thuốc cảm — bạn nên?',
    '["Bán giống lần trước ngay","Hỏi tình trạng hiện tại trước khi tư vấn","Ép mua thêm nhiều sản phẩm"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 4,
    'POS gợi ý lô gần hết hạn hơn — bạn nên?',
    '["Chọn lô mới hơn cho đẹp","Chọn theo FEFO / gợi ý hệ thống","Tắt cảnh báo rồi bán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 5,
    'Khách không muốn để lại số điện thoại — bạn nên?',
    '["Ép khách mới bán","Giải thích lợi ích và tôn trọng quyết định","Từ chối bán"]'::jsonb,
    1
),
(
    '22222222-2222-2222-2222-222222222212', 6,
    'Khi không chắc về chuyên môn / liều phức tạp — bạn nên?',
    '["Tra mạng rồi bán","Mời dược sĩ phụ trách","Đoán theo đơn cũ"]'::jsonb,
    1
);
