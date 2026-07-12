-- KitPlatform 106: Novixa Connect C1 — org links (Pharmacy ↔ Clinic)
-- Depends on: 105_novixa_connect_pack.sql
-- Note: discoverable lives in pack_connect (not ALTER public.tenants) so app role
-- that owns pack_connect can apply without owning public.tenants.

-- =============================================================================
-- Partner directory opt-in (tenant appears in Connect directory)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_connect.directory_opt_in (
    tenant_id       UUID PRIMARY KEY REFERENCES public.tenants(id),
    discoverable    BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

COMMENT ON TABLE pack_connect.directory_opt_in IS
    'Connect C1 — tenants that opt into partner discovery (code/name/address only)';

-- Local pilot: allow both pharmacies to discover each other for C1 testing
INSERT INTO pack_connect.directory_opt_in (tenant_id, discoverable)
SELECT t.id, TRUE
FROM public.tenants t
WHERE t.tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY')
  AND t.deleted_at IS NULL
ON CONFLICT (tenant_id) DO UPDATE SET
    discoverable = EXCLUDED.discoverable,
    updated_at = NOW();

-- =============================================================================
-- Org partnership link (directed invite; either side can accept/approve)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_connect.org_links (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    initiator_tenant_id UUID NOT NULL REFERENCES public.tenants(id),
    partner_tenant_id   UUID NOT NULL REFERENCES public.tenants(id),
    initiator_org_role  VARCHAR(20) NOT NULL,
    partner_org_role    VARCHAR(20) NOT NULL,
    link_status         VARCHAR(30) NOT NULL,
    notes               TEXT,
    invited_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    responded_at        TIMESTAMPTZ,
    responded_by        UUID,
    revoked_at          TIMESTAMPTZ,
    revoked_by          UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_connect_org_links_pair UNIQUE (initiator_tenant_id, partner_tenant_id),
    CONSTRAINT ck_connect_org_links_distinct CHECK (initiator_tenant_id <> partner_tenant_id),
    CONSTRAINT ck_connect_org_links_initiator_role CHECK (
        initiator_org_role IN ('pharmacy', 'clinic')
    ),
    CONSTRAINT ck_connect_org_links_partner_role CHECK (
        partner_org_role IN ('pharmacy', 'clinic')
    ),
    CONSTRAINT ck_connect_org_links_status CHECK (
        link_status IN (
            'pending_partner_accept',
            'pending_our_approval',
            'active',
            'rejected',
            'revoked'
        )
    )
);

COMMENT ON TABLE pack_connect.org_links IS
    'Connect C1 — partnership between tenants (Pharmacy/Clinic). Not e-Rx issuance.';

COMMENT ON COLUMN pack_connect.org_links.link_status IS
    'pending_partner_accept = we invited them; pending_our_approval = they requested us; active/rejected/revoked';

CREATE INDEX IF NOT EXISTS ix_connect_org_links_initiator_status
    ON pack_connect.org_links (initiator_tenant_id, link_status);

CREATE INDEX IF NOT EXISTS ix_connect_org_links_partner_status
    ON pack_connect.org_links (partner_tenant_id, link_status);
