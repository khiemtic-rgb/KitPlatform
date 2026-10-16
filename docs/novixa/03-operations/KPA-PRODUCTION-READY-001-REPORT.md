# KPA-PRODUCTION-READY-001 — NOVIXA production readiness

**Date:** 2026-10-16  
**Decision:** **NOT READY**  
**Worktree:** `E:\KitPlatform-wt-phase5-novixa-product-proof`  
**Not modified:** `E:\KitPlatform`, `E:\KitPlatform-wt-video-mvp`, Architecture Freeze register, `origin/main`, production binaries, production data  
**Prior records:** `KPA-RELEASE-SAFETY-001-REPORT.md`, `KPA-RELEASE-RECOVERY-001-REPORT.md`

A green local build is not a production release. This session made the recovery source immutable, rebuilt the artifacts from that commit, re-ran the required tests, and re-read the live host. It did not deploy, and it did not execute a rollback.

## 1. Release baseline and commit SHA

| Item | Value |
|---|---|
| Candidate | `643932c0ba35cac0cad069a789a7197671f4ba24` |
| Subject | `fix(novixa): make the Phase 5 candidate build from an immutable tree` |
| Branch | `phase5-novixa-product-proof`. Not pushed. |
| Parent | `b3ea8c8c3cc759d2c04d3a17400342d067c6c9d1` |
| Pharmacy runtime | `76e8177b134017164db5297ffa4b049be713d9d7`. No Pharmacy or API C# diff from that commit to the candidate. |
| `origin/main` | `44fd508552ac47e4f2366aa0cfe1f523cc65be2c`. Not a fast-forward. Merge base `73d3c4a6`. The candidate is 14 commits ahead of that base. |
| Video MVP | `45d21801` on `kit-mkt-video-mvp`. Separate line. **BLOCKED.** Not in this candidate. |
| Artifact source | Clean tree at `643932c0` before this report. The publish directory is outside the git worktree. |

The earlier hashes in the recovery report belong to the uncommitted tree. They are not the release artifact.

Dependency that actually ships:

`Core → Pharmacy Pack → API (project references also pull Content, Clinic, Survey, Connect, FamilyOS, Care, LocalOS, and Sales) → migration-files.prod.txt → admin and the other SPAs named in scripts/deploy-production.ps1`

Content stays inside the API binary because `KitPlatform.Api.csproj` references it. Content SQL stays on `migration-files.content.txt` only. Files `383`, `384`, `387`, and `388` are not in `migration-files.prod.txt`. Family files `230` and `231` are also absent from that manifest. This candidate adds no SQL.

## 2. Work completed and files changed

Commit `643932c0` records the recovery that was previously uncommitted. 38 files. No Pharmacy sale-rule source changed.

Pharmacy and shared admin surfaces:

- `client/admin/src/modules/inventory/AdjustmentListPage.tsx` — the create button no longer passes the click event into `openCreate`.
- `client/admin/src/modules/procurement/GoodsReceiptListPage.tsx` — a lot pick keeps a required expiry string.
- `client/admin/src/shared/api/api-error.ts` — fallback message so one-argument callers compile.
- `client/admin/tsconfig.json` — six Node operator scripts excluded from the browser project. SPA modules were not excluded.
- `client/staff-app/src/modules/sales/pos-batch.smoke.ts` — assert messages only. The conditions are unchanged.
- `tests/KitPlatform.Application.Tests/NovixaReleaseSafetyBehaviorTests.cs` — discount, report-permission predicate, and sellable-lot rules.

The other files in that commit are admin Content TypeScript fixes required by the single admin `tsc`, plus the two earlier operations reports and the Phase 5 dossier note. Those Content edits make the admin bundle compile. They do not add a Video Production writer, and they do not change counter-sale C#.

`E:\KitPlatform` and the video worktree were not edited. No merge. No push.

## 3. Build and test results

Commands ran in `E:\KitPlatform-wt-phase5-novixa-product-proof` at `643932c0`. SPA `node_modules` were junctions to `E:\KitPlatform\client\<app>\node_modules` because each `package-lock.json` SHA-256 matched. The junctions were removed after the builds. They are not source.

| Level | Command | Result |
|---|---|---|
| API publish | `dotnet publish src\KitPlatform.Api\KitPlatform.Api.csproj -c Release -o <temp>\api --no-self-contained` | **PASS.** Exit 0. Existing `NU1903` warning for `Newtonsoft.Json` 10.0.3. Existing nullable warnings in `Program.cs`. |
| Application.Tests | `NovixaReleaseSafetyBehaviorTests\|InventoryEngineFefoTests\|PricingEngineTests\|PharmacyConsultationOtcGateTests` | **PASS.** 18 passed, 0 failed. |
| Platform.Tests | `ContentSeriesTakeNamedReadTests\|PharmacySalesOrderCompletedHandlerTests\|PharmacySalesReturnCompletedHandlerTests` | **PASS.** 6 passed, 0 failed. |
| Api.Tests | `ControlledPilotSaleTests` on local `localhost` / `kitplatform`, tenant `DEMO_PHARMACY` | **PASS.** 3 passed, 0 failed, about 6 seconds. Writes one local DEMO sale. Refuses `NT_XUANHOA`. |
| Admin, customer-app, staff-app, prescriber-portal, partner-portal, assessment-web, family-app | `npm run build` with `VITE_API_BASE_URL=https://api.novixa.vn` | **PASS.** Exit 0 for each app. |
| `deploy-production.ps1` / `deploy-update-vps.ps1` | Upload and possible migration | **NOT RUN.** |

