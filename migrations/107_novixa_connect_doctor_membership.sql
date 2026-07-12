-- KitPlatform 107: Novixa Connect C2 — doctors + clinic memberships
-- Depends on: 106_novixa_connect_org_links.sql

-- =============================================================================
-- Platform doctor identity (Connect)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_connect.doctors (
    id                      UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    full_name               VARCHAR(200) NOT NULL,
    phone                   VARCHAR(30) NOT NULL,
    license_number          VARCHAR(50),
    specialty               VARCHAR(120),
    status                  VARCHAR(30) NOT NULL DEFAULT 'pending_verification',
    legacy_prescriber_id    UUID,
    verified_at             TIMESTAMPTZ,
    verified_by             UUID,
    notes                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at              TIMESTAMPTZ,
    CONSTRAINT ck_connect_doctors_status CHECK (
        status IN ('pending_verification', 'active', 'suspended')
    )
);

COMMENT ON TABLE pack_connect.doctors IS
    'Connect C2 — doctor identity. Membership is under Clinic; not e-Rx issuance.';

CREATE UNIQUE INDEX IF NOT EXISTS uq_connect_doctors_phone_active
    ON pack_connect.doctors (phone)
    WHERE deleted_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_connect_doctors_license_active
    ON pack_connect.doctors (license_number)
    WHERE deleted_at IS NULL AND license_number IS NOT NULL AND license_number <> '';

CREATE INDEX IF NOT EXISTS ix_connect_doctors_status
    ON pack_connect.doctors (status)
    WHERE deleted_at IS NULL;

-- Optional strangler pointer (no FK — pack_pharmacy may be absent in some envs)
CREATE INDEX IF NOT EXISTS ix_connect_doctors_legacy_prescriber
    ON pack_connect.doctors (legacy_prescriber_id)
    WHERE legacy_prescriber_id IS NOT NULL AND deleted_at IS NULL;

-- =============================================================================
-- Doctor ↔ Clinic membership
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_connect.doctor_memberships (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    doctor_id           UUID NOT NULL REFERENCES pack_connect.doctors(id),
    clinic_tenant_id    UUID NOT NULL REFERENCES public.tenants(id),
    membership_role     VARCHAR(30) NOT NULL DEFAULT 'attending',
    membership_status   VARCHAR(30) NOT NULL,
    initiated_by        VARCHAR(20) NOT NULL,
    invited_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    responded_at        TIMESTAMPTZ,
    responded_by        UUID,
    revoked_at          TIMESTAMPTZ,
    revoked_by          UUID,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_connect_doctor_memberships_pair UNIQUE (doctor_id, clinic_tenant_id),
    CONSTRAINT ck_connect_doctor_memberships_role CHECK (
        membership_role IN ('attending', 'consultant', 'owner')
    ),
    CONSTRAINT ck_connect_doctor_memberships_status CHECK (
        membership_status IN (
            'pending_doctor_accept',
            'pending_clinic_approval',
            'active',
            'rejected',
            'revoked'
        )
    ),
    CONSTRAINT ck_connect_doctor_memberships_initiated_by CHECK (
        initiated_by IN ('clinic', 'doctor', 'system')
    )
);

COMMENT ON TABLE pack_connect.doctor_memberships IS
    'Connect C2 — doctor belongs to Clinic org. Pharmacy reads via active C1 org_links.';

CREATE INDEX IF NOT EXISTS ix_connect_doctor_memberships_clinic_status
    ON pack_connect.doctor_memberships (clinic_tenant_id, membership_status);

CREATE INDEX IF NOT EXISTS ix_connect_doctor_memberships_doctor_status
    ON pack_connect.doctor_memberships (doctor_id, membership_status);
