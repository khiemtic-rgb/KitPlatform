# KPA-RELEASE-RECOVERY-001 — NOVIXA end-to-end release recovery

**Date:** 2026-10-16  
**Decision:** **NOT READY — REMEDIATION COMPLETE**  
**Worktree:** `E:\KitPlatform-wt-phase5-novixa-product-proof`  
**Not modified:** `E:\KitPlatform`, `E:\KitPlatform-wt-video-mvp`, Architecture Freeze register, `origin/main`  
**Prior record:** `docs/novixa/03-operations/KPA-RELEASE-SAFETY-001-REPORT.md`

The admin deploy gate that blocked KPA-RELEASE-SAFETY-001 is closed on this worktree. The published API DLL and the SPA bundles below were built from that worktree. There is still no immutable commit for the recovery edits, no staging deploy, and no executed production rollback. A local build and a DEMO test are not a Xuân Hòa production release.

## 1. Baseline and scope

| Item | Value |
|---|---|
| HEAD | `b3ea8c8c3cc759d2c04d3a17400342d067c6c9d1` |
| Pharmacy runtime already in HEAD | `76e8177b134017164db5297ffa4b049be713d9d7` |
| Recovery edits | Uncommitted. Admin TypeScript, one staff-app smoke assertion message, this report, the safety report, the Phase 5 dossier note, and `NovixaReleaseSafetyBehaviorTests.cs`. |
| Candidate identity | Not an immutable SHA. The reproducible source is HEAD plus the uncommitted file list in section 4. It becomes a release candidate only after the owner commits that scoped set. |
| `origin/main` | `44fd508552ac47e4f2366aa0cfe1f523cc65be2c`. Not a fast-forward. Merge base `73d3c4a6`. |
| Local `main` | `8c59ceb4`, dirty. Not used. |
| Video MVP | `45d21801`. Kept out. **BLOCKED.** |

No C# file was edited in this recovery. The published `KitPlatform.Api.dll` hash matches the local Release compile recorded in the safety report.

Content stays inside the API binary. `KitPlatform.Api.csproj` project-references Content, Clinic, Survey, Connect, FamilyOS, Care, LocalOS, and Sales as well as Pharmacy. Removing Content from that binary would reopen the frozen architecture. Content SQL stays off `migration-files.prod.txt`.

## 2. Dependency map

Same map as the safety report, confirmed again by a Release publish of the API and by `scripts/deploy-production.ps1` build order:

`Core → Pharmacy Pack → API (also Content and the other pack references) → migration-files.prod.txt → admin and the other SPAs in the deploy script`

| Link | This recovery |
|---|---|
| Pharmacy sale rules | Unchanged in source. Re-tested. |
| Content in the API compile | Still required. Publish succeeded with Content in the graph. |
| Content in the admin bundle | Required by the deploy script. `npm run build` of admin is one command for the whole admin SPA, including pharmacy screens and content screens. The TypeScript failures were in that one command. |
| Content SQL | Not applied. Not added to the production manifest. |
| Video readers | Not required to compile or publish this API. Not fixed. |

## 3. Root-cause groups

The 173 admin errors were not 173 independent defects. After the first contract fixes, `tsc` reported 94, then 66, then 37, then 0.

