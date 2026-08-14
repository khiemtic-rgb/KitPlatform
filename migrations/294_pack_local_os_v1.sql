-- KitPlatform 294: Local OS (Thái Nguyên Life) — isolated park
-- Pack/module/package: local_os | Tenant: KIT_LOCAL | Schema: pack_local
-- Manifest: deploy/ubuntu/migration-files.local-os.txt ONLY
-- Do NOT add to migration-files.prod.txt / family-os.txt / content.txt
-- Idempotent: local DB may already have leftover 291/293 objects.

CREATE SCHEMA IF NOT EXISTS pack_local;

COMMENT ON SCHEMA pack_local IS
    'KIT Local OS — Thái Nguyên Life. Isolated from Pharmacy / Family OS / Marketing Park.';

-- =============================================================================
-- Platform catalog (opt-in). Controllers: ADMIN + RequirePlatformModule(local_os).
-- =============================================================================
INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT
    'local_os',
    'KIT Local OS',
    'Local Life Platform — listings (job/event/room). Independent park, tenant KIT_LOCAL.',
    ARRAY['local', 'hybrid'],
    96
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = 'local_os'
);

INSERT INTO kit_tenant.tenant_package (
    package_code, package_name, description, verticals, module_codes, sort_order
)
VALUES (
    'local_os',
    'KIT Local OS',
    'Thái Nguyên Life — local listings. Own org KIT_LOCAL. Not Pharmacy / Family / Content.',
    ARRAY['local', 'hybrid'],
    ARRAY['local_os'],
    96
)
ON CONFLICT (package_code) DO UPDATE SET
    package_name = EXCLUDED.package_name,
    description = EXCLUDED.description,
    verticals = EXCLUDED.verticals,
    module_codes = EXCLUDED.module_codes,
    sort_order = EXCLUDED.sort_order,
    updated_at = NOW();

INSERT INTO permissions (permission_code, permission_name, module_name)
VALUES
    ('local_os.read', 'Local OS — xem tin', 'local_os'),
    ('local_os.write', 'Local OS — duyệt / ẩn tin', 'local_os')
ON CONFLICT (permission_code) DO NOTHING;

