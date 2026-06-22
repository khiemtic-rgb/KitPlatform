-- Supplier payment workflow: Nháp → Đã ghi sổ | Đã hủy
ALTER TABLE supplier_payments
    ADD COLUMN IF NOT EXISTS status SMALLINT NOT NULL DEFAULT 2,
    ADD COLUMN IF NOT EXISTS posted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

UPDATE supplier_payments
SET posted_at = COALESCE(payment_date, created_at)
WHERE status = 2 AND posted_at IS NULL;

CREATE TRIGGER trg_supplier_payments_updated
    BEFORE UPDATE ON supplier_payments
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
