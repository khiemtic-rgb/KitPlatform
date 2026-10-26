# SB-RESEARCH-SAFE-HOST-RELEASE-PREP-003

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`

Executive decision: **BLOCKED**

The Research host now has a fixed commit and a new build whose hashes were computed in this mission. It is still not a production release. Owner decisions, a browser or server login, and the `narrative_json` catalog check remain open. No production system was changed.

## 1. Why the earlier gates were not one proof

`docs/novixa/05-operations/deployment-model-v1.md` section 4.4 lists upload, restart, optional migration, and a smoke test. It marks the detailed runbook NVX-OPS-02 as Planned. Nothing in that document, in `docs/novixa-deploy.md`, or in `.github/workflows/admin-typecheck.yml` and `pharmacore-ci.yml` requires a clean commit, a hash tied to that commit, or a re-run of `ResearchPublicationTests` before an artifact is called ready.

The delivery mission therefore built from a dirty tree and recorded its own test output. The following review was read-only, so it could not turn that output into a new run. The pack note in `docs/novixa/03-solution/platform-kernel-and-solution-packs-v1.md` says a new pack adds a route inside the same monolith. It does not define a research-only registration profile. `scripts/deploy-update-vps.ps1` copies the whole API directory, including appsettings, and can restart the service in the same script. The first runbook followed that shape.

This mission commits the host, re-runs the checks, and replaces the operative runbook. It does not add a CI workflow or edit the Novixa authority documents. Those remain owner decisions. The proposed checklist is in section 10.

## 2. Git

| Item | Value |
|---|---|
| Base | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| New commit | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Parent confirmed | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| Upstream | Not configured |
| Remote | `https://github.com/khiemtic-rgb/KitPlatform.git` |
| Push | Not done |

The commit contains only the nine isolation and test files. Review markdown stays untracked. After the build, `git status` showed no tracked modifications. Untracked files are the four earlier reviews, the corrected delivery runbook, and this report.

## 3. Architecture conformance

Authority used: `deployment-model-v1.md` (NVX-OPS-01), `docs/novixa-deploy.md`, `platform-kernel-and-solution-packs-v1.md` (KIT-PLT-01), `deploy/ubuntu/kit-platform-api.service`, and `scripts/deploy-update-vps.ps1`. No replacement architecture was written.

| Contract | Evidence | Result |
|---|---|---|
| One API process and one admin SPA | NVX-OPS-01 Model B. `Program.cs` still builds `KitPlatform.Api` and registers the existing packs. | CONFORMANT. Research is a registration mode, not a second host. |
| Pack behavior checks `ITenantPlatformSettings` | `RequirePersonalIntelligenceModule` and `ResearchReadPage` both use `personal_intelligence`. The filter has no ADMIN bypass. | CONFORMANT |
| New pack is a registry row, tenant flags, and an additive migration | Migration 390 is registry-only and was not applied. The reader has no table. | OWNER_DECISION_REQUIRED for 390 and the tenant flags. The code does not apply the eight-file trading manifest. |
| Do not break pilot routes; a new pack adds a route | Research routes are new. Trading controllers are not on `origin/main`. Hiding them does not remove a production route. | CONFORMANT for production `main`. The shared admin bundle still contains trading screens. That is the one-SPA rule, not a separate admin. Calls to the hidden API return 404. Code in the bundle is not permission to trade. |
| Secrets stay in `/etc/kit-platform/api.env` | `novixa-deploy.md`. The corrected runbook excludes `appsettings*.json` and does not print `api.env`. | CONFORMANT for this runbook. `deploy-update-vps.ps1` still copies appsettings and must not be used for this release. |
| Rollback | NVX-OPS-03 is Planned, not written. | DOCUMENTATION_GAP for the platform. Closed for this release by sections 6–8 of the delivery runbook. Not executed. |
| A documented research-only DI profile | Not in KIT-PLT-01. | DOCUMENTATION_GAP. Proposed sentence, not inserted: "A pack may expose a read registration that omits its workers and credential services. The deployment unit remains the shared API and the shared admin SPA. Shipping unused UI code is not the same as mapping its API." |
| CI proves this host | No workflow runs these tests or hashes a publish. | TEST_GAP for the standing pipeline. This mission's local run is below. No workflow was added. |

## 4. Files changed

Committed in `6596cfb9`:

| File | Why |
|---|---|
| `Program.cs` | Research registration, publication options, controller convention. No exchange backfill binding. |
| `PersonalIntelligencePackDependencyInjection.cs` | `AddPersonalIntelligenceResearch` registers only the file reader. |
| `PersonalIntelligenceResearchHost.cs` | Drops every Personal Intelligence controller except `ResearchPublicationController`. |
| `ResearchReadPage.tsx` | `/second-brain/research` without `tradingApi`. |
| `router.tsx` | Route sits outside `TradingLayout`. |
| `registry.tsx` | Module nav opens that route. |
| `TradingPages.tsx` | Trading overview no longer embeds `ResearchPanel`. |
| `trading-routes.smoke.ts` | Static check for the split. |
| `ResearchPublicationTests.cs` | Host fact plus the policy, module, and service fact added in this mission. |

