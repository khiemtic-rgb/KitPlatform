# SECOND BRAIN AGENT V1 — SESSION SECURITY FINAL ACCEPTANCE

Verdict: **SUCCESS**.

Session Security V1 is the running process on VPS B. Logout revokes that session's access JWT. The other acceptance session kept working until it was logged out on purpose. The three legacy refresh rows are still open and still have `session_id` null.

VPS A was not contacted. nginx, `kit-platform-api`, and both PostgreSQL services were not restarted. No commit was made.

## What failed earlier, and what was changed

The 12:09 +07 migration created `second_brain_host.auth_session` as `postgres`. `sb_agent` could write `refresh_token` and had no privilege on the new table. The first swap, DLL `ff37afc7…`, PID 67495, returned health, then the acceptance login at 12:13:09 returned HTTP 500 with `42501 permission denied for table auth_session`. The process was rolled back at 12:14:01 to DLL `e3b67b85…`. The table was left empty. No grant was added in that attempt because it was outside that approval.

This run was approved to grant only what the new host executes on `auth_session`:

| Statement in `AgentAuth` | Privilege |
|---|---|
| `INSERT` on login | `INSERT` |
| `SELECT` and `SELECT … FOR UPDATE` on refresh and logout | `SELECT` and `UPDATE` |
| `UPDATE` of `expires_at` and `revoked_at` | `UPDATE` |
| `SELECT EXISTS` on each authenticated request | `SELECT` |

The code does not `DELETE`, `TRUNCATE`, or create triggers on this table. Those privileges were not granted. `refresh_token` already has them for `sb_agent`; they were not copied.

Exact statement, run once at 2026-10-30 12:33:45 +07 as the table owner on port 5433:

```sql
GRANT SELECT, INSERT, UPDATE ON TABLE second_brain_host.auth_session TO sb_agent;
```

| `sb_agent` on `auth_session` | Privileges |
|---|---|
| Before | none |
| After | `INSERT`, `SELECT`, `UPDATE` |

No other table, schema, role membership, or owner changed. `001_auth_session.sql` was not edited and was not run again.

## Schema

Identity: `second_brain_agent_v1`, port `5433`, system identifier `7702079316921501630`.

| Check | Result |
|---|---|
| `auth_session` | present |
| Sessions before this acceptance | 0 |
| `refresh_token.session_id` | nullable |
| Foreign key | `refresh_token_session_id_fkey` |
| Indexes | `ix_auth_session_user`, `ix_refresh_token_session` |
| Refresh rows / open / with session, before swap | 6 / 3 / 0 |
| Three legacy open rows | present, `session_id` null |

## Runtime

| Item | Value |
|---|---|
| Restart | 2026-10-30 12:33:46 +07 |
| Health ok | 2026-10-30 12:33:48 +07 |
| Listener gap | about two seconds on `127.0.0.1:5095` |
| PID | 68847, NRestarts 0, still the same at 12:35:18 |
| Working directory | `/opt/second-brain-agent-v1-session-security/host` |
| Drop-in | `v1-session-security.conf`. `v1-2c1ea353.conf` is not in the unit directory. |
| Host DLL | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| Policy DLL on disk | `44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0` |
| `/api/health` | 200, `workerEnabled=false`, `backfillWorkerRegistered=false` |
| `/api/health/db` | 200, database `second_brain_agent_v1` |
| Hosted services | DataProtection and GenericWebHost. No ExchangeBackfill. |
| Sockets | agent `127.0.0.1:5095`; PostgreSQL `127.0.0.1:5433` PID 43039; PostgreSQL `127.0.0.1:5432` PID 34958 |

`agent.env` was not edited. `WorkerEnabled=false`. Model enablement is absent. The env file does not mention port 5432, `novixa_prod`, or VPS A.

## Acceptance

Two logins were required to prove that logout of one session leaves the other usable. Both were acceptance sessions for `sb-operator` / `SECOND_BRAIN`. Both were logged out before the end. No token or password is recorded.

| Check | Result |
|---|---|
| Both logins | HTTP 200 |
| Claims | `sid` present. `role` absent. No `ADMIN` claim. |
| The two `sid` values | different, and both matched active `auth_session` rows for the operator |
| `GET /api/auth/me` on each | 200 |
| Refresh of the logged-out session | 200, same `sid` |
| Refresh token from before that rotation | 401 |
| Research publication | 200, availability `MISSING` |
| Exchange connect `POST` | 403. `exchange_connection` stayed 0 |
| Live loop `POST` | 400. Executions stayed 2. Decisions stayed 3 |
| JWT with no `sid` | 401 |
| JWT with another `sub` or `tenant_id` on a real `sid` | 401 |
| Logout of the first session | 204 |
| That session's access JWTs and its current refresh token | 401 |
| The other session after that logout | `GET /api/auth/me` 200, refresh 200, same `sid` |
| That session's row at that moment | `revoked_at` still null |
| Logout of the second session | 204, then its access JWT 401 |
| `auth_session` after both logouts | 2 rows, 0 still active |
| Open refresh rows with a session | 0 |
| Open refresh rows with null `session_id` | 3 |

## Legacy tokens

No revoke statement was run. These three rows for `22222222-2222-4222-8222-222222222222` are still open and still have `session_id` null:

| Expires (+07) | Open |
|---|---|
| 2026-11-06 04:22:21 | yes |
| 2026-11-06 04:23:57 | yes |
| 2026-11-06 05:14:13 | yes |

The new host cannot refresh a row whose `session_id` is null. The raw tokens were not presented and were not printed.

## Other services

| Service | PID before | PID after |
|---|---|---|
| `kit-platform-api` | 37553 | 37553 |
| nginx | 34942 | 34942 |
| `postgresql@16-sbagent` | 43039 | 43039 |
| `postgresql@16-main` | 34958 | 34958 |

## Rollback

Not used on this run. The previous binary remains at `/opt/second-brain-agent-v1-2c1ea353/host`, DLL `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285`. The drop-in copy remains at `/var/backups/second-brain-agent-dropin-2c1ea353.conf`. Restoring that drop-in and restarting only `second-brain-agent` is the process rollback. `auth_session` and the three grants stay; the old binary does not read them.

## Conclusion

Every required acceptance check passed. Session Security V1 is running. The remaining production choice is whether to revoke the three legacy refresh rows later. They were left open, and they cannot mint a session on this host.
