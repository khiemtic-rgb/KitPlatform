# SECOND BRAIN AGENT V1 — SESSION SECURITY DEPLOYMENT REPORT

Verdict of the 12:16 +07 re-execution: **BLOCKED**.

The approved sequence stops when the schema no longer matches the preflight. At 2026-10-30 12:16:59 +07 the database already has `second_brain_host.auth_session` and nullable `refresh_token.session_id` from the 12:09 migration. `sb_agent` still has no privilege on `auth_session`. No DDL, grant, upload, or restart was performed on this pass. The running process is unchanged from the rollback below.

Read-only confirmation at 12:16:59 +07:

| Check | Result |
|---|---|
| Database | `second_brain_agent_v1`, port `5433`, system identifier `7702079316921501630` |
| Service | active, PID 68026, NRestarts 0, since 12:14:01 +07 |
| Working directory | `/opt/second-brain-agent-v1-2c1ea353/host` |
| DLL | `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` |
| Drop-in | `v1-2c1ea353.conf` present, `v1-session-security.conf` absent |
| Health and DB health | 200, worker off, no ExchangeBackfill hosted service |
| Sessions / refresh / open / with session | 0 / 6 / 3 / 0 |
| Three legacy open rows | still open, `session_id` null |
| `sb_agent` grants on `auth_session` | none |
| Peer PIDs | `kit-platform-api` 37553, nginx 34942, `postgresql@16-sbagent` 43039, `postgresql@16-main` 34958 |
| Env | `WorkerEnabled=false`, model enablement absent, no port 5432, `novixa_prod`, or VPS A |

A retry of the same binary needs a separate approval to grant `sb_agent` the same table privileges on `auth_session` that it already has on `refresh_token`. That grant is not in `001_auth_session.sql` and was not applied.

## First execution

Verdict: **ROLLED BACK**.

Session Security V1 is not the running process. The migration is present on `second_brain_agent_v1`. The agent process was returned to DLL `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` after the new host rejected login with `42501 permission denied for table auth_session`. No grant was added. The three legacy refresh rows were not revoked.

VPS A was not contacted. nginx, `kit-platform-api`, and both PostgreSQL services were not restarted.

## Deployment window

| Event | Time (+07) |
|---|---|
| Preflight and backup completed | 2026-10-30 12:08:48 |
| Migration committed | 2026-10-30 12:09:22 |
| New process started | 2026-10-30 12:10:02, PID 67495 |
| New health ok | 2026-10-30 12:10:04 |
| Acceptance login | 2026-10-30 12:13:09, HTTP 500 |
| Rollback restart | 2026-10-30 12:14:01, PID 68026 |
| Restored health ok | 2026-10-30 12:14:03 |

Host: `157.10.53.182`, hostname `interdata.1793262774`.

Each restart refused `127.0.0.1:5095` for about two seconds, then health returned. No other service was restarted.

Postgres administration used the local peer socket on port 5433. `inet_server_port()` is null on that socket, so identity used `current_setting('port')`. TCP as `postgres` to `127.0.0.1:5433` has no password configured and was not used. The agent itself still connects to `127.0.0.1:5433`.

## Migration

File checksum `7aa9fcc61788720350f7ee36fda190a1249354a4c5d1fd5763014c1cb454b268`.

Identity before DDL: `second_brain_agent_v1`, port `5433`, system identifier `7702079316921501630`.

Before DDL: `auth_session` absent, `session_id` absent, 6 refresh rows, 3 open.

After DDL, all required checks passed:

| Check | Result |
|---|---|
| `second_brain_host.auth_session` | present |
| Session rows | 0 |
| `refresh_token.session_id` | nullable `YES` |
| Foreign key | `refresh_token_session_id_fkey` |
| Indexes | `ix_auth_session_user`, `ix_refresh_token_session` |
| Refresh rows / open / with session | 6 / 3 / 0 |
| Three legacy rows | still open, `session_id` null |

The service was still PID 64986 at the end of the migration. It was not restarted until the binary checks passed.

Backup is `/var/backups/second-brain-host-before-001-auth-session.dump` (4994 bytes, mode 600, left on the server). Drop-in copy is `/var/backups/second-brain-agent-dropin-2c1ea353.conf`. The dump was not restored.

