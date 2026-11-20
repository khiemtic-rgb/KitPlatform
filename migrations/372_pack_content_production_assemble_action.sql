-- Production OS: ASSEMBLE is a control-plane action (no FINAL render, no Film mutation).
-- Schema-only. Does not write EP01/EP99/Canon rows.

ALTER TABLE pack_content.production_requests
    DROP CONSTRAINT IF EXISTS ck_prod_request_action;
ALTER TABLE pack_content.production_requests
    ADD CONSTRAINT ck_prod_request_action CHECK (user_action IN (
        'CREATE_IMAGE', 'CREATE_VIDEO', 'CREATE_VOICE', 'CREATE_LIPSYNC',
        'MIX_VIDEO', 'ACCEPT', 'BIND', 'REGENERATE', 'ASSEMBLE'
    ));

ALTER TABLE pack_content.production_tasks
    DROP CONSTRAINT IF EXISTS ck_prod_task_action;
ALTER TABLE pack_content.production_tasks
    ADD CONSTRAINT ck_prod_task_action CHECK (action IN (
        'CREATE_IMAGE', 'CREATE_VIDEO', 'CREATE_VOICE', 'CREATE_LIPSYNC',
        'MIX_VIDEO', 'ACCEPT', 'BIND', 'REGENERATE', 'ASSEMBLE'
    ));
