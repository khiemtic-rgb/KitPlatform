# Second Brain Agent V1 — release review package

Status: ready for human review. This document does not authorize a change on VPS A.

## Artifact

| Role | SHA-256 | Bytes |
|---|---|---|
| Candidate host DLL | `90265F6C3E87565EA38579943D437D8195AD5DDE1C74DD95B55DAACF82BE87A2` | 83456 |
| Host currently serving on B | `891E5726D532B2923A92F38E24C902256B92CF6A326EBD748B213B71DDA9ACC7` | 78848 |
| UI `index.html` | `BCF870925D0C65115C3D0160CE6AA58B1813A6F209ED69E7AF99D8EAB05EE8FB` | 409 |
| UI `index-DmF68tyh.js` | `7C337D09588F1775A8C2B3289B7CFCD4F3B2CAE2BE148127ED601845102AF659` | 1305300 |
| UI `index-BdOndhxL.css` | `98ADF782BD027847CFEAC99BC2F2735177375EA17BB836601CE1D7290CA699C5` | 2940 |

Full file list: `deploy/second-brain-agent/candidate-release.sha256`. Bytes already applied on B: `deploy/second-brain-agent/b-deployed.sha256`.

Branch `second-brain/paper-proposal-v1`, HEAD `ad06c4a81b16f7aca6808828eb273eb70a1af39a`. The host project and schema `000`, `385–393` are untracked on this branch. `399` is in HEAD. No commit was created.

## Schema applied on B

Order: `000`, `385`, `387`, `388`, `389`, `390`, `391`, `392`, `393`, `399`. There is no Personal Intelligence `386`.

`388` blob `610597b2337e06310481515b800d7e1baced5553` and `389` blob `e68df02cf067ec76f0bcaa195e3e279cf650dfcf` match commit `797ca615`. They are not the later `origin/main` blobs from `9c14c508`.

## Scoped commit proposal

Do not `git add -A`. The working tree has other unrelated changes. Stage only:

- `src/Hosts/SecondBrainAgent/`
- `tests/KitPlatform.SecondBrainAgent.Tests/`
- `deploy/second-brain-agent/`
- `client/admin/second-brain-host/`
- `client/admin/src/second-brain-host/`
- `client/admin/vite.second-brain-host.config.ts`
- `migrations/385_pack_personal_intelligence_trading_ledger_v1.sql`
- `migrations/387_pack_personal_intelligence_loop_v1.sql`
- `migrations/388_pack_personal_intelligence_loop_integrity_v1.sql`
- `migrations/389_pack_personal_intelligence_operation_v1.sql`
- `migrations/390_platform_module_personal_intelligence.sql`
- `migrations/391_pack_personal_intelligence_exchange_connection_v1.sql`
- `migrations/392_pack_personal_intelligence_exchange_execution_sync_v1.sql`
- `migrations/393_pack_personal_intelligence_exchange_backfill_v1.sql`

`399` is already committed. After staging, `git diff --cached --stat` must not show Pharmacy, Family, Content, or client files outside `second-brain-host`.

## Secrets supplied outside Git

- `ConnectionStrings__Default` to `second_brain_agent_v1` on `127.0.0.1` and a port other than 5432
- `SecondBrain__JwtSecret` of at least 32 characters
- `SecondBrain__BootstrapAdminPassword` for the first empty operator table
- `SecondBrain__SystemIdentifier` equal to `system_identifier` of the target cluster
- `ASPNETCORE_URLS` on `127.0.0.1` or `[::1]`
- `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`
- `PersonalIntelligence__ResearchPublication__Root` empty until a private research directory exists
- `SecondBrain__WebRoot` pointing at the verified UI directory

## B versus A

B already has cluster `16/sbagent` on `127.0.0.1:5433`, database `second_brain_agent_v1`, identifier `7702079316921501630`. The serving unit is `second-brain-agent.service` on `127.0.0.1:5095` with the old DLL. The candidate was started only as a transient unit on `127.0.0.1:5096` and then stopped.

A, from the read-only survey in document 008, has PostgreSQL on `5432` with `novixa_prod`, `trading_research`, and `trading_agent`. It has no `second_brain_agent_v1` unit. Nginx and UFW are shared. Do not create the database on that cluster.

## Deploy order after approval

1. Commit the scoped file list and confirm the checksum files still match.
2. On the target machine, create a new PostgreSQL cluster whose port is not 5432 and whose data directory is not the production cluster.
3. Create role and database `second_brain_agent_v1` only on that cluster. Apply the ten SQL files in the order above. Confirm `current_database()`, `inet_server_port()`, and `system_identifier`.
4. Install the candidate DLL and the UI bytes above into a new directory. Do not replace `kit-platform-api`.
5. Create a new systemd unit. Bind loopback only. Set the system identifier from step 3.
6. Health checks: `/api/health` reports `workerEnabled=false` and `backfillWorkerRegistered=false`; `/api/health/db` returns `value=1`; anonymous summary is 401; `live:true` is 400; a repeated paper `operationKey` returns the same decision.
7. Confirm no socket to `103.200.23.229` and no new database on port 5432.

Stop if any health check fails, if the process binds a non-loopback address, or if the connected system identifier differs.

Rollback is `systemctl stop` and `disable` of the new unit only. Do not restart `kit-platform-api`, Nginx, or the production PostgreSQL cluster, and do not drop `novixa_prod`.

## Approval still required

- The scoped commit.
- Any swap that replaces the DLL currently serving on B.
- A new cluster on A.
- Nginx, DNS, and UFW changes.
- A DataProtection certificate before any listener that is not loopback. The candidate refuses `0.0.0.0` and hostnames, and the key ring is still unencrypted at rest.
