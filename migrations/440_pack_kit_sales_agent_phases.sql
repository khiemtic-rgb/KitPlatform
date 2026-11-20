-- KIT Sales phases 0–1. Park kit_sales only. Does not grant the three new
-- permissions to a role: release_offer and autonomy_grant must be held by
-- different people, and ADMIN alone is not enough.

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('kit_sales.approve_outbound', 'KIT Sales — duyệt tin gửi', 'kit_sales'),
    ('kit_sales.release_offer', 'KIT Sales — duyệt hồ sơ chào', 'kit_sales'),
    ('kit_sales.autonomy_grant', 'KIT Sales — cấp quyền tự chủ', 'kit_sales')
ON CONFLICT (permission_code) DO NOTHING;

ALTER TABLE pack_sales.signal
    ADD COLUMN IF NOT EXISTS evidence_json JSONB NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE pack_sales.signal
    DROP CONSTRAINT IF EXISTS ck_sales_signal_evidence_object;
ALTER TABLE pack_sales.signal
    ADD CONSTRAINT ck_sales_signal_evidence_object
    CHECK (jsonb_typeof(evidence_json) = 'object');

CREATE OR REPLACE FUNCTION pack_sales.signal_layer_ok(kind text, signal_type text, evidence jsonb)
RETURNS boolean
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT CASE
        WHEN kind IS NULL OR kind NOT IN ('legal_fact', 'consent', 'market_need') THEN TRUE
        WHEN evidence IS NULL OR jsonb_typeof(evidence) <> 'object' THEN FALSE
        WHEN kind = 'legal_fact' THEN signal_type IN (
                'directory_listed', 'license_granted', 'license_revoked',
                'gpp_non_compliant', 'gpp_assessment_due')
            AND evidence ?& ARRAY['source_key','doc_number','doc_date','published_at','source_url','pdf_page','collected_at']
        WHEN kind = 'consent' THEN signal_type IN ('consent_granted', 'consent_withdrawn')
            AND evidence ?& ARRAY['channel','subject_hash','basis','purpose','captured_at','captured_by','evidence_ref','expires_at']
            AND evidence->>'purpose' = 'marketing'
            AND (
                (signal_type = 'consent_granted' AND evidence->>'basis' IN ('form_opt_in', 'verbal_recorded', 'written'))
                OR (signal_type = 'consent_withdrawn' AND evidence->>'basis' IN ('form_opt_in', 'verbal_recorded', 'written', 'customer_message'))
            )
        WHEN kind = 'market_need' THEN signal_type = 'need_observed'
            AND evidence ?& ARRAY['category','product_code','origin','quote_redacted','origin_ref']
        ELSE FALSE
    END;
$$;

ALTER TABLE pack_sales.signal
    DROP CONSTRAINT IF EXISTS ck_sales_signal_layer;
ALTER TABLE pack_sales.signal
    ADD CONSTRAINT ck_sales_signal_layer
    CHECK (pack_sales.signal_layer_ok(kind, signal_type, evidence_json));

CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_signal_legal_source
    ON pack_sales.signal (tenant_id, (evidence_json->>'source_key'))
    WHERE kind = 'legal_fact';

CREATE INDEX IF NOT EXISTS ix_sales_signal_consent
    ON pack_sales.signal (
        tenant_id,
        (evidence_json->>'channel'),
        (evidence_json->>'subject_hash'),
        detected_at DESC)
    WHERE kind = 'consent';

CREATE OR REPLACE FUNCTION pack_sales.reject_layered_signal_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.kind IN ('legal_fact', 'consent', 'market_need') THEN
        RAISE EXCEPTION 'signal_immutable';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_sales_signal_immutable ON pack_sales.signal;
CREATE TRIGGER tr_sales_signal_immutable
    BEFORE UPDATE ON pack_sales.signal
    FOR EACH ROW
    EXECUTE FUNCTION pack_sales.reject_layered_signal_update();

CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_identity_license
    ON pack_sales.business_identity (tenant_id, identity_type, platform, username)
    WHERE identity_type = 'license' AND username IS NOT NULL;

ALTER TABLE pack_sales.agent_task
    ADD COLUMN IF NOT EXISTS business_id UUID REFERENCES pack_sales.business(id) ON DELETE CASCADE;
ALTER TABLE pack_sales.agent_task
    ADD COLUMN IF NOT EXISTS resolved_by_user_id UUID;

ALTER TABLE pack_sales.agent_task
    DROP CONSTRAINT IF EXISTS ck_sales_agent_task_type;
ALTER TABLE pack_sales.agent_task
    ADD CONSTRAINT ck_sales_agent_task_type
    CHECK (task_type IN ('accept_score', 'account_review', 'need_triage', 'autonomy_grant', 'autonomy_incident'));

ALTER TABLE pack_sales.agent_task
    DROP CONSTRAINT IF EXISTS ck_sales_agent_task_account_review;
ALTER TABLE pack_sales.agent_task
    ADD CONSTRAINT ck_sales_agent_task_account_review
    CHECK (task_type <> 'account_review' OR business_id IS NOT NULL);

CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_account_review_open
    ON pack_sales.agent_task (tenant_id, business_id, (payload_json->>'reason'))
    WHERE task_type = 'account_review' AND status = 'pending';

CREATE INDEX IF NOT EXISTS ix_sales_agent_task_queue
    ON pack_sales.agent_task (tenant_id, task_type, status, created_at DESC);

INSERT INTO kit_schema_migrations (filename) VALUES ('440_pack_kit_sales_agent_phases.sql')
ON CONFLICT (filename) DO NOTHING;
