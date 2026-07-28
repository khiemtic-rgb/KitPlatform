-- KitPlatform 240: Behavior OS Wave 1 — habit lifecycle, reflection, behavior events
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.
-- Depends on: 192+

-- =============================================================================
-- Habit stage on commitment templates (Routine items graduate over time)
-- =============================================================================
ALTER TABLE pack_family.commitment_template
    ADD COLUMN IF NOT EXISTS habit_stage VARCHAR(32) NOT NULL DEFAULT 'new',
    ADD COLUMN IF NOT EXISTS habit_streak_days INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS habit_last_done_date DATE,
    ADD COLUMN IF NOT EXISTS reminder_suppressed BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS habit_stage_changed_at TIMESTAMPTZ;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_template_habit_stage'
    ) THEN
        ALTER TABLE pack_family.commitment_template
            ADD CONSTRAINT ck_commitment_template_habit_stage
            CHECK (habit_stage IN (
                'new', 'guided', 'assisted', 'habit_forming', 'autonomous', 'maintained'
            ));
    END IF;
END $$;

COMMENT ON COLUMN pack_family.commitment_template.habit_stage IS
    'Behavior OS lifecycle: new→…→autonomous→maintained. Autonomous+ suppress reminders.';
COMMENT ON COLUMN pack_family.commitment_template.reminder_suppressed IS
    'When true, FamilyOS treats this habit as graduated — no due/overdue nudge.';

-- Snapshot on daily commitment (copied at materialize; refreshed on read via template join)
ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS habit_stage VARCHAR(32),
    ADD COLUMN IF NOT EXISTS reminder_suppressed BOOLEAN NOT NULL DEFAULT FALSE;

-- =============================================================================
-- Behavior event bus (KPI / Twin later)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.behavior_event (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    member_id       UUID REFERENCES pack_family.membership(id),
    event_type      VARCHAR(64) NOT NULL,
    commitment_id   UUID REFERENCES pack_family.commitment(id),
    template_id     UUID REFERENCES pack_family.commitment_template(id),
    payload_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_behavior_event_type CHECK (event_type IN (
        'commitment_done',
        'commitment_skipped',
        'reflection_submitted',
        'habit_stage_changed',
        'reminder_suppressed',
        'reminder_fired',
        'parent_nudge',
        'self_start'
    ))
);

CREATE INDEX IF NOT EXISTS ix_behavior_event_family_time
    ON pack_family.behavior_event (family_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_behavior_event_type_time
    ON pack_family.behavior_event (tenant_id, event_type, occurred_at DESC);

ALTER TABLE pack_family.behavior_event ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.behavior_event;
CREATE POLICY tenant_isolation ON pack_family.behavior_event
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.behavior_event IS
    'Behavior OS event bus — Autonomy / Intervention / Graduation KPIs.';

-- =============================================================================
-- Post-completion reflection (one question, ~15s)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.commitment_reflection (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    commitment_id   UUID NOT NULL REFERENCES pack_family.commitment(id),
    member_id       UUID REFERENCES pack_family.membership(id),
    prompt_code     VARCHAR(40) NOT NULL,
    answer_text     TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_commitment_reflection UNIQUE (commitment_id),
    CONSTRAINT ck_reflection_prompt CHECK (prompt_code IN (
        'hardest', 'learned', 'improve_tomorrow'
    )),
    CONSTRAINT ck_reflection_answer_len CHECK (
        char_length(btrim(answer_text)) BETWEEN 1 AND 500
    )
);

CREATE INDEX IF NOT EXISTS ix_commitment_reflection_family
    ON pack_family.commitment_reflection (family_id, created_at DESC);

ALTER TABLE pack_family.commitment_reflection ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.commitment_reflection;
CREATE POLICY tenant_isolation ON pack_family.commitment_reflection
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.commitment_reflection IS
    'Wave 1 Behavior OS — one reflection prompt after commitment done.';