Untracked, not committed:

| File | Why |
|---|---|
| `SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` | Sections 6–8 replaced. The old copy of `sb-research-safe-host-001` is withdrawn. |
| This report | Release evidence. |
| The three earlier reviews | Left as they were, except this mission did not edit 001 or 002. |

## 5. Commands and results

Run on the worktree at `6596cfb9` with no tracked edits. These results replace every earlier PASS.

| Command | Exit | Result |
|---|---|---|
| `dotnet test tests/KitPlatform.PersonalIntelligence.Tests/KitPlatform.PersonalIntelligence.Tests.csproj --filter FullyQualifiedName~ResearchPublicationTests --nologo` | 0 | Passed 16, failed 0, skipped 0. Debug test host, not the Release artifact. |
| `python -m unittest tests.test_publish_research_v1` in `tools/research-publication` | 0 | 7 tests, OK |
| `npx tsx src/modules/second-brain/research-view.smoke.ts` | 0 | `research view ok` |
| `npx tsx src/modules/second-brain/trading-routes.smoke.ts` | 0 | `trading routes ok` |
| `dotnet publish src/KitPlatform.Api/KitPlatform.Api.csproj -c Release -o artifacts/sb-research-safe-host-003/api --nologo` | 0 | Wrote the API directory |
| `npm run build` in `client/admin` | 0 | `tsc --noEmit && vite build`, built in 15.99s |

NU1903 for Newtonsoft.Json 10.0.3 is the existing host warning. PostgreSQL trading integration was not run. The reader does not use those tables. Browser login was not run.

## 6. Artifact provenance

| Item | SHA-256 |
|---|---|
| `artifacts/sb-research-safe-host-003/api/KitPlatform.Api.dll` | `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` |
| `artifacts/sb-research-safe-host-003/admin/index.html` | `12B52DBC551C5F987A6F5CEB69FECF5BC8D6D53FD2BB52FF34230F77388B9B6D` |
| `artifacts/sb-research-safe-host-003/admin-SHA256SUMS.txt` | `0174F8E9EDB9B8C22C5D0F343D3D5127C342D9E753464A27F5AB9C945586514B` |
| `tools/research-publication/publish_research_v1.py` | `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361` |

The admin manifest lists 312 files. `artifacts/` is gitignored, so the hashes live in this report rather than in Git.

UTF-8 scan of the new DLL: `KitPlatform.Api.dll` contains `ResearchOnlyControllerConvention` and `AddPersonalIntelligenceResearch`, and does not contain `ExchangeBackfillWorker`. The infrastructure DLL still contains `ExchangeBackfillWorker`.

This is one build from the pinned tracked tree. It is not a second clean clone, and it is not byte-identical to the earlier DLL `11B04205E9E8F88DFA44107043F0E0FF5992595C1701FF33E1B70C16D70D41F9`. Do not use `artifacts/sb-research-safe-host-001`. The admin `index.html` hash happens to match that earlier admin build. The API DLL does not, so the old directory is not this release.

## 7. Isolation verification

| Check | Evidence in this mission | Status |
|---|---|---|
| Reader only in `AddPersonalIntelligenceResearch` | Source of the committed DI method. `Research_only_host_enforces_policy_without_trading_services` asserts `IPersonRepository`, `ITradeRepository`, `ITradingDesk`, `IExchangeBackfill`, and `IExchangeCredentialKeyRing` are null. | PASS as a test host. The production process was not started. |
| Trading controllers not served | Same suite: trading summary, exchange catalog, and trading loop POST return 404. | PASS in the test host |
| Worker not registered or started | `IHostedService` named `ExchangeBackfillWorker` is absent. The type remains in the infrastructure DLL and is not constructed by this host. | PASS in the test host |
| `research.read` without `trading.run` | New fact returns 200 `MISSING` for that permission alone. `ResearchReadPage` admits it and does not call `tradingApi`. Smoke is a source text check. | PASS for the test host and static UI. Browser UNVERIFIED |
| `trading.run` and ADMIN | New fact returns 200 for both while the module flag is on. They also match the existing policy, so they are not read-only. | PASS in the test host |
| Exchange-only permissions | New fact returns 403. The page redirects them away from `ResearchPanel`. | PASS in the test host. UI redirect is source, not a browser run. |
| Module off, including ADMIN | New fact returns 403 for `research.read` and for ADMIN. | PASS in the test host |
| Anonymous | New fact returns 401. | PASS in the test host |
| Trading UI shipped in the admin bundle | Vite output includes `TradingPages`, `ExchangePages`, `trading.api`, and `exchange.api`. | Not a privilege bypass. The test host does not map those APIs. A `trading.run` user can still open the screen and receive 404. |
| Production JWT, database permission claim, and browser route | Not run | UNVERIFIED |

