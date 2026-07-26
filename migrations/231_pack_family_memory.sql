-- KitPlatform 231: FamilyOS Family Memories (kỷ niệm gia đình lưu lâu dài)
-- Depends on: 192, 212 (gratitude), 199 (evidence), 213 (team unlock)
-- Memories today are derived client-side and vanish next day. This persists them.

CREATE TABLE IF NOT EXISTS pack_family.family_memory (
    id               UUID PRIMARY KEY DEFAULT kit_uuid_v7(),
    tenant_id        UUID NOT NULL REFERENCES public.tenants(id),
    family_id        UUID NOT NULL REFERENCES pack_family.family(id),
    member_id        UUID REFERENCES pack_family.membership(id),
    flow_date        DATE NOT NULL,
    kind             VARCHAR(32) NOT NULL,
    title_vi         VARCHAR(200) NOT NULL,
    note_vi          VARCHAR(600),
    icon             VARCHAR(16),
    photo_url        VARCHAR(500),
    source_ref       VARCHAR(120),
    is_favorite      BOOLEAN NOT NULL DEFAULT FALSE,
    happened_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version      INT NOT NULL DEFAULT 0,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by       UUID,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by       UUID,
    deleted_at       TIMESTAMPTZ,
    CONSTRAINT ck_family_memory_kind CHECK (
        kind IN (
            'beautiful_day',
            'streak_milestone',
            'gratitude',
            'photo',
            'team_unlock',
            'reward',
            'first_time',
            'manual'
        )
    )
);

-- Auto-captured memories carry source_ref so the same event never duplicates.
CREATE UNIQUE INDEX IF NOT EXISTS ux_family_memory_source
    ON pack_family.family_memory (tenant_id, family_id, kind, source_ref)
    WHERE source_ref IS NOT NULL AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_family_memory_family_date
    ON pack_family.family_memory (family_id, happened_at DESC)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_family_memory_favorite
    ON pack_family.family_memory (family_id, is_favorite, happened_at DESC)
    WHERE deleted_at IS NULL AND is_favorite;

DROP TRIGGER IF EXISTS trg_family_memory_row_version ON pack_family.family_memory;
CREATE TRIGGER trg_family_memory_row_version
    BEFORE UPDATE ON pack_family.family_memory
    FOR EACH ROW EXECUTE FUNCTION kit_bump_row_version();

ALTER TABLE pack_family.family_memory ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON pack_family.family_memory;
CREATE POLICY tenant_isolation ON pack_family.family_memory
    FOR ALL
    USING (kit_rls_tenant_match(tenant_id))
    WITH CHECK (kit_rls_tenant_match(tenant_id));

COMMENT ON TABLE pack_family.family_memory IS
    'Durable family memories: beautiful days, streaks, thank-you notes, photos, Movie Night, manual notes.';
