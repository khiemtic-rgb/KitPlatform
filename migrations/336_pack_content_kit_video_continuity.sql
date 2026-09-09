-- KitPlatform 336: KIT Video Engine Phase 03 — Scene / Beat / Shot / Continuity
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Script is SoT. Graph JSONB per production. Do not invent characters/actions/dialogue.
-- No Gemini / Runway / LipSync in this migration.

CREATE TABLE IF NOT EXISTS pack_content.video_story_graph (
    production_id  UUID PRIMARY KEY REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    script_text    TEXT NOT NULL DEFAULT '',
    script_hash    VARCHAR(64) NOT NULL DEFAULT '',
    graph_json     JSONB NOT NULL DEFAULT '{}'::jsonb,
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pack_content.video_continuity_override (
    id             UUID PRIMARY KEY,
    production_id  UUID NOT NULL REFERENCES pack_content.video_production (id) ON DELETE RESTRICT,
    shot_id        VARCHAR(80) NOT NULL,
    reason         TEXT NOT NULL,
    approved_by    VARCHAR(160) NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_video_continuity_override_prod
    ON pack_content.video_continuity_override (production_id, shot_id, created_at DESC);

COMMENT ON TABLE pack_content.video_story_graph IS
    'Phase 03 story graph: SCRIPT → SCENE → BEAT → SHOT. Script SoT. Continuity snapshots live in graph_json.';
COMMENT ON TABLE pack_content.video_continuity_override IS
    'Human override of CONTINUITY_WARNING only. Reason + ApprovedBy + Timestamp required.';
