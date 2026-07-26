-- KitPlatform 232: Adaptive Family Engine — child_request, ai_proposal, screen_time_wallet
-- Depends on: 192 (family/membership), 221 (calendar_period), 230 (reminder_dispatch kinds)
-- Child proposes · AI summarizes · parent decides. Wallet = agreement minutes (not device metering).

-- ─── child_request ───────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS pack_family.child_request (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id         UUID NOT NULL REFERENCES public.tenants(id),
    family_id         UUID NOT NULL REFERENCES pack_family.family(id),
    member_id         UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date         DATE NOT NULL,
    kind              VARCHAR(32) NOT NULL DEFAULT 'screen_minutes',
    amount_minutes    INT NOT NULL,
    reason_codes      TEXT[] NOT NULL DEFAULT '{}',
    reason_note       VARCHAR(400),
    status            VARCHAR(24) NOT NULL DEFAULT 'pending',
    ai_summary_vi     VARCHAR(800),
    ai_recommend      VARCHAR(16),
    decided_by        UUID REFERENCES pack_family.membership(id),
    decided_at        TIMESTAMPTZ,
    decision_note     VARCHAR(400),
    granted_minutes   INT,
    row_version       INT NOT NULL DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by        UUID,
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by        UUID,
    deleted_at        TIMESTAMPTZ,
    CONSTRAINT ck_child_request_kind CHECK (
        kind IN ('screen_minutes', 'pause_routine', 'movie_night', 'other')
    ),
    CONSTRAINT ck_child_request_status CHECK (
        status IN ('pending', 'approved', 'rejected', 'partial', 'expired')
    ),
    CONSTRAINT ck_child_request_recommend CHECK (
        ai_recommend IS NULL OR ai_recommend IN ('approve', 'reject', 'partial')
    ),
    CONSTRAINT ck_child_request_minutes CHECK (amount_minutes > 0 AND amount_minutes <= 240)
);

CREATE INDEX IF NOT EXISTS ix_child_request_family_pending
    ON pack_family.child_request (family_id, status, created_at DESC)
    WHERE deleted_at IS NULL AND status = 'pending';

CREATE INDEX IF NOT EXISTS ix_child_request_member_week
    ON pack_family.child_request (family_id, member_id, flow_date DESC)
    WHERE deleted_at IS NULL;

DROP TRIGGER IF EXISTS trg_child_request_row_version ON pack_family.child_request;
CREATE TRIGGER trg_child_request_row_version
    BEFORE UPDATE ON pack_family.child_request
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.child_request ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.child_request;
CREATE POLICY tenant_isolation ON pack_family.child_request
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

