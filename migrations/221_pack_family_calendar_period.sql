-- KitPlatform 221: FamilyOS calendar periods (seasons) above routines
-- Depends on: 192_pack_family_os.sql
-- Parents define date ranges (school year, summer, exam, travel…) and map weekdays → routines.

CREATE TABLE IF NOT EXISTS pack_family.calendar_period (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    code             VARCHAR(40) NOT NULL,
    display_name     VARCHAR(120) NOT NULL,
    kind             VARCHAR(30) NOT NULL DEFAULT 'custom',
    start_date       DATE NOT NULL,
    end_date         DATE NOT NULL,
    -- Higher wins when ranges overlap (travel > exam > summer > school_year).
    priority         INT NOT NULL DEFAULT 0,
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    notes            TEXT,
    settings         JSONB NOT NULL DEFAULT '{}'::jsonb,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT uq_calendar_period_family_code UNIQUE (family_id, code),
    CONSTRAINT ck_calendar_period_dates CHECK (end_date >= start_date),
    CONSTRAINT ck_calendar_period_kind CHECK (
        kind IN ('school_year', 'summer', 'exam', 'travel', 'holiday', 'custom')
    )
);

CREATE INDEX IF NOT EXISTS ix_calendar_period_family_range
    ON pack_family.calendar_period (family_id, start_date, end_date)
    WHERE deleted_at IS NULL AND is_active;

DROP TRIGGER IF EXISTS trg_calendar_period_row_version ON pack_family.calendar_period;
CREATE TRIGGER trg_calendar_period_row_version
    BEFORE UPDATE ON pack_family.calendar_period
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.calendar_period ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.calendar_period;
CREATE POLICY tenant_isolation ON pack_family.calendar_period
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.calendar_period IS
    'Family calendar season/period — date range that selects which routines apply.';
COMMENT ON COLUMN pack_family.calendar_period.priority IS
    'Higher priority wins on overlap; shorter ranges break ties in application code.';

CREATE TABLE IF NOT EXISTS pack_family.calendar_period_slot (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    period_id        UUID NOT NULL REFERENCES pack_family.calendar_period(id),
    -- ISO weekdays: 1=Mon … 7=Sun
    weekdays         SMALLINT[] NOT NULL DEFAULT '{}',
    routine_id       UUID NOT NULL REFERENCES pack_family.routine(id),
    sort_order       INT NOT NULL DEFAULT 0,
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_calendar_period_slot_weekdays CHECK (cardinality(weekdays) >= 1)
);

CREATE INDEX IF NOT EXISTS ix_calendar_period_slot_period
    ON pack_family.calendar_period_slot (period_id, sort_order)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_calendar_period_slot_row_version ON pack_family.calendar_period_slot;
CREATE TRIGGER trg_calendar_period_slot_row_version
    BEFORE UPDATE ON pack_family.calendar_period_slot
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.calendar_period_slot ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.calendar_period_slot;
CREATE POLICY tenant_isolation ON pack_family.calendar_period_slot
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.calendar_period_slot IS
    'Maps ISO weekdays inside a calendar period to a routine (e.g. Mon–Fri → summer_day).';
