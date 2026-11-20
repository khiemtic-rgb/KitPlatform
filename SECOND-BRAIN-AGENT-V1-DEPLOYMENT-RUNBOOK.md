# SECOND BRAIN AGENT V1 — DEPLOYMENT RUNBOOK

Baseline confirmed: **DEPLOYMENT_BASELINE_CONFIRMED**.

Read-only check at 2026-10-30 12:41:04 +07. No DDL, grant, restart, or token revoke was performed for this confirmation.

## Current VPS B baseline

| Item | Confirmed value |
|---|---|
| Host | `157.10.53.182` |
| Service | `second-brain-agent`, active |
| PID | 68847, NRestarts 0, since 2026-10-30 12:33:46 +07 |
| Listen | `127.0.0.1:5095` |
| Working directory | `/opt/second-brain-agent-v1-session-security/host` |
| Drop-in | `/etc/systemd/system/second-brain-agent.service.d/v1-session-security.conf` |
| Host DLL SHA-256 | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| Policy DLL SHA-256 | `44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0` |
| `/api/health` | HTTP 200, `status=ok`, `workerEnabled=false`, `backfillWorkerRegistered=false` |
| Hosted services | DataProtection and GenericWebHost. ExchangeBackfill absent. |
| `/api/health/db` | HTTP 200, database `second_brain_agent_v1` |
| Database | `second_brain_agent_v1`, port 5433, system identifier `7702079316921501630` |
| Role `sb_agent` on `auth_session` | `INSERT`, `SELECT`, `UPDATE` |
| Legacy refresh rows | 2026-11-06 04:22:21, 04:23:57, and 05:14:13 +07, all open, `session_id` null |
| Peers | `kit-platform-api` 37553, nginx 34942, `postgresql@16-sbagent` 43039, `postgresql@16-main` 34958 |

`ASPNETCORE_URLS` is `http://127.0.0.1:5095`. `WorkerEnabled=false`. Model enablement is absent. The env file does not mention port 5432, `novixa_prod`, or `103.200.23.229`.

Do not deploy this release again while those values hold.

## Package

`E:\sb-agent-v1-session-security\package`

From that directory:

```text
sha256sum -c SHA256SUMS.txt
```

The host DLL line must be `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`. `host/appsettings.json` is a sample and contains no secret.

## Fresh install

Use this only when the service is not already on this DLL. VPS B is already on it.

1. Confirm the database name, port 5433, and system identifier `7702079316921501630`. Refuse port 5432 and refuse `novixa_prod`.
2. Back up schema `second_brain_host` and the current systemd drop-in under `/var/backups`.
3. Apply `001_auth_session.sql` with `psql -p 5433` as the database owner. Do not backfill `session_id`.
4. Grant the host role only:

```sql
GRANT SELECT, INSERT, UPDATE ON TABLE second_brain_host.auth_session TO sb_agent;
```

5. Copy `host/` to `/opt/second-brain-agent-v1-session-security/host` and `chown -R sbagent:sbagent`.
6. Re-check both DLL hashes.
7. Install drop-in `v1-session-security.conf`:

```ini
[Service]
WorkingDirectory=/opt/second-brain-agent-v1-session-security/host
ExecStart=
ExecStart=/usr/bin/dotnet /opt/second-brain-agent-v1-session-security/host/KitPlatform.SecondBrainAgent.Host.dll
```

8. Remove the previous drop-in from the unit directory only after the new files match. `systemctl daemon-reload` and restart only `second-brain-agent`.
9. Require HTTP 200 from `/api/health` and `/api/health/db`. If either fails, roll back the process. Do not drop the table.

Do not use `deploy/second-brain-agent/install-candidate.sh` for this release. It targets an older drop-in and does not apply the session migration or the grant.

Leave `agent.env` unchanged. Do not enable the model, exchange backfill, or a research publication root. Do not revoke the three legacy refresh rows as part of install. A null `session_id` cannot refresh on this host.

Administration SQL on this server uses `sudo -u postgres psql -p 5433`. `inet_server_port()` is null on the local socket; use `current_setting('port')`.

## Rollback

1. Remove `v1-session-security.conf`.
2. Restore `/var/backups/second-brain-agent-dropin-2c1ea353.conf` to `/etc/systemd/system/second-brain-agent.service.d/v1-2c1ea353.conf`.
3. `systemctl daemon-reload` and restart only `second-brain-agent`.
4. Confirm DLL `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` at `/opt/second-brain-agent-v1-2c1ea353/host` and health HTTP 200.

Leave `auth_session`, `refresh_token.session_id`, and the grants. Do not restore the backup dump over the live database. Do not revoke tokens.

## Acceptance already recorded

The checks in `SECOND-BRAIN-AGENT-V1-SESSION-SECURITY-FINAL-ACCEPTANCE-REPORT.md` passed on PID 68847. This baseline check did not repeat a login.
