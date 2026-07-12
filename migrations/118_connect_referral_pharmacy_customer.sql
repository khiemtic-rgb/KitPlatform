-- KitPlatform 118: Connect referral/booking link to pharmacy CRM customer
-- Depends on: 109_novixa_connect_referrals.sql, 110_novixa_connect_bookings.sql
-- Enables NT→PK referral from existing customers (no re-type); correlation for later Rx sync.
-- Does NOT share customer rows across tenants — only stores pharmacy-side customer_id.

ALTER TABLE pack_connect.referrals
    ADD COLUMN IF NOT EXISTS pharmacy_customer_id UUID REFERENCES public.customers(id);

COMMENT ON COLUMN pack_connect.referrals.pharmacy_customer_id IS
    'CRM customer on pharmacy tenant — source of truth for NT→PK referral identity.';

CREATE INDEX IF NOT EXISTS ix_connect_referrals_pharmacy_customer
    ON pack_connect.referrals (pharmacy_tenant_id, pharmacy_customer_id)
    WHERE pharmacy_customer_id IS NOT NULL;

ALTER TABLE pack_connect.bookings
    ADD COLUMN IF NOT EXISTS pharmacy_customer_id UUID REFERENCES public.customers(id);

COMMENT ON COLUMN pack_connect.bookings.pharmacy_customer_id IS
    'Copied from referral when booking is NT→PK — for clinic bridge / later Rx correlate.';

CREATE INDEX IF NOT EXISTS ix_connect_bookings_pharmacy_customer
    ON pack_connect.bookings (pharmacy_tenant_id, pharmacy_customer_id)
    WHERE pharmacy_customer_id IS NOT NULL;
