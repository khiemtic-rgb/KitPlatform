-- Approval history and a review queue for safety flags already coded in ConsultationSafetyRules.
-- Inserts stay PENDING_REVIEW. This script does not approve clinical content.
-- Not in migration-files.prod.txt.

CREATE TABLE IF NOT EXISTS pharmacy_knowledge_approval_event (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    rule_code         VARCHAR(64)  NOT NULL,
    rule_version      TEXT,
    from_status       VARCHAR(32)  NOT NULL,
    to_status         VARCHAR(32)  NOT NULL,
    actor_user_id     TEXT         NOT NULL,
    reference_source  TEXT,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_pharmacy_knowledge_approval_event_rule
    ON pharmacy_knowledge_approval_event (rule_code, created_at);

CREATE TABLE IF NOT EXISTS pharmacy_safety_flag_review (
    flag_code         VARCHAR(64) PRIMARY KEY,
    safety_level      VARCHAR(32)  NOT NULL,
    message_vi        TEXT         NOT NULL,
    source_ref        TEXT         NOT NULL,
    approval_status   VARCHAR(32)  NOT NULL DEFAULT 'PENDING_REVIEW',
    reference_source  TEXT,
    approved_by       TEXT,
    approved_at       TIMESTAMPTZ,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_pharmacy_safety_flag_review_status
        CHECK (approval_status IN ('DRAFT', 'PENDING_REVIEW', 'APPROVED', 'RETIRED'))
);

INSERT INTO pharmacy_safety_flag_review (flag_code, safety_level, message_vi, source_ref, approval_status)
VALUES
    ('infant_under_2', 'refer_pharmacist', 'Trẻ dưới 2 tuổi — không tự tư vấn OTC tại quầy; chuyển dược sĩ.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('pregnant', 'refer_pharmacist', 'Khách mang thai — chỉ dược sĩ tư vấn và quyết định sản phẩm.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('breastfeeding', 'refer_pharmacist', 'Khách đang cho con bú — chỉ dược sĩ tư vấn và quyết định sản phẩm.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('difficulty_breathing', 'refer_medical', 'Khó thở — không bán OTC; hướng cấp cứu / bác sĩ ngay.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('shortness_of_breath', 'refer_medical', 'Khó thở — không bán OTC; hướng cấp cứu / bác sĩ ngay.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('chest_pain', 'refer_medical', 'Đau ngực — không bán OTC; hướng cấp cứu / bác sĩ ngay.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('unconscious', 'stop_sale', 'Ngất / co giật — dừng bán, gọi cấp cứu.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('seizure', 'stop_sale', 'Ngất / co giật — dừng bán, gọi cấp cứu.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('severe_bleeding', 'refer_medical', 'Chảy máu nặng — không bán OTC; hướng cấp cứu.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('swallowing_difficulty', 'refer_pharmacist', 'Khó nuốt — chuyển dược sĩ, không tự chọn thuốc.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('high_fever_infant', 'refer_medical', 'Trẻ nhỏ sốt cao — hướng khám ngay.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('infant_fever', 'refer_medical', 'Trẻ nhỏ có sốt — không tự tư vấn OTC; hướng khám.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW'),
    ('allergy_symptom', 'caution', 'Triệu chứng dị ứng — hỏi thêm tiền sử dị ứng / phản ứng thuốc trước khi gợi ý.', 'ConsultationSafetyRules.cs', 'PENDING_REVIEW')
ON CONFLICT (flag_code) DO NOTHING;

COMMENT ON TABLE pharmacy_safety_flag_review IS
    'Review queue only. The counter copilot does not read this table for OTC product lists.';

CREATE OR REPLACE FUNCTION pharmacy_knowledge_approval_event_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'pharmacy_knowledge_approval_event is append-only';
END;
$$;

DROP TRIGGER IF EXISTS trg_pharmacy_knowledge_approval_event_immutable
    ON pharmacy_knowledge_approval_event;

CREATE TRIGGER trg_pharmacy_knowledge_approval_event_immutable
    BEFORE UPDATE OR DELETE ON pharmacy_knowledge_approval_event
    FOR EACH ROW
    EXECUTE FUNCTION pharmacy_knowledge_approval_event_immutable();
