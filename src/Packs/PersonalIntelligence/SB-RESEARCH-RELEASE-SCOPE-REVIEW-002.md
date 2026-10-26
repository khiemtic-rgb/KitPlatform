# SB-RESEARCH-RELEASE-SCOPE-REVIEW-002

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Remote: `https://github.com/khiemtic-rgb/KitPlatform.git`

Executive recommendation: **B — Create a Research-only candidate.**

Do not release option A. Option C is the holding rule for the current candidate only: leave `3d5d7b42` unreleased. The next implementation mission should build the narrower candidate. This review does not create that candidate.

Candidate status remains **NOT READY FOR PRODUCTION**. This document does not approve a release.

## 1. Executive recommendation

Option B is the smallest safe release scope.

The publication reader, its five GET routes, and the publisher do not reference trading types. The current candidate still cannot be hosted as Research Read alone. `AddPersonalIntelligencePack` registers `ExchangeBackfillWorker` in the same method that registers the reader. `Program.cs` loads the Personal Intelligence API assembly through `TradingLoopController`, so trading and exchange controllers are mapped with the research controller. The Research panel is rendered inside `TradingOverviewPage`, behind `TradingLayout`, and that page calls the trading summary API.

Option A keeps that host, the eight-file Personal Intelligence manifest, and exchange credential storage. That is a trading release with a research reader attached. It is not the smallest Research Read scope.

Option C avoids a new candidate until owner decisions land. Several of those decisions exist because the candidate includes trading migrations, a default-on worker, and PostgreSQL tests that write ledger rows. Creating the narrower candidate reduces that surface. It does not require those trading decisions first.

B is source-feasible only after a bounded host and UI split. It is not a cherry-pick, and this review did not verify the split at runtime.

## 2. Baseline Git provenance

Verified in this review with read-only Git commands, including `git ls-remote`.

| Item | Actual |
|---|---|
| Root | `E:/KitPlatform-sb-research` |
| Branch | `admin-build-blocker-001` |
| Upstream | Not configured |
| HEAD | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| `origin/admin-build-blocker-001` | Same hash |
| `origin/main` | `608bc054c9b6389190c4c214ac478b3397b110dc` |
| Ahead / behind `origin/main` | 8 / 0 |

The expected baseline matches. No discrepancy.

Worktree before this report: one untracked file, `src/Packs/PersonalIntelligence/SB-RESEARCH-OWNER-APPROVAL-REVIEW-001.md`. That review was read and not modified. This mission adds only this report. No commit.

`E:/KitPlatform` was not modified.

## 3. Research dependency map

