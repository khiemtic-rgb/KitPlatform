-- Transfer ship/receive workflow: audit columns + per-line received quantity.

ALTER TABLE inventory_transfers
    ADD COLUMN IF NOT EXISTS shipped_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS shipped_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS received_by UUID REFERENCES users(id),
    ADD COLUMN IF NOT EXISTS received_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS receive_notes TEXT;

ALTER TABLE inventory_transfer_items
    ADD COLUMN IF NOT EXISTS received_quantity NUMERIC(18,3) NULL;

ALTER TABLE inventory_transfer_items
    DROP CONSTRAINT IF EXISTS ck_transfer_items_received_qty;

ALTER TABLE inventory_transfer_items
    ADD CONSTRAINT ck_transfer_items_received_qty
        CHECK (
            received_quantity IS NULL
            OR (received_quantity >= 0 AND received_quantity <= quantity)
        );

COMMENT ON COLUMN inventory_transfers.shipped_at IS 'Set when status moves Draft -> Pending (stock OUT from source).';
COMMENT ON COLUMN inventory_transfers.received_at IS 'Set when status moves Pending -> Completed (stock IN to destination).';
COMMENT ON COLUMN inventory_transfer_items.received_quantity IS 'Actual received qty; NULL until receive. Shortage restores to source warehouse.';
