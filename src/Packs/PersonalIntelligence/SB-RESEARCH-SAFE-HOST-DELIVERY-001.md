# SB-RESEARCH-SAFE-HOST-DELIVERY-001

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`

Candidate status: **SUPERSEDED**
Production status: **NOT READY FOR PRODUCTION**

Correction recorded by `SB-RESEARCH-SAFE-HOST-RELEASE-PREP-003`. Sections 6–8 below are the procedure an operator may follow after a separate approval. The original wording copied a dirty-tree artifact and could overwrite live configuration. Do not use `artifacts/sb-research-safe-host-001`. The pinned source is `6596cfb996b3b4698917cb6f72b809931a42cc84`. The pinned artifacts are under `artifacts/sb-research-safe-host-003/`. This procedure has not been executed.

The Research-only host is implemented. Owner decisions in section 9 are still open. No server files were changed.

## 1. Baseline and resulting Git status

Verified before edits.

| Item | Value |
|---|---|
| Root | `E:/KitPlatform-sb-research` |
| Branch | `admin-build-blocker-001` |
| Upstream | Not configured |
| HEAD | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| `origin/admin-build-blocker-001` | Same hash |
| `origin/main` | `608bc054c9b6389190c4c214ac478b3397b110dc` |

`96657e06` and `c57d8d7e` are already ancestors of HEAD. They were not cherry-picked again.

Content fix `96657e06` and admin fix `c57d8d7e` are build prerequisites already on this branch. They are not Research product behavior. The Content fix selects `narrative_json`. Migration 384 was not run.

Untracked reviews preserved: `SB-RESEARCH-OWNER-APPROVAL-REVIEW-001.md` and `SB-RESEARCH-RELEASE-SCOPE-REVIEW-002.md`.

HEAD is unchanged. The delivery is the uncommitted worktree plus local artifacts under `artifacts/sb-research-safe-host-001/`, which is gitignored.

## 2. Files changed and why

| File | Why |
|---|---|
| `PersonalIntelligencePackDependencyInjection.cs` | `AddPersonalIntelligenceResearch` registers only `FileResearchPublicationReader`. `AddPersonalIntelligencePack` remains for trading tests and still registers the worker. The API host does not call it. |
| `PersonalIntelligenceResearchHost.cs` | MVC convention removes every controller in the Personal Intelligence API assembly except `ResearchPublicationController`. |
| `Program.cs` | Host uses the research registration, publication options only, and the convention. It does not bind `ExchangeBackfill` and does not call `AddPersonalIntelligencePack`. |
| `ResearchReadPage.tsx` | Route screen. Renders `ResearchPanel` only. Does not call `tradingApi`. |
| `router.tsx` | Adds `/second-brain/research` outside `TradingLayout`. |
| `registry.tsx` | Module nav opens `/second-brain/research`. |
| `TradingPages.tsx` | Removes `ResearchPanel` from the trading overview so that page is not required to show Research. Trading summary behavior is otherwise unchanged. |
| `trading-routes.smoke.ts` | Asserts the research route is separate and the overview no longer embeds the panel. |
| `ResearchPublicationTests.cs` | Host test for five GETs, denial, hidden trading routes, and absent worker registration. |

## 3. API and UI boundary

`Program.cs` maps the Personal Intelligence API assembly and then `ResearchOnlyControllerConvention` drops trading and exchange controllers. The five research GETs are unchanged:

- `GET /api/personal-intelligence/research/publication`
- `GET /api/personal-intelligence/research/experiments`
- `GET /api/personal-intelligence/research/experiments/{experimentId}`
- `GET /api/personal-intelligence/research/paper/status`
- `GET /api/personal-intelligence/research/paper/report`

Policy remains `PersonalIntelligenceResearchRead`: ADMIN, `personal_intelligence.research.read`, or `personal_intelligence.trading.run`. The module filter still calls `IsModuleEnabledAsync`. No permission was seeded or granted.

`/second-brain/research` admits ADMIN, `research.read`, or `trading.run`. It does not admit exchange permissions. A user who can open the trading layout but cannot read Research is sent to `/second-brain/overview` and does not receive research data. The UI check does not replace the API policy or the module gate.

The reader still returns `MISSING` when the configured root is empty or the directory is absent. It does not query `pack_personal_intelligence`.

The published infrastructure assembly still contains trading types because it is one project. This host does not register `ExchangeBackfillWorker`, exchange connectors, or the credential key ring. Trading test hosts that call `AddPersonalIntelligencePack` are unchanged and are not the deployment host.

## 4. Artifacts

Built from this worktree. Not deployed.

| Artifact | Path | SHA-256 |
|---|---|---|
| API host | `artifacts/sb-research-safe-host-001/api/KitPlatform.Api.dll` | `11B04205E9E8F88DFA44107043F0E0FF5992595C1701FF33E1B70C16D70D41F9` |
| Admin SPA | `artifacts/sb-research-safe-host-001/admin/index.html` | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| Publisher | `tools/research-publication/publish_research_v1.py` | `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361` |

API publish command: `dotnet publish src/KitPlatform.Api/KitPlatform.Api.csproj -c Release -o artifacts/sb-research-safe-host-001/api`. Exit 0.

Admin source build: `npm run build` in `client/admin` (`tsc --noEmit && vite build`). Exit 0. Vite built in 36.95s. The dist was copied to `artifacts/sb-research-safe-host-001/admin/`.

Published `appsettings*.json` files contain no `PersonalIntelligence` section and no `Kit-Brain` or `E:\` path. Publication root must be supplied in the server environment file, not in Git.

Included artifacts are the API publish directory and the admin SPA. Customer app, POS, survey, prescriber, partner, and family SPAs were not rebuilt.

## 5. Test evidence

| Check | Command | Result |
|---|---|---|
| Publisher tests | `python -m unittest tests.test_publish_research_v1` in `tools/research-publication` | PASS. 7 tests, exit 0 |
| Research API and host isolation | `dotnet test tests/KitPlatform.PersonalIntelligence.Tests/KitPlatform.PersonalIntelligence.Tests.csproj --filter FullyQualifiedName~ResearchPublicationTests` | PASS. 15 passed, 0 failed, exit 0. The new fact checks five MISSING GETs, 403 without a research permission, 404 for trading summary, exchange catalog, and trading loops, and no `ExchangeBackfillWorker`, `IExchangeBackfill`, or `IExchangeCredentialKeyRing` |
| API build | `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj -c Release` | PASS. 0 errors, 62 warnings, exit 0. Warnings include existing NU1903 Newtonsoft.Json 10.0.3 |
| Research view smoke | `npx tsx src/modules/second-brain/research-view.smoke.ts` | PASS. `research view ok`, exit 0 |
| Route smoke | `npx tsx src/modules/second-brain/trading-routes.smoke.ts` | PASS. `trading routes ok`, exit 0 |
| Admin production build | `npm run build` in `client/admin` | PASS. Exit 0 |
| PostgreSQL trading integration | Not run | NOT RUN. The research reader has no trading table dependency. `KIT_PI_TEST_CONNECTION` was not set. No database was opened |
| Lab/Paper Ops suite outside Git | Not run | NOT RUN |
| Browser login against a running server | Not run | NOT RUN. No server was started and no credentials were used |

`research.read` without `trading.run` is covered by the existing fact `Trading_run_and_research_read_and_admin_can_read` in the same 15-test run. Anonymous requests remain 401. Module-off remains 403. Those facts were executed in this run, not copied from an older report.

## 6. Deployment runbook

This describes the existing shared KitPlatform topology. It was not executed. It is not a second service and it is not `scripts/deploy-update-vps.ps1` or `scripts/deploy-production.ps1`. Those scripts copy the whole API tree, can copy other SPAs with `--delete`, can run migrations, and restart `kit-platform-api`. Do not run them for this release.

The deployment unit is the one API process in `deploy/ubuntu/kit-platform-api.service` plus the one admin tree served for `admin.novixa.vn`. Customer, POS, survey, prescriber, partner, and family trees are out of scope and must not be copied.

Pinned input:

| Item | Value |
|---|---|
| Commit | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Parent | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| API DLL | `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` |
| API SHA-256 | `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| Admin tree | `artifacts/sb-research-safe-host-003/admin/` |
| Admin `index.html` SHA-256 | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| Admin file manifest | `artifacts/sb-research-safe-host-003/admin-SHA256SUMS.txt` (312 files, manifest SHA-256 `0174F8E9EDB9B8C22C5D0F343D3D5127C342D9E753464A27F5AB9C945586514B`) |

