-- Customer app: lý do bỏ liều (học pattern soft-accountability từ Family OS, domain care).
ALTER TABLE medication_adherence_events
    ADD COLUMN IF NOT EXISTS skip_reason VARCHAR(40);

COMMENT ON COLUMN medication_adherence_events.skip_reason IS
    'Optional when response=skipped: forgot|away|unwell|out_of_stock|other';
