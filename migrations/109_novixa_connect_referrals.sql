-- KitPlatform 109: Novixa Connect C3 — Pharmacy → Clinic referrals
-- Depends on: 108_novixa_connect_org_profiles.sql
-- Coordination only — not clinical care / e-Rx issuance.

CREATE TABLE IF NOT EXISTS pack_connect.referrals (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    pharmacy_tenant_id      UUID NOT NULL REFERENCES public.tenants(id),
    clinic_tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    doctor_id               UUID REFERENCES pack_connect.doctors(id),
    patient_display_name    VARCHAR(200) NOT NULL,
    patient_phone           VARCHAR(30),
    reason                  TEXT,
    notes                   TEXT,
    referral_status         VARCHAR(30) NOT NULL,
    created_by              UUID,
    responded_at            TIMESTAMPTZ,
    responded_by            UUID,
    completed_at            TIMESTAMPTZ,
    completed_by            UUID,
    cancelled_at            TIMESTAMPTZ,
    cancelled_by            UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_connect_referrals_distinct CHECK (pharmacy_tenant_id <> clinic_tenant_id),
    CONSTRAINT ck_connect_referrals_status CHECK (
        referral_status IN (
            'pending_clinic_accept',
            'accepted',
            'rejected',
            'completed',
            'cancelled'
        )
    )
);

COMMENT ON TABLE pack_connect.referrals IS
    'Connect C3 — Pharmacy refers patient to linked Clinic. Not clinical care / e-Rx.';

CREATE INDEX IF NOT EXISTS ix_connect_referrals_pharmacy_status
    ON pack_connect.referrals (pharmacy_tenant_id, referral_status);

CREATE INDEX IF NOT EXISTS ix_connect_referrals_clinic_status
    ON pack_connect.referrals (clinic_tenant_id, referral_status);

CREATE INDEX IF NOT EXISTS ix_connect_referrals_doctor
    ON pack_connect.referrals (doctor_id)
    WHERE doctor_id IS NOT NULL;