`KitPlatform.Api.dll` from this publish contains `ResearchOnlyControllerConvention` and `AddPersonalIntelligenceResearch`. It does not contain `ExchangeBackfillWorker`. The infrastructure DLL still contains that type. The earlier DLL `11B04205E9E8F88DFA44107043F0E0FF5992595C1701FF33E1B70C16D70D41F9` is not this release.

A second `dotnet publish -c Release` on this machine, SDK `10.0.301`, commit `6596cfb9`, wrote `artifacts/sb-research-safe-host-004-repro/api/KitPlatform.Api.dll` with the same SHA-256. A second `npm run build` reproduced all 312 admin files in the manifest. That is a repeat on this machine, not a clean clone on another computer. The deploy input remains `artifacts/sb-research-safe-host-003`.

### 6.1 Preflight

Stop before copying anything if any of these is true:

- The commit, DLL hash, or admin manifest hash differs from this section.
- The publication path, if one is being set, is inside a `root` in `deploy/ubuntu/nginx-kit-platform.conf`.
- `pack_content.brand.narrative_json` has not been confirmed with the read-only catalog query. Do not run migration 384 to find out.
- The backup directory has not been named, or it is inside `/var/www/kit-platform`.
- The operator intends a successful authorized Research read while section 9 is still open.
- The plan includes a migration, a permission change, or a restart. Those require their own approvals and are not part of the file copy.

