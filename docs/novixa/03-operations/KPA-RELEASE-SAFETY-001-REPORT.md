# KPA-RELEASE-SAFETY-001 — NOVIXA production-safe release

**Date:** 2026-10-16  
**Decision:** **NOT READY**  
**Superseded in part by KPA-RELEASE-RECOVERY-001:** the admin build gate in section 9 item 1 is closed on the same worktree. Production readiness is still **NOT READY — REMEDIATION COMPLETE**. See `docs/novixa/03-operations/KPA-RELEASE-RECOVERY-001-REPORT.md`.  
**Worktree used for the candidate:** `E:\KitPlatform-wt-phase5-novixa-product-proof`  
**Worktree not modified:** `E:\KitPlatform` (dirty local `main`)  
**Video worktree investigated, source not modified:** `E:\KitPlatform-wt-video-mvp`  
**Architecture Freeze register:** not edited.

This record does not treat a green `dotnet build`, a unit-test pass, or HTTP 200 on `/api/health` as production readiness. The earlier Phase 5 dossier §12 ("READY FOR OWNER DEPLOY") remains a host read and a proposed runbook. Section 13 of `KPA-PHASE5-NOVIXA-PRODUCT-PROOF.md` points here and supersedes that call.

## 1. Release candidate

| Item | Value |
|---|---|
| Pharmacy runtime code | `76e8177b134017164db5297ffa4b049be713d9d7` |
| Subject | `fix(pharmacy): refuse expired lots before a counter sale` |
| Branch tip | `b3ea8c8c` — one documentation line on top of `76e8177b`, no runtime diff |
| Parent of the pharmacy patch | `bf5dc643` — withhold OTC suggestions when a consultation needs medical care |
| Phase 4 freeze contained in this history | `0c4adc87` |
| Phase 1.5 contained in this history | `37ffb3f6` |
| Merge base with `origin/main` | `73d3c4a6` |
| `origin/main` | `44fd508552ac47e4f2366aa0cfe1f523cc65be2c` |
| Local `main` | `8c59ceb4` — dirty, ahead 6 / behind 142 of `origin/main`. Not a candidate. |
| Video MVP | `45d21801811b80c70050632c682c50e03218169d` on `kit-mkt-video-mvp`. Separate artifact. **BLOCKED.** |

`76e8177b` is not an ancestor of `origin/main`, and `origin/main` is not an ancestor of `76e8177b`. Publishing the candidate does not fast-forward the remote source of truth. The dirty working tree at `E:\KitPlatform` was not used as the artifact.

Local Release build of `KitPlatform.Api.dll` from the Phase 5 worktree (source matches `76e8177b` for compiled projects; the only extra files are this report, the dossier note, and an uncommitted test):

`SHA-256 E9A5CC26DB670BFD89E47662210208E2A8E616D4FBAE472D46086D31F7027E0D`

That hash is a local compile. It is not a published artifact and it is not the live DLL recorded in the Phase 5 dossier (`26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`, mtime 2026-09-27). This session did not re-read the server.

## 2. Dependency map

`Core → Pharmacy Pack → API/Infrastructure → Database/Migrations → Deployment configuration → NOVIXA UI`

| Link | Kind | Evidence |
|---|---|---|
| Core to Pharmacy | Compile-time and runtime | `c447814d` moved the workflow engine into the Pharmacy pack. `SalesService`, `SalesRepository`, and `BatchResolver` on the candidate call that pack. Pharmacy does not build as a standalone tree from `origin/main` without that move. |
| Pharmacy to API | Compile-time | `KitPlatform.Api.csproj` project-references Pharmacy Application and Infrastructure. |
| API to Content | Compile-time. Not a pharmacy schema dependency. | The same csproj project-references `KitPlatform.Packs.Content.Infrastructure`. A Content compile error blocks the API binary. Content code from `98f0632c` and `c663196f` is inside any API built from this candidate. |
| API to Clinic, Survey, Connect, FamilyOS, Care, LocalOS, Sales | Compile-time | Same csproj. Those packs ship inside the API binary even when the change being released is a counter-sale rule. |
| Database | Deployment | `run-incremental-migrations-prod.sh` reads `migration-files.prod.txt` only. The candidate adds no pharmacy file to that manifest. It removes `230_pack_family_parent_emotion_push.sql` and `231_pack_family_memory.sql` from it. Those names stay on `migration-files.family-os.txt`. |
| Content database | Separate manifest | `383`, `384`, `387`, and `388` are listed only in `migration-files.content.txt`. The production runner does not apply them. |
| NOVIXA UI | Deployment | `scripts/deploy-production.ps1` runs `npm run build` for admin before copy. Admin TypeScript failure stops that script (`LASTEXITCODE`). |

