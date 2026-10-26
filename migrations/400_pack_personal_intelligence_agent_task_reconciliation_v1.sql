-- KitPlatform 400: record a task whose tool effect could not be confirmed.
-- Forward-only. Does not rewrite 399.
-- Not in migration-files.prod.txt.
-- Not in migration-files.personal-intelligence.txt.
-- Not in migration-files.trading-research.txt.
-- ReconciliationRequired is not completion and does not authorize a retry of the tool.

DO $$
BEGIN
    IF current_database() IN ('novixa_prod', 'trading_research', 'trading_agent', 'kitplatform', 'postgres', 'kit_pi_validation') THEN
        RAISE EXCEPTION '400_pack_personal_intelligence_agent_task_reconciliation_v1 must not run on %', current_database();
    END IF;
END $$;

ALTER TABLE pack_personal_intelligence.pi_agent_task
    DROP CONSTRAINT IF EXISTS ck_pi_agent_task_status;

ALTER TABLE pack_personal_intelligence.pi_agent_task
    ADD CONSTRAINT ck_pi_agent_task_status CHECK (status IN (
        'Received', 'Planned', 'Running', 'AwaitingApproval',
        'Verified', 'Completed', 'Failed', 'Cancelled', 'ReconciliationRequired'));
