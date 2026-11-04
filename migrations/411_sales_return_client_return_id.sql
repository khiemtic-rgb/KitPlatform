-- Return idempotency. Same tenant + client_return_id replays the original return.
-- Not in migration-files.prod.txt. Do not apply on production or the shared kitplatform database.

ALTER TABLE sales_returns
    ADD COLUMN IF NOT EXISTS client_return_id VARCHAR(64);

COMMENT ON COLUMN sales_returns.client_return_id IS
    'Client idempotency key. Null means a normal return. Unique per tenant when set.';

CREATE UNIQUE INDEX IF NOT EXISTS uq_sales_returns_tenant_client_return_id
    ON sales_returns (tenant_id, client_return_id)
    WHERE client_return_id IS NOT NULL;