Business paths re-checked on this commit:

- Discount policy and the report-permission predicate: `NovixaReleaseSafetyBehaviorTests`. The dashboard assertion restates the predicate. It does not execute `DashboardService`.
- FEFO and expired lots: `InventoryEngineFefoTests` and `SellableLotRules`.
- Consultation OTC gate: `PharmacyConsultationOtcGateTests`.
- Completed sale and return handlers: Platform.Tests.
- Local DEMO login, tenant isolation, expired-lot refusal, and FEFO sale: `ControlledPilotSaleTests`.

Not run: report HTTP authorization in a browser, a draft-order database round trip, a downed database, and any Xuân Hòa login or sale. The adjustment button and the goods-receipt lot pick were not clicked in a browser. `DEMO_PHARMACY` does not verify Xuân Hòa.

Checked-in `appsettings.Production.json` still says `NationalDrugCatalog:Mode=mock` and `EnableStockOutSync=false`. The live process environment overrides the mode. A future deploy must not replace those environment keys.

The API project does not set a deterministic build. The checksum below identifies this publish. A later compile of the same SHA can differ by byte even when the source is unchanged. Record the hash of the directory that is actually uploaded.

## 4. Artifact checksums

Directory: `C:\Users\Admin\AppData\Local\Temp\kpa-prodready-643932c0`. Built from clean `643932c0`. SPA bundles use API base `https://api.novixa.vn`.

| File | SHA-256 | Bytes |
|---|---|---|
| `api\KitPlatform.Api.dll` | `41C921CBD7A66ADAC40168E93D2CD8B902A518410F4529833504D200E417201E` | 1808896 |
| `api\KitPlatform.Packs.Pharmacy.Application.dll` | `B4F1E220818264A1AD7CCA9BB8E4765F2087A0CDD7C29B41B6553D05BBC5B24C` | 833024 |
| `api\KitPlatform.Packs.Pharmacy.Infrastructure.dll` | `509F54EE0078DB9F148826F0ECB06E1255FDD4374A2C8F9A759E90F1BAB5A4CB` | 2331136 |
| `api\KitPlatform.Packs.Content.Infrastructure.dll` | `F80B734D45D19DCE0F7FBBC31F7B5C655A83C8C20FD325276DD418FEAFDC8283` | 3375616 |
| `admin\index.html` | `4B1BCC9511D0787CFAAF8EF83F5D443C02DC2F6AD4B7C12B0D6395228213DE6E` | 1058 |
| `customer-app\index.html` | `749BCD01906782911225330155959F81B981102406C91AFB6DE238B1C811EED6` | 1515 |
| `staff-app\index.html` | `8D626A0CE41E3346A36759CF1B0674F34CBA590EF15E525844A4E4FC546AAD88` | 1099 |
| `prescriber-portal\index.html` | `9AD5709161AB9ABB17719B7F18660D183DA38ADA5C92C76D33BF20E1A23FE0B0` | 777 |
| `partner-portal\index.html` | `6A4CBF1A37D3AAFB9C1D9107A0010A4C81338A3677D509183D147B69428968AB` | 713 |
| `assessment-web\index.html` | `D24F6EBE7B067C2A65D3968FE3568A7549761DF3775ECD5899D6369E965B8085` | 660 |
| `family-app\index.html` | `BA3507D2B173E0F3C5F2C9387D69A1EC7552637497D7B7FD871F2B6A80C047D5` | 1185 |

## 5. Deployment validation

**Not executed. BLOCKED.**

Every deploy script in this tree defaults to `root@103.200.23.229`. That address is `api.novixa.vn`, `admin.novixa.vn`, `pos.novixa.vn`, and `family.kittech.vn`. It is the production host. It was not used as a drill.

`uat-api.novixa.vn` resolves to Cloudflare and returned HTTP 522. No script deploys there. No other staging host is named. None was created.

Public read of the current production process, not a smoke of this candidate:

| Check | Result |
|---|---|
| `GET https://api.novixa.vn/api/health` | 200, `{"status":"ok"}` |
| `GET https://api.novixa.vn/api/health/db` | 200, `database=true` |
| `GET /api/sales/pos/consultation/symptom-options` with no token | 401 |
| Admin and POS index | 200 HTML. Last-Modified 2026-09-27. |

Read-only host inspection in this session. No restart, no upload, no migration, no data write:

