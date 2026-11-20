# SECOND BRAIN — RESEARCH PUBLICATION V1 READINESS

## 1. Executive verdict

**READY_FOR_DEPLOYMENT**

The accepted agent already reads this publication. No source change and no new host binary are required. The release package is a byte copy of the original candidate. VPS B was inspected and not changed. The live root is still empty, so the running API still returns `MISSING` until the command sequence below is approved and executed. This report does not claim that the publication is deployed.

Checked at 2026-10-30 12:58:41 +07. Host DLL SHA-256 remains `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`.

## 2. Source audit

`FileResearchPublicationReader` accepts only `research-publication/v1`. It requires `publicationId`, `publisher`, `publishedAt`, and four SHA-256 fields. It opens only `manifest.json`, `lab/LAB_RESULT.json`, `paper/PAPER_OPS_STATUS.json`, `paper/PAPER_RESEARCH_REPORT.json`, and `paper/PAPER_OPS_HEALTH.json`. A missing root or missing required file is `MISSING`. A hash mismatch is `INCONSISTENT`. Health `OK` becomes `FRESH`. Health `STALE` stays `STALE`. The reader does not recompute bar age at request time.

`ResearchPublicationController` exposes the five GET routes below. The class policy is `personal_intelligence.research.read`. Platform role `ADMIN` also passes. `trading.run` does not. The module filter reads `public.tenants.settings.platform.enabled_modules`.

The isolated host calls `AddPersonalIntelligenceServices`, then forces `ExchangeBackfillOptions.WorkerEnabled=false`. That registration does not add `ExchangeBackfillWorker`. Exchange connector types are constructed in the same process and are not called by the research reader. A research GET does not open an exchange connection, does not read exchange credentials, and does not write a decision, task, or execution. Splitting that registration is not required for this publication, and it would mean a new host binary. That was not done.

`publish_research_v1.py` lives beside the lab output, not in this repository. It copies lab and health bytes, removes status keys whose names contain `api_key`, drops `frozen_lab_result` from the report, and writes `manifest.json`. The candidate was already produced. It was not published again.

## 3. Publication integrity

Candidate: `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\output\publication-candidate-20261030`

The directory contains exactly five files. No secret key, password, or token field was present. The candidate was not modified.

| File | SHA-256 | Bytes |
|---|---|---|
| `manifest.json` | `cf0661e9a64f2a40ded3202b9a1dca4621839a5be44b45b0aed57f969095c578` | 544 |
| `lab/LAB_RESULT.json` | `8f19e6e010999637fe2b1b793106ea2c22b5def60e06fbb8b06dcb6267d5714b` | 41712 |
| `paper/PAPER_OPS_STATUS.json` | `0cdecbb0e7b2a5f8430cd32d713aaf5be447096272a273cb72e62b0f32bc84b5` | 1250 |
| `paper/PAPER_RESEARCH_REPORT.json` | `84b67af769b13ff13eae823f07446de682334f03fff2b7643a6069f2b83f549f` | 3845 |
| `paper/PAPER_OPS_HEALTH.json` | `d4e557a3510047b127cd9ff1b714a63dd9030b115e0a25399fa27d26b4fab6ab` | 299 |

Manifest identity: schema `research-publication/v1`, id `pub-e65b6912e02a168d`, publisher `research-lab-v1`, published at `2026-10-30T04:30:00+00:00`. Each manifest hash matches the file bytes.

Health is `OK`. Runner status `SUCCEEDED` and cycle `PAPER_SIMULATED` agree between status and health. Both record `last_run_at` `2026-10-30T04:04:02.086108+00:00`. `last_bar_age_ms` is `242250` at that capture. `last_processed_open_time` `1793329200000` is `2026-10-30T03:00:00+00:00`, equal to `extension_end_utc`. Extension count is 211 in the status and the report. Symbol is `SOLUSDT`, timeframe `1h`. Snapshot `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3` matches the frozen snapshot in the status file. `live_orders` is false and `exchange_orders_sent` is 0 in all four data files. Effective mode is `LIVE_DISABLED`.

The existing reader returned `FRESH` for this directory, with 3 experiments, health `OK`, 3 strategies, and no exchange orders. A missing directory returned `MISSING`. A changed lab file with the original manifest returned `INCONSISTENT`. A deleted health file returned `MISSING`. No corrected candidate was created.

## 4. Five API endpoints

Local `ResearchPublicationTests` called all five routes. The real candidate was read by the same reader class, not by the VPS process. VPS still has an empty root, so an authenticated live read would be `MISSING`. No login was performed.