| Dependency | Class | Evidence |
|---|---|---|
| Publisher `tools/research-publication/publish_research_v1.py` and README | Required for Research Read | Writes `manifest.json`, `lab/LAB_RESULT.json`, `paper/PAPER_OPS_STATUS.json`, `paper/PAPER_RESEARCH_REPORT.json`, `paper/PAPER_OPS_HEALTH.json`. Schema `research-publication/v1`, publisher `research-lab-v1`. No trading import. Deletes `.previous` after a successful publish. |
| `FileResearchPublicationReader` and `ResearchPublication.cs` | Required for Research Read | Reads those five relative paths. Empty root or missing directory returns `MISSING`. No trading or database reference in the Research reader or Research application types. |
| `ResearchPublicationController` | Required for Research Read | Five GETs under `api/personal-intelligence/research`. Policy `PersonalIntelligenceResearchRead` and `[RequirePersonalIntelligenceModule]`. No trading type in the controller. |
| JWT auth, permission claims, role `ADMIN` | Shared infrastructure required by Research Read | Existing `JwtTokenService` emits `ClaimTypes.Role` and `permission` claims. Research does not add a new auth stack. |
| Module gate `PersonalIntelligenceModuleFilter` | Shared infrastructure required by Research Read | Calls `ITenantPlatformSettings.IsModuleEnabledAsync`, which checks `settings.platform.enabled_modules` only. It does not query `pack_personal_intelligence`. |
| Module registry row and `allowed_modules` | Shared infrastructure for the supported entitlement path | Migration 390 inserts the registry row. `NormalizeAllowedModules` / `NormalizeModules` reject or ignore a code absent from the registry. Runtime gate does not read the registry. |
| `TradingLayout`, `TradingOverviewPage`, router `/second-brain/*` | Unrelated behavior currently bundled with the Research UI | Every Second Brain route uses `TradingLayout`. Overview loads `tradingApi.summary()` and then renders `ResearchPanel`. Layout admits ADMIN, `trading.run`, `exchange.connect`, or `exchange.read`. It does not admit `research.read` alone. Nav key `secondBrain` maps to module `personal_intelligence`. |
| `research-view.ts`, `research.api.ts`, `ResearchPanel.tsx`, `research-view.smoke.ts` | Required for Research Read UI | Publication client and panel. They do not grant trading rights. They are not reachable as a standalone route. |
| `AddPersonalIntelligencePack` | Must be split before isolation | Line 36 registers the reader. Lines 37–54 register exchange connectors, credential key ring, connection, sync, backfill, and `AddHostedService<ExchangeBackfillWorker>()`. |
| `Program.cs` host wiring | Must be split before isolation | `AddApplicationPart` uses `TradingLoopController`'s assembly, so the research assembly also maps trading controllers. Options binding covers both publication root and `ExchangeBackfill`. |
| Trading and exchange controllers, stores, connectors, migrations 385 and 387–393 | Trading/Exchange-only | Present in the same pack projects and the same manifest as the reader. Not referenced by the reader or research controller. Manifest application creates ledger tables, exchange ciphertext columns, and backfill jobs. `391` seeds exchange permissions only. |
| `vite.second-brain.config.ts` and `scripts/second-brain-trading-ui-*.ps1` | Trading/Exchange-only | Dev proxy for a loopback trading validation API. Production admin build is `tsc --noEmit && vite build` in `client/admin/package.json`. |
| Content fix `96657e06` | Unrelated to Research Read, required to compile the API host on this base | On `origin/main`, `BrandNarrativeFit.SeriesEntryBlock` reads `ContentBrandFitDto.Territory` and `.NarrativeFit`, and the record has neither member. `ContentArticleSeriesService` already calls `brand.NarrativeJson`. The fix adds the members and selects `narrative_json`. |
| Admin fix `c57d8d7e` | Unrelated to Research Read, required to produce the shared admin artifact | 28 admin files, including Famixa and `AdjustmentListPage.tsx`, `GoodsReceiptListPage.tsx`, `PosConsultationModal.tsx`. Production build typechecks the whole admin app. |
| PostgreSQL integration tests | Trading/Exchange-only for this read path | They require `KIT_PI_TEST_CONNECTION`, write tenant and ledger rows, and do not clean them up. The reader does not use those tables. |

Unknown: whether the destination Content database already has `pack_content.brand.narrative_json`. Not queried.

## 4. Option comparison

| Criterion | A — current candidate | B — Research-only candidate | C — freeze current candidate |
|---|---|---|---|
| Product surface | Research GETs plus trading desk, exchange connect, sync, and backfill in one API process and one `/second-brain` UI | Research publisher, reader, five GETs, and a research screen that does not call trading | No new surface. Current candidate stays unreleased |
| Migrations | Eight-file manifest. Trading schema, ciphertext, backfill jobs, two exchange permissions. 390 does not enable a tenant | No trading migrations. Registry row 390 only if the supported entitlement path is used, as its own approved change | No migration |
| Permissions | `research.read` still unseeded. `trading.run` and ADMIN can read Research and call trading APIs | Read-only role gets `research.read` only, after that row exists. Do not grant `trading.run` | No permission change |
| Worker and credentials | Worker is registered and defaults to enabled. Credentials are not needed for the reader, but the process contains the exchange stack | Worker not registered. No exchange credential for this release | Worker stays out of production because this build is not deployed |
| UI | Research sits on the trading overview. `research.read` alone cannot pass the layout | Needs a route and gate that admit `research.read` and do not call `tradingApi.summary()` | Current UI remains unreleased |
| Content and admin fixes | Both are in the candidate. Content fix adds a runtime read of `narrative_json` | Same compile facts if the host and admin app are built. They stay build fixes, not research features | Both remain unreleased |
| Tests still required | Reported publisher, research, API, and admin results, plus trading persistence that is blocked | Publisher, `ResearchPublicationTests`, API build, admin build if admin ships, research UI smoke. PI PostgreSQL suite not required for the reader | No new test run |
| Rollback | App artifact plus publication copy. Schema and ciphertext would already exist. `.previous` is not a rollback copy | App artifact plus publication copy. No trading schema from this release | Nothing to roll back |
| Main risk | A research approval also ships trading and exchange | Isolation is not done and was not runtime-tested. Content column remains an owner check | The unsafe bundle stays the only candidate and can be mistaken for the release |

## 5. Minimum Research-only scope