-- ─── ai_proposal ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS pack_family.ai_proposal (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id         UUID NOT NULL REFERENCES public.tenants(id),
    family_id         UUID NOT NULL REFERENCES pack_family.family(id),
    member_id         UUID REFERENCES pack_family.membership(id),
    kind              VARCHAR(40) NOT NULL,
    title_vi          VARCHAR(200) NOT NULL,
    body_vi           VARCHAR(800) NOT NULL,
    payload_json      JSONB,
    status            VARCHAR(24) NOT NULL DEFAULT 'pending',
    source_ref        VARCHAR(120),
    decided_by        UUID REFERENCES pack_family.membership(id),
    decided_at        TIMESTAMPTZ,
    row_version       INT NOT NULL DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by        UUID,
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by        UUID,
    deleted_at        TIMESTAMPTZ,
    CONSTRAINT ck_ai_proposal_kind CHECK (
        kind IN (
            'screen_budget',
            'screen_adjust',
            'family_mode',
            'movie_night',
            'pause_routine',
            'reward_minutes',
            'other'
        )
    ),
    CONSTRAINT ck_ai_proposal_status CHECK (
        status IN ('pending', 'approved', 'rejected', 'expired')
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ai_proposal_source
    ON pack_family.ai_proposal (tenant_id, family_id, kind, source_ref)
    WHERE source_ref IS NOT NULL AND deleted_at IS NULL AND status = 'pending';

CREATE INDEX IF NOT EXISTS ix_ai_proposal_family_pending
    ON pack_family.ai_proposal (family_id, status, created_at DESC)
    WHERE deleted_at IS NULL AND status = 'pending';

DROP TRIGGER IF EXISTS trg_ai_proposal_row_version ON pack_family.ai_proposal;
CREATE TRIGGER trg_ai_proposal_row_version
    BEFORE UPDATE ON pack_family.ai_proposal
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.ai_proposal ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.ai_proposal;
CREATE POLICY tenant_isolation ON pack_family.ai_proposal
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

-- ─── screen_time_wallet (weekly agreement budget) ────────────────────────────
CREATE TABLE IF NOT EXISTS pack_family.screen_time_wallet (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id         UUID NOT NULL REFERENCES public.tenants(id),
    family_id         UUID NOT NULL REFERENCES pack_family.family(id),
    member_id         UUID NOT NULL REFERENCES pack_family.membership(id),
    iso_year          INT NOT NULL,
    iso_week          INT NOT NULL,
    budget_minutes    INT NOT NULL DEFAULT 0,
    spent_minutes     INT NOT NULL DEFAULT 0,
    earned_minutes    INT NOT NULL DEFAULT 0,
    granted_minutes   INT NOT NULL DEFAULT 0,
    status            VARCHAR(24) NOT NULL DEFAULT 'proposed',
    row_version       INT NOT NULL DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by        UUID,
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by        UUID,
    deleted_at        TIMESTAMPTZ,
    CONSTRAINT ck_screen_wallet_status CHECK (
        status IN ('proposed', 'active', 'closed')
    ),
    CONSTRAINT ck_screen_wallet_week CHECK (iso_week BETWEEN 1 AND 53),
    CONSTRAINT uq_screen_wallet_member_week UNIQUE (tenant_id, family_id, member_id, iso_year, iso_week)
);

CREATE TABLE IF NOT EXISTS pack_family.screen_time_ledger (
    id                UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id         UUID NOT NULL REFERENCES public.tenants(id),
    family_id         UUID NOT NULL REFERENCES pack_family.family(id),
    wallet_id         UUID NOT NULL REFERENCES pack_family.screen_time_wallet(id),
    member_id         UUID NOT NULL REFERENCES pack_family.membership(id),
    flow_date         DATE NOT NULL,
    entry_kind        VARCHAR(32) NOT NULL,
    minutes_delta     INT NOT NULL,
    note_vi           VARCHAR(400),
    source_ref        VARCHAR(120),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by        UUID,
    CONSTRAINT ck_screen_ledger_kind CHECK (
        entry_kind IN (
            'budget_set',
            'spend',
            'earn',
            'grant',
            'adjust',
            'movie_night_exempt'
        )
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_screen_ledger_source
    ON pack_family.screen_time_ledger (tenant_id, family_id, entry_kind, source_ref)
    WHERE source_ref IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_screen_ledger_wallet
    ON pack_family.screen_time_ledger (wallet_id, created_at DESC);

DROP TRIGGER IF EXISTS trg_screen_wallet_row_version ON pack_family.screen_time_wallet;
CREATE TRIGGER trg_screen_wallet_row_version
    BEFORE UPDATE ON pack_family.screen_time_wallet
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.screen_time_wallet ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.screen_time_wallet;
CREATE POLICY tenant_isolation ON pack_family.screen_time_wallet
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

ALTER TABLE pack_family.screen_time_ledger ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.screen_time_ledger;
CREATE POLICY tenant_isolation ON pack_family.screen_time_ledger
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

-- Expand push kinds for AFE
ALTER TABLE pack_family.reminder_dispatch
    DROP CONSTRAINT IF EXISTS ck_reminder_dispatch_kind;

ALTER TABLE pack_family.reminder_dispatch
    ADD CONSTRAINT ck_reminder_dispatch_kind CHECK (
        kind IN (
            'due_now',
            'overdue',
            'evening_digest',
            'approval_digest',
            'gratitude',
            'all_done',
            'beautiful_day',
            'streak_milestone',
            'child_request',
            'ai_proposal'
        )
    );

CREATE UNIQUE INDEX IF NOT EXISTS ux_reminder_dispatch_child_request
    ON pack_family.reminder_dispatch (tenant_id, family_id, kind, flow_date, payload_summary)
    WHERE kind IN ('child_request', 'ai_proposal');

COMMENT ON TABLE pack_family.child_request IS
    'Child proposals (e.g. +30 screen minutes). AI summarizes; parent approves.';
COMMENT ON TABLE pack_family.ai_proposal IS
    'AI-generated proposals awaiting parent decision (budget, mode, adjust).';
COMMENT ON TABLE pack_family.screen_time_wallet IS
    'Weekly screen-minute agreement budget — not device metering.';