| Group | Cause | Fix |
|---|---|---|
| Node operator scripts in the browser project | Six `kit-video-phase0*.ts` files use `process`, `node:fs`, and `node:crypto`. They are not imported by the SPA. `tsconfig` already excluded `*.smoke.ts`. | Excluded those six files in `client/admin/tsconfig.json`. SPA modules were not excluded. |
| Mix preferences | `SeriesPilotState` had no `mixPrefs`. `ContentFamixaStudioView` did not forward `onMixPrefs` even though the series desk and `ContentFamixaProdV2` already had the callback. | Added optional `mixPrefs` and forwarded the existing callback. |
| Runway attempt identity | `patchRunwayAttempt` spread `Partial<RunwayAttempt>` and dropped required `n` and `at`. | The patch keeps `n` and `at` from the previous attempt. |
| React 19 refs | `useRef()` with no argument is a type error. | Initial value supplied (`undefined` or `null`). |
| API error helper | `apiErrorMessage` required a second string. Content callers pass the error only. | Default fallback. Two-argument callers keep their own text. |
| Character lookup | `resolveCharacterByName` demanded a full record, so name/code lookups failed, and the full-record caller needs era and canon fields. | Generic over `{ characterCode, name }`. The return keeps the caller's element type. |
| Planned era status | The first element inferred `status` as only `'active'`. | Annotated `'active' \| 'planned'`. |
| Goods receipt lot pick | A spread copied optional dates into `expiryDate: string`. | Keeps the current required expiry string and fills from the pick when present. |
| Adjustment create button | `onClick={openCreate}` passed the click event into `(preset?) => void`. | `onClick={() => openCreate()}`. |
| Accepted take and Fal/mute stamps | Stored takes use `url` as optional. `stampFalFinal` / `stampMuteTake` inferred `finalSource: string` and a loose `runwayAttempts` array, which is not `RunwayAttempt[]`. | `AcceptedTake.url` is optional, matching `resolveTakeUrl`. Stamp functions return only the fields they write. `stampFalFinal.finalSource` is `'FAL'`. |
| Editorial timing | `{ ...(s.timing \|\| {}), ...timing }` made required `leadInSec` optional. | If the shot has no timing, it is left unchanged. Otherwise the preset overlays the existing `ShotTiming`. |
| Shot identity keys | `identityMutated` indexed camelCase hash names that are not properties of `ProductionShotSpec`. | `shotIdentityValue` reads `master_sha256`, `dna_sha256`, and `prp_sha256` for those keys. |
| Picture detach | `detachAvAfterPictureChange` required `MotionLifecycleRun` while the picture guard holds a `PictureIdentityRun`. | The function accepts `FinalSourceRun`, which is what `resolveTakeUrl` already reads. Picture identity uses the same optional take shape. |
| Staff readiness and studio regenerate | `missingTypes` and age/status values are `string \| null` from the API and `string \| undefined` in the helpers. | Helpers accept `null`. They already treat empty as missing. |
| Unused names | `noUnusedLocals` / `noUnusedParameters` failed the deploy `tsc`. | Removed unused imports and dead locals. `episodeDone` is bound on the series root because `kit-video-production-ux.smoke.ts` checks that the source still contains that name. `insertShot` uses `afterDisplay` only when `displayOrder` is not already positive; the phase-03 smoke shot has `displayOrder: 1.5`, so that path is unchanged. |
| Staff POS smoke | `assert` requires a message. Two calls passed only the condition. | Messages added. The conditions are the same. |

Video `ReadVeoTake` / `ReadFalTake` were not given a stub. The Phase 5 API already compiles those readers. The video line `45d21801` still has no writer for `{videoRoot}/veo|fal/{fileId}.mp4`. That remains outside this candidate.

## 4. Files changed

Uncommitted on `phase5-novixa-product-proof`. Do not mix these with the dirty `E:\KitPlatform` tree or with the video worktree.

Admin and staff sources listed by `git status` after the edits: `client/admin/tsconfig.json`, `client/admin/src/shared/api/api-error.ts`, `client/admin/src/modules/inventory/AdjustmentListPage.tsx`, `client/admin/src/modules/procurement/GoodsReceiptListPage.tsx`, the content modules in that status list, and `client/staff-app/src/modules/sales/pos-batch.smoke.ts`.

Docs and the earlier safety test remain uncommitted: this file, `KPA-RELEASE-SAFETY-001-REPORT.md`, `KPA-PHASE5-NOVIXA-PRODUCT-PROOF.md`, `tests/KitPlatform.Application.Tests/NovixaReleaseSafetyBehaviorTests.cs`.

