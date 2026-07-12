-- KitPlatform 119: Store clinic CRM customer on accepted referral (NT→PK continuity)
-- Depends on: 118_connect_referral_pharmacy_customer.sql

ALTER TABLE pack_connect.referrals
    ADD COLUMN IF NOT EXISTS clinic_customer_id UUID REFERENCES public.customers(id);

COMMENT ON COLUMN pack_connect.referrals.clinic_customer_id IS
    'CRM customer on clinic tenant — provisioned on accept so BN appears in Clinic patients.';

CREATE INDEX IF NOT EXISTS ix_connect_referrals_clinic_customer
    ON pack_connect.referrals (clinic_tenant_id, clinic_customer_id)
    WHERE clinic_customer_id IS NOT NULL;
