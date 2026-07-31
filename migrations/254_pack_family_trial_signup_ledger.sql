-- Family OS trial signup ledger — cross-tenant ops view (no RLS).
-- Written on self-serve register; backfilled from existing family tenants.
-- Depends on: 222_pack_family_commercial_foundation.sql

CREATE TABLE IF NOT EXISTS public.family_os_trial_signup (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id           UUID NOT NULL REFERENCES public.tenants(id),
    tenant_code         VARCHAR(64) NOT NULL,
    family_id           UUID NOT NULL,
    family_name         VARCHAR(160) NOT NULL,
    parent_display_name VARCHAR(120) NOT NULL DEFAULT '',
    email               VARCHAR(200) NOT NULL DEFAULT '',
    username            VARCHAR(80) NOT NULL DEFAULT '',
    member_count        INT NOT NULL DEFAULT 1,
    plan_code           VARCHAR(40) NOT NULL DEFAULT 'starter_trial',
    status              VARCHAR(24) NOT NULL DEFAULT 'trial',
    trial_ends_at       TIMESTAMPTZ,
    source              VARCHAR(32) NOT NULL DEFAULT 'self_register',
    registered_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_family_os_trial_signup_family UNIQUE (family_id)
);

CREATE INDEX IF NOT EXISTS ix_family_os_trial_signup_registered
    ON public.family_os_trial_signup (registered_at DESC);

CREATE INDEX IF NOT EXISTS ix_family_os_trial_signup_status
    ON public.family_os_trial_signup (status, registered_at DESC);

COMMENT ON TABLE public.family_os_trial_signup IS
    'Ops ledger of Family OS trial / interest signups — readable across tenants (no RLS).';

-- Backfill from existing family_os tenants (migration role bypasses pack_family RLS).
INSERT INTO public.family_os_trial_signup (
    tenant_id, tenant_code, family_id, family_name,
    parent_display_name, email, username, member_count,
    plan_code, status, trial_ends_at, source, registered_at
)
SELECT
    t.id,
    t.tenant_code,
    f.id,
    COALESCE(NULLIF(TRIM(f.display_name), ''), t.tenant_name, t.tenant_code),
    COALESCE(NULLIF(TRIM(admin.full_name), ''), NULLIF(TRIM(admin.username), ''), ''),
    COALESCE(NULLIF(TRIM(admin.email), ''), ''),
    COALESCE(NULLIF(TRIM(admin.username), ''), ''),
    GREATEST(1, COALESCE(mc.cnt, 1)),
    COALESCE(NULLIF(TRIM(s.plan_code), ''), 'starter_trial'),
    COALESCE(NULLIF(TRIM(s.status), ''), 'trial'),
    s.trial_ends_at,
    CASE
        WHEN t.tenant_code = 'DEMO_FAMILY' THEN 'seed'
        ELSE 'backfill'
    END,
    COALESCE(f.created_at, t.created_at, NOW())
FROM public.tenants t
INNER JOIN pack_family.family f
    ON f.tenant_id = t.id AND f.deleted_at IS NULL
LEFT JOIN pack_family.family_subscription s
    ON s.family_id = f.id
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt
    FROM pack_family.membership m
    WHERE m.family_id = f.id AND m.deleted_at IS NULL
) mc ON TRUE
LEFT JOIN LATERAL (
    SELECT
        u.username,
        u.email,
        e.full_name
    FROM public.users u
    LEFT JOIN public.employees e ON e.id = u.employee_id
    WHERE u.tenant_id = t.id
      AND (u.deleted_at IS NULL)
    ORDER BY u.created_at ASC NULLS LAST
    LIMIT 1
) admin ON TRUE
WHERE t.deleted_at IS NULL
  AND (
      COALESCE(NULLIF(TRIM(t.settings->'platform'->>'vertical'), ''), '') = 'family'
      OR COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb) ? 'family_os'
  )
ON CONFLICT (family_id) DO NOTHING;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE ON TABLE public.family_os_trial_signup TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE ON TABLE public.family_os_trial_signup TO pharmacore;
    END IF;
END $$;
