-- KitPlatform 186: cho phép kind customer_feedback (1–3★) + backfill ghi nhận từ phản hồi khách

-- 1) Mở CHECK — app đã ghi kind customer_feedback nhưng DB chỉ cho customer_praise
ALTER TABLE pack_learning.recognition
    DROP CONSTRAINT IF EXISTS ck_learning_recognition_kind;

ALTER TABLE pack_learning.recognition
    ADD CONSTRAINT ck_learning_recognition_kind CHECK (
        kind IN (
            'course_complete',
            'module_complete',
            'birthday',
            'work_anniversary',
            'customer_praise',
            'customer_feedback',
            'custom',
            'badge_award'
        )
    );

-- 2) Backfill recognition cho feedback chưa gắn (employee có sẵn)
DO $$
DECLARE
    fb RECORD;
    new_id UUID;
    rec_kind TEXT;
    rec_title TEXT;
    rec_body TEXT;
    rec_badge TEXT;
BEGIN
    FOR fb IN
        SELECT id, tenant_id, employee_id, rating, comment
        FROM pack_learning.customer_sale_feedback
        WHERE recognition_id IS NULL
          AND employee_id IS NOT NULL
        ORDER BY created_at
    LOOP
        IF fb.rating >= 4 THEN
            rec_kind := 'customer_praise';
            rec_badge := 'customer_praise';
            rec_title := format('Khách đánh giá %s★', fb.rating);
            rec_body := COALESCE(NULLIF(trim(fb.comment), ''), 'Khách hàng hài lòng với phục vụ tại quầy.');
        ELSE
            rec_kind := 'customer_feedback';
            rec_badge := NULL;
            rec_title := format('Khách đánh giá %s★', fb.rating);
            rec_body := COALESCE(NULLIF(trim(fb.comment), ''), 'Khách đã góp ý sau mua hàng.');
        END IF;

        INSERT INTO pack_learning.recognition (
            tenant_id, employee_id, kind, title, body, badge_code,
            created_by_user_id, is_public
        )
        VALUES (
            fb.tenant_id, fb.employee_id, rec_kind, rec_title, rec_body, rec_badge,
            NULL, TRUE
        )
        RETURNING id INTO new_id;

        UPDATE pack_learning.customer_sale_feedback
        SET recognition_id = new_id
        WHERE id = fb.id;

        IF rec_kind = 'customer_praise' THEN
            INSERT INTO pack_learning.badge (
                tenant_id, employee_id, badge_code, title, source_recognition_id, earned_at
            )
            VALUES (
                fb.tenant_id, fb.employee_id, 'customer_praise', 'Được khách hàng khen',
                new_id, NOW()
            )
            ON CONFLICT (tenant_id, employee_id, badge_code) DO UPDATE SET
                title = EXCLUDED.title,
                source_recognition_id = EXCLUDED.source_recognition_id,
                earned_at = NOW();
        END IF;
    END LOOP;
END $$;

INSERT INTO public.kit_schema_migrations (filename, applied_at)
VALUES ('186_learning_customer_feedback_kind.sql', NOW())
ON CONFLICT (filename) DO NOTHING;
