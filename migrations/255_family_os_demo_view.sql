-- Family OS demo-house visit ledger — GTM ops (no RLS).
-- Written when /demo enters successfully; readable cross-tenant for admin stats.
-- Depends on: 222_pack_family_commercial_foundation.sql

CREATE TABLE IF NOT EXISTS public.family_os_demo_view (
    id           UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id    UUID NOT NULL REFERENCES public.tenants(id),
    tenant_code  VARCHAR(64) NOT NULL DEFAULT 'DEMO_FAMILY',
    user_id      UUID REFERENCES public.users(id),
    client_key   VARCHAR(64),
    source       VARCHAR(32) NOT NULL DEFAULT 'spa_demo',
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_family_os_demo_view_created
    ON public.family_os_demo_view (created_at DESC);

CREATE INDEX IF NOT EXISTS ix_family_os_demo_view_client_day
    ON public.family_os_demo_view (client_key, created_at DESC)
    WHERE client_key IS NOT NULL;

COMMENT ON TABLE public.family_os_demo_view IS
    'GTM: each /demo enter (viewer). Ops aggregate today/7d/unique — no RLS.';
