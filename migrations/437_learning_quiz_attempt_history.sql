-- KitPlatform 437: Learning — lịch sử từng lần làm bài kiểm tra và từng câu trả lời.
-- Append-only. module_progress vẫn giữ trạng thái / điểm tốt nhất; bảng này dùng để tìm câu hay sai và khoảng trống năng lực.
-- Idempotent; không sửa / backfill dữ liệu cũ. Not in migration-files.prod.txt.

CREATE TABLE IF NOT EXISTS pack_learning.quiz_attempt (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         UUID         NOT NULL REFERENCES public.tenants(id),
    enrollment_id     UUID         NOT NULL REFERENCES pack_learning.enrollment(id) ON DELETE CASCADE,
    module_id         UUID         NOT NULL REFERENCES pack_learning.module(id),
    employee_id       UUID         NOT NULL REFERENCES public.employees(id),
    user_id           UUID         NOT NULL REFERENCES public.users(id),
    variant           INT          NOT NULL,
    practice          BOOLEAN      NOT NULL,
    question_count    INT          NOT NULL CHECK (question_count > 0),
    correct_count     INT          NOT NULL CHECK (correct_count >= 0),
    score_pct         INT          NOT NULL CHECK (score_pct BETWEEN 0 AND 100),
    passed            BOOLEAN      NOT NULL,
    submitted_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_learning_quiz_attempt_module
    ON pack_learning.quiz_attempt (tenant_id, module_id, submitted_at DESC);

CREATE INDEX IF NOT EXISTS ix_learning_quiz_attempt_employee
    ON pack_learning.quiz_attempt (tenant_id, employee_id, submitted_at DESC);

CREATE TABLE IF NOT EXISTS pack_learning.quiz_attempt_answer (
    attempt_id        UUID         NOT NULL REFERENCES pack_learning.quiz_attempt(id) ON DELETE CASCADE,
    -- No FK: content migrations delete and reseed quiz_question; history must survive and must not block them.
    question_id       UUID         NOT NULL,
    chosen_option     INT          NOT NULL,
    is_correct        BOOLEAN      NOT NULL,
    PRIMARY KEY (attempt_id, question_id)
);

CREATE INDEX IF NOT EXISTS ix_learning_quiz_attempt_answer_question
    ON pack_learning.quiz_attempt_answer (question_id);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT ON pack_learning.quiz_attempt, pack_learning.quiz_attempt_answer TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT ON pack_learning.quiz_attempt, pack_learning.quiz_attempt_answer TO pharmacore;
    END IF;
END $$;