No commit. No push.

## 5. Build and test

Commands were run in `E:\KitPlatform-wt-phase5-novixa-product-proof`. SPA `node_modules` were junctions to `E:\KitPlatform\client\<app>\node_modules` because each `package-lock.json` SHA-256 matched. Junctions are removed after the builds. They are not source.

| Level | Command | Result |
|---|---|---|
| API project | `dotnet build src\KitPlatform.Api\KitPlatform.Api.csproj -c Release` | **PASS.** Exit 0. 0 errors. Incremental log showed 22 warnings, including existing `NU1903` for `Newtonsoft.Json` 10.0.3. |
| API publish | `dotnet publish` same csproj `-c Release -o C:\Users\Admin\AppData\Local\Temp\kpa-rr001-20261016\api --no-self-contained` | **PASS.** Exit 0. New directory. Did not overwrite source or the previous local bin. |
| Application.Tests | Filter `NovixaReleaseSafetyBehaviorTests\|InventoryEngineFefoTests\|PricingEngineTests\|PharmacyConsultationOtcGateTests` | **PASS.** 18 passed, 0 failed. Exit 0. |
| Platform.Tests | Filter `ContentSeriesTakeNamedReadTests\|PharmacySalesOrderCompletedHandlerTests\|PharmacySalesReturnCompletedHandlerTests` | **PASS.** 6 passed, 0 failed. Exit 0. |
| Api.Tests integration | `ControlledPilotSaleTests` on local `kitplatform`, tenant `DEMO_PHARMACY` | **PASS.** 3 passed, 0 failed, about 3 seconds. Exit 0. Writes one local DEMO sale. Refuses `NT_XUANHOA`. Not a production proof. |
| Admin SPA | `npm run build` (`tsc --noEmit && vite build`) | **PASS.** Exit 0. Vite built in 28.23s. |
| customer-app | `npm run build` | **PASS.** Exit 0. |
| staff-app | `npm run build` | **PASS.** Exit 0 after the smoke assert messages. The first run exited 2. |
| prescriber-portal | `npm run build` | **PASS.** Exit 0. |
| partner-portal | `npm run build` | **PASS.** Exit 0. |
| assessment-web | `npm run build` | **PASS.** Exit 0. |
| family-app | `npm run build` | **PASS.** Exit 0. |
| Deploy script end to end | `deploy-production.ps1` / `deploy-update-vps.ps1` | **NOT RUN.** Those scripts upload and can migrate. The build steps inside the script were run individually. |
| Video Content infrastructure | Not rebuilt | Still **BLOCKED** as recorded in the safety report. |

This table is a local compile and a local DEMO integration run. It is not a staging deploy and not a production smoke.

## 6. Artifact SHA-256

Output directory: `C:\Users\Admin\AppData\Local\Temp\kpa-rr001-20261016`. SPA `dist` trees were copied in after each `npm run build`. The directory is outside the git worktree.

| File | SHA-256 | Bytes |
|---|---|---|
| `api\KitPlatform.Api.dll` | `E9A5CC26DB670BFD89E47662210208E2A8E616D4FBAE472D46086D31F7027E0D` | 1808896 |
| `admin\index.html` | `29D24781CBCF9B530DA29E8F171CB522877ADFFA1604DE22399ECE8DB22D1D0C` | 1058 |
| `customer-app\index.html` | `C02C7AA0844A1681E741B266994307287DBA7C108D13BFC7A256B3F8FFB60D71` | 1515 |
| `staff-app\index.html` | `24BE0431B90F723E4D4C1B661C2AD4A817232E7350657A5F065967CA98B0C691` | 1099 |
| `prescriber-portal\index.html` | `6171210F28DD88D6C8ACEFC5EC4E16A214B82B32E0E719152B3927AF11D01E2B` | 777 |
| `partner-portal\index.html` | `1B529D92A7E1D9A98C0CC190D677565A9DC531C780029ECBFA9C411D5318918C` | 713 |
| `assessment-web\index.html` | `D24F6EBE7B067C2A65D3968FE3568A7549761DF3775ECD5899D6369E965B8085` | 660 |
| `family-app\index.html` | `18284E23559802AC5BACC1D7685E63C8A27F7463248735903106486595003F7A` | 1185 |

