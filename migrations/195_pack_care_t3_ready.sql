-- KitPlatform 195: Pack:Care — T3-ready care events / cohort / KPI hooks
-- Purpose: shape Community Health data plane (instrumentation-first).
-- NOT a production Community Health product. CustomerApp tables stay channel adapters.
-- Depends on: kit_uuid_v7, kit_rls, kit_bump_row_version, public.tenants, public.customers
-- Layer: Pack:Care

CREATE SCHEMA IF NOT EXISTS pack_care;

COMMENT ON SCHEMA pack_care IS
    'Novixa Care OS — T3-ready care event plane (cohorts + KPI hooks). Shaping / expansion; not live Community Health outcomes.';

-- =============================================================================
-- Platform module + tenant package (opt-in — do not force-enable on all pharmacies)
-- =============================================================================
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT v.code, v.name, v.description, v.verticals, v.sort_order
FROM (
    VALUES
        (
            'care_os',
            'Care OS',
            'Care event plane + cohort/KPI hooks for Community Health (T3-ready)',
            ARRAY['pharmacy', 'hybrid'],
            55
        )
) AS v(code, name, description, verticals, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = v.code
);

INSERT INTO kit_tenant.tenant_package (
    package_code, package_name, description, verticals, module_codes, sort_order
)
VALUES (
    'care_os',
    'Care OS (T3-ready)',
    'Instrumentation plane for Smart Care → Community Health — enable only for design/pilot shaping',
    ARRAY['pharmacy', 'hybrid'],
    ARRAY['care_os'],
    45
)
ON CONFLICT (package_code) DO UPDATE SET
    package_name = EXCLUDED.package_name,
    description = EXCLUDED.description,
    verticals = EXCLUDED.verticals,
    module_codes = EXCLUDED.module_codes,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

-- =============================================================================
-- care_event — append-oriented care facts (link to existing channel tables via source_*)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_care.care_event (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id         UUID NOT NULL REFERENCES public.tenants(id),
    workspace_id      UUID,
    customer_id       UUID REFERENCES public.customers(id),
    family_member_id  UUID,
    event_type        VARCHAR(64) NOT NULL,
    tier              SMALLINT NOT NULL DEFAULT 2,
    occurred_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source_system     VARCHAR(64) NOT NULL DEFAULT 'manual',
    source_ref_type   VARCHAR(64),
    source_ref_id     UUID,
    payload           JSONB NOT NULL DEFAULT '{}'::jsonb,
    correlation_id    UUID,
    actor_user_id     UUID,
    row_version       INT NOT NULL DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by        UUID,
    CONSTRAINT ck_care_event_tier CHECK (tier IN (1, 2, 3)),
    CONSTRAINT ck_care_event_type CHECK (event_type IN (
        'medication_reminder_scheduled',
        'medication_adherence_recorded',
        'medication_adherence_missed',
        'repurchase_suggested',
        'repurchase_converted',
        'chronic_cohort_assigned',
        'chronic_cohort_removed',
        'follow_up_due',
        'follow_up_completed',
        'referral_linked',
        'referral_completed',
        'booking_linked',
        'shift_quality_flagged',
        'academy_progress',
        'health_education_delivered',
        'care_note',
        'other'
    ))
);

