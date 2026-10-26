# SB-RESEARCH-SAFE-HOST-RELEASE-REVIEW-002

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Review type: read-only. No source, test, configuration, or runbook was edited. No build, publish, test, migration, or deploy was run.

Executive conclusion: **BLOCKED**

The worktree cannot yet be treated as a reproducible release candidate. Research-only isolation is present in uncommitted source, and the existing artifact bytes contain the new host type names, but those bytes are not tied to a committed revision. The delivery runbook is not safe to execute as written. Owner data decisions remain open. This is not a production approval.

## 1. Answers

1. The current artifacts are not traceable to a fixed source revision. HEAD is still `3d5d7b42`. The host changes are uncommitted. The artifact directory is gitignored.
2. Isolation is visible in source and is consistent with strings inside the published DLLs. The implementer reported `ResearchPublicationTests` 15 passed. This review did not re-run that test. Browser and server runtime remain unverified.
3. A user whose token contains only `personal_intelligence.research.read` is admitted by both the Research API policy and `ResearchReadPage`, without `personal_intelligence.trading.run`. No SQL seeds that permission, so a real account cannot receive the claim until a separate data change. That login was not exercised.
4. Unresolved prerequisites are migration 390, tenant `allowed_modules` and `enabled_modules`, the permission row and role, the publication directory and its owner, a read-only confirmation of `pack_content.brand.narrative_json`, and a separate restart approval. The research reader itself has no database dependency.
5. The runbook names the right topology and a backup idea, and it was not executed. It is not complete or safe: copy steps can overwrite live configuration, pre-restart and post-restart checks are mixed, and rollback does not restore the environment file.
6. Before a production-deployment approval can be considered, the isolation must be committed, artifacts must be rebuilt from that commit, the checks below must be re-run and recorded, and the runbook must be corrected. Owner decisions still have to be made in a later approval. A green build does not grant that approval.

## 2. Git provenance

Verified in this review.

| Item | Value |
|---|---|
| Root | `E:/KitPlatform-sb-research` |
| Branch | `admin-build-blocker-001` |
| Upstream | Not configured |
| HEAD | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| Remote | `https://github.com/khiemtic-rgb/KitPlatform.git` |
| `git ls-remote` `admin-build-blocker-001` | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| `git ls-remote` `main` | `608bc054c9b6389190c4c214ac478b3397b110dc` |
| `96657e06` ancestor of HEAD | Yes |
| `c57d8d7e` ancestor of HEAD | Yes |

Worktree is dirty. HEAD was not changed by the delivery.

Modified:

- `client/admin/src/app/router.tsx`
- `client/admin/src/modules/registry.tsx`
- `client/admin/src/modules/second-brain/TradingPages.tsx`
- `client/admin/src/modules/second-brain/trading-routes.smoke.ts`
- `src/KitPlatform.Api/Program.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Infrastructure/PersonalIntelligencePackDependencyInjection.cs`
- `tests/KitPlatform.PersonalIntelligence.Tests/ResearchPublicationTests.cs`

Untracked:

- `client/admin/src/modules/second-brain/ResearchReadPage.tsx`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Api/PersonalIntelligenceResearchHost.cs`
- `src/Packs/PersonalIntelligence/SB-RESEARCH-OWNER-APPROVAL-REVIEW-001.md`
- `src/Packs/PersonalIntelligence/SB-RESEARCH-RELEASE-SCOPE-REVIEW-002.md`
- `src/Packs/PersonalIntelligence/SB-RESEARCH-SAFE-HOST-DELIVERY-001.md`

`git diff --stat` for the seven modified files: 124 insertions, 12 deletions. This review adds only this file.

`E:\KitPlatform` was not opened or modified.

## 3. Artifact provenance

Hashes recomputed in this review. They match `SB-RESEARCH-SAFE-HOST-DELIVERY-001.md`.

| File | SHA-256 | UTC write time |
|---|---|---|
| `artifacts/sb-research-safe-host-001/api/KitPlatform.Api.dll` | `11B04205E9E8F88DFA44107043F0E0FF5992595C1701FF33E1B70C16D70D41F9` | 2026-10-22T08:57:38Z |
| `artifacts/sb-research-safe-host-001/admin/index.html` | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` | 2026-10-22T08:59:18Z |
| `tools/research-publication/publish_research_v1.py` | `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361` | committed file; hash matches the publisher README |

The host sources that feed the API and admin were last written before those two artifacts. That order is consistent with the delivery report. It does not prove the bytes were produced from this exact tree, because the tree was never committed and this review did not rebuild.

UTF-8 strings inside the published DLLs, read without disassembly:

