-- Operational work inside Learning OS. Not a separate task product.
-- Not in migration-files.prod.txt.

CREATE TABLE IF NOT EXISTS pack_learning.operational_task (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id             UUID         NOT NULL REFERENCES tenants(id),
    branch_id             UUID         REFERENCES branches(id),
    sales_shift_id        UUID         REFERENCES sales_shifts(id),
    assignee_employee_id  UUID         NOT NULL REFERENCES employees(id),
    title                 VARCHAR(200) NOT NULL,
    due_at                TIMESTAMPTZ,
    status                VARCHAR(20)  NOT NULL DEFAULT 'open',
    evidence_note         TEXT,
    created_by_user_id    UUID         NOT NULL,
    completed_at          TIMESTAMPTZ,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_learning_operational_task_status
        CHECK (status IN ('open', 'in_progress', 'done', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS ix_learning_operational_task_assignee
    ON pack_learning.operational_task (tenant_id, assignee_employee_id, status);

CREATE INDEX IF NOT EXISTS ix_learning_operational_task_shift
    ON pack_learning.operational_task (tenant_id, sales_shift_id)
    WHERE sales_shift_id IS NOT NULL;