The DLL hash equals the safety-report local compile because pharmacy and API C# did not change. The admin hash is new. The live DLL recorded in Phase 5 §12 (`26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`, mtime 2026-09-27) was not re-read.

## 7. Business verification

No permission, discount, FEFO, expired-lot, consultation, or tenant-isolation C# path was edited. The re-run above is the evidence that those paths still pass on this tree:

- Discount policy and the dashboard analytics predicate: `NovixaReleaseSafetyBehaviorTests`. The dashboard assertion still restates the predicate; it does not execute `DashboardService`.
- FEFO and expired lots: `InventoryEngineFefoTests` and `SellableLotRules` inside the safety behavior tests.
- Consultation OTC gate: `PharmacyConsultationOtcGateTests`.
- Completed sale and return handlers: the two Platform.Tests pharmacy handlers.
- Local DEMO login, tenant `DEMO_PHARMACY`, expired-lot refusal, and FEFO sale: `ControlledPilotSaleTests`.

Admin fixes that touch pharmacy screens are type-level: the adjustment button no longer feeds a click event into the create preset, and a goods-receipt lot pick no longer drops a required expiry string. They were not exercised in a browser. Staff POS lot visibility checks are the two assertions that already existed; only the missing assert messages were added.

Report HTTP authorization in a browser, a draft-order database round trip, a downed database, and any Xuân Hòa production login were **not** run.

## 8. Migration, staging, rollback

Unchanged from the safety report. Production runner remains `migration-files.prod.txt`. This recovery adds no SQL. Content files `383`, `384`, `387`, and `388` stay off that manifest. Do not pass `-RunMigrations`.

No permitted staging host was available. None was created. Production was not deployed, restarted, migrated, or written. Binary rollback was not executed.

**Rollback status: not VERIFIED.**

The local 387 `ROLLBACK` from the safety session remains evidence only for an uncommitted content DDL transaction with no owned rows. It is not a production rollback.

Before any future deploy, read the live host again: DLL hash, service `kit-platform-api`, catalog mode, stock-out sync, `NT_XUANHOA` tenant link, and migration ledger. The §12 snapshot is not the current live state.

## 9. Remaining blockers

1. The recovery source is uncommitted. The temp publish is not a commit-identified artifact.
2. The candidate is not `origin/main`. Shipping it still replaces the September binary with a tree that compiles Content and the other packs into the API.
3. No staging deploy and no executed rollback. Live DLL, catalog mode, stock-out sync, and the migration ledger were not re-read.
4. Video `ReadVeoTake` / `ReadFalTake` on `45d21801` stay blocked. No writer for that file layout was verified. The dirty video worktree was not edited.

## 10. Owner approval before any deploy

1. Commit only the scoped recovery files on `phase5-novixa-product-proof`. Do not include `E:\KitPlatform` or the video worktree. Record the new SHA and rebuild the publish directory from that commit before upload. The hashes above are for the uncommitted tree.
2. Accept that Content and the other API project references stay in the binary. That is the frozen project graph, not a new split.
3. Confirm `migration-files.content.txt` will not be the manifest, and that `-RunMigrations` stays off.
4. Provide a non-production host that already has the current binary and config. Deploy this candidate there, smoke health, database, admin, and POS without creating a sale and without calling an AI provider, restore the previous binary and config, and record both DLL hashes. Rollback stays unverified until that restore is done.
5. Immediately before a production upload, re-read the live DLL hash, service state, catalog mode, stock-out sync, tenant link, and migration ledger. Stop if they differ from the accepted snapshot.
6. Leave video closure as a separate decision.