| Route | Contract | Local result |
|---|---|---|
| `GET /api/personal-intelligence/research/publication` | `availability`, `schemaVersion`, `publicationId`, `publishedAt`, `publisher`, `objects`, `readError` | Fixture suite HTTP 200. Candidate reader: `FRESH`, id `pub-e65b6912e02a168d`. |
| `GET /api/personal-intelligence/research/experiments` | `availability`, `experiments`, `readError` | HTTP 200. Candidate has 3 catalog ids. |
| `GET /api/personal-intelligence/research/experiments/{experimentId}` | Fresh row, or HTTP 404 `NOT_IN_PUBLICATION` | HTTP 200 for a published id. Unknown and traversal ids do not return file text. |
| `GET /api/personal-intelligence/research/paper/status` | `availability`, `status`, `readError` | HTTP 200. Candidate health `OK`, `live_orders=false`, orders `0`. |
| `GET /api/personal-intelligence/research/paper/report` | `availability`, `report`, `readError` | HTTP 200. Candidate has 3 strategies. `labResult` is `SEE_LAB_RESULT_ENDPOINT`. |

Anonymous is HTTP 401. A disabled module is HTTP 403. A hash mismatch hides experiment and report bodies and returns `INCONSISTENT`. A missing file returns `MISSING` with a null report.

## 5. Authorization and module filtering

`ResearchRead` allows `personal_intelligence.research.read` or platform role `ADMIN`. It does not accept `trading.run`, `trading.approve`, `exchange.read`, or `exchange.connect`. The agent JWT does not copy the database role onto the token, so the operator is not treated as `ADMIN`.

`sb-operator` already has `personal_intelligence.trading.run`, `personal_intelligence.trading.approve`, `personal_intelligence.research.read`, and `personal_intelligence.exchange.read`. Tenant `SECOND_BRAIN` has `personal_intelligence` in `enabled_modules`. `platform_module_registry` already contains that module code.

The module filter runs before the action. Research read does not require an exchange module and does not start a worker.

## 6. UI access

`ResearchPanel` is rendered on `/second-brain/overview`. `TradingLayout` shows trading tabs only when the caller is `ADMIN` or has `trading.run`, `exchange.connect`, or `exchange.read`. `research.read` alone gets the workspace and is redirected away from overview, so that caller does not see the panel. The permission was not broadened.

`sb-operator` has `trading.run` and `exchange.read`, so `canTrading` is true without an `ADMIN` role. That operator can open overview, where the panel calls the five research routes with `research.read`.

`research-view.smoke.ts` and `intent-workspace.smoke.ts` passed. The served UI at `/opt/second-brain-agent/ui` contains `personal-intelligence/research` and the panel copy `Đang đọc publication`. The UI directory is mode `777`, owned by `sbagent`. That mode was not changed.

## 7. Migration and schema prerequisites

No migration is required to read this publication, and none was run.

Research read uses the file root plus the existing tenant module flag and operator permission. It does not query the trading ledger. The database is already `second_brain_agent_v1` on port 5433, system identifier `7702079316921501630`.

These objects are already present and must not be reapplied for this release: `pack_personal_intelligence.person`, `trade`, `pi_events`, `pi_task`, `exchange_connection`, and `platform_module_registry`. They come from migrations 385 and 387–393, plus the later task migration 399. Manifest `deploy/ubuntu/migration-files.personal-intelligence.txt` is not part of the agent publication install. Migration 390 only inserts the registry row. The isolated host decides module enablement from `tenants.settings`, which is already enabled.

`001_auth_session.sql` is already applied. It was not edited.

## 8. Local test results

| Suite | Result |
|---|---|
| `ResearchPublicationTests` | 14 passed, 0 failed |
| `DatabaseGuardTests` | 24 passed, 0 failed |
| Existing reader on the candidate | `FRESH`, id `pub-e65b6912e02a168d` |
| Missing root | `MISSING` |
| Tampered lab file | `INCONSISTENT` |
| Deleted health file | `MISSING` |
| `research-view.smoke.ts` | passed |
| `intent-workspace.smoke.ts` | passed |

The test build wrote a Debug host under `src\Hosts\SecondBrainAgent\KitPlatform.SecondBrainAgent.Host\bin\Debug`. That output is not the release. The accepted package DLL was hashed again and is still `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`.

No exchange connection, paper execution, or intent that creates a task was run. `NU1903` for Newtonsoft.Json 10.0.3 is the existing warning.

## 9. Release package and checksums

Package: `E:\KitPlatform\deploy\second-brain-agent\research-publication-v1`

It contains the original manifest, the four data files, `PUBLICATION.md`, `READER.md`, `DEPLOYMENT.md`, `ROLLBACK.md`, and `SHA256SUMS.txt`. Nine files, checked with 0 mismatches. No secret is included. The session-security package was not modified.

Data checksums are the table in section 3. Document checksums:

| File | SHA-256 |
|---|---|
| `PUBLICATION.md` | `822558d2061a6d71a47a87ee0a9f039d2b0abe3cebdb59a39ee0a25cdb864398` |
| `READER.md` | `e669644822babdac46b044ba49e578790a7d1bbb50c7185316b215d14f9d552e` |
| `DEPLOYMENT.md` | `64e810d80ea1b89ece55ad848bca112030f494ef4a35ab84af605cb84567904e` |
| `ROLLBACK.md` | `86612e35ae4fa978cdd5f3e1a350a5ca1b9c74dff1872b0e16b2b5283831e27f` |

Configuration key: `PersonalIntelligence__ResearchPublication__Root`

Proposed root: `/opt/second-brain-research-publication-v1`

