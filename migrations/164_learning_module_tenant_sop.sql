-- KitPlatform 164: Tenant SOP override for learning modules (nhà thuốc tự ghi nội dung)

CREATE TABLE IF NOT EXISTS pack_learning.module_tenant_override (
    tenant_id       UUID         NOT NULL REFERENCES public.tenants(id),
    module_id       UUID         NOT NULL REFERENCES pack_learning.module(id) ON DELETE CASCADE,
    title           VARCHAR(200),
    summary         TEXT,
    body_markdown   TEXT         NOT NULL,
    updated_by_user_id UUID REFERENCES public.users(id),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (tenant_id, module_id)
);

CREATE INDEX IF NOT EXISTS ix_learning_module_override_module
    ON pack_learning.module_tenant_override (module_id);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.module_tenant_override TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE pack_learning.module_tenant_override TO pharmacore;
    END IF;
END
$$;
