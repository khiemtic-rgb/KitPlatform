-- Allow counted quantity = 0 on inventory count entries (physical out of stock).
-- Previously CHECK (quantity > 0) blocked legitimate zero counts during stocktake.

ALTER TABLE inventory_adjustment_count_entries
    DROP CONSTRAINT IF EXISTS ck_count_entries_qty_pos;

ALTER TABLE inventory_adjustment_count_entries
    ADD CONSTRAINT ck_count_entries_qty_nonneg CHECK (quantity >= 0);

COMMENT ON CONSTRAINT ck_count_entries_qty_nonneg ON inventory_adjustment_count_entries IS
    'Counted qty may be 0 (physically out of stock); negative not allowed.';