- `KitPlatform.Api.dll` contains `ResearchOnlyControllerConvention` and `AddPersonalIntelligenceResearch`. It does not contain the text `ExchangeBackfillWorker`.
- `KitPlatform.Packs.PersonalIntelligence.Api.dll` contains `ResearchOnlyControllerConvention`.
- `KitPlatform.Packs.PersonalIntelligence.Infrastructure.dll` contains `AddPersonalIntelligenceResearch`, `FileResearchPublicationReader`, and `ExchangeBackfillWorker`.

The worker type therefore still ships inside the infrastructure assembly. The API entry assembly names the research registration and the controller convention. Publish output also contains `appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json`. Their contents were not printed.

`index.html` identifies one admin file, not the whole `admin/` tree.

### Minimum procedure for a later fixed commit

Do this only after the isolation diff is committed, and from a clean checkout of that commit. Do not point the output at an existing artifact directory.

```
dotnet test tests/KitPlatform.PersonalIntelligence.Tests/KitPlatform.PersonalIntelligence.Tests.csproj --filter FullyQualifiedName~ResearchPublicationTests
dotnet publish src/KitPlatform.Api/KitPlatform.Api.csproj -c Release -o <new-empty-api-dir>
npm run build
```

Run the admin command in `client/admin`. Copy `client/admin/dist` to a new empty directory. From `tools/research-publication`, run `python -m unittest tests.test_publish_research_v1`.

Record the commit SHA, both commands' exit codes, SHA-256 of `KitPlatform.Api.dll`, SHA-256 of every file under the admin output, and the publisher hash. Confirm `KitPlatform.Api.dll` contains `ResearchOnlyControllerConvention` and does not contain `ExchangeBackfillWorker`. Do not treat the current `artifacts/sb-research-safe-host-001` directory as that record.

## 4. Isolation dependency map

Source inspected in this review.

| Piece | What the research host uses | What remains elsewhere |
|---|---|---|
| `Program.cs` | `AddApplicationPart` of the Personal Intelligence API assembly, `ResearchOnlyControllerConvention`, `AddPersonalIntelligenceResearch`, bind `PersonalIntelligence:ResearchPublication` only | Pharmacy, Content, Family, Local OS, Kit Sales, and `KitSalesJourneyWorker` stay registered. This is still the shared API process, not a second service. |
| `AddPersonalIntelligenceResearch` | `IResearchPublicationReader` to `FileResearchPublicationReader` | `AddPersonalIntelligencePack` still registers trading repositories, exchange connectors, the credential key ring, and `AddHostedService<ExchangeBackfillWorker>`. `Program.cs` does not call it. |
| `ResearchOnlyControllerConvention` | Keeps `ResearchPublicationController` | Removes `TradingDeskController`, `TradingExchangeController`, and `TradingLoopController`. Those are the only other controllers in that assembly. |
| Reader | Files under the configured root. Empty or missing root returns `MISSING` | No reference to `pack_personal_intelligence` on this path |
| `ResearchReadPage` | `ResearchPanel`, which calls `researchApi` | No `tradingApi` import. Trading overview no longer renders `ResearchPanel` |
| Route | `/second-brain/research` is a sibling outside `TradingLayout` | Trading routes remain behind `TradingLayout` |

The older `ResearchHost` in `ResearchPublicationTests` still maps the whole controller assembly and registers `ITradingDesk`. Isolation evidence is the separate `ResearchOnlyHost` fact, not those older facts.

Not verified here: a running `KitPlatform.Api` process, JWT login, or a browser. The delivery report's test run is implementer evidence, not a result of this review.

Deploying this host turns trading and exchange HTTP routes off for every tenant on `kit-platform-api`, including ADMIN and `trading.run`. The admin bundle still contains those screens. `ResearchReadPage` still links `trading.run` and ADMIN users to `/second-brain/session`.

## 5. Authorization matrix

API policy `PersonalIntelligenceResearchRead` requires an authenticated user and one of: role `ADMIN`, permission `personal_intelligence.research.read`, or permission `personal_intelligence.trading.run`. `PersonalIntelligenceModuleFilter` then requires `enabled_modules` to contain `personal_intelligence`. The filter has no ADMIN exception. `IsModuleEnabledAsync` reads that list only.

UI `ResearchReadPage` uses the same three identities. It does not treat exchange permissions as research access. When the platform store has loaded and the module is off, it shows a warning and does not render `ResearchPanel`. `TradingLayout` still admits ADMIN, `trading.run`, `exchange.connect`, and `exchange.read`, and does not admit `research.read`.