-- =============================================================================
-- Domain tables
-- =============================================================================
CREATE TABLE IF NOT EXISTS pack_local.source (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    source_kind     VARCHAR(32) NOT NULL,
    name            TEXT NOT NULL,
    url             TEXT,
    status          VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_local.community (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    name            TEXT NOT NULL,
    city_code       VARCHAR(32) NOT NULL DEFAULT 'thai_nguyen',
    audience        TEXT[] NOT NULL DEFAULT ARRAY['student'],
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_local.listing (
    id                  UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    kind                VARCHAR(16) NOT NULL,
    title               TEXT NOT NULL,
    summary             TEXT,
    organization_name   TEXT,
    place_text          TEXT,
    audience            TEXT[] NOT NULL DEFAULT ARRAY['student'],
    city_code           VARCHAR(32) NOT NULL DEFAULT 'thai_nguyen',
    source_kind         VARCHAR(32) NOT NULL DEFAULT 'group_manual',
    source_url          TEXT,
    contact_phone       TEXT,
    contact_name        TEXT,
    salary_text         TEXT,
    working_time        TEXT,
    employment_type     TEXT,
    category            TEXT,
    requirements        TEXT,
    start_at            TIMESTAMPTZ,
    end_at              TIMESTAMPTZ,
    registration_url    TEXT,
    price_month         NUMERIC(12, 0),
    room_type           VARCHAR(32),
    trust               VARCHAR(24) NOT NULL DEFAULT 'UNVERIFIED',
    safety_flag         BOOLEAN NOT NULL DEFAULT FALSE,
    status              VARCHAR(24) NOT NULL DEFAULT 'NEEDS_REVIEW',
    publisher_id        UUID,
    published_at        TIMESTAMPTZ,
    last_checked_at     TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_local_listing_kind CHECK (kind IN ('job', 'event', 'room')),
    CONSTRAINT ck_local_listing_status CHECK (status IN ('ACTIVE', 'EXPIRED', 'NEEDS_REVIEW', 'HIDDEN')),
    CONSTRAINT ck_local_listing_trust CHECK (trust IN ('UNVERIFIED', 'COMMUNITY', 'VERIFIED'))
);

ALTER TABLE pack_local.listing ADD COLUMN IF NOT EXISTS contact_name TEXT;
ALTER TABLE pack_local.listing ADD COLUMN IF NOT EXISTS category TEXT;
ALTER TABLE pack_local.listing ADD COLUMN IF NOT EXISTS requirements TEXT;
ALTER TABLE pack_local.listing ADD COLUMN IF NOT EXISTS publisher_id UUID;
ALTER TABLE pack_local.listing ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
ALTER TABLE pack_local.listing ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

CREATE INDEX IF NOT EXISTS ix_local_listing_public
    ON pack_local.listing (city_code, kind, status, safety_flag, expires_at);

CREATE TABLE IF NOT EXISTS pack_local.publisher (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    name            TEXT NOT NULL DEFAULT '',
    phone           VARCHAR(16) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_local_publisher_phone UNIQUE (phone)
);

CREATE TABLE IF NOT EXISTS pack_local.publisher_otp (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    phone           VARCHAR(16) NOT NULL,
    code_hash       TEXT NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    consumed_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_local_publisher_otp_phone
    ON pack_local.publisher_otp (phone, created_at DESC);

CREATE TABLE IF NOT EXISTS pack_local.publisher_session (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    publisher_id    UUID NOT NULL REFERENCES pack_local.publisher (id) ON DELETE CASCADE,
    token           TEXT NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_local_publisher_session_token UNIQUE (token)
);

CREATE TABLE IF NOT EXISTS pack_local.community_group (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    name            TEXT NOT NULL,
    url             TEXT NOT NULL,
    platform        VARCHAR(24) NOT NULL DEFAULT 'facebook',
    category        VARCHAR(32) NOT NULL DEFAULT 'job',
    audience        VARCHAR(32) NOT NULL DEFAULT 'student',
    geo             VARCHAR(32) NOT NULL DEFAULT 'thai_nguyen',
    status          VARCHAR(16) NOT NULL DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_local_community_group_url UNIQUE (url)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_local_community_group_url
    ON pack_local.community_group (url);

CREATE TABLE IF NOT EXISTS pack_local.share_event (
    id              UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    listing_id      UUID NOT NULL REFERENCES pack_local.listing (id) ON DELETE CASCADE,
    group_id        UUID REFERENCES pack_local.community_group (id) ON DELETE SET NULL,
    event_kind      VARCHAR(16) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 7 Facebook groups (watch + copy; never crawl / never auto-post)
INSERT INTO pack_local.community_group (id, name, url, platform, category, audience, geo, status)
VALUES
    ('a1111111-1111-1111-1111-111111111101', 'Group việc / SV Thái Nguyên (G1)',
     'https://www.facebook.com/groups/5719882594776915/', 'facebook', 'job', 'student', 'thai_nguyen', 'active'),
    ('a1111111-1111-1111-1111-111111111102', 'Group việc / SV Thái Nguyên (G2)',
     'https://www.facebook.com/groups/159548018082242/', 'facebook', 'job', 'student', 'thai_nguyen', 'active'),
    ('a1111111-1111-1111-1111-111111111103', 'Group việc / SV Thái Nguyên (G3)',
     'https://www.facebook.com/groups/552660281927244/', 'facebook', 'job', 'student', 'thai_nguyen', 'active'),
    ('a1111111-1111-1111-1111-111111111104', 'Group việc / SV Thái Nguyên (G4)',
     'https://www.facebook.com/groups/783713308689243/', 'facebook', 'job', 'mixed', 'thai_nguyen', 'active'),
    ('a1111111-1111-1111-1111-111111111105', 'Group việc / SV Thái Nguyên (G5)',
     'https://www.facebook.com/groups/1156179755511070/', 'facebook', 'job', 'student', 'thai_nguyen', 'active'),
    ('a1111111-1111-1111-1111-111111111106', 'Group việc / SV Thái Nguyên (G6)',
     'https://www.facebook.com/groups/2188436011518830/', 'facebook', 'job', 'mixed', 'thai_nguyen', 'active'),
    ('a1111111-1111-1111-1111-111111111107', 'Group việc / SV Thái Nguyên (G7)',
     'https://www.facebook.com/groups/250881661082648/', 'facebook', 'job', 'student', 'thai_nguyen', 'active')
ON CONFLICT (url) DO UPDATE SET
    name = EXCLUDED.name,
    category = EXCLUDED.category,
    audience = EXCLUDED.audience,
    geo = EXCLUDED.geo,
    status = 'active';

-- =============================================================================
-- KIT_LOCAL — dedicated Local OS tenant (password = Admin@123)
-- =============================================================================
INSERT INTO public.tenants (
    id, tenant_code, tenant_name, country_code, default_currency,
    business_vertical, settings, status
)
VALUES (
    '11111111-1111-1111-1111-111111111106',
    'KIT_LOCAL',
    'Thái Nguyên Life',
    'VN', 'VND',
    'hybrid',
    jsonb_build_object(
        'platform', jsonb_build_object(
            'vertical', 'local',
            'enabled_modules', jsonb_build_array('local_os'),
            'allowed_modules', jsonb_build_array('local_os'),
            'features', jsonb_build_object()
        ),
        'product', jsonb_build_object(
            'package_code', 'local_os',
            'org_code', 'KIT_LOCAL'
        )
    ),
    1
)
ON CONFLICT (tenant_code) DO UPDATE SET
    tenant_name = EXCLUDED.tenant_name,
    business_vertical = EXCLUDED.business_vertical,
    settings = EXCLUDED.settings,
    updated_at = NOW(),
    deleted_at = NULL,
    status = 1;

INSERT INTO public.branches (
    id, tenant_id, branch_code, branch_name, address, phone, is_head_office, status
)
VALUES (
    '11111111-1111-1111-1111-111111111206',
    '11111111-1111-1111-1111-111111111106',
    'HQ',
    'Thái Nguyên Life HQ',
    'KIT Local OS — independent product',
    '0240000006',
    TRUE,
    1
)
ON CONFLICT (tenant_id, branch_code) DO UPDATE SET
    branch_name = EXCLUDED.branch_name,
    address = EXCLUDED.address,
    phone = EXCLUDED.phone,
    is_head_office = TRUE,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.employees (
    id, tenant_id, employee_code, full_name, phone, email, status
)
VALUES (
    '11111111-1111-1111-1111-111111111306',
    '11111111-1111-1111-1111-111111111106',
    'EMP001',
    'Admin Thái Nguyên Life',
    '0906000006',
    'admin@kit-local.kittech.vn',
    1
)
ON CONFLICT (tenant_id, employee_code) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone = EXCLUDED.phone,
    email = EXCLUDED.email,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.users (
    id, tenant_id, employee_id, username, email, password_hash, status
)
VALUES (
    '11111111-1111-1111-1111-111111111406',
    '11111111-1111-1111-1111-111111111106',
    '11111111-1111-1111-1111-111111111306',
    'admin',
    'admin@kit-local.kittech.vn',
    '$2a$11$Oq8dLLVbqREcBk4VBW0ELOuBQneydTDK7VLpR9FcHEiQdWoUTQyJS',
    1
)
ON CONFLICT (tenant_id, username) DO UPDATE SET
    employee_id = EXCLUDED.employee_id,
    email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash,
    deleted_at = NULL,
    updated_at = NOW();

INSERT INTO public.roles (id, tenant_id, role_code, role_name)
VALUES (
    '11111111-1111-1111-1111-111111111506',
    '11111111-1111-1111-1111-111111111106',
    'ADMIN',
    'Quản trị viên'
)
ON CONFLICT (tenant_id, role_code) DO UPDATE SET
    role_name = EXCLUDED.role_name;

INSERT INTO public.user_roles (user_id, role_id)
VALUES (
    '11111111-1111-1111-1111-111111111406',
    '11111111-1111-1111-1111-111111111506'
)
ON CONFLICT DO NOTHING;

-- KIT_LOCAL ADMIN: system.* + local_os.* only
DELETE FROM public.role_permissions rp
USING public.permissions p
WHERE rp.role_id = '11111111-1111-1111-1111-111111111506'
  AND rp.permission_id = p.id
  AND p.permission_code NOT LIKE 'system.%'
  AND p.permission_code NOT LIKE 'local_os.%';

INSERT INTO public.role_permissions (role_id, permission_id)
SELECT '11111111-1111-1111-1111-111111111506', p.id
FROM public.permissions p
WHERE (p.permission_code LIKE 'system.%' OR p.permission_code LIKE 'local_os.%')
  AND NOT EXISTS (
    SELECT 1 FROM public.role_permissions rp
    WHERE rp.role_id = '11111111-1111-1111-1111-111111111506'
      AND rp.permission_id = p.id
  );

SELECT kit_provision_pack_workspace(
    '11111111-1111-1111-1111-111111111106'::uuid,
    'local_os'
);

-- =============================================================================
-- Isolation: never leak local_os onto Pharmacy / Family / Marketing tenants
-- =============================================================================
DELETE FROM public.role_permissions rp
USING public.roles r, public.permissions p, public.tenants t
WHERE rp.role_id = r.id
  AND rp.permission_id = p.id
  AND r.tenant_id = t.id
  AND p.permission_code LIKE 'local_os.%'
  AND t.tenant_code <> 'KIT_LOCAL';

UPDATE public.tenants t
SET
    settings = jsonb_set(
        jsonb_set(
            t.settings,
            '{platform,enabled_modules}',
            COALESCE((
                SELECT jsonb_agg(to_jsonb(m))
                FROM jsonb_array_elements_text(
                    COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                ) AS m
                WHERE m <> 'local_os'
            ), '[]'::jsonb),
            true
        ),
        '{platform,allowed_modules}',
        COALESCE((
            SELECT jsonb_agg(to_jsonb(m))
            FROM jsonb_array_elements_text(
                COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)
            ) AS m
            WHERE m <> 'local_os'
        ), COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb)),
        true
    ),
    updated_at = NOW()
WHERE t.tenant_code <> 'KIT_LOCAL'
  AND (
        COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb) ? 'local_os'
     OR COALESCE(t.settings->'platform'->'allowed_modules', '[]'::jsonb) ? 'local_os'
  );