Two additional refresh rows, both already revoked and expiring `2026-11-06 19:22:41 +07`, were present before this deploy. They are not the three open legacy rows. They were not modified.

## Binary

Uploaded package checksums matched before install. Installed copies matched:

| DLL | SHA-256 |
|---|---|
| `KitPlatform.SecondBrainAgent.Host.dll` | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | `44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0` |

Install path: `/opt/second-brain-agent-v1-session-security/host`.

Drop-in used during the attempt: `/etc/systemd/system/second-brain-agent.service.d/v1-session-security.conf`, working directory and `ExecStart` pointing at that host DLL. `install-candidate.sh` was not used. `agent.env` was not edited.

That drop-in was removed on rollback. The directory remains on disk and is not the running unit.

## Service after rollback

| Item | Value |
|---|---|
| Unit | `second-brain-agent`, active |
| PID | 68026 |
| NRestarts | 0 |
| Working directory | `/opt/second-brain-agent-v1-2c1ea353/host` |
| DLL SHA-256 | `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` |
| Active since | 2026-10-30 12:14:01 +07 |
| `/api/health` | 200, `status=ok`, `workerEnabled=false`, `backfillWorkerRegistered=false` |
| `/api/health/db` | 200, `status=ok`, database `second_brain_agent_v1` |
| Hosted services | DataProtection and GenericWebHost only. No ExchangeBackfill. |
| Listen | `127.0.0.1:5095` owned by PID 68026 |

While the new process was up, its health body had the same worker flags and the same two hosted services. Its DLL hash was the new hash above. PID was 67495 from 12:10:02 until the rollback.

## Acceptance

One login was attempted: `sb-operator`, tenant `SECOND_BRAIN`, at 12:13:09 +07. HTTP 500. No token was issued. The journal error is `42501: permission denied for table auth_session`.

No refresh, logout, `/api/auth/me`, forged JWT, exchange call, or live loop was run, because there was no access token. No second login was attempted.

`auth_session` stayed at 0 rows. Refresh rows stayed at 6. Open rows stayed at 3. No business row was written.

## Legacy tokens

Not revoked. After rollback the three open rows for `22222222-2222-4222-8222-222222222222` still have `session_id` null and `revoked_at` null:

| Expires (+07) | Open |
|---|---|
| 2026-11-06 04:22:21 | yes |
| 2026-11-06 04:23:57 | yes |
| 2026-11-06 05:14:13 | yes |

The already revoked `2026-11-06 18:00:42` row is unchanged. No `UPDATE` was run.

## Peer processes

| Service | PID before | PID after |
|---|---|---|
| `kit-platform-api` | 37553 | 37553 |
| nginx | 34942 | 34942 |
| `postgresql@16-sbagent` | 43039 | 43039 |
| `postgresql@16-main` | 34958 | 34958 |

Port 5432 is still the main cluster, PID 34958. Port 5433 is still the agent cluster, PID 43039. VPS A `103.200.23.229` was not opened.

## Rollback

Process rollback completed. Drop-in `v1-2c1ea353.conf` is restored. Drop-in `v1-session-security.conf` is absent. Health and the old DLL hash match the table above.

Left in place, as required:

- `second_brain_host.auth_session`
- `refresh_token.session_id`
- both indexes and the foreign key
- the backup dump
- `/opt/second-brain-agent-v1-session-security` (not running)

The dump was not restored. The table was not dropped. Tokens were not revoked.

## Why login failed

`auth_session` is owned by `postgres`. `information_schema.role_table_grants` shows `sb_agent` has `SELECT`, `INSERT`, `UPDATE`, and `DELETE` on `refresh_token`, and no privilege on `auth_session`. The approved SQL creates the table and does not grant the agent role. The running role is `sb_agent`. Table privileges on `refresh_token` already cover the new nullable column, so the old binary can still insert refresh rows.

No grant, owner change, or other schema edit was made to bypass this.

## Conclusion

Session Security V1 is **not in effect**. The running agent is the previous `2c1ea353` binary. Logout still does not invalidate an access JWT on that binary. The session table exists and is empty, so a later retry can reuse it after an approved grant to `sb_agent` that matches the privileges already held on `refresh_token`, then a new swap of the same DLL hashes. That grant was outside this approval and was not applied.

No commit, push, or merge was made.
