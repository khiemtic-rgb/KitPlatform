-- KitPlatform 380: allow ASSEMBLE_EPISODE on production request and task actions.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Replaces ck_prod_request_action and ck_prod_task_action.
-- Keeps every action from 372, including ASSEMBLE.
-- Does not change columns, existing rows, tenant_id, credentials, artifacts, or bindings.

BEGIN;

ALTER TABLE pack_content.production_requests
    DROP CONSTRAINT IF EXISTS ck_prod_request_action;
ALTER TABLE pack_content.production_requests
    ADD CONSTRAINT ck_prod_request_action CHECK (user_action IN (
        'CREATE_IMAGE', 'CREATE_VIDEO', 'CREATE_VOICE', 'CREATE_LIPSYNC',
        'MIX_VIDEO', 'ACCEPT', 'BIND', 'REGENERATE', 'ASSEMBLE', 'ASSEMBLE_EPISODE'
    ));

ALTER TABLE pack_content.production_tasks
    DROP CONSTRAINT IF EXISTS ck_prod_task_action;
ALTER TABLE pack_content.production_tasks
    ADD CONSTRAINT ck_prod_task_action CHECK (action IN (
        'CREATE_IMAGE', 'CREATE_VIDEO', 'CREATE_VOICE', 'CREATE_LIPSYNC',
        'MIX_VIDEO', 'ACCEPT', 'BIND', 'REGENERATE', 'ASSEMBLE', 'ASSEMBLE_EPISODE'
    ));

COMMIT;
