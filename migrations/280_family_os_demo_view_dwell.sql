-- Family OS demo-house dwell (session duration) — GTM ops.
-- Depends on: 255_family_os_demo_view.sql

ALTER TABLE public.family_os_demo_view
    ADD COLUMN IF NOT EXISTS session_id UUID,
    ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS duration_seconds INT NOT NULL DEFAULT 0;

UPDATE public.family_os_demo_view
SET last_seen_at = COALESCE(last_seen_at, created_at)
WHERE last_seen_at IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_family_os_demo_view_session
    ON public.family_os_demo_view (session_id)
    WHERE session_id IS NOT NULL;

COMMENT ON COLUMN public.family_os_demo_view.session_id IS
    'SPA-generated visit id — heartbeats update the same row.';
COMMENT ON COLUMN public.family_os_demo_view.duration_seconds IS
    'Approx dwell seconds (capped); from last_seen_at - created_at.';
