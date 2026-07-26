-- KitPlatform 228: Global Kit Account + cross-product memberships (P0)
-- Link is kit_account_memberships.user_id → users.id (no ALTER on users required).
-- JWT subject remains public.users.id.

CREATE TABLE IF NOT EXISTS public.kit_accounts (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email           CITEXT       NOT NULL,
    phone           VARCHAR(30),
    password_hash   VARCHAR(255) NOT NULL,
    display_name    VARCHAR(255),
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_kit_accounts_email UNIQUE (email)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_kit_accounts_phone_active
    ON public.kit_accounts (phone)
    WHERE phone IS NOT NULL AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_kit_accounts_email_active
    ON public.kit_accounts (email)
    WHERE deleted_at IS NULL AND status = 1;

COMMENT ON TABLE public.kit_accounts IS
    'Global KitPlatform login identity (email). Staff JWT sub remains public.users.id.';

CREATE TABLE IF NOT EXISTS public.kit_account_memberships (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID         NOT NULL REFERENCES public.kit_accounts(id),
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    user_id         UUID         NOT NULL REFERENCES public.users(id),
    product_code    VARCHAR(40)  NOT NULL,
    is_default      BOOLEAN      NOT NULL DEFAULT FALSE,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_kit_membership_user UNIQUE (user_id),
    CONSTRAINT uq_kit_membership_tenant UNIQUE (account_id, tenant_id),
    CONSTRAINT ck_kit_membership_product CHECK (
        product_code IN ('family_os', 'pharmacy', 'clinic', 'hybrid')
    )
);

CREATE INDEX IF NOT EXISTS ix_kit_memberships_account
    ON public.kit_account_memberships (account_id, status);

-- Optional: denormalized FK on users when role owns the table (ignore on failure).
DO $$
BEGIN
    ALTER TABLE public.users
        ADD COLUMN IF NOT EXISTS kit_account_id UUID REFERENCES public.kit_accounts(id);
    CREATE INDEX IF NOT EXISTS ix_users_kit_account
        ON public.users (kit_account_id)
        WHERE kit_account_id IS NOT NULL;
EXCEPTION WHEN insufficient_privilege THEN
    RAISE NOTICE 'Skipping users.kit_account_id — app role is not table owner (memberships remain source of truth).';
END $$;

-- Backfill accounts
WITH primary_user AS (
    SELECT DISTINCT ON (lower(u.email::text))
        u.id,
        u.email,
        u.password_hash,
        u.username
    FROM public.users u
    WHERE u.deleted_at IS NULL
      AND u.email IS NOT NULL
      AND length(trim(u.email::text)) > 0
    ORDER BY lower(u.email::text), u.created_at ASC, u.id ASC
)
INSERT INTO public.kit_accounts (email, password_hash, display_name, status)
SELECT
    pu.email,
    pu.password_hash,
    pu.username,
    1
FROM primary_user pu
WHERE NOT EXISTS (
    SELECT 1
    FROM public.kit_accounts a
    WHERE a.email = pu.email
      AND a.deleted_at IS NULL
)
ON CONFLICT DO NOTHING;

INSERT INTO public.kit_account_memberships (
    account_id, tenant_id, user_id, product_code, is_default, status
)
SELECT
    a.id,
    u.tenant_id,
    u.id,
    CASE lower(COALESCE(t.business_vertical, ''))
        WHEN 'pharmacy' THEN 'pharmacy'
        WHEN 'clinic' THEN 'clinic'
        WHEN 'family' THEN 'family_os'
        WHEN 'hybrid' THEN 'family_os'
        ELSE 'hybrid'
    END,
    FALSE,
    1
FROM public.users u
INNER JOIN public.tenants t ON t.id = u.tenant_id
INNER JOIN public.kit_accounts a
    ON a.email = u.email
   AND a.deleted_at IS NULL
   AND a.password_hash = u.password_hash
WHERE u.deleted_at IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM public.kit_account_memberships m
      WHERE m.user_id = u.id
  )
ON CONFLICT DO NOTHING;

UPDATE public.kit_account_memberships m
SET is_default = TRUE
WHERE m.id IN (
    SELECT DISTINCT ON (m2.account_id) m2.id
    FROM public.kit_account_memberships m2
    ORDER BY m2.account_id, m2.created_at ASC, m2.id ASC
)
AND m.is_default = FALSE;

-- Best-effort denormalized sync (no-op if column missing / no privilege).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'kit_account_id'
    ) THEN
        UPDATE public.users u
        SET kit_account_id = m.account_id,
            updated_at = NOW()
        FROM public.kit_account_memberships m
        WHERE m.user_id = u.id
          AND u.kit_account_id IS NULL;
    END IF;
EXCEPTION WHEN insufficient_privilege THEN
    RAISE NOTICE 'Skipping users.kit_account_id backfill.';
END $$;