| Subject | API research GET | UI `/second-brain/research` | Trading layout | Notes |
|---|---|---|---|---|
| Only `personal_intelligence.research.read` | Allowed when the module is enabled | Renders `ResearchPanel`. No trading link | Denied by `TradingLayout` | Source contract matches. Permission is not seeded. Runtime login not verified |
| `personal_intelligence.trading.run` | Allowed when the module is enabled | Renders `ResearchPanel` and a link to the trading session | Admitted | Also a trading privilege. On this host the trading API is removed, so the linked screen has no API |
| ADMIN | Allowed when the module is enabled | Same as `trading.run` | Admitted | Module filter still applies |
| Only `exchange.read` or `exchange.connect` | Denied by the research policy | Redirects to `/second-brain/overview` and does not render `ResearchPanel` | Admitted | No research data. Trading API is also absent on this host |
| Module disabled | 403 from the filter for every identity | Warning, no panel, once the platform store has loaded | Same warning | If the store has not loaded, the page can render the panel; the API still returns 403. Filter test uses the full controller host and a `research.read` client, not ADMIN and not `ResearchOnlyHost` |

Anonymous research GET is 401 in the older full-host fact. `ResearchOnlyHost` checks an authenticated caller with no permission and expects 403. It does not itself assert 401.

No permission, role, or tenant setting was changed.

## 6. Runtime and data prerequisites

Migration 390 only inserts `personal_intelligence` into `platform_module_registry` when the code is absent. It does not enable a tenant and does not seed `research.read`. The eight-file manifest `deploy/ubuntu/migration-files.personal-intelligence.txt` also creates trading and exchange schema. The delivery decision not to apply that manifest is still correct. Applying 390 alone remains an owner decision. The runtime module check does not read the registry; entitlement updates do.

`personal_intelligence.research.read` appears in no file under `migrations/`.

`ContentRepository` selects `pack_content.brand.narrative_json`. The shared API will not serve brand reads correctly if that column is absent. The research reader does not select it. This review did not query a database and does not claim the column exists.

Read-only check for the database owner, on the Content database they name, in a session that cannot write:

```sql
SELECT table_schema, table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'pack_content'
  AND table_name = 'brand'
  AND column_name = 'narrative_json';
```

One row means the column is present. Zero rows means it is absent. Do not run `migrations/384_pack_content_brand_narrative_dna.sql` to discover this. That file also updates brand narratives.

Publication root: `PersonalIntelligence__ResearchPublication__Root` in `/etc/kit-platform/api.env`. The directory must sit outside every `root` in `deploy/ubuntu/nginx-kit-platform.conf` (`/var/www/kit-platform/admin`, `customer-app`, `staff-app`, `assessment-web`, `prescriber-portal`, `partner-portal`, `family-app`). The service user is `www-data` and needs directory traversal and read only. The contract is `manifest.json`, `lab/LAB_RESULT.json`, `paper/PAPER_OPS_STATUS.json`, `paper/PAPER_RESEARCH_REPORT.json`, and `paper/PAPER_OPS_HEALTH.json`. The owner and the actual path are not named. The publisher deletes `.previous` after success, so a separate copy is the rollback copy.

This host does not bind `PersonalIntelligence:ExchangeBackfill` and does not register the worker or credential key ring. Setting `WorkerEnabled` has no effect on this host. Exchange credential variables are not required and should not be added. The old approval note that the worker defaults to on applies to `AddPersonalIntelligencePack`, which this host does not call.

Backups required before any later approved copy: the live API directory, the live admin directory, the current `api.env`, and the publication tree if one already exists. Record hashes of the live `KitPlatform.Api.dll` and the live admin tree before replacing them.

## 7. Runbook gaps

`SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` correctly says it was not executed, names `kit-platform-api`, `www-data`, `/var/www/kit-platform/api`, `/etc/kit-platform/api.env`, `journalctl -u kit-platform-api`, and the admin root. The following steps are ambiguous or unsafe. This review did not perform them.

