# Second Brain Agent V1 — deployment report

Conclusion: `DEPLOYED_AND_RUNNING`

VPS B `157.10.53.182`, service `second-brain-agent` only. Commit `2c1ea353c58fcb1cf74d996bade25df28f34f8af`. No migration, no env edit, no LLM key, no worker, no exchange call, no VPS A change, no nginx or `kit-platform-api` restart, and no refresh-token revoke.

## Time

Precheck read: 2026-10-30 11:11:06 +07.  
Install window: 2026-10-30 11:12:29–11:12:32 +07.  
Service entered active: 2026-10-30 11:12:30 +07.  
Postcheck: 2026-10-30 11:12:53 +07.

## Process

| | Before | After |
|---|---|---|
| PID | 62086 | 64986 |
| NRestarts | 0 | 0 |
| Active since | 2026-10-30 09:51:55 +07 | 2026-10-30 11:12:30 +07 |
| Working directory | `/opt/second-brain-agent-v1-5f29e1bd/host` | `/opt/second-brain-agent-v1-2c1ea353/host` |
| Drop-in | `v1-5f29e1bd.conf` | `v1-2c1ea353.conf` |
| User | `sbagent` | `sbagent` |

Host DLL before: `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263` (83456 bytes).  
Host DLL after: `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` (83456 bytes).  
The staged file on the server matched that hash before the swap. `SHA256SUMS.txt` in `E:\sb-agent-v1-2c1ea353` had 0 mismatches before upload. `source.tar` commit id is `2c1ea353c58fcb1cf74d996bade25df28f34f8af`.

The previous DLL is still at `/opt/second-brain-agent-v1-5f29e1bd/host/KitPlatform.SecondBrainAgent.Host.dll` with the same hash. The previous drop-in was copied to `/var/backups/second-brain-agent-dropin-5f29e1bd.conf` before it was removed from the unit directory.

## Health

`GET /api/health` HTTP 200:

`status=ok`, `service=second-brain-agent`, `database=second_brain_agent_v1`, `workerEnabled=false`, `backfillWorkerRegistered=false`. Hosted services are DataProtection and GenericWebHost.

`GET /api/health/db` HTTP 200:

`status=ok`, `database=second_brain_agent_v1`, `value=1`, `workerEnabled=false`.

During the first second after restart, the installer saw two connection refusals on port 5095, then both health URLs returned ok. The service stayed active. Rollback did not run.

## Security boundary

- Listen: `127.0.0.1:5095` owned by PID 64986. Port 5096 is closed.
- The agent socket is `127.0.0.1` to `127.0.0.1:5433` only. No socket to port 5432 and no socket to `103.200.23.229`.
- Port 5432 remains the `postgresql@16-main` listener, PID 34958, unchanged.
- `PersonalIntelligence__BusinessIntent__` key count in the env file is 0. The model stays off.
- `PersonalIntelligence__ResearchPublication__Root` is empty. Health still returns 200.
- `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`.
- `SecondBrain__SystemIdentifier=7702079316921501630`.
- `ASPNETCORE_URLS=http://127.0.0.1:5095`.
- `SecondBrain__WebRoot=/opt/second-brain-agent/ui`. `index.html` SHA-256 is still `bcf870925d0c65115c3d0160ce6aa58b1813a6f209ed69e7af99d8eab05ee8fb`.
- Env file has no `5432`, `novixa_prod`, or `103.200.23.229`.

Peer PIDs unchanged and still active: `kit-platform-api` 37553, nginx 34942, `postgresql@16-sbagent` 43039, `postgresql@16-main` 34958.

## Rollback status

Not used. The rollback binary and the drop-in backup are both on disk.

## Smoke

Unauthenticated reads only. No login, no paper decision, and no task was created.

- `GET /api/personal-intelligence/trading/summary` HTTP 401.
- `GET /api/personal-intelligence/research/publication` HTTP 401.

## Still outside this acceptance

- A live model plan. No provider key was added.
- A real `research-publication/v1` package. The root is empty.
- The three existing `sb-operator` refresh tokens. They were not revoked.
- A paper write on production. That was skipped so the existing ledger rows stay unchanged.

`DEPLOYED_AND_RUNNING`