Do not print `/etc/kit-platform/api.env`, `secrets.generated`, or connection strings into a shell log, ticket, or this report.

### 6.2 Backup

On the server, copy these to a timestamped directory outside the web root:

- `/var/www/kit-platform/api`, including `uploads/`
- `/var/www/kit-platform/admin`
- `/etc/kit-platform/api.env`, with `cp -a`

Record SHA-256 of the live `KitPlatform.Api.dll` and `admin/index.html`, and the byte size of `api.env`. Do not record the file contents. Confirm the backup DLL hash matches the live DLL hash. If it does not, stop.

If no publication directory exists, record that fact and do not create one. If one exists, copy that tree to the same backup area. Do not use `.previous`.

### 6.3 Copy

API, without `--delete`:

```bash
rsync -a --exclude 'appsettings*.json' --exclude 'uploads/' --exclude 'api.env' \
  <pinned-api-dir>/ /var/www/kit-platform/api/
```

Admin, with `--delete`, only after the admin backup hash matched:

```bash
rsync -a --delete <pinned-admin-dir>/ /var/www/kit-platform/admin/
```

Do not copy `admin-SHA256SUMS.txt` into the web root. Do not rsync a publication tree into the admin root.

Files the copy may change:

- Any file under `/var/www/kit-platform/api` that is present in the pinned API directory, except `appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json`.
- Files under `/var/www/kit-platform/admin`. The admin copy uses `--delete` because the SPA filenames are content-hashed. The backup is the only rollback for files that `--delete` removes.

Files and trees the copy must not change:

- `/var/www/kit-platform/api/uploads/`
- `/etc/kit-platform/api.env` and `/etc/kit-platform/secrets.generated`
- `customer-app`, `staff-app`, `assessment-web`, `prescriber-portal`, `partner-portal`, and `family-app` under `/var/www/kit-platform`
- nginx site files
- the publication directory
- the database, roles, and tenant settings

The publication-root key, if approved later, is the only intended edit to `api.env`. It is not part of the rsync.

Leave `api.env` unchanged in this step. Adding `PersonalIntelligence__ResearchPublication__Root` is a separate owner edit, done with an editor on the server after the `cp -a` backup, without printing the file. Do not add exchange credential variables.

### 6.4 Verify the copy, then stop

Before any restart:

- The live DLL hash equals `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8`.
- The live `index.html` hash equals `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D`.
- `appsettings*.json` and `uploads/` on the server were not replaced. Compare timestamps or hashes with the backup, and do not print file contents.
- `api.env` byte size matches the backup unless the separate environment approval already changed it.
- `systemctl is-active kit-platform-api` is unchanged from the preflight note.

If any check fails, restore from the backup and do not restart.

### 6.5 Restart

Restart only under a later approval that names commit `6596cfb9`, both hashes above, and the window. The command is `systemctl restart kit-platform-api`. This mission does not run it.

Topology that the restart affects:

- User `www-data`, working directory `/var/www/kit-platform/api`, environment file `/etc/kit-platform/api.env`, logs `journalctl -u kit-platform-api`.
- ASP.NET Core runtime `Microsoft.AspNetCore.App 10`, as installed by `deploy/ubuntu/bootstrap-vps.sh`.
- The same process continues to serve Pharmacy, Content, and the other packs already registered in `Program.cs`. Trading and exchange controllers from the Personal Intelligence assembly are not mapped. The admin bundle still contains those screens; their API calls return 404.

## 7. Rollback

Use this only inside an approval that covers the rollback window.

1. `systemctl stop kit-platform-api`.
2. Replace `/var/www/kit-platform/api`, `/var/www/kit-platform/admin`, and `/etc/kit-platform/api.env` with the timestamped backup. Do not copy the new artifact back over the restore.
3. If a publication tree was backed up, point the restored environment at that copy, or leave the restored `api.env` as it was. A cleared publication root returns `MISSING` and does not delete files.
4. `systemctl start kit-platform-api`.
5. Confirm the live DLL and `index.html` hashes equal the pre-change record, and `GET /api/health` is 200.
6. Do not drop schemas, run migrations backward, or delete the backup.

