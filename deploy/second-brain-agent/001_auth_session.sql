-- Second Brain agent session identity.
-- Apply only to second_brain_agent_v1 on the 5433 cluster, and only after approval.
-- Do not add this file to migration-files.prod.txt or migration-files.personal-intelligence.txt.
-- Existing refresh rows stay in place. Their session_id remains null, so they cannot mint a new access token.

CREATE TABLE IF NOT EXISTS second_brain_host.auth_session (
    session_id  UUID PRIMARY KEY,
    user_id     UUID NOT NULL REFERENCES public.users(id),
    tenant_id   UUID NOT NULL REFERENCES public.tenants(id),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at  TIMESTAMPTZ NOT NULL,
    revoked_at  TIMESTAMPTZ
);

ALTER TABLE second_brain_host.refresh_token
    ADD COLUMN IF NOT EXISTS session_id UUID REFERENCES second_brain_host.auth_session(session_id);

CREATE INDEX IF NOT EXISTS ix_refresh_token_session
    ON second_brain_host.refresh_token (session_id)
    WHERE session_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_auth_session_user
    ON second_brain_host.auth_session (user_id, tenant_id);
