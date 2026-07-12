-- KitPlatform 110: Novixa Connect C4 — booking stub (+ notify hooks in app)
-- Depends on: 109_novixa_connect_referrals.sql
-- Stub slots only — not Clinic Lite calendar / telemedicine.

CREATE TABLE IF NOT EXISTS pack_connect.bookings (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    clinic_tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    pharmacy_tenant_id      UUID REFERENCES public.tenants(id),
    referral_id             UUID REFERENCES pack_connect.referrals(id),
    doctor_id               UUID REFERENCES pack_connect.doctors(id),
    patient_display_name    VARCHAR(200) NOT NULL,
    patient_phone           VARCHAR(30),
    scheduled_at            TIMESTAMPTZ NOT NULL,
    duration_minutes        INT NOT NULL DEFAULT 30,
    booking_status          VARCHAR(30) NOT NULL,
    notes                   TEXT,
    notified_at             TIMESTAMPTZ,
    created_by              UUID,
    updated_by              UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_connect_bookings_duration CHECK (duration_minutes BETWEEN 5 AND 480),
    CONSTRAINT ck_connect_bookings_status CHECK (
        booking_status IN (
            'proposed',
            'confirmed',
            'cancelled',
            'completed',
            'no_show'
        )
    )
);

COMMENT ON TABLE pack_connect.bookings IS
    'Connect C4 — stub appointment slot + notify. Not clinical EMR / telemedicine.';

CREATE INDEX IF NOT EXISTS ix_connect_bookings_clinic_status
    ON pack_connect.bookings (clinic_tenant_id, booking_status, scheduled_at);

CREATE INDEX IF NOT EXISTS ix_connect_bookings_pharmacy_status
    ON pack_connect.bookings (pharmacy_tenant_id, booking_status)
    WHERE pharmacy_tenant_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_connect_bookings_referral
    ON pack_connect.bookings (referral_id)
    WHERE referral_id IS NOT NULL;
