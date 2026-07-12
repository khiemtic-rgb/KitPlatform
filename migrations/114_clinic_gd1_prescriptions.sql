-- KitPlatform 114: ClinicOS GĐ1 CL1.2 — internal prescriptions (no CKS)
-- Depends on: 113_clinic_gd1_demo_customer.sql, 078/080 clinic tables

CREATE TABLE IF NOT EXISTS pack_clinic.clinic_prescription (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    workspace_id        UUID REFERENCES kit_workspace.workspace_workspace(id),
    visit_id            UUID NOT NULL REFERENCES pack_clinic.clinic_visit(id),
    customer_id         UUID NOT NULL REFERENCES public.customers(id),
    provider_id         UUID REFERENCES pack_clinic.clinic_provider(id),
    prescription_code   VARCHAR(40) NOT NULL,
    prescription_status VARCHAR(20) NOT NULL DEFAULT 'draft',
    diagnosis_text      TEXT,
    notes               TEXT,
    finalized_at        TIMESTAMPTZ,
    finalized_by        UUID,
    pdf_sha256          VARCHAR(64),
    created_by          UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT uq_clinic_prescription_code UNIQUE (tenant_id, prescription_code),
    CONSTRAINT ck_clinic_prescription_status CHECK (
        prescription_status IN ('draft', 'finalized', 'cancelled')
    )
);

COMMENT ON TABLE pack_clinic.clinic_prescription IS
    'ClinicOS GĐ1 — internal Rx on visit. draft/finalized only; NOT CKS / not Pharmacy e-Rx issuer.';

CREATE INDEX IF NOT EXISTS ix_clinic_prescription_visit
    ON pack_clinic.clinic_prescription (tenant_id, visit_id, created_at DESC)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_clinic_prescription_status
    ON pack_clinic.clinic_prescription (tenant_id, prescription_status, created_at DESC)
    WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS pack_clinic.clinic_prescription_line (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    prescription_id     UUID NOT NULL REFERENCES pack_clinic.clinic_prescription(id) ON DELETE CASCADE,
    drug_name           VARCHAR(255) NOT NULL,
    strength            VARCHAR(120),
    quantity            NUMERIC(18, 4) NOT NULL DEFAULT 1,
    unit                VARCHAR(40),
    dosage_instruction  TEXT,
    sort_order          INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_clinic_prescription_line_rx
    ON pack_clinic.clinic_prescription_line (prescription_id, sort_order);

-- If applied as schema owner (pharmacore), grant app role:
-- GRANT SELECT, INSERT, UPDATE, DELETE ON pack_clinic.clinic_prescription TO kitplatform;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON pack_clinic.clinic_prescription_line TO kitplatform;