Source-level answer: Research Read can serve publication files without trading migrations and without the backfill worker. The current projects cannot be hosted that way until the registration and UI split below exist. Runtime behavior of that split was not executed.

### Keep

- `tools/research-publication/` including tests and README.
- `ResearchPublication.cs`, `FileResearchPublicationReader`, `ResearchPublicationController`.
- Research policy and the module filter's use of the existing tenant-settings service.
- `PersonalIntelligence:ResearchPublication` binding only.
- `research-view.ts`, `research.api.ts`, `ResearchPanel.tsx`, `research-view.smoke.ts`.
- Platform module code `personal_intelligence` in `PlatformModuleCodes`.
- Content compile fix `96657e06` on any candidate that builds `KitPlatform.Api` from this `origin/main`.
- Admin compile fix `c57d8d7e` on any candidate that runs `npm run build` for `client/admin`. Treat it as a separate build-fix bundle. It includes pharmacy and POS type repairs.

### Exclude

- Trading and exchange controllers, application services, connectors, repositories, and tests that open `KIT_PI_TEST_CONNECTION`.
- `ExchangeBackfillWorker` registration and exchange credential key ring.
- Migrations `385`, `387`, `388`, `389`, `391`, `392`, `393`, and the current eight-file manifest.
- Trading UI routes, `TradingLayout` as the research entry, `vite.second-brain.config.ts`, and the trading UI scripts.
- `SecondBrainV1Tests` as a Research Read gate. Those six facts cover in-memory trading rules.

### Must be refactored before isolation

- Split pack registration so a research host does not call `AddHostedService<ExchangeBackfillWorker>` or construct exchange connectors.
- Stop using `TradingLoopController`'s assembly as the way to map research controllers, or otherwise avoid mapping trading and exchange controllers in the research host.
- Render `ResearchPanel` on a route that does not call `tradingApi.summary()` and whose gate accepts `personal_intelligence.research.read`.
- Keep `96657e06` and `c57d8d7e` as compile prerequisites. Do not describe them as research product changes.

### Requires a separate owner decision

- Whether to apply migration 390 alone so the supported entitlement API can accept `personal_intelligence`. The current manifest cannot apply 390 by itself.
- Tenant, `allowed_modules`, `enabled_modules`, and the role that receives `research.read`.
- Whether ADMIN and `trading.run` may remain inside the Research policy. The read-only role still must not receive `trading.run`.
- Publication root owner, process identity read access, and a retained publication copy.
- Confirmation that the target Content database already has `narrative_json` before the API that selects it is run. Do not apply migration 384 as part of Research Read. That file also updates brand narratives for `kittech`, `novixa`, and `famixa`.

## 6. Authorization and module gate

Smallest safe read-only design:

1. Data, owner-approved and not done here: insert `personal_intelligence.research.read` into `public.permissions`. `UpdateRolePermissionsAsync` only attaches codes that already exist. Assign that code to a role that does not have `personal_intelligence.trading.run`, `trading.approve`, `exchange.read`, or `exchange.connect`.
2. Data, owner-approved and not done here: put `personal_intelligence` in the tenant's `allowed_modules` and `enabled_modules`. The registry row from 390 is what lets the supported entitlement API accept the code. `IsModuleEnabledAsync` then reads `enabled_modules`. Enabling the module also runs the existing workspace provisioner, which always calls `kit_provision_pack_workspace` for `novixa_pharmacy` and has no Personal Intelligence branch.
3. Code, in the future isolation mission: the research screen must accept `research.read`. Today `TradingLayout` will block that user, and the overview will still request trading summary.
4. Do not grant `trading.run` as a substitute. It authorizes `TradingDeskController`.
5. ADMIN passes the Research policy when the module is enabled, and the same role passes the trading policies. That is broader than a read-only user. Accepting it for ADMIN accounts is DEC-03. It is not the read-only design.

`trading.approve`, `exchange.read`, and `exchange.connect` do not satisfy `PersonalIntelligenceResearchRead`. Tests in `ResearchPublicationTests` encode the 403 for those three codes. Those tests were not rerun in this review.

## 7. Content migration 384 and the admin build fix

`96657e06` cannot be dropped from a candidate whose gate is `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` on top of `origin/main`. The API project always compiles the Content pack. On `origin/main`, `SeriesEntryBlock(ContentBrandFitDto)` reads `Territory` and `NarrativeFit`, and `ContentBrandFitDto` does not declare them. This review did not run the build. The mismatch is visible in source at `origin/main` and in the fix at HEAD.

