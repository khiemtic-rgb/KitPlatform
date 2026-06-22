-- V3: ghi hình thức hoàn tiền khi trả hàng (đối soát cuối ca)

CREATE TABLE sales_return_payments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sales_return_id UUID          NOT NULL REFERENCES sales_returns(id) ON DELETE CASCADE,
    payment_method  SMALLINT      NOT NULL,
    amount          NUMERIC(18,2) NOT NULL,
    paid_at         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    reference_no    VARCHAR(100),
    CONSTRAINT ck_return_payments_amount_pos CHECK (amount >= 0)
);

CREATE INDEX ix_sales_return_payments_return ON sales_return_payments (sales_return_id);
CREATE INDEX ix_sales_return_payments_paid_at ON sales_return_payments (paid_at);