Content is in the NOVIXA binary because the API project references it. Content migrations are not in the NOVIXA production migration runner. Those are different dependencies and must not be collapsed into one "ship Content" step.

## 3. What the candidate changes, and why

Commits on the candidate that are not in `origin/main`:

| Commit | Why it is in the tree |
|---|---|
| `c447814d` | Pharmacy workflow leaves Core. Later sale and draft-order code depends on this. |
| `98f0632c` | Content editorial workflow. Present because the API reference pulls Content along. |
| `d54652d4`, `b64f6772`, `2af9ba12`, `0c4adc87` | Architecture records. No runtime behavior. |
| `0896f45a` | Callers compile after `IPermissionEngine` / `IInventoryEngine` / `IPricingEngine` were removed. |
| `c663196f` | Content production rows gain a customer owner. Also the manifest split described above. |
| `37ffb3f6` | Content narrative boundary already required by existing tests. |
| `bf5dc643` | Server stops offering automatic OTC products when the consultation must stop or refer for medical care. |
| `76e8177b` | Counter sale refuses an expired lot and keeps FEFO on the earliest in-date lot. |
| `b3ea8c8c` | Documentation only. |

`0896f45a` replaces the deleted engine with the same calls the engine already made:

- Dashboard store figures stay visible for role `ADMIN` or permission `reports.read`, `reports.write`, or `reports.export`.
- Draft-order discounts call `SalesDiscountPolicy.FromPermissions(permissions, isAdmin)`. The old `PermissionEngine.GetSalesDiscountPolicy` did that exact call. `reports.read` and `sales.read` do not grant a discount.
- `ReportsAuthorizationExtensions.cs`, `DashboardAuthorizationExtensions.cs`, and `SalesAuthorizationExtensions.cs` have no diff from `73d3c4a6` to `b3ea8c8c`.

`origin/main` since `73d3c4a6` is site content and stats snapshots (40 files under `novixa-site` and `famixa-site`). It does not contain `SellableLotRules` or the OTC gate.

## 4. Build and test

| Check | Scope | Result |
|---|---|---|
| `dotnet build` `KitPlatform.Api.csproj` `-c Release` | Phase 5 worktree, candidate source | **PASS.** 0 errors, 58 warnings. Includes `NU1903` for `Newtonsoft.Json` 10.0.3 and existing nullable warnings. Elapsed 00:00:36. |
| `dotnet test` Application.Tests filter `NovixaReleaseSafetyBehaviorTests\|InventoryEngineFefoTests\|PricingEngineTests\|PharmacyConsultationOtcGateTests` | Same worktree | **PASS.** 18 passed, 0 failed. |
| `dotnet test` Platform.Tests filter `ContentSeriesTakeNamedReadTests\|PharmacySalesOrderCompletedHandlerTests\|PharmacySalesReturnCompletedHandlerTests` | Same worktree | **PASS.** 6 passed, 0 failed. The three named-read tests cover the Veo/Fal readers that exist on this lineage. |
| `dotnet test` Api.Tests `ControlledPilotSaleTests` | Local `localhost` database `kitplatform`, tenant `DEMO_PHARMACY` | **PASS.** 3 passed, 0 failed, about 6 seconds. The test refuses `NT_XUANHOA`. It did write a local DEMO counter sale. It did not open production. |
| `npm run build` in `client/admin` | Phase 5 admin sources, lockfile identical to `E:\KitPlatform\client\admin` | **FAIL.** Exit 2. 173 TypeScript errors: 169 under `src/modules/content`, 3 in `GoodsReceiptListPage.tsx`, 1 in `AdjustmentListPage.tsx`. No error under `src/modules/sales`. |
| customer-app, staff-app, prescriber-portal, partner-portal, assessment-web, family-app | Deploy script builds these after admin | **NOT RUN.** The script stops at the first failed `npm run build`. |
| Clean `45d21801` Content infrastructure | Detached worktree `E:\KitPlatform-wt-kpa-rs001-video` | **FAIL.** 4 errors, `IPermissionEngine` missing in `DashboardService` and `CustomerDraftOrderService`. The take-proxy errors are behind this failure on the clean commit. |
| Dirty `E:\KitPlatform-wt-video-mvp` Content infrastructure | Uncommitted permission swap already present; source of the proxy was not edited | **FAIL.** `CS0535` `ReadVeoTake` and `CS0535` `ReadFalTake` on `ContentSeriesTakeProxyService`. |

