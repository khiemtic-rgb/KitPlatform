-- KitPlatform 117: Clinic CL2.0 Soft-CKS — signature store + optional status widen
-- Depends on: 114_clinic_gd1_prescriptions.sql, 115_clinic_gd1_rx_handoff.sql
-- Signatures live in pack_connect (app-role ownership). Soft-CKS is NOT legal CA.

CREATE TABLE IF NOT EXISTS pack_connect.clinic_rx_signatures (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    clinic_tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    clinic_prescription_id  UUID NOT NULL,
    pdf_sha256              VARCHAR(64) NOT NULL,
    signature_alg           VARCHAR(40) NOT NULL,
    signature_value         TEXT NOT NULL,
    signer_cert_thumbprint  VARCHAR(128),
    signature_provider      VARCHAR(40) NOT NULL DEFAULT 'mock',
    signed_by               UUID,
    signed_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_clinic_rx_signatures_rx UNIQUE (clinic_tenant_id, clinic_prescription_id),
    CONSTRAINT ck_clinic_rx_signatures_provider CHECK (
        signature_provider IN ('mock', 'usb_ca')
    )
);

COMMENT ON TABLE pack_connect.clinic_rx_signatures IS
    'CL2 Soft-CKS — structured signature on clinic Rx hash. mock ≠ legal CA.';

CREATE INDEX IF NOT EXISTS ix_clinic_rx_signatures_tenant_signed
    ON pack_connect.clinic_rx_signatures (clinic_tenant_id, signed_at DESC);

-- Optional: widen pack_clinic status when role owns the table (pharmacore / owner).
-- Safe to skip if "must be owner"; API overlays status=signed from this table.
DO $$
BEGIN
    ALTER TABLE pack_clinic.clinic_prescription
        DROP CONSTRAINT IF EXISTS ck_clinic_prescription_status;
    ALTER TABLE pack_clinic.clinic_prescription
        ADD CONSTRAINT ck_clinic_prescription_status CHECK (
            prescription_status IN ('draft', 'finalized', 'signed', 'cancelled')
        );
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE '117: skip pack_clinic status check widen: %', SQLERRM;
END $$;

-- GRANT SELECT, INSERT, UPDATE ON pack_connect.clinic_rx_signatures TO kitplatform;