## 8. Runbook

Operative text: `src/Packs/PersonalIntelligence/SB-RESEARCH-SAFE-HOST-DELIVERY-001.md` sections 6–8, corrected in this mission.

The procedure now has a preflight stop, a backup of the API tree, the admin tree, and `api.env`, a copy that excludes `appsettings*.json`, `uploads/`, and `api.env`, a hash check before restart, a separate restart approval, post-restart checks, and a rollback that restores `api.env`. It names the shared API and the shared admin app as the deployment unit and forbids `deploy-update-vps.ps1` and `deploy-production.ps1`. It was not executed.

Authority files `deployment-model-v1.md` and `novixa-deploy.md` were not edited.

## 9. Remaining blockers

| Item | Evidence | Status | Decider | Next action | In this release |
|---|---|---|---|---|---|
| Migration 390 | File inserts one registry row. The eight-file manifest also creates trading schema. Neither was applied. | OWNER_DECISION_REQUIRED | Project owner and database owner | Approve 390 alone on a named database, or leave the registry row absent. Do not run the eight-file manifest for Research Read. | Decision yes. Execution no. |
| Tenant `allowed_modules` and `enabled_modules` | Runtime gate reads `enabled_modules` only. No tenant was selected. | OWNER_DECISION_REQUIRED | Tenant owner | Name the tenant and both lists. | Decision yes. Execution no. |
| Permission row and role | No migration contains `personal_intelligence.research.read`. Role update cannot attach a missing code. | OWNER_DECISION_REQUIRED | Project owner | Approve the insert and the role. Confirm that role does not also receive `trading.run`, `trading.approve`, `exchange.read`, or `exchange.connect`. Decide whether ADMIN and `trading.run` stay in the research policy. | Decision yes. Execution no. |
| Publication directory and owner | No path is named. The publisher deletes `.previous`. | OWNER_DECISION_REQUIRED | API process owner | Name the owner, the directory outside every nginx root, and the backup copy. `www-data` gets read-only access. | Decision yes. Execution no. |
| `pack_content.brand.narrative_json` | `ContentRepository` selects it. No database was queried. | OWNER_DECISION_REQUIRED | Content database owner | Run only the read-only `information_schema.columns` query from review 002. Do not run migration 384 to discover the column. | Verification yes. Migration no. |
| Browser and server runtime | Tests use `HeaderAuth` and a temp server. Smokes read source text. | UNVERIFIED | Whoever runs the later approved window | After the data decisions, sign in as the read-only user and as a user without the permission. | Not in this mission. |
| Restart approval | Runbook section 6.5 requires a later approval naming `6596cfb9` and the two hashes. | OWNER_DECISION_REQUIRED | Production operator | Do not restart from this report. | No |

## 10. Reusable release checklist

Use this before calling any later KIT module ready for a deployment review. It belongs as a future addition to NVX-OPS-01 section 4.4. It was not inserted there.

1. Architecture: name the existing authority document and class each difference as conformant, an implementation gap, a documentation gap, a test gap, or an owner decision.
2. Revision: the deployed tree is one commit. Review notes and generated output stay untracked.
3. Evidence: re-run the module tests and the production build on that commit. Record the command, exit code, and result. Do not copy an older PASS forward.
4. Provenance: record SHA-256 of the API DLL and a manifest of the admin tree. Say whether a second build reproduced those bytes.
5. Isolation: state which services and controllers the host registers, and which code is merely present in a shared binary or bundle.
6. Configuration: secrets stay in `api.env`. The copy excludes live appsettings, uploads, and the environment file. Do not print their values.
7. Backup and rollback: back up every file the copy or environment edit will change, verify the backup hash, and restore `api.env` as well as the application files. Restart is a separate approval.
8. Owner approval: list every database, permission, and restart decision still open. A green build does not close them.

Existing commands to reuse, without a new framework: `dotnet test` with a fully qualified filter, `dotnet publish -c Release`, `npm run build` in `client/admin`, and `Get-FileHash`. Do not point this release at `scripts/deploy-update-vps.ps1`.

## 11. What was and was not done

Done: architecture comparison, isolation commit, new tests, publisher tests, UI smokes, Release API publish, admin production build, hash manifest, runbook correction.

Not done: push, deploy, service restart, migration, database query, permission or tenant change, server configuration change, live credential read, exchange connection, or a trade. `E:\KitPlatform` was not modified. Production readiness is not claimed.