CREATE INDEX IF NOT EXISTS ix_care_event_tenant_occurred
    ON pack_care.care_event (tenant_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_care_event_tenant_customer
    ON pack_care.care_event (tenant_id, customer_id, occurred_at DESC)
    WHERE customer_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_care_event_tenant_type
    ON pack_care.care_event (tenant_id, event_type, occurred_at DESC);

CREATE INDEX IF NOT EXISTS ix_care_event_source
    ON pack_care.care_event (tenant_id, source_system, source_ref_type, source_ref_id)
    WHERE source_ref_id IS NOT NULL;

DROP TRIGGER IF EXISTS trg_care_event_row_version ON pack_care.care_event;
CREATE TRIGGER trg_care_event_row_version
    BEFORE UPDATE ON pack_care.care_event
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_care.care_event ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_care.care_event;
CREATE POLICY tenant_isolation ON pack_care.care_event
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_care.care_event IS
    'T3-ready care facts. Populate from adapters (CustomerApp / Connect / Success / Learning) when wired. Manual insert OK for shaping.';

COMMENT ON COLUMN pack_care.care_event.tier IS
    '1=Smart Pharmacy ops signal, 2=Smart Care, 3=Community Health narrative signal';

-- =============================================================================
-- care_cohort_definition — catalog (seeded). Membership needs real data rules later.
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_care.care_cohort_definition (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    cohort_code     VARCHAR(64) NOT NULL,
    display_name    VARCHAR(160) NOT NULL,
    description     TEXT,
    tier_target     SMALLINT NOT NULL DEFAULT 2,
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    criteria        JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_care_cohort_code UNIQUE (cohort_code),
    CONSTRAINT ck_care_cohort_status CHECK (status IN ('active', 'draft', 'retired')),
    CONSTRAINT ck_care_cohort_tier CHECK (tier_target IN (1, 2, 3))
);

COMMENT ON TABLE pack_care.care_cohort_definition IS
    'Global cohort catalog (not tenant-scoped). criteria JSON is design-time until rule engine exists.';

COMMENT ON COLUMN pack_care.care_cohort_definition.criteria IS
    'NEEDS DATA / RULES: auto-assignment not implemented. Manual membership only until rule engine ships.';

INSERT INTO pack_care.care_cohort_definition (cohort_code, display_name, description, tier_target, status, criteria)
VALUES
    ('chronic_hypertension', 'Tăng huyết áp (theo dõi NT)', 'Cohort mạn tính HTA — theo dõi / nhắc / không bỏ sót', 2,
     'active', '{"needs_rules": true, "signals": ["rx_class", "counseling_tag", "manual"]}'::jsonb),
    ('chronic_diabetes', 'Đái tháo đường (theo dõi NT)', 'Cohort ĐTĐ — nhắc thuốc / tái khám', 2,
     'active', '{"needs_rules": true, "signals": ["rx_class", "manual"]}'::jsonb),
    ('medication_adherence_risk', 'Rủi ro tuân thủ thuốc', 'Missed doses / low adherence', 2,
     'active', '{"needs_rules": true, "signals": ["medication_adherence_events"]}'::jsonb),
    ('repurchase_due', 'Cần nhắc tái mua', 'Supply end approaching', 2,
     'active', '{"needs_rules": true, "signals": ["repurchase_suggestions"]}'::jsonb),
    ('follow_up_overdue', 'Quá hạn tái khám / follow-up', 'Care reminder overdue', 2,
     'active', '{"needs_rules": true, "signals": ["care_reminders"]}'::jsonb),
    ('community_education', 'Giáo dục sức khỏe cộng đồng', 'T3 narrative — health education touchpoints', 3,
     'draft', '{"needs_rules": true, "signals": ["manual", "campaign"]}'::jsonb)
ON CONFLICT (cohort_code) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    tier_target = EXCLUDED.tier_target,
    criteria = EXCLUDED.criteria,
    updated_at = NOW();

-- =============================================================================
-- care_cohort_membership — tenant members (empty until rules or manual assign)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_care.care_cohort_membership (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    cohort_id       UUID NOT NULL REFERENCES pack_care.care_cohort_definition(id),
    customer_id     UUID NOT NULL REFERENCES public.customers(id),
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    assigned_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    assigned_by     UUID,
    source          VARCHAR(32) NOT NULL DEFAULT 'manual',
    notes           TEXT,
    ended_at        TIMESTAMPTZ,
    row_version     INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_care_cohort_member_status CHECK (status IN ('active', 'ended')),
    CONSTRAINT ck_care_cohort_member_source CHECK (source IN ('manual', 'rule', 'import', 'event')),
    CONSTRAINT uq_care_cohort_member UNIQUE (tenant_id, cohort_id, customer_id)
);

CREATE INDEX IF NOT EXISTS ix_care_cohort_member_tenant_cohort
    ON pack_care.care_cohort_membership (tenant_id, cohort_id)
    WHERE status = 'active';

CREATE INDEX IF NOT EXISTS ix_care_cohort_member_customer
    ON pack_care.care_cohort_membership (tenant_id, customer_id)
    WHERE status = 'active';

DROP TRIGGER IF EXISTS trg_care_cohort_member_row_version ON pack_care.care_cohort_membership;
CREATE TRIGGER trg_care_cohort_member_row_version
    BEFORE UPDATE ON pack_care.care_cohort_membership
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_care.care_cohort_membership ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_care.care_cohort_membership;
CREATE POLICY tenant_isolation ON pack_care.care_cohort_membership
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_care.care_cohort_membership IS
    'NEEDS DATA: empty until manual assign or rule engine. Runtime KPI for cohorts needs active rows.';

-- =============================================================================
-- care_kpi_definition — seeded from NVX-CMP-05 seven operating problems
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_care.care_kpi_definition (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    kpi_code         VARCHAR(64) NOT NULL,
    display_name     VARCHAR(200) NOT NULL,
    description      TEXT,
    tier_target      SMALLINT NOT NULL DEFAULT 2,
    problem_index    SMALLINT,
    unit             VARCHAR(32) NOT NULL DEFAULT 'count',
    status           VARCHAR(20) NOT NULL DEFAULT 'design',
    compute_hints    JSONB NOT NULL DEFAULT '{}'::jsonb,
    runnable_when    TEXT NOT NULL DEFAULT 'Sau khi có đủ care_event / cohort membership theo compute_hints',
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_care_kpi_code UNIQUE (kpi_code),
    CONSTRAINT ck_care_kpi_status CHECK (status IN ('design', 'instrumented', 'live', 'retired')),
    CONSTRAINT ck_care_kpi_tier CHECK (tier_target IN (1, 2, 3))
);

COMMENT ON TABLE pack_care.care_kpi_definition IS
    'KPI catalog for Community Health shaping. status=design until compute job exists + enough events.';

INSERT INTO pack_care.care_kpi_definition (
    kpi_code, display_name, description, tier_target, problem_index, unit, status, compute_hints, runnable_when
)
VALUES
    ('hta_followup_coverage',
     'Không bỏ sót khách HTA',
     '% khách cohort HTA có follow-up trong N ngày',
     2, 1, 'percent', 'design',
     '{"cohort":"chronic_hypertension","events":["follow_up_completed","care_note"],"window_days":30}'::jsonb,
     'Khi có membership HTA + event follow-up (≥ vài chục khách / 30 ngày). Chưa có job aggregate.'),
    ('repurchase_reminder_conversion',
     'Nhắc mua → tái mua',
     'Tỷ lệ repurchase_suggested → repurchase_converted',
     2, 2, 'percent', 'design',
     '{"events":["repurchase_suggested","repurchase_converted"]}'::jsonb,
     'Khi adapter ghi event từ repurchase_suggestions + sales. Hiện chỉ schema.'),
    ('follow_up_overdue_count',
     'Chưa tái khám / quá hạn follow-up',
     'Số open follow_up_due chưa completed',
     2, 3, 'count', 'design',
     '{"events":["follow_up_due","follow_up_completed"]}'::jsonb,
     'Khi care_reminders được dual-write vào care_event.'),
    ('referral_completion_rate',
     'Chuyển tuyến / kết nối bác sĩ',
     '% referral_linked → referral_completed',
     2, 4, 'percent', 'design',
     '{"events":["referral_linked","referral_completed"],"sources":["pack_connect.referrals"]}'::jsonb,
     'Khi Connect referral dual-write vào care_event (chưa wire).'),
    ('care_effectiveness_index',
     'Hiệu quả chăm sóc (composite)',
     'Chỉ số tổng hợp — định nghĩa lại theo quý khi đủ event',
     3, 5, 'index', 'design',
     '{"depends_on":["hta_followup_coverage","repurchase_reminder_conversion","follow_up_overdue_count"]}'::jsonb,
     'T3 later — sau khi các KPI con status=live ≥ 1 quý.'),
    ('shift_quality_exceptions',
     'Ngoại lệ chất lượng ca',
     'Số ca gắn shift_quality_flagged',
     1, 6, 'count', 'design',
     '{"events":["shift_quality_flagged"],"sources":["success_shift_checklist"]}'::jsonb,
     'Khi Success checklist dual-write exception vào care_event.'),
    ('academy_counseling_pass_rate',
     'Đào tạo tư vấn chuẩn',
     '% NV pass module tư vấn / enrolled',
     1, 7, 'percent', 'design',
     '{"events":["academy_progress"],"sources":["pack_learning"]}'::jsonb,
     'Khi Learning dual-write academy_progress (pack_learning tồn tại trên tenant).')
ON CONFLICT (kpi_code) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    compute_hints = EXCLUDED.compute_hints,
    runnable_when = EXCLUDED.runnable_when,
    updated_at = NOW();

-- =============================================================================
-- care_metric_snapshot — future aggregates (empty; compute job NOT implemented)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_care.care_metric_snapshot (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    kpi_id          UUID NOT NULL REFERENCES pack_care.care_kpi_definition(id),
    period_start    DATE NOT NULL,
    period_end      DATE NOT NULL,
    value_num       NUMERIC(18, 4),
    value_den       NUMERIC(18, 4),
    value_ratio     NUMERIC(18, 6),
    sample_size     INT NOT NULL DEFAULT 0,
    computed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    meta            JSONB NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT uq_care_metric_snap UNIQUE (tenant_id, kpi_id, period_start, period_end)
);

CREATE INDEX IF NOT EXISTS ix_care_metric_snap_tenant
    ON pack_care.care_metric_snapshot (tenant_id, period_end DESC);

ALTER TABLE pack_care.care_metric_snapshot ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_care.care_metric_snapshot;
CREATE POLICY tenant_isolation ON pack_care.care_metric_snapshot
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_care.care_metric_snapshot IS
    'NOT IMPLEMENTED: no compute worker yet. Table reserved for expansion when KPIs go live.';
