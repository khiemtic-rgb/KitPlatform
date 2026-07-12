-- KitPlatform 111: Novixa Connect C5 — status sync events (ready → Pharmacy consume)
-- Depends on: 110_novixa_connect_bookings.sql
-- Coordination queue only — not e-Rx issuance into pharmacy tenant.

CREATE TABLE IF NOT EXISTS pack_connect.status_events (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    pharmacy_tenant_id      UUID NOT NULL REFERENCES public.tenants(id),
    clinic_tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    event_type              VARCHAR(40) NOT NULL,
    source_type             VARCHAR(20) NOT NULL,
    source_id               UUID,
    patient_display_name    VARCHAR(200),
    patient_phone           VARCHAR(30),
    summary                 TEXT,
    event_status            VARCHAR(30) NOT NULL,
    created_by              UUID,
    consumed_at             TIMESTAMPTZ,
    consumed_by             UUID,
    dismissed_at            TIMESTAMPTZ,
    dismissed_by            UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_connect_status_events_distinct CHECK (pharmacy_tenant_id <> clinic_tenant_id),
    CONSTRAINT ck_connect_status_events_type CHECK (
        event_type IN ('ready_to_dispense', 'referral_completed', 'booking_completed')
    ),
    CONSTRAINT ck_connect_status_events_source CHECK (
        source_type IN ('referral', 'booking', 'manual')
    ),
    CONSTRAINT ck_connect_status_events_status CHECK (
        event_status IN ('pending_pharmacy', 'consumed', 'dismissed')
    )
);

COMMENT ON TABLE pack_connect.status_events IS
    'Connect C5 — Clinic→Pharmacy readiness queue. Pharmacy consumes; not e-Rx issuer.';

CREATE INDEX IF NOT EXISTS ix_connect_status_events_pharmacy_status
    ON pack_connect.status_events (pharmacy_tenant_id, event_status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_connect_status_events_clinic_created
    ON pack_connect.status_events (clinic_tenant_id, created_at DESC);

CREATE UNIQUE INDEX IF NOT EXISTS uq_connect_status_events_source_ready
    ON pack_connect.status_events (source_type, source_id, event_type)
    WHERE source_id IS NOT NULL AND event_type = 'ready_to_dispense';
