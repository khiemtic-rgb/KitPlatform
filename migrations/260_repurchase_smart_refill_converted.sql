-- Smart Refill P0 (A1/A2): status converted + attribution + fix accept family_member_id

ALTER TABLE repurchase_suggestions
    DROP CONSTRAINT IF EXISTS ck_repurchase_suggestions_status;

ALTER TABLE repurchase_suggestions
    ADD CONSTRAINT ck_repurchase_suggestions_status CHECK (
        status IN ('pending', 'dismissed', 'snoozed', 'expired', 'converted')
    );

ALTER TABLE repurchase_suggestions
    ADD COLUMN IF NOT EXISTS converted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS converted_reservation_id UUID REFERENCES customer_reservations(id) ON DELETE SET NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'repurchase_suggestions'
          AND column_name = 'family_member_id'
    ) THEN
        ALTER TABLE repurchase_suggestions
            ADD COLUMN family_member_id UUID REFERENCES family_members(id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_repurchase_suggestions_converted
    ON repurchase_suggestions (tenant_id, status, converted_at DESC)
    WHERE status = 'converted';

COMMENT ON COLUMN repurchase_suggestions.converted_at IS 'Smart Refill: when customer/staff created reorder reservation';
COMMENT ON COLUMN repurchase_suggestions.converted_reservation_id IS 'Reservation created from Đặt lại CTA';
