-- KitPlatform 155: Learning OS (Platform Capability) — pack_learning schema
-- Generic engine for Pharmacy / Clinic / SPA tracks. Content is versioned; progress is tenant-scoped.

CREATE SCHEMA IF NOT EXISTS pack_learning;

COMMENT ON SCHEMA pack_learning IS
    'Learning OS — shared onboarding/academy engine. Catalog + progress; pack-specific content via pack_code.';

-- Platform catalog (tenant_id NULL) or tenant-custom programs
CREATE TABLE IF NOT EXISTS pack_learning.program (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID REFERENCES public.tenants(id),
    code            VARCHAR(80)  NOT NULL,
    pack_code       VARCHAR(60)  NOT NULL,
    title           VARCHAR(200) NOT NULL,
    summary         TEXT,
    locale          VARCHAR(10)  NOT NULL DEFAULT 'vi-VN',
    version         INT          NOT NULL DEFAULT 1,
    status          VARCHAR(20)  NOT NULL DEFAULT 'published',
    sort_order      INT          NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_learning_program_status CHECK (status IN ('draft', 'published', 'archived'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_learning_program_platform_code
    ON pack_learning.program (code, version)
    WHERE tenant_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_learning_program_tenant_code
    ON pack_learning.program (tenant_id, code, version)
    WHERE tenant_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_learning_program_pack
    ON pack_learning.program (pack_code, status);

CREATE TABLE IF NOT EXISTS pack_learning.module (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    program_id          UUID         NOT NULL REFERENCES pack_learning.program(id) ON DELETE CASCADE,
    code                VARCHAR(80)  NOT NULL,
    title               VARCHAR(200) NOT NULL,
    summary             TEXT,
    body_markdown       TEXT         NOT NULL DEFAULT '',
    duration_minutes    INT          NOT NULL DEFAULT 5,
    level_code          VARCHAR(10)  NOT NULL DEFAULT 'L0',
    competency_codes    TEXT[]       NOT NULL DEFAULT '{}',
    sort_order          INT          NOT NULL DEFAULT 0,
    pass_score_pct      INT          NOT NULL DEFAULT 70,
    require_ack         BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_module_program_code UNIQUE (program_id, code),
    CONSTRAINT ck_learning_module_duration CHECK (duration_minutes > 0 AND duration_minutes <= 120),
    CONSTRAINT ck_learning_module_pass CHECK (pass_score_pct >= 0 AND pass_score_pct <= 100)
);

CREATE INDEX IF NOT EXISTS ix_learning_module_program_sort
    ON pack_learning.module (program_id, sort_order);

CREATE TABLE IF NOT EXISTS pack_learning.quiz_question (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    module_id             UUID         NOT NULL REFERENCES pack_learning.module(id) ON DELETE CASCADE,
    sort_order            INT          NOT NULL DEFAULT 0,
    prompt                TEXT         NOT NULL,
    options_json          JSONB        NOT NULL,
    correct_option_index  INT          NOT NULL,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_learning_quiz_correct CHECK (correct_option_index >= 0)
);

CREATE INDEX IF NOT EXISTS ix_learning_quiz_module
    ON pack_learning.quiz_question (module_id, sort_order);

CREATE TABLE IF NOT EXISTS pack_learning.enrollment (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            UUID         NOT NULL REFERENCES public.tenants(id),
    employee_id          UUID         NOT NULL REFERENCES public.employees(id),
    program_id           UUID         NOT NULL REFERENCES pack_learning.program(id),
    status               VARCHAR(20)  NOT NULL DEFAULT 'assigned',
    assigned_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    assigned_by_user_id  UUID REFERENCES public.users(id),
    started_at           TIMESTAMPTZ,
    completed_at         TIMESTAMPTZ,
    CONSTRAINT uq_learning_enrollment UNIQUE (tenant_id, employee_id, program_id),
    CONSTRAINT ck_learning_enrollment_status CHECK (
        status IN ('assigned', 'in_progress', 'completed', 'cancelled')
    )
);

CREATE INDEX IF NOT EXISTS ix_learning_enrollment_employee
    ON pack_learning.enrollment (tenant_id, employee_id, status);

CREATE TABLE IF NOT EXISTS pack_learning.module_progress (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    enrollment_id           UUID         NOT NULL REFERENCES pack_learning.enrollment(id) ON DELETE CASCADE,
    module_id               UUID         NOT NULL REFERENCES pack_learning.module(id),
    status                  VARCHAR(20)  NOT NULL DEFAULT 'not_started',
    score_pct               INT,
    quiz_attempts           INT          NOT NULL DEFAULT 0,
    started_at              TIMESTAMPTZ,
    completed_at            TIMESTAMPTZ,
    acknowledged_at         TIMESTAMPTZ,
    acknowledged_by_user_id UUID REFERENCES public.users(id),
    updated_at              TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_learning_module_progress UNIQUE (enrollment_id, module_id),
    CONSTRAINT ck_learning_progress_status CHECK (
        status IN ('not_started', 'in_progress', 'passed', 'failed')
    )
);

CREATE INDEX IF NOT EXISTS ix_learning_progress_enrollment
    ON pack_learning.module_progress (enrollment_id, status);

GRANT USAGE ON SCHEMA pack_learning TO kitplatform;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA pack_learning TO kitplatform;
ALTER DEFAULT PRIVILEGES IN SCHEMA pack_learning
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO kitplatform;