| Check | Result |
|---|---|
| `kit-platform-api` | `active`. Current start `2026-09-29 21:00:52 +07`. `pharmacore-api` inactive. |
| Live DLL | `/var/www/kit-platform/api/KitPlatform.Api.dll`, 1,786,368 bytes, mtime `2026-09-27 11:47:15 +07`, SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`. |
| Previous release directory | `/var/www/kit-platform.prev` absent. `/opt/kit-platform/releases` absent. |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `NationalDrugCatalog__Mode` | `sandbox` |
| `NationalDrugCatalog__EnableStockOutSync` | `false` |
| Database | `novixa_prod` |
| Migration ledger | 232 rows. Present: `230`, `231`, and `294` through `300`. Absent from the same lookup: `383`, `384`, `387`, `388`. |
| `NT_XUANHOA` batch mode | `suggest` |
| Tenant link | 0 rows in `tenant_csdl_duoc_link`. 0 of those have stock-out sync enabled. |
| Stock-out log | 0 rows in `csdl_duoc_sync_log` for `NT_XUANHOA`. |
| Open shifts | 2 |
| Orders | status 2 (completed) 6,436. Status 4 (refunded) 8. No draft or cancelled row in this grouping. §12.1 had 6,411 completed at 10:12 +07. The counter kept selling. |
| Consultations | 0 sessions for `NT_XUANHOA`. |
| On-hand lots | 3,094 batches with quantity. 3,091 in-date. 3 expired. 0 undated. |

The connection string user in `api.env` did not authenticate to PostgreSQL. The ledger and counts above were read with local peer authentication as `postgres`. No secret value was printed.

## 6. Rollback evidence

**Not VERIFIED. BLOCKED.**

No non-production host holds the current binary, so the candidate was not installed and the previous binary was not restored. The live host has no previous-release directory. `deploy-update-vps.ps1` overwrites the live tree and does not keep the previous binaries. The local content-migration `ROLLBACK` from the safety report remains evidence only for an uncommitted DDL transaction. It is not a production rollback.

A future rollback, after a snapshot has been taken and once restored on a non-production host, is still: stop `kit-platform-api`, copy the snapshot back over `api` without deleting `uploads`, copy the UI trees back, start the service, and confirm DLL `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941` plus both health URLs. This candidate has no down migration. A database restore is a separate decision because it discards sales written after the dump.

## 7. Conditions still missing

1. A non-production host that already runs the current binary and configuration. `uat-api.novixa.vn` is not that host while it returns 522, and production must not be the drill.
2. One executed staging deploy of `643932c0`, a smoke that does not create a sale and does not call an AI provider, then a real restore of the previous binary and config, with both DLL hashes recorded.
3. Owner acceptance that this candidate is not `origin/main` and that Content and the other API project references stay in the binary.
4. Confirmation that the upload will not pass `-RunMigrations` and will not use `migration-files.content.txt`.
5. A fresh code snapshot and a gzip-valid database dump taken immediately before any future production upload. The host does not keep the previous tree.
6. Another live read immediately before that upload. Stop if the DLL is no longer `26afdaa7…` without an accepted successor, if catalog mode is `live`, if stock-out sync is true, if a `NT_XUANHOA` tenant-link row has sync enabled, or if content migrations `383`–`388` appear in the ledger.
7. A quiet window. This read still found 2 open shifts and a completed-order count that has moved since the morning.
8. Video `ReadVeoTake` / `ReadFalTake` on `45d21801` stay blocked. That closure is a separate decision and is not required to compile this API.

## 8. Decision and next plan

**NOT READY.**

The candidate is identified, the required local builds and tests passed, the production migration manifest does not contain the content files, and the live stop-conditions that were re-read still match the safe side: sandbox catalog, stock-out sync off, no Xuân Hòa tenant link, September DLL still running. Deployment validation and rollback were not done. Those two gates are blocked until a permitted non-production host exists. This session did not lower the bar.

Controlled path for a later production approval:

1. Keep the binary identity at `643932c0` unless a later commit changes compiled source. This report does not change C# or TypeScript. Rebuild from the Phase 5 worktree. Do not build from `E:\KitPlatform`.
2. Do not merge or push `origin/main` as part of this release until the owner accepts the divergence.
3. Obtain a staging host that is not `103.200.23.229`. Deploy this candidate there with `-RunMigrations` off. Smoke health, database, unauthenticated consultation route 401, and the admin and POS index pages. Create no sale.
4. Restore the previous binary and configuration on that host. Record both DLL hashes and both health results. Rollback stays unverified until that restore succeeds.
5. In a quiet hour, re-read the live facts in section 5. Take a code snapshot and a real database dump. Upload only if those facts still match and the owner has accepted the candidate. Pass `-Root` as this worktree. Do not pass `-RunMigrations`.
6. After upload, repeat the smoke without a sale. Confirm mode `sandbox`, stock-out sync false, and zero tenant-link rows. If smoke fails, restore the snapshot. Do not restore the database dump over a day of trade unless the owner accepts that loss.
7. Leave video closure outside this release.