`ControlledPilotSaleTests` covers, on local DEMO only: unauthenticated consultation save rejected; expired-only products not sellable; in-date FEFO allocation; a completed sale linked to the consultation; a label-optional scan of an expired lot does not fall through to another lot. It does not cover production Xuân Hòa, report-page authorization in the browser, or a failed database dependency on the live host.

The new file `tests/KitPlatform.Application.Tests/NovixaReleaseSafetyBehaviorTests.cs` is uncommitted. It locks `SalesDiscountPolicy.FromPermissions` and `SellableLotRules`. The store-analytics assertion repeats the dashboard predicate; it does not execute `DashboardService`. Equivalence of the dashboard change is the diff against `PermissionEngine` at `c447814d^`.

## 5. Business scenarios

| Scenario | This session |
|---|---|
| Login and tenant isolation | **PASS on local DEMO** inside `ControlledPilotSaleTests` (login returns `DEMO_PHARMACY`; the fixture throws if the code is `NT_XUANHOA`). Production login was not run. |
| Open shift, sale, complete order | **PASS on local DEMO** as part of the same fixture, which opens a shift when needed and completes one sale. |
| Stock reduced on the sold lot | Covered by that fixture's lot comparison. Local DEMO only. |
| FEFO chooses the earliest in-date lot | **PASS** in `InventoryEngineFefoTests` and in the DEMO fixture. |
| Expired lot blocked | **PASS** in `SellableLotRules` unit test, `AllocateFromBatches_skips_expired_and_keeps_earliest_sellable`, `AllocateFromBatches_refuses_when_only_expired_stock_remains`, and the DEMO fixture including the label scan. |
| Draft versus completed | Discount policy for drafts is unit-tested. A draft-order round trip against the database was **NOT RUN**. |
| Report permission and discount policy | **PASS** as unit predicates. `reports.read` does not grant a discount. Report HTTP policies were not executed in a browser. |
| Database down / integration dependency down | **NOT RUN.** |
| Production Xuân Hòa | **NOT RUN.** The Phase 5 dossier §12 is a prior read-only snapshot. This session did not repeat it, and a DEMO pass is not that snapshot. |

## 6. Migration, backup, rollback

Pharmacy production runner: no new SQL file. Do not pass `-RunMigrations` for this candidate. Removing 230 and 231 from the prod manifest does not uninstall them. The Phase 5 dossier says both names were already in the live ledger on 2026-07-26. This session did not re-read that ledger.

Local `kitplatform` ledger has 141 rows and does not contain 230, 231, 383, 384, 387, or 388. `pack_content.video_production.tenant_id` was absent before the drill below. The local database is not `novixa_prod`.

Content migration `387_pack_content_production_customer_owner.sql` was applied inside one transaction on local `kitplatform` and then `ROLLBACK`. During the transaction, `tenant_id` and trigger `trg_video_production_owner` existed. After `ROLLBACK`, both were absent. That verifies DDL rollback only while no owned row has been committed. The migration text itself says the old unique key cannot be restored after two customers legally share one episode code. No such rows were inserted. Migration 388 was not applied. A separate database could not be created: `CREATE DATABASE` returned `permission denied` for user `kitplatform`.

Backup files named in the Phase 5 dossier were not downloaded and not restored. Binary rollback of `/var/www/kit-platform` was not executed. There is no staging host in this session. Production was not used as the drill.

**Rollback status: not VERIFIED for production.** The local 387 transaction rollback is evidence about a content migration that the pharmacy runner must not apply.

## 7. Video Production — BLOCKED, separate from NOVIXA

On clean `45d21801`, Content infrastructure does not compile because `IPermissionEngine` is still referenced and the type is gone. On the designated dirty worktree, those two pharmacy files are already edited and the remaining errors are exactly:

- `ContentSeriesTakeProxyService` does not implement `IContentSeriesTakeProxyService.ReadVeoTake(string?)`
- the same class does not implement `ReadFalTake(string?)`

The interface and `ContentController` calls have been on this line since `98f0632c`. `KitVideoVideoStore` on this line only has `Persist`, `PersistProductionExecution`, and `Read(string? path)`. There is no `TryReadNamed`, no `FamixaVeoTakePlaybackV1`, and no `FamixaFalLipsyncPlaybackV1`. Nothing on this line writes `{videoRoot}/veo/{fileId}.mp4` or `{videoRoot}/fal/{fileId}.mp4`.

