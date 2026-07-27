-- KitPlatform 241: Behavior OS Wave 2 — evidence ladder, confidence, retrieval check
-- Layer: Pack:FamilyOS
-- Local / pilot only until deploy is explicitly approved.
-- Depends on: 240

-- =============================================================================
-- Evidence + confidence snapshot on daily commitment
-- =============================================================================
ALTER TABLE pack_family.commitment
    ADD COLUMN IF NOT EXISTS evidence_level INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS confidence_score INT,
    ADD COLUMN IF NOT EXISTS confidence_updated_at TIMESTAMPTZ;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_evidence_level'
    ) THEN
        ALTER TABLE pack_family.commitment
            ADD CONSTRAINT ck_commitment_evidence_level
            CHECK (evidence_level BETWEEN 0 AND 3);
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_commitment_confidence_score'
    ) THEN
        ALTER TABLE pack_family.commitment
            ADD CONSTRAINT ck_commitment_confidence_score
            CHECK (confidence_score IS NULL OR confidence_score BETWEEN 0 AND 100);
    END IF;
END $$;

COMMENT ON COLUMN pack_family.commitment.evidence_level IS
    'Behavior OS Wave 2 ladder: 0=self tick, 1=reflection, 2=retrieval check, 3=photo.';
COMMENT ON COLUMN pack_family.commitment.confidence_score IS
    'Completion confidence 0–100 — need more data, not accusing the child.';

-- =============================================================================
-- Extend behavior_event types
-- =============================================================================
ALTER TABLE pack_family.behavior_event
    DROP CONSTRAINT IF EXISTS ck_behavior_event_type;

ALTER TABLE pack_family.behavior_event
    ADD CONSTRAINT ck_behavior_event_type CHECK (event_type IN (
        'commitment_done',
        'commitment_skipped',
        'reflection_submitted',
        'habit_stage_changed',
        'reminder_suppressed',
        'reminder_fired',
        'parent_nudge',
        'self_start',
        'retrieval_submitted',
        'confidence_scored',
        'evidence_uploaded'
    ));

-- =============================================================================
-- Illusion-of-learning retrieval check (2 MCQs, learning missions only)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_family.commitment_retrieval_check (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id       UUID NOT NULL REFERENCES public.tenants(id),
    family_id       UUID NOT NULL REFERENCES pack_family.family(id),
    commitment_id   UUID NOT NULL REFERENCES pack_family.commitment(id),
    member_id       UUID REFERENCES pack_family.membership(id),
    method_answer   VARCHAR(32) NOT NULL,
    recall_answer   VARCHAR(32) NOT NULL,
    illusion_risk   BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_commitment_retrieval_check UNIQUE (commitment_id),
    CONSTRAINT ck_retrieval_method CHECK (method_answer IN (
        'skim', 'practice', 'retrieve'
    )),
    CONSTRAINT ck_retrieval_recall CHECK (recall_answer IN (
        'can_explain', 'vaguely', 'need_review'
    ))
);

CREATE INDEX IF NOT EXISTS ix_commitment_retrieval_family
    ON pack_family.commitment_retrieval_check (family_id, created_at DESC);

ALTER TABLE pack_family.commitment_retrieval_check ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.commitment_retrieval_check;
CREATE POLICY tenant_isolation ON pack_family.commitment_retrieval_check
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.commitment_retrieval_check IS
    'Wave 2 Behavior OS — meta-cognitive retrieval check after learning missions.';