| Gap | Why it matters |
|---|---|
| Artifact is a dirty-tree build | The runbook tells the operator to copy `artifacts/sb-research-safe-host-001` and compare the DLL hash with this uncommitted delivery. That hash is not a release identity. |
| `rsync` of the whole API directory | Publish output includes three appsettings files. The command does not exclude them, `api.env`, or other live-only files, and it does not say whether `--delete` is used. Either choice can remove or replace live configuration. |
| Shared process | The copy replaces the platform API and the whole admin SPA, including Pharmacy, Content, and Famixa surfaces. The runbook reads as a research-only file drop. Trading and exchange routes become unavailable. |
| STOP list order | Anonymous 401, trading 404, and `/api/health` 200 can be observed only after the new process is running. The same section says not to restart when they fail. An operator can block on the current process or restart before the backup is proven. |
| Hash check | It compares the built DLL with the report, not the file after it lands in `/var/www/kit-platform/api`. Admin verification is `index.html` only. |
| First publication | Step 2 always copies "the current publication directory" but the root may not exist yet. There is no branch for that case. |
| Environment rollback | Step 4 edits `api.env`. Rollback restores API and admin directories only. It does not restore the previous environment file or the previous publication-root value. |
| Rollback wording | "Stop only if a production approval covers the rollback window" does not say to stop the service before replacing files, and it does not say to leave the service running. |
| Secret handling | The runbook says not to commit values. It does not say that reading or backing up `api.env` must stay on the server and must not be pasted into a report, shell log, or ticket. |
| Content failure mode | Post-deploy checks do not include a brand read, which is the call that selects `narrative_json`. `/api/health` does not prove that column. |
| Missing publication | An authorized 200 with `MISSING` is a valid empty root. The runbook does not distinguish that from a wrong path. |

Health, journald, and the five research routes are the right checks once the copy and restart are split into approved steps. They are not sufficient while the gaps above remain.

## 8. Evidence matrix

| Check | This review | Delivery report |
|---|---|---|
| HEAD, branch, remote tips | PASS | Matches, rechecked |
| Artifact hashes on disk | PASS | Matches |
| Artifacts tied to a committed revision | BLOCKED | States uncommitted; still marks the candidate ready for deployment review |
| DLL contains research host names and not the worker name in `KitPlatform.Api.dll` | PASS | Not separately recorded |
| Worker type absent from the infrastructure DLL | Not the case. The type is present and unregistered by this host | Stated |
| Five research routes, MISSING, hidden trading routes, no worker registration | NOT VERIFIED. Source and `ResearchOnlyHost` were read. Test not run | REPORTED. 15 passed, exit 0 |
| `research.read` without `trading.run` on the API | Source PASS. Execution NOT VERIFIED | REPORTED inside the same 15 |
| Anonymous 401 and module-off 403 | Source PASS. Execution NOT VERIFIED. Those facts use the full controller test host | REPORTED |
| Publisher unittest | NOT RUN | REPORTED. 7 passed, exit 0 |
| Publisher file hash | PASS | Matches |
| API and admin builds | NOT RUN | REPORTED. Exit 0 |
| UI smokes | NOT RUN. Source of the route and page was read. Smokes are text checks, not a browser | REPORTED |
| Browser route and login | NOT VERIFIED | NOT RUN |
| PostgreSQL trading integration | NOT RUN. Not required for the file reader | NOT RUN |
| `narrative_json` on a target database | NOT RUN. No database was opened | BLOCKED |
| Production config, deploy, restart | NOT RUN | NOT RUN |

## 9. Owner decisions and approval gates

Still open, and not decided by this review:

- Whether to apply migration 390 by itself, on a named database, outside the eight-file manifest.
- Which tenant, and the exact `allowed_modules` and `enabled_modules` values.
- Which role receives `personal_intelligence.research.read`, and the separate insert of that permission row.
- Whether ADMIN and `personal_intelligence.trading.run` remain inside the research policy.
- Confirmation that the read-only role does not also receive `personal_intelligence.trading.run`, `personal_intelligence.trading.approve`, `personal_intelligence.exchange.read`, or `personal_intelligence.exchange.connect`.
- The read-only catalog result for `narrative_json`.
- Named owner, path, and backup location for the publication tree.
- A later approval that names the commit, the artifact hashes, and the restart window.

Until those exist, production deployment cannot be considered. This document does not grant it.

## 10. Next mission

`SB-RESEARCH-SAFE-HOST-PIN-003`

Commit only the isolation diff, rebuild from that commit, and correct the runbook. Do not deploy, migrate, change permissions, or restart a service.

Acceptance criteria:

- One new commit on top of `3d5d7b42` contains only the nine isolation and test files. Review markdown stays untracked. No push unless a later request asks for it.
- A clean checkout of that commit produces a new API directory and a new admin directory. Record the commit SHA, command exit codes, DLL hash, and a hash for every admin output file.
- `ResearchPublicationTests`, the publisher unittest, and both UI smoke commands are run on that commit and their exit codes are stored in a new report.
- The runbook no longer copies appsettings or `api.env` over the live host, states the shared-process blast radius, separates backup from restart, verifies hashes after the copy and before restart, and restores `api.env` on rollback.
- The report still says not ready for production while section 9 is open.