Those types and `TryReadNamed` exist on the Phase 5 lineage, added in `0896f45a`, which is not an ancestor of `45d21801`. Even there, no production writer stores that layout; the unit test is the only writer. Copying that reader onto the video branch would look up files this branch does not create. A method that returns null was not added.

The video worktree also has uncommitted pharmacy edits in `DashboardService.cs` and `CustomerDraftOrderService.cs`. Those were not committed and were not folded into a video fix. No video source file was edited in this session.

## 8. Changes made in this session

| Path | What |
|---|---|
| `docs/novixa/03-operations/KPA-RELEASE-SAFETY-001-REPORT.md` | This report. Uncommitted, Phase 5 worktree. |
| `KPA-PHASE5-NOVIXA-PRODUCT-PROOF.md` | Header and §13 record NOT READY. §12 was already an uncommitted edit and was not rewritten. |
| `tests/KitPlatform.Application.Tests/NovixaReleaseSafetyBehaviorTests.cs` | Uncommitted behavior tests. Not part of SHA `76e8177b`. |

No commit. No push. No merge. `E:\KitPlatform` source was not edited. Video source was not edited. Production was not deployed, restarted, migrated, or written. No AI provider was called.

A temporary detached worktree `E:\KitPlatform-wt-kpa-rs001-video` was created to compile clean `45d21801` and is removed after this report. An admin `node_modules` junction used for the typecheck is removed after the build; it was not a source change.

## 9. Blockers

1. Admin `npm run build` fails, so `deploy-production.ps1` cannot produce the NOVIXA UI bundle from this candidate.
2. Production binary rollback has not been executed on a non-production host. The deploy script does not keep the previous tree.
3. The live DLL hash and migration ledger were not re-read in this session. The §12 snapshot is stale relative to any sale after 2026-10-16 10:12 +07.
4. The candidate is not `origin/main`. Shipping it replaces a September binary with a tree that also contains Content, Family, and the other project references, not a pharmacy-only patch.
5. Video `ReadVeoTake` / `ReadFalTake` stay blocked. Closing them is not required to compile the Phase 5 API, and it must not be mixed into the pharmacy commit.

## 10. Commands — do not run against production until the blockers close

Identity and build, from the Phase 5 worktree only:

```powershell
Set-Location E:\KitPlatform-wt-phase5-novixa-product-proof
git rev-parse HEAD
dotnet publish src\KitPlatform.Api\KitPlatform.Api.csproj -c Release -o <empty-dir>\api --no-self-contained
```

Record SHA-256 of `KitPlatform.Api.dll` and of `client\admin\dist\index.html` after `npm run build` exits 0. Today that admin command exits 2, so there is no admin bundle to hash.

Do not pass `-RunMigrations`. Do not point `deploy-update-vps.ps1 -Root` at `E:\KitPlatform`.

Before any later upload, on the server, read only: service active, live DLL still `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941` or an explicitly accepted successor, catalog mode `sandbox`, stock-out sync false, and zero `tenant_csdl_duoc_link` rows for `NT_XUANHOA`. If any of those differ, stop.

Smoke after a future deploy, still without creating a sale: `systemctl is-active kit-platform-api`, `GET /api/health` body `status=ok`, `GET /api/health/db` body `database=true`, unauthenticated `GET /api/sales/pos/consultation/symptom-options` returns 401, admin and POS index URLs return HTML. Stop if the service is inactive, health fails, the consultation route returns 500, catalog mode is `live`, or stock-out sync becomes true.

Rollback, only after a snapshot taken before the upload has been restored once on a non-production host: stop the service, copy the snapshot back over `api` without deleting `uploads`, copy the UI trees back, start the service, confirm the previous DLL hash and both health URLs. No down migration for this pharmacy candidate. A database restore is a separate decision because it discards sales written after the dump.

## 11. What the owner still has to accept

- A candidate SHA that is not `origin/main`, with Content compiled into the API.
- Admin `npm run build` exit 0, or a written exception that names the 173 errors and a different build command. This report does not grant that exception.
- A staging host where the previous binary is deployed, the new binary replaces it, smoke is run, and the previous binary is restored, with both DLL hashes recorded.
- A fresh read-only confirmation of the live DLL hash, catalog mode, stock-out flag, and migration ledger immediately before build.
- Confirmation that `migration-files.content.txt` will not be the manifest used for this deploy.
- Video closure remains a separate decision and is **BLOCKED** until a writer for the Veo/Fal file layout exists on `kit-mkt-video-mvp`.