`deploy/second-brain-agent/publication-sample` is documentation and must not be used as the root.

## 10. VPS B read-only preflight

No directory was created, no file was uploaded, no environment line was edited, no service was restarted, and no migration ran. VPS A was not contacted.

| Check | Result |
|---|---|
| Service | `second-brain-agent` active, PID 68847, NRestarts 0, since 2026-10-30 12:33:46 +07 |
| Working directory | `/opt/second-brain-agent-v1-session-security/host` |
| Host DLL | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| Listen | `127.0.0.1:5095` |
| `/api/health` | HTTP 200, `workerEnabled=false`, `backfillWorkerRegistered=false` |
| Hosted services | DataProtection and GenericWebHost. ExchangeBackfill absent. |
| `/api/health/db` | HTTP 200, `second_brain_agent_v1` |
| Database | port 5433, system identifier `7702079316921501630` |
| Publication root | key present, value empty. No publication directory. |
| Model flag | absent |
| Forbidden env | port 5432, `novixa_prod`, and `103.200.23.229` absent |
| Module | `personal_intelligence` enabled for `SECOND_BRAIN` |
| Operator permissions | `trading.run`, `trading.approve`, `research.read`, `exchange.read` |
| Proposed path | `/opt/second-brain-research-publication-v1` does not exist. `/opt` is `755` root:root. `sbagent` exists. |

## 11. Risks and unresolved issues

`FRESH` means the health file says `OK`. It does not mean the market bar is still fresh when someone opens the page. The captured age is about four minutes at 04:04 UTC. The manifest was published at 04:30 UTC.

A caller who has only `research.read` can use the API and cannot see the panel, because overview is a trading tab. The current operator also has trading permissions, so that operator can open the panel. The permission set was left as it is.

The host process registers exchange connector types. They are not hosted services and the research reader does not call them. Worker and model stay off.

`/opt/second-brain-agent/ui` is mode `777`. This publication does not change it.

The five research routes were not called on VPS. The root is empty, so the meaningful live result after deployment is the check in the command sequence. Local tests already cover the contracts.

Nothing here authorizes a production change.

## 12. Exact deployment command sequence

Run this only after a separate approval. It was not run for this report. Work from the package directory on VPS B. Do not point the root at `publication-sample`.

```bash
set -euo pipefail
sha256sum -c SHA256SUMS.txt
test "$(sha256sum /opt/second-brain-agent-v1-session-security/host/KitPlatform.SecondBrainAgent.Host.dll | awk '{print $1}')" = "ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc"
install -d -o sbagent -g sbagent -m 755 /opt/second-brain-research-publication-v1/lab /opt/second-brain-research-publication-v1/paper
install -o sbagent -g sbagent -m 644 manifest.json /opt/second-brain-research-publication-v1/manifest.json
install -o sbagent -g sbagent -m 644 lab/LAB_RESULT.json /opt/second-brain-research-publication-v1/lab/LAB_RESULT.json
install -o sbagent -g sbagent -m 644 paper/PAPER_OPS_STATUS.json /opt/second-brain-research-publication-v1/paper/PAPER_OPS_STATUS.json
install -o sbagent -g sbagent -m 644 paper/PAPER_RESEARCH_REPORT.json /opt/second-brain-research-publication-v1/paper/PAPER_RESEARCH_REPORT.json
install -o sbagent -g sbagent -m 644 paper/PAPER_OPS_HEALTH.json /opt/second-brain-research-publication-v1/paper/PAPER_OPS_HEALTH.json
cp -a /etc/second-brain-agent/agent.env /var/backups/agent.env.before-research-publication-v1
python3 - <<'PY'
from pathlib import Path
path = Path("/etc/second-brain-agent/agent.env")
key = "PersonalIntelligence__ResearchPublication__Root="
value = key + "/opt/second-brain-research-publication-v1"
lines, seen = [], False
for line in path.read_text().splitlines():
    if line.startswith(key):
        lines.append(value)
        seen = True
    else:
        lines.append(line)
if not seen:
    lines.append(value)
path.write_text("\n".join(lines) + "\n")
PY
systemctl restart second-brain-agent
curl -fsS http://127.0.0.1:5095/api/health
curl -fsS http://127.0.0.1:5095/api/health/db
```

Then sign in as `sb-operator` with the password already stored for that host. Do not print the token. `GET /api/personal-intelligence/research/publication` must be HTTP 200, `FRESH`, id `pub-e65b6912e02a168d`. Repeat the other four GET routes. Confirm `workerEnabled` is false, ExchangeBackfill is absent, and trading row counts did not change.

## 13. Rollback procedure

```bash
set -euo pipefail
cp -a /var/backups/agent.env.before-research-publication-v1 /etc/second-brain-agent/agent.env
systemctl restart second-brain-agent
curl -fsS http://127.0.0.1:5095/api/health
```

After the service is healthy, an authenticated publication GET must return `MISSING`. The host DLL must still be `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`. Leave the session binary, `auth_session`, and refresh tokens alone. Deleting `/opt/second-brain-research-publication-v1` is optional and only after the root is empty again.
