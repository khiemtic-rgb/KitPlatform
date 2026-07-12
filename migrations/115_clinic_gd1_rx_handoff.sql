-- KitPlatform 115: ClinicOS GĐ1 CL1.3 — clinic Rx → Pharmacy via Connect handoff
-- Depends on: 111_novixa_connect_status_events.sql, 114_clinic_gd1_prescriptions.sql
-- Snapshot handoff only — not Pharmacy e-Rx issuer / not CKS.
-- Sent-state lives on pack_connect.rx_handoffs (avoid ALTER pack_clinic ownership).

ALTER TABLE pack_connect.status_events
    DROP CONSTRAINT IF EXISTS ck_connect_status_events_source;

ALTER TABLE pack_connect.status_events
    ADD CONSTRAINT ck_connect_status_events_source CHECK (
        source_type IN ('referral', 'booking', 'manual', 'clinic_rx')
    );

CREATE TABLE IF NOT EXISTS pack_connect.rx_handoffs (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    clinic_tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    pharmacy_tenant_id      UUID NOT NULL REFERENCES public.tenants(id),
    clinic_prescription_id  UUID NOT NULL,
    prescription_code       VARCHAR(40) NOT NULL,
    patient_display_name    VARCHAR(200),
    patient_phone           VARCHAR(30),
    provider_display_name   VARCHAR(200),
    diagnosis_text          TEXT,
    notes                   TEXT,
    lines_json              JSONB NOT NULL DEFAULT '[]'::jsonb,
    pdf_sha256              VARCHAR(64),
    handoff_status          VARCHAR(30) NOT NULL DEFAULT 'pending_pharmacy',
    status_event_id         UUID REFERENCES pack_connect.status_events(id),
    created_by              UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    consumed_at             TIMESTAMPTZ,
    consumed_by             UUID,
    dismissed_at            TIMESTAMPTZ,
    dismissed_by            UUID,
    CONSTRAINT ck_connect_rx_handoffs_distinct CHECK (clinic_tenant_id <> pharmacy_tenant_id),
    CONSTRAINT ck_connect_rx_handoffs_status CHECK (
        handoff_status IN ('pending_pharmacy', 'consumed', 'dismissed')
    ),
    CONSTRAINT uq_connect_rx_handoffs_clinic_rx UNIQUE (clinic_tenant_id, clinic_prescription_id)
);

COMMENT ON TABLE pack_connect.rx_handoffs IS
    'Connect CL1.3 — Clinic finalized Rx snapshot for Pharmacy queue. Not e-Rx issuance into pack_pharmacy.';

CREATE INDEX IF NOT EXISTS ix_connect_rx_handoffs_pharmacy_status
    ON pack_connect.rx_handoffs (pharmacy_tenant_id, handoff_status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_connect_rx_handoffs_clinic_created
    ON pack_connect.rx_handoffs (clinic_tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_connect_rx_handoffs_status_event
    ON pack_connect.rx_handoffs (status_event_id)
    WHERE status_event_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_connect_rx_handoffs_clinic_rx
    ON pack_connect.rx_handoffs (clinic_tenant_id, clinic_prescription_id);

-- If applied as schema owner (pharmacore), grant app role:
-- GRANT SELECT, INSERT, UPDATE, DELETE ON pack_connect.rx_handoffs TO kitplatform;
