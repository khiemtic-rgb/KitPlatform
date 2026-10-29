-- Isolated identity shell for second_brain_agent_v1.
-- This file is not part of migration-files.prod.txt and must not be applied to novixa_prod or trading_research.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE OR REPLACE FUNCTION kit_uuid_v7()
RETURNS uuid
LANGUAGE plpgsql
VOLATILE PARALLEL SAFE
AS $$
DECLARE
    unix_ts_ms bytea;
    rand_a     bytea;
    rand_b     bytea;
    uuid_bytes bytea;
BEGIN
    unix_ts_ms := substring(int8send(floor(extract(epoch FROM clock_timestamp()) * 1000)::bigint) FROM 3);
    rand_a     := gen_random_bytes(2);
    rand_b     := gen_random_bytes(8);
    uuid_bytes := unix_ts_ms || rand_a || rand_b;
    uuid_bytes := set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & 15) | 112);
    uuid_bytes := set_byte(uuid_bytes, 8, (get_byte(uuid_bytes, 8) & 63) | 128);
    RETURN encode(uuid_bytes, 'hex')::uuid;
END;
$$;

CREATE OR REPLACE FUNCTION kit_bump_row_version()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.row_version := COALESCE(OLD.row_version, 0) + 1;
    NEW.updated_at  := NOW();
    RETURN NEW;
END;
$$;

CREATE TABLE IF NOT EXISTS public.tenants (
    id              UUID PRIMARY KEY,
    tenant_code     VARCHAR(50)  NOT NULL,
    tenant_name     VARCHAR(255) NOT NULL,
    country_code    CHAR(2)      NOT NULL DEFAULT 'VN',
    default_currency CHAR(3)     NOT NULL DEFAULT 'VND',
    timezone        VARCHAR(50)  NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    settings        JSONB        NOT NULL DEFAULT '{}'::jsonb,
    status          SMALLINT     NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_tenants_code UNIQUE (tenant_code)
);

CREATE TABLE IF NOT EXISTS public.users (
    id              UUID PRIMARY KEY,
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    username        VARCHAR(100) NOT NULL,
    email           TEXT         NOT NULL,
    password_hash   VARCHAR(255) NOT NULL,
    status          SMALLINT     NOT NULL DEFAULT 1,
    last_login_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_users_tenant_username UNIQUE (tenant_id, username),
    CONSTRAINT uq_users_tenant_email UNIQUE (tenant_id, email)
);

CREATE TABLE IF NOT EXISTS public.permissions (
    id              UUID PRIMARY KEY,
    permission_code VARCHAR(100) NOT NULL,
    permission_name VARCHAR(255) NOT NULL,
    module_name     VARCHAR(100) NOT NULL,
    CONSTRAINT uq_permissions_code UNIQUE (permission_code)
);

CREATE TABLE IF NOT EXISTS public.platform_module_registry (
    module_code     VARCHAR(50)  PRIMARY KEY,
    module_name     VARCHAR(255) NOT NULL,
    description     TEXT,
    verticals       TEXT[]       NOT NULL DEFAULT '{}',
    sort_order      INT          NOT NULL DEFAULT 0,
    status          SMALLINT     NOT NULL DEFAULT 1
);

CREATE SCHEMA IF NOT EXISTS second_brain_host;

CREATE TABLE IF NOT EXISTS second_brain_host.operator (
    user_id     UUID PRIMARY KEY REFERENCES public.users(id),
    roles       TEXT[] NOT NULL,
    permissions TEXT[] NOT NULL
);

CREATE TABLE IF NOT EXISTS second_brain_host.refresh_token (
    token_hash  TEXT PRIMARY KEY,
    user_id     UUID NOT NULL REFERENCES public.users(id),
    expires_at  TIMESTAMPTZ NOT NULL,
    revoked_at  TIMESTAMPTZ
);