If the previous build had no Research route, an anonymous Research request may return 404 again. That is the previous state. If the backup hashes were not recorded, stop and do not guess which files were live.

## 8. Post-restart checks

Run these only after the approved restart:

- `GET /api/health` is 200.
- Anonymous `GET /api/personal-intelligence/research/publication` is 401.
- `GET /api/personal-intelligence/trading/summary` is 404.
- `https://admin.novixa.vn/` returns HTML.
- `journalctl -u kit-platform-api` shows no startup exception from `ValidateProductionConfiguration`. Do not copy environment values out of the log.
- A brand read is allowed as a check only after the owner has confirmed `narrative_json`. `/api/health` does not prove that column.
- An authorized Research call is 200 only when section 9 has been decided and the module is enabled. `MISSING` means the root is empty or incomplete. `FRESH` requires source health `OK`. A 403 before the permission exists is the current policy.

Stop and roll back if health is not 200, the Research route is not 401 when anonymous, or the trading summary is not 404.

### 8.1 Configuration and publication contract

Set keys in `/etc/kit-platform/api.env` only during the separate environment approval. Do not put them in appsettings and do not commit values. Do not print the file.

| Key | Purpose | Safe provisioning |
|---|---|---|
| `PersonalIntelligence__ResearchPublication__Root` | Directory the reader loads. Empty or missing directory returns `MISSING`. | Owner creates the directory outside every nginx `root`. Grant `www-data` read and execute on the directory only. |
| Existing `ConnectionStrings__Default`, `Jwt__Secret`, CORS, SMS | Already required by `ValidateProductionConfiguration` and the current service. | Leave the current production values in place. This mission did not read them. |
| `PersonalIntelligence__ExchangeCredentialKey` and `PersonalIntelligence__ExchangeCredentialKeyId` | Exchange secret wrapping. | Do not set them for this release. The research host does not resolve them. |

Publication files inside the root:

- `manifest.json`
- `lab/LAB_RESULT.json`
- `paper/PAPER_OPS_STATUS.json`
- `paper/PAPER_RESEARCH_REPORT.json`
- `paper/PAPER_OPS_HEALTH.json`

Schema `research-publication/v1`. Publish with `tools/research-publication/publish_research_v1.py` only after the owner has a separate copy of the previous tree. The script deletes `.previous` after success. The API process must not have write access to that directory.

The original copy procedure that followed this contract is withdrawn. It rsynced `artifacts/sb-research-safe-host-001` without excluding appsettings, treated post-restart checks as reasons not to restart, and restored the API and admin directories without restoring `api.env`.

## 9. Owner decisions still pending

- Apply migration 390 alone or not. Do not apply the eight-file Personal Intelligence manifest for this release.
- Which tenant receives Research Read, and the `allowed_modules` and `enabled_modules` values.
- Which role receives `personal_intelligence.research.read`.
- Whether ADMIN stays in the Research policy. `trading.run` still satisfies that policy and must not be granted as a substitute.
- Confirm the read-only role does not also have `personal_intelligence.trading.run`, `trading.approve`, `exchange.read`, or `exchange.connect`.
- Read-only catalog confirmation that `pack_content.brand.narrative_json` exists on the target Content database.
- Name the publication-root owner and the backup directory.
- Separate approval before the service restart.

## 10. Gate table

These labels are the original delivery record. The re-run against commit `6596cfb9` is in `SB-RESEARCH-SAFE-HOST-RELEASE-PREP-003.md`.

| Gate | Status |
|---|---|
| Research-only registration and controller isolation | PASS |
| Five research GETs and MISSING contract | PASS |
| Trading controllers and worker not registered on this host | PASS |
| `research.read` without `trading.run` | PASS |
| Missing permission denied | PASS |
| Module gate still present | PASS |
| Publisher tests | PASS |
| API build and publish | PASS |
| Admin build | PASS |
| UI smokes | PASS |
| No local publication path in shared appsettings | PASS |
| Deployment runbook and rollback steps | PASS as documentation. Server execution NOT RUN |
| PostgreSQL trading integration | NOT RUN. Not a gate for this reader |
| Migration 390, entitlement, permission, Content column, production restart | BLOCKED. Owner input required |
| Production readiness | NOT READY FOR PRODUCTION |

## 11. What was not done

No production deploy, service restart, migration, database connection, permission change, entitlement change, credential read, or credential write. No commit, push, merge, or history rewrite. `E:\KitPlatform` was not modified. Prior review files were not edited.