The same fix selects `narrative_json`. Migration `384_pack_content_brand_narrative_dna.sql` is already on `origin/main`. Its header says it belongs on the Content manifest. `deploy/ubuntu/migration-files.content.txt` does not list `383` or `384` and ends at `367`. The file adds the column and updates three brand narratives. This review did not query a database and did not run the file.

Safe check, for the Content database owner, not for this mission:

1. Name the target database and confirm it is the Content database for the API that will be deployed.
2. Use a read-only session.
3. Read the catalog for `pack_content.brand.narrative_json` only. Do not apply migration 384 to discover whether the column exists.
4. If the column is absent, do not run the candidate API against that database until a separate migration decision exists. Applying 384 also writes brand narrative JSON.

`c57d8d7e` is a separate shared-admin compile fix. `npm run build` typechecks every admin module, so a research-only UI change does not remove the need for this commit when the release artifact is the admin app. The pharmacy, procurement, and POS hunks are part of that compile fix. They are not Research Read behavior and must stay labeled as build-fix scope. `vite.second-brain.config.ts` does not replace that production build.

An API-and-publisher-only release could omit the admin fix and the Research UI. That would not meet the Research UI contract in `SECOND_BRAIN_RESEARCH_UI_INTEGRATION.md`. This review does not recommend that cut.

## 8. Validation matrix and release gates

| Item | This review | Release meaning |
|---|---|---|
| Git HEAD, `origin/main`, remote branch | Verified | Baseline matches |
| Reader has no trading reference; host registers the worker; UI is inside the trading overview | Verified from source | Isolation is required before B exists |
| Publisher hash `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361` | Reported by review 001, not recomputed here | Still the documented pin |
| Publisher tests, 7 | Reported, not rerun | Required again on the future B candidate |
| Lab/Paper Ops suite, 14 | Reported, outside this Git tree, not rerun | Not a substitute for the vendored publisher tests |
| `ResearchPublicationTests`, 14 facts | Reported, not rerun | Required on the future B candidate |
| `SecondBrainV1Tests`, 6 facts | Reported, not rerun | Not required for Research Read once trading code is excluded |
| API build | Reported, not rerun | Required. Includes the Content compile fix |
| Admin build | Reported, not rerun | Required if the admin artifact ships. Includes `c57d8d7e` |
| Research view and trading route smokes | Reported, not rerun | Research view smoke remains. Trading route smoke is not a Research Read gate after isolation |
| PostgreSQL integration | Blocked / not run | Not required for a reader that does not use PI tables. Still blocked for any scope that keeps trading persistence |
| Migration 390 and the eight-file manifest | Reviewed from files, not executed | Owner approval still required before any apply |
| Destination `narrative_json` | Not run | Owner catalog check before API runtime |
| Production process env, publication root, worker flag | Not inspected | Owner input |

Do not treat reported PASS results as results of this review.

Gates still open before any deploy of a future B candidate: provenance of that new commit, the validation set above, owner approval for 390 if used, permission and entitlement data, publication root and rollback copy, the Content column check, and a separate production approval. No `DEPLOY READY` state follows from this document.

## 9. Owner decisions still required

From review 001, still pending or blocked: DEC-01 through DEC-06. This scope review changes the technical recommendation under DEC-01: do not approve the eight-file manifest for a Research Read release. A later decision may approve migration 390 alone.

Additional decisions for B:

- Approve creation of a research-only candidate that splits host registration and the research route. This review does not create it.
- Choose the read-only role and tenant. Do not use `trading.run`.
- Decide whether ADMIN remaining inside the Research policy is acceptable.
- Name the publication-root owner and the Content database owner who will run the read-only column check.

## 10. What was not executed

No source edit except creation of this untracked report. Review 001 was not edited. No migration, database connection, production config read, permission change, build, test, branch, commit, push, merge, or deploy. No secrets were read. The recovery worktree was not modified. Publisher tests and the publisher hash were not rerun in this mission. Runtime behavior of a split host was not verified because that host does not exist.

## Next mission

**SB-RESEARCH-ISOLATION-CANDIDATE-001**

Prepare a new candidate that hosts the publication reader, research API, research policy, and Research panel without registering `ExchangeBackfillWorker`, without mapping trading or exchange controllers, and without calling the trading summary from the research screen. Keep the Content and admin compile fixes as labeled build prerequisites. Do not apply migrations, do not seed permissions, do not set production configuration, and do not deploy. Leave migration 390, the read permission, tenant entitlement, publication root, and the `narrative_json` catalog check as owner decisions.
