# KPA-PHASE5 — NOVIXA Product Proof

**Date:** 2026-10-15  
**Release-safety decision (2026-10-16, KPA-RELEASE-SAFETY-001):** **NOT READY**. §12 stays the earlier read. It is not the current release decision. See §13 and `docs/novixa/03-operations/KPA-RELEASE-SAFETY-001-REPORT.md`.  
**Recovery decision (2026-10-16, KPA-RELEASE-RECOVERY-001):** **NOT READY — REMEDIATION COMPLETE**. See §14 and `docs/novixa/03-operations/KPA-RELEASE-RECOVERY-001-REPORT.md`.  
**Production-readiness decision (2026-10-16, KPA-PRODUCTION-READY-001):** **NOT READY**. The candidate SHA is `643932c0ba35cac0cad069a789a7197671f4ba24`. Staging deployment and rollback were not executed. See §15 and `docs/novixa/03-operations/KPA-PRODUCTION-READY-001-REPORT.md`.  
**Go-live decision recorded in §12 (Nhà thuốc Xuân Hòa):** 2026-10-16 — **READY FOR OWNER DEPLOY**. That call is superseded by §13. §11 remains the first preflight, which stopped before host and database access.  
**Controlled pilot decision:** 2026-10-16 — **CONTROLLED PILOT READY** on this branch only. That local result is not a Xuân Hòa go-live. See §10.  
**Pilot data validation:** 2026-10-16 morning — **PARTIAL** (kept as the record in §9). §10 closes the expired-lot and read-only gaps that §9 left open.  
**Branch:** `phase5-novixa-product-proof`  
**Worktree:** `E:\KitPlatform-wt-phase5-novixa-product-proof`  
**Parent (Phase 4 freeze):** `0c4adc87eb834d2b79c0207dd48fc08f77b71754`  
**Phase 4 branch:** `phase4-architecture-freeze` left at that same commit  
**Scope:** one pharmacy workflow. No push, merge, or deploy.

## 1. Selected workflow

**Counter consultation that becomes a paid, stock-reducing sale.**

A pharmacist records a walk-in complaint, gets a safety result, sees only in-stock OTC products when the case is suitable for counter advice, saves the consultation, sells one suggested unit on the open shift, and leaves the consultation linked to that completed order.

This is the shortest path that is already built and still shows a recurring pharmacy result: the complaint, the customer, the product on hand, and the sale stay one record. It does not need a new Core primitive, a router, or a module-gate change. Inventory receiving, national drug sync, and financial reports stay outside this proof.

Local evidence before the run: `DEMO_PHARMACY` has the consultation tables, 190 symptom rows, and in-stock OTC in `HO_HAP` and `GIAM_DAU`. The local `NT_XUANHOA` row has **zero** consultation sessions. Xuân Hòa is not treated as a current deployment of this workflow.

## 2. User-visible business result

On POS, staff with `sales.read` can analyze a complaint. Staff with `sales.pos` can save the session and link it when the sale completes.

For an adult with cough and sore throat, no fever, and no red flags:

- Safety level is `none`.
- Preliminary assessment is `likely` (mild upper respiratory).
- The counter offers in-stock OTC suggestions and can add one to the cart.
- Checkout creates a completed sale, reduces on-hand quantity by the quantity sold, and sets the consultation to `linked`.

For a seizure red flag, or for fever in a child under two, the counter and the suggest API return **no** automatic OTC list. Pregnant-adult cases stay `refer_pharmacist` and may still show suggestions for the pharmacist to accept or reject.

An unauthenticated save is rejected.

## 3. Implementation

The suggest API previously returned sellable products for `refer_medical`, including infant fever, while the counter already hid that list. The server now uses the same stop rule as the counter for `stop_sale` and `refer_medical`. `refer_pharmacist` and `caution` are unchanged.

| File | Change |
|---|---|
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Consultation/ConsultationSafetyRules.cs` | Named gate and block message |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Consultation/PharmacyConsultationService.cs` | Suggest returns no products when the gate blocks |
| `client/admin/src/modules/sales/consultation-otc-gate.ts` | Shared counter gate |
| `client/admin/src/modules/sales/PosConsultationModal.tsx` | Counter uses that gate |
| `client/admin/src/modules/sales/consultation-otc-gate.smoke.ts` | Counter boundary smoke |
| `tests/KitPlatform.Application.Tests/PharmacyConsultationOtcGateTests.cs` | Safety, assessment, and OTC-rule tests |

No migration. No RBAC policy edit. No Master Architecture, ADR, GOVERNANCE, or INDEX edit.

## 4. Validation

| Check | Result |
|---|---|
| `dotnet build` `KitPlatform.Api` | Succeeded. 0 errors. Existing nullable warnings in `Program.cs` and the known `Newtonsoft.Json` 10.0.3 advisory. |
| `dotnet test` filter `PharmacyConsultationOtcGateTests` | Passed 4, failed 0. |
| `consultation-otc-gate.smoke.ts` | OK |
| `consultation-customer-profile.smoke.ts` | OK |
| `checkout-symptom-chips.smoke.ts` | OK |

Admin `tsc` was not a clean worktree build: this worktree has no `node_modules`. A typecheck that borrowed another tree's compiler reported existing Content/Famixa errors. None of those errors are in the sales files changed here, and they do not affect this pharmacy workflow.

## 5. Isolated evidence

API from this worktree on `127.0.0.1:5295`, Development, platform-event dispatch off, content worker off, national drug stock-out sync forced off. Database: local `kitplatform`, tenant **`DEMO_PHARMACY` only**.

Synthetic customer `KH016`. No real patient data.

| Step | Observed |
|---|---|
| `POST /api/sales/pos/consultation/sessions` without a token | 401 |
| Pharmacist login permissions | `sales.pos` and `sales.read` present. `reports.read` absent. |
| Extract “ho và đau họng 2 ngày, không sốt” | `safety=none`, `source=local_fast`, assessment `likely` |
| Suggest on `WH_MAIN` | 3 in-stock OTC rows, not blocked |
| Suggest with red flag `seizure` | `stop_sale`, blocked, 0 products |
| Suggest fever, age 1 year | `refer_medical`, blocked, 0 products |
| Confirm session | `confirmed` |
| FEFO preview for `DECK006`, qty 1 | batch `DECK-LOT-0006`, expiry `2026-07-15` |
| `POST /api/sales/orders` cash | `SO-000746`, status completed (`2`), total 36,000, paid 36,000, outstanding 0 |
| Sold line | same batch and expiry as the preview |
| Link session | status `linked` |
| On-hand for that lookup | 344 → 343 |
| SQL after the call | session and order both on `DEMO_PHARMACY`; `NT_XUANHOA` session count remains 0 |

The existing open shift on `WH_MAIN` was reused. This proof did not open or close a shift.

`DECK006` on this local DEMO warehouse has one on-hand lot, and that lot’s expiry is already past (`2026-07-15`). FEFO allocated it because no later lot existed. A pilot sale should use a product whose lot expiry is still in the future. Changing global expiry blocking was not part of this change.

## 6. Pilot validation

Run on `DEMO_PHARMACY` (or another explicitly approved non-production tenant). Use a synthetic customer. Do not use Xuân Hòa data until a separate authorization.

1. Sign in as a counter user who has `sales.pos` (analyze also needs `sales.read`; the current SalesRead policy already includes `sales.pos`).
2. Open POS, select the selling warehouse, and confirm a shift is open.
3. Create or select a synthetic customer. Open **Tư vấn tại quầy**.
4. Record cough and sore throat for an adult, no fever, no danger signs. Analyze.
5. Expect a mild respiratory assessment and at least one OTC line with stock greater than zero and a future expiry. Add one unit to the cart.
6. Save the consultation. The POS chip shows the saved session.
7. Take cash payment for the line total and complete the sale.
8. Expect a completed order, on-hand reduced by 1, and the consultation status `linked` to that order id.
9. Repeat analyze for “co giật” or for fever in a child under 2. Expect no OTC list and no sale from the suggestion panel.
10. Sign in as a user who only has `sales.read` if one exists. Analyze may succeed. Saving a session and creating the sale must be denied.

**Success measures**

- One completed order for the suggested unit, payment covering the total, outstanding 0.
- The order line’s batch is the FEFO preview batch, and that batch is not past expiry.
- On-hand for that unit decreases by the sold quantity.
- The consultation row for that tenant is `linked` to the order.
- `stop_sale` and `refer_medical` return zero suggestions.
- A caller without `sales.pos` cannot save or link.
- `reports.read` is not granted by `sales.pos` or `sales.read`.

## 7. Git baseline

- Isolated branch `phase5-novixa-product-proof` was created from `0c4adc87eb834d2b79c0207dd48fc08f77b71754`.
- `phase4-architecture-freeze` was not moved.
- The dirty `E:\KitPlatform` tree was not used as product evidence and was not modified by this work.
- This branch was not pushed or merged.

## 8. Still requires explicit authorization

- Deploying this branch, or running it against production.
- Any write, migration, or pilot script on `NT_XUANHOA`.
- Turning national drug stock-out sync back on for a live sale.
- A product decision to refuse sale of already-expired lots inside FEFO. This proof did not change that allocator.

## 9. Pilot data validation (2026-10-16)

Technical result for one synthetic `DEMO_PHARMACY` sale: the consultation, the in-date lot, the completed order, the stock decrease, and the session link all match. Pilot readiness for Nhà thuốc Xuân Hòa remains **not ready**. No code, schema, or FEFO rule was changed in this pass.

### 9.1 Environment

| Check | Result |
|---|---|
| Worktree | `E:\KitPlatform-wt-phase5-novixa-product-proof` |
| Branch / HEAD at start | `phase5-novixa-product-proof` / `bf5dc6439dd06a509f79f5991134f464869ecd8f` |
| Parent | `0c4adc87eb834d2b79c0207dd48fc08f77b71754` |
| Database | local PostgreSQL, `inet_server_addr()` `127.0.0.1`, port 5432, database `kitplatform` |
| Tenant used | `DEMO_PHARMACY` (`11111111-1111-1111-1111-111111111101`) |
| `NT_XUANHOA` consultation sessions | 0 before and 0 after. No business rows from that tenant were read or written for the sale. |
| Dirty `E:\KitPlatform` tree | Not used. |

The full API host was not started. `pack_local.watch_run` had no scheduled finish on 2026-10-16, and `LocalOsWatchWorker` runs after 08:00 Vietnam time and can publish outside this tenant. The sale was executed through the phase 5 API assembly with every `IHostedService` removed. Platform event dispatch, national drug stock-out sync, the content worker, family reminders, customer push, the integration outbox, and the notify queue were set off. `csdl_duoc_sync_log` has 0 rows for this order. The latest Local OS watch start stayed `2026-10-15 23:18:26+07`.

No new synthetic lot was inserted. Existing `DEMO_PHARMACY` stock was used. Expiry dates were not edited.

### 9.2 Transaction

Customer `KH016` (synthetic walk-in). Warehouse `WH_MAIN`. Existing open shift `SH-000004` was reused. Complaint text: `ho và đau họng 2 ngày, không sốt`. Confirmed age 34 when the extract did not supply an age.

| Step | Observed |
|---|---|
| Confirm without a token | 401 |
| Pharmacist on `DEMO_PHARMACY` | `sales.pos` and `sales.read` present. `reports.read` absent. |
| Extract | `safety=none`, `source=local_fast`, assessment `likely` |
| Suggest, limit 12 | 12 OTC rows, each with stock greater than 0. Not blocked. |
| Seizure red flag | `stop_sale`, blocked, 0 products |
| Fever, age 1 year | `refer_medical`, blocked, 0 products |
| Pregnant adult with cough | `refer_pharmacist`, not blocked, 6 products. This path was not changed. |
| Confirm | session `01a14253-2433-78b3-914b-8ec113a0b776`, status `confirmed` |
| FEFO preview for `PARA500`, qty 1 | batch `5555`, expiry `2026-11-30` |
| Cash sale | `SO-000747`, status completed (`2`), total 5,000, paid 5,000, outstanding 0 |
| Sold line | `PARA500`, batch `5555`, expiry `2026-11-30`, qty 1, `batch_source` 1 (FEFO auto) |
| Link | status `linked` to `49f43e88-3c68-45f1-a7a1-47b2b79482bb` |
| Both rows | tenant `DEMO_PHARMACY` |

`PARA500` is Paracetamol 500mg, OTC, category `GIAM_DAU`, retail price 5,000. It was inside the suggestion list.

### 9.3 FEFO

On-hand lots for `PARA500` at `WH_MAIN` before the sale, ordered as the allocator orders them:

| Batch | Expiry | Qty before | Qty after |
|---|---|---:|---:|
| `5555` | 2026-11-30 | 49 | 48 |
| `LOT2026A` | 2028-12-31 | 2500 | 2500 |
| `LOT2027B` | 2029-06-30 | 1800 | 1800 |
| `33333` | none | 499 | 499 |

Three lots were unexpired and in stock. Preview and the completed line both took the earliest, `5555`. The later dated lots and the undated lot did not change. That is FEFO order among the unexpired lots, plus null expiry sorted last.

`PARA500` had no expired lot with stock. This run does **not** prove that an expired lot is refused. The suggest list also included `DECK001` (only on-hand expiry 2026-07-10) and `DECK006` (only on-hand expiry 2026-07-15). Both are OTC with stock and both are past 2026-10-16. They were not sold. Suggestion search still keys off stock, not expiry. The allocator was not changed.

### 9.4 Checks

| Check | Result |
|---|---|
| `dotnet build` `src/KitPlatform.Api/KitPlatform.Api.csproj` | Succeeded. 0 errors. Existing nullable warnings in `Program.cs` and the known `Newtonsoft.Json` 10.0.3 advisory. |
| `dotnet test` filter `PharmacyConsultationOtcGateTests` | Passed 4, failed 0. |
| `consultation-otc-gate.smoke.ts` | OK |
| `consultation-customer-profile.smoke.ts` | OK |
| `checkout-symptom-chips.smoke.ts` | OK |
| In-process DEMO sale | Passed once. Order `SO-000747`. |

This worktree has no `node_modules`. The three smoke files were run with the TypeScript runner already present under the main admin tree. They import only the local sales modules.

A first auth probe confirmed a session as `quay_smoke` and was deleted before the sale: session `01a14252-16f9-7571-b3a4-30ca01730b47`, status `confirmed`, no order. `quay_smoke` has `sales.read` and `sales.write`. The existing POS policy allows `sales.write`, so that 200 response is the current rule. `DEMO_PHARMACY` has no user who has `sales.read` without `sales.pos` or `sales.write`. A live denial for a read-only caller is **NOT VERIFIED**. Unauthenticated save remains 401.

### 9.5 Reproduce

1. Use this worktree. Do not use the dirty `E:\KitPlatform` tree.
2. Confirm PostgreSQL on `127.0.0.1:5432`, database `kitplatform`, and that the sale tenant is `DEMO_PHARMACY`.
3. Before starting the normal API host, read `pack_local.watch_run`. If no scheduled run has finished on the current Vietnam date and the clock is at or after 08:00, do not start the host: the Local OS watch can publish outside this test. Run the HTTP calls with background workers disabled, and force national stock-out sync off.
4. Sign in as the DEMO pharmacist. Send `POST /api/sales/pos/consultation/sessions` with no token and expect 401.
5. Extract `ho và đau họng 2 ngày, không sốt`. Expect `safety=none`.
6. Suggest on `WH_MAIN`. Choose an OTC line whose earliest in-stock expiry is still in the future. On this database that line was `PARA500`, not the first suggestion.
7. Repeat suggest with red flag `seizure`, and with fever at age 1. Expect zero products.
8. Suggest cough for a pregnant adult. Expect `refer_pharmacist` and a non-blocked list.
9. Confirm the adult session, preview allocation for quantity 1, complete a cash sale on the open `WH_MAIN` shift, then link the session.
10. Compare preview batch, order line, and `inventory_batches.quantity_available` for every on-hand lot of that product.

### 9.6 Acceptance

| Criterion | Result |
|---|---|
| Local environment and `DEMO_PHARMACY` | Verified |
| Sold lot still in date | Verified (`5555`, 2026-11-30) |
| Consult, confirm, sell, stock decrease, link | Verified (`SO-000747`) |
| Stock and order match | Verified, 49 to 48 on the sold lot only |
| Safety behavior | Verified for `none`, `stop_sale`, `refer_medical`, and unchanged `refer_pharmacist` |
| Permission behavior | 401 verified. Pharmacist lacks `reports.read`. Live read-only denial **NOT VERIFIED** |
| FEFO | Verified for three unexpired lots. Expired-lot refusal **NOT VERIFIED** |
| Build, gate, related smoke | Pass |
| Reproduction steps | In §9.5 |
| No production write and no `NT_XUANHOA` business write | Verified for this run |

### 9.7 Separate conclusions

**Technical validation:** one valid counter consultation on local `DEMO_PHARMACY` sold one unit of in-date `PARA500` by FEFO, reduced that lot by 1, and linked the session to completed order `SO-000747`.

**Pilot readiness for Nhà thuốc Xuân Hòa:** not ready. This branch is not deployed. Xuân Hòa data was not used. The suggestion list still offers OTC products whose only on-hand lots are already expired, and FEFO still does not contain an expiry refusal. Those rules were left as they are. A read-only save denial was not exercised on a live user. Deploy, national stock-out sync, and any write on `NT_XUANHOA` still need a separate approval.

§9.7 is the morning record. The decision that replaces it is §10.

## 10. Controlled pilot decision

**Decision: CONTROLLED PILOT READY.**

This is a local decision for the counter consultation sale on branch `phase5-novixa-product-proof`. It means the owner can authorize a controlled pilot. It does not mean Nhà thuốc Xuân Hòa has been exercised, and it does not mean revenue or profit will rise. Deploy, push, merge, and any write on `NT_XUANHOA` were not done.

### 10.1 What changed

Expired lots are no longer a valid counter-sale choice. A lot with no expiry date is still sellable, and FEFO still sorts those lots last. That undated-lot policy was not changed.

| File | Change |
|---|---|
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/SellableLotRules.cs` | Sellable when expiry is null or on/after today. Dated lots before today are expired. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/BatchResolver.cs` | Preview and hints load only sellable lots, earliest expiry first, null last. The sale reload still sees expired rows and `AllocateFromBatches` refuses them. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Sales/SalesRepository.cs` | POS lookup, search, and stock counts use sellable quantity. A scanned lot that is expired is refused before stock is taken. The shift alert no longer treats an expired lot as the lot that should have been sold. |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Consultation/ConsultationRepository.cs` | OTC suggestions count only sellable stock, so a product whose only on-hand lots are expired drops out of the list. |
| `tests/KitPlatform.Application.Tests/CoreEngineTests.cs` | Earliest sellable lot, expired-only refusal, undated lot still sellable, expiry today still sellable. |
| `tests/KitPlatform.Api.Tests/SalesReadOnlyPrincipalTests.cs` | Test principal with only `sales.read`. No DEMO user was created. |
| `tests/KitPlatform.Api.Tests/ControlledPilotHostFactory.cs` | Host with background workers removed and national catalog forced to mock. |
| `tests/KitPlatform.Api.Tests/ControlledPilotSaleTests.cs` | DEMO proof: safety gates, expired-only refusal, in-date FEFO sale and link, label scan of an expired lot. |

No migration. No RBAC policy edit. No Architecture Freeze edit. `phase4-architecture-freeze` was not moved.

### 10.2 Build and tests

`dotnet test` built `KitPlatform.Api` with 0 errors. Existing nullable warnings in `Program.cs` remain. The known `Newtonsoft.Json` 10.0.3 advisory remains.

| Run | Result |
|---|---|
| `InventoryEngineFefoTests` and `PharmacyConsultationOtcGateTests` | Passed 11, failed 0 |
| `SalesReadOnlyPrincipalTests` and the consultation/FEFO sale facts | Passed 3, failed 0 |
| `Label_optional_scan_of_expired_lot_does_not_fall_through_to_fefo` | Passed 1, failed 0 |

The counter TypeScript gate files were not edited. Their server counterparts were re-run in `PharmacyConsultationOtcGateTests`.

The API proof used a host with every `IHostedService` removed, `NationalDrugCatalog:Mode=mock`, and `EnableStockOutSync=false`. Database: local PostgreSQL on `::1`, `current_date` 2026-10-16, timezone `Asia/Bangkok`. Login tenant was `DEMO_PHARMACY`. The test aborts if the tenant is `NT_XUANHOA`.

### 10.3 Transaction evidence

Synthetic customer seed `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01`. Warehouse `WH_MAIN`. Existing open shift reused. No real patient text.

| Check | Observed |
|---|---|
| Save session with no token | 401 |
| Adult cough and sore throat | suggestions not blocked; `DECK001` and `DECK006` absent |
| Seizure | `stop_sale`, blocked, 0 products |
| Fever at age 1 | `refer_medical`, blocked, 0 products |
| Pregnant adult cough | `refer_pharmacist`, not blocked, suggestions present |
| `DECK001` / `DECK006` sellable stock | 0. Only on-hand lots are `DECK-LOT-0001` 2026-07-10 qty 76 and `DECK-LOT-0006` 2026-07-15 qty 343. Preview 400. Sale 400, message contains `hết hạn`. Order count unchanged. Both quantities unchanged. |
| `PARA500` preview and completed line | batch `5555`, expiry 2026-11-30, `batch_source` 1 |
| Cash sale | `SO-000748`, status completed (`2`), total 5,000, paid 5,000, outstanding 0 |
| Link | status `linked` to that order |
| Other `PARA500` lots | `LOT2026A` 2500, `LOT2027B` 1800, undated `33333` 499. Unchanged. Sold lot 48 → 47. |
| `csdl_duoc_sync_log` for `SO-000748` | 0 |
| Local OS watch | last start still `2026-10-15 23:18:26+07` |
| Local tenant row `NT_XUANHOA` | 0 consultation sessions. 0 sales orders created during this proof. This row is in the local database. The production server was not contacted. |

`DEMO_PHARMACY` batch mode is `suggest` (FEFO unless a label mode is on). The local `NT_XUANHOA` row is `label_optional`. For that path, the test switched DEMO to `label_optional`, posted `VITC1000` with batch `VITC-NEAR` (expired 2026-07-16, qty 120) while in-date batch `4444` (2026-12-31, qty 1000) was on hand, and got 400. Neither quantity changed and no order was created. The in-date lot was not used as a fallback. DEMO was then restored to `suggest` and read back as `suggest`.

A test principal with only `sales.read`, and without the ADMIN role, may call the analyze policy and may not call the POS save policy, the sales write policy, or the reports read/export policies. An anonymous principal may not. No DEMO account was created. DEMO has no user who holds `sales.read` without `sales.pos` or `sales.write`. The DEMO user `pharmacist` also has the ADMIN role, and the existing reports policy allows the ADMIN role through. A counter account for the pilot must not be given that role if reports are supposed to stay closed.

### 10.4 Undated lots

Lots with a null expiry stay in the sellable set and sort after dated lots. That is the current rule. This decision does not refuse them and does not start requiring an expiry on receipt. The shift compliance alert still treats an undated on-hand lot as earlier than a dated lot that was sold, and that alert runs only when batch mode is `label_required`. DEMO is `suggest`. The local Xuân Hòa row is `label_optional`, so that alert is off there. An owner who wants undated lots blocked has to decide that separately.

### 10.5 Git

| Item | Value |
|---|---|
| Worktree | `E:\KitPlatform-wt-phase5-novixa-product-proof` |
| Branch | `phase5-novixa-product-proof` |
| Architecture freeze | `0c4adc87eb834d2b79c0207dd48fc08f77b71754` (not moved) |
| Parent of this decision | `9274016d1b4e8009697d63f88a818b15b9f4dd31` |
| Decision commit | `76e8177b134017164db5297ffa4b049be713d9d7` |
| `main` | `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be`, one commit not in this branch: low-stock paging. This branch is not merged into `main`. |
| Dirty `E:\KitPlatform` | Not used and not modified. |
| Push / merge / deploy | Not done. |

### 10.6 What has to be true before a Xuân Hòa write

Read the running Xuân Hòa environment. Do not assume it matches DEMO or the local `NT_XUANHOA` row.

1. The deployed build is this branch, not `main`, and the owner has accepted that `main` still has the low-stock paging commit this branch does not.
2. Migrations `294` through `300` in `deploy/ubuntu/migration-files.prod.txt` are already applied. This decision adds no migration.
3. `NationalDrugCatalog__Mode` is `mock` and `NationalDrugCatalog__EnableStockOutSync` is false on the running API. Also read the tenant CSDL link: stock-out sync stays off unless the owner explicitly turns it on later. The checked-in production file has mode `mock` and sync false. The VPS environment can override that file. Production `Auth:DefaultTenantCode` in that file is `NT_XUANHOA`, so a login without a tenant code is aimed at Xuân Hòa.
4. The selling warehouse has an open shift, retail prices, and at least one in-date lot for the products the counter will actually sell.
5. Batch mode is read and written down. If it is `label_optional` or `label_required`, staff must scan the lot they take. An expired scan is refused. If they leave the lot blank in `label_optional`, FEFO runs.
6. Counter users have `sales.read` and `sales.pos`. `sales.write` also opens the POS save policy. They do not need `reports.read`. They should not be in the ADMIN role if revenue reports must stay closed. Confirm the Xuân Hòa role rows. Do not copy the DEMO pharmacist, who also holds ADMIN.
7. Before the first real sale, record a baseline: consultation session count, completed order count for the chosen window, and on-hand quantity of the pilot products. Take a backup under the owner's existing practice.
8. Training uses the scripted adult case below and a synthetic customer. Do not type a real patient's complaint into a training session.

Dependencies already in this build: consultation tables, POS order create, open shift, FEFO or label allocation, inventory batches, and the consultation link call. National stock-out sync, receiving, and financial reports are outside the pilot path.

### 10.7 Pilot procedure

Ten business days after the owner opens the write window. One store. One workflow: adult counter advice that becomes a paid sale. No revenue target and no profit target.

At the counter:

1. Sign in as the counter user. Confirm the warehouse and that the shift is open.
2. Select or create the customer. Open **Tư vấn tại quầy**.
3. Record cough and sore throat for an adult, no fever, no danger signs. Analyze.
4. Expect a mild respiratory assessment and OTC lines with stock greater than zero. Add one unit whose lot is still in date.
5. Save the consultation. Take payment for the line. Complete the sale.
6. Expect a completed order, the consultation `linked` to that order, and the sold lot reduced by the quantity sold.
7. For a seizure description, or fever in a child under 2, expect no OTC list and no sale from that list.
8. After the shift, write one line: how many consultations were started, how many reached a linked sale, how many checkouts were refused and why, and what got in the way.

### 10.8 What to measure

Record counts. Do not convert them into a PASS by inventing a percentage.

| Measure | How | What it is not |
|---|---|---|
| Flow completion | Linked sessions ÷ confirmed sessions in the 10 days | Not a minimum bar |
| Integrity | For each linked sale: order completed, outstanding 0, sold lot decreased by the sold quantity, sold lot not expired. Expired completed sales must stay 0. A completed expired sale stops the pilot. | Not a revenue result |
| Errors | Refused checkouts ÷ checkout attempts, split into expired lot, shift closed, and other. Staff notes plus the refused response. | Not a target rate |
| Time | Clock from opening the consultation to payment, on the first 10 real sales. Report the median. | Not a staffing grade |
| Use | Sessions per shift and distinct usernames. One obstacle line per shift. | Not an adoption quota |
| Business comparison | Baseline is the 10 business days before the write window, from the books Xuân Hòa already keeps: ticket count for this store. During the pilot, record Novixa completed orders per day and how many of those are linked to a consultation. | Not profit, not a claim that Novixa changed revenue. Season, staffing, and mix are uncontrolled. Describe the counts. The owner decides whether to continue. |

### 10.9 Proved, not proved, and owner decisions

Proved on local `DEMO_PHARMACY`:

- A product with only expired lots is not suggested and has sellable stock 0.
- Expired lots are skipped by FEFO. The earliest in-date lot is sold.
- The sale API refuses an expired lot, including a scanned expired lot while an in-date lot of the same product exists. No completed order, no stock movement, no consultation link.
- The adult consultation sale completes, reduces only that lot, and links.
- Safety gates for `stop_sale`, `refer_medical`, and `refer_pharmacist` still hold.
- A `sales.read` principal cannot save. Reports are not granted by `sales.read` or `sales.pos`.

Not proved:

- Any screen, role, lot, price, shift, or setting on the production Xuân Hòa server.
- That the VPS national-catalog environment matches the checked-in production file.
- That undated lots are refused. They are still sellable.
- Inventory barcode suggestion outside POS sales lookup. The counter uses POS search and POS lookup.
- A live DEMO user who has `sales.read` and nothing else. The denial is the test principal.

Owner decisions, not made here:

- Authorize deploy of this branch.
- Authorize the first write on `NT_XUANHOA`, after §10.6.
- Keep national stock-out sync off.
- Accept or replace the undated-lot rule.
- Continue, narrow, or stop after the 10 days, using the counts in §10.8.

### 10.10 Confirmation

No production write. No migration on `NT_XUANHOA`. No deploy. No push. No merge. The local `NT_XUANHOA` tenant row was counted and not updated. DEMO batch mode was changed to `label_optional` only inside the label-scan test and restored to `suggest`.

## 11. Xuân Hòa go-live preflight (2026-10-16)

**First-pass record only.** The go-live decision is §12. This section stopped when OpenSSH had no key for the deploy host.

**GO-LIVE DECISION AT THE TIME OF THIS SECTION: NOT DETERMINABLE.**

The published counter screens are older than this branch. The running API binary, production migrations, national-catalog process settings, shift, stock, and counter roles were not readable from this session. A go-live approval is not available from this preflight. This section does not replace the local DEMO record in §9 or the branch decision in §10.

### 11.1 What was verified before any read

| Tree | Branch | HEAD | State during this preflight |
|---|---|---|---|
| `E:\KitPlatform` | `main` | `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` | Dirty. Five commits ahead of `origin/main`. Not reset, not checked out, not cleaned. |
| `E:\KitPlatform-wt-phase5-novixa-product-proof` | `phase5-novixa-product-proof` | `b3ea8c8c3cc759d2c04d3a17400342d067c6c9d1` | Clean until this §11 edit. Parent of the tip is the expiry-safety commit `76e8177b`. |

`origin/main` is `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` (2026-09-29 16:14 +0700). No push, merge, cherry-pick, or commit was made.

### 11.2 Running environment

Public reads at 2026-10-16 09:56 +07. Host header `nginx/1.24.0 (Ubuntu)`.

| Surface | Observed |
|---|---|
| `GET https://api.novixa.vn/api/health` | 200, `{"status":"ok"}` |
| `GET https://api.novixa.vn/api/health/db` | 200, `{"status":"ok","database":true,"value":1}` |
| `GET /api/sales/pos/consultation/symptom-options` with no token | 401. The consultation route is registered. The handler body did not run. |
| Admin `https://admin.novixa.vn/` | `Last-Modified: Sun, 27 Sep 2026 04:48:14 GMT`. Script `/assets/index-D2cVnNrQ.js`, 833,561 bytes, `Last-Modified: Sun, 27 Sep 2026 04:47:55 GMT`, ETag `6ab89ffb-cb819`. |
| POS `https://pos.novixa.vn/` | `Last-Modified: Sun, 27 Sep 2026 04:48:29 GMT`. Script `/assets/index-5OdKaG-L.js`. `version.json` build `2026-09-25T08:59:48.376Z`. |

Those file times are before the low-stock commit (2026-10-13 12:43 +0700) and before the pharmacy commits on this branch (2026-10-15 23:27 +0700 and 2026-10-16 09:46 +0700). The admin and POS files being served are not this branch.

The API process was not opened. `GET /api/health` does not return a commit. SSH to the deploy target used by `scripts/deploy-update-vps.ps1` returned `Permission denied (publickey,password)`. No password was read or sent. The checked-in `appsettings.Production.json` says `NationalDrugCatalog:Mode=mock`, `EnableStockOutSync=false`, and `Auth:DefaultTenantCode=NT_XUANHOA`. That file is not the running process. The VPS `api.env` can override it, and that file was not read.

### 11.3 Migrations 294–300

The files are listed in `deploy/ubuntu/migration-files.prod.txt` on `origin/main` and on this branch:

`294_pharmacy_consultation_mvp1.sql`, `295_pharmacy_symptom_taxonomy_schema.sql`, `296_pharmacy_symptom_taxonomy_seed.sql`, `297_pharmacy_consultation_dehydration_tune.sql`, `298_pharmacy_consultation_ent_otc.sql`, `299_pharmacy_consultation_preliminary_assessment.sql`, `300_pharmacy_consultation_customer_snapshot.sql`.

`origin/main` already contains `ConsultationSafetyRules.cs`. It does not contain `SellableLotRules` or `BlocksAutomaticOtcSuggestions`. The 401 above shows a consultation route on the live API. It does not show that `kit_schema_migrations` contains 294–300, that `pharmacy_symptom` is seeded, or that the live DLL contains the October gate or the expiry refusal.

This branch adds no pharmacy migration. `76e8177b` and `bf5dc643` are code and tests.

The production manifest on this branch is not the same list as `origin/main`. Commit `c663196f` removes `230_pack_family_parent_emotion_push.sql` and `231_pack_family_memory.sql` from `migration-files.prod.txt`. Those two names remain on `migration-files.family-os.txt`. The same commit range adds `383`, `384`, `387`, and `388` to `migration-files.content.txt` only. `run-incremental-migrations-prod.sh` applies `migration-files.prod.txt`. A later production migration run from this branch would not apply the two family files or the content files. Whether 230 and 231 are already in the live ledger is unread.

### 11.4 Version comparison

| Line | Identity | Pharmacy pilot code | Other runtime difference |
|---|---|---|---|
| Published admin and POS | File times 2026-09-25 to 2026-09-27. Asset names above. Commit hash not embedded. | UI files predate `bf5dc643` and `76e8177b`. | Also predate `0d8568ae`. |
| Live API binary | Not read. Consultation route returns 401. | Expiry refusal and server `refer_medical` block are not proven on this process. | National-catalog mode, tenant link, and DLL time are unread. |
| `phase5-novixa-product-proof` | `b3ea8c8c`, parent `76e8177b` | Server withholds OTC suggestions for `stop_sale` and `refer_medical`. FEFO and POS lookup use sellable lots. A dated lot before today is refused, including a scanned expired lot. A null expiry stays sellable. | Also contains the core engine move `c447814d`, content workflow `98f0632c`, content ownership `c663196f`, and the narrative-boundary fix `37ffb3f6`. |
| Local `main` | `0d8568ae` | Does not contain `bf5dc643` or `76e8177b`. | One commit this branch does not have. |
| `origin/main` | `73d3c4a6` | Consultation MVP is present. Expiry refusal is absent. Server `refer_medical` OTC block is absent. | Ancestor of both local lines. |

`0d8568ae` (`fix(novixa): page low-stock without dropping tied rows`, 2026-10-13 12:43 +0700) changes low-stock from a full list to `page` / `pageSize` (default 20) and keeps UTC date bounds plus a product-warehouse tie-breaker so expiry filters and low-stock pages stay stable. Paths include `LowStockPage.tsx`, `StockController.cs`, `InventoryRepository.cs`, report date helpers, and dashboard date bounds. None of those paths appear in the diff from `origin/main` to this branch. The commit is not on the consultation sale path. It was not merged or cherry-picked. A simulated merge of this branch onto `origin/main` reported no conflicts; that simulation does not include `0d8568ae`, and nothing was written to either branch.

Deploying the tip of this branch as one artifact would publish the pharmacy gate and the expiry refusal together with Content pack changes, the removal of the old core engine interfaces, `Program.cs`, customer draft-order code, and dashboard code. The pharmacy commits themselves do not add a migration. A minimal pharmacy publish is technically identifiable: the file set in `bf5dc643` and `76e8177b` under `src/Packs/Pharmacy` and `client/admin/src/modules/sales`. Those commits sit after `c447814d`, which already edits `SalesRepository.cs` and `SalesService.cs`. This preflight does not cut that artifact and does not choose a new commit.

### 11.5 Deploy risk and rollback

The update script rsyncs the new tree onto `/var/www/kit-platform`. Admin, staff, customer, and prescriber trees use `rsync --delete`. The API tree is rsync without `--delete`. When `/var/www/pharmacore` still exists, the script copies the new tree onto that legacy root. That copy is the new artifact, not a retained previous release. The script then restarts `kit-platform-api`. No previous-release directory is created. Whether the server currently has another copy was not verified.

A rollback that matches this script is a redeploy of the operator's previous `publish/` output, then a service restart. It was not executed. Consultation tables stay if real rows exist; the consultation runbook rolls back code and does not drop those tables. This pilot adds no migration, so a code rollback does not need a down migration. National stock-out sync stays off during any later rollback.

### 11.6 Go-live checklist

| Condition | Result |
|---|---|
| Worktrees and the named commits exist | Đạt. `0c4adc87`, `bf5dc643`, `9274016d`, `76e8177b`, and tip `b3ea8c8c` are on `phase5-novixa-product-proof`. |
| Running admin/POS artifact is this branch | Chưa đạt. Served files are dated 2026-09-25 to 2026-09-27. |
| Running API commit identified | Không thể xác minh. |
| Migrations 294–300 applied on the Xuân Hòa database | Không thể xác minh. The route exists. The ledger was not read. |
| Expired lot is not a valid sale; API refuses it | Không thể xác minh on the live API. Proven on local `DEMO_PHARMACY` in §10.3. |
| FEFO picks the earliest in-date lot | Không thể xác minh on Xuân Hòa. Proven on local DEMO in §10.3. |
| `stop_sale`, `refer_medical`, `refer_pharmacist` gates | Không thể xác minh on the live API. The client condition for `stop_sale` and `refer_medical` already existed before `bf5dc643`. The server `refer_medical` block is in `bf5dc643`. |
| Counter account does not need `reports.read` | Code on this branch and on `origin/main`: `ReportsRead` allows `reports.read`, `reports.write`, `reports.export`, or role `ADMIN`. `sales.read` and `sales.pos` are not in that policy. `sales.pos` or `sales.write` satisfies the POS policy. Xuân Hòa role rows were not read. |
| `NationalDrugCatalog` mode `mock`, stock-out sync off, tenant link off | Không thể xác minh. Checked-in production file says mock and sync false. Process environment and `tenant_csdl_duoc_link` were not read. |
| In-date on-hand stock suitable for a pilot sale | Không thể xác minh. |
| Open shift and batch mode recorded | Không thể xác minh. |
| Baseline ticket count and on-hand can be written down before the first sale | Không thể xác minh. No production count was read, and no baseline was written. |
| Ten-day measures in §10.8 | Defined. Not started. |
| Null-expiry lots | Unchanged. Still sellable. No rule change in this preflight. |

### 11.7 Approvals still required

1. Read-only access to the host that serves `api.novixa.vn`: service status, DLL identity, and only the keys `NationalDrugCatalog__Mode`, `NationalDrugCatalog__EnableStockOutSync`, and `Auth__DefaultTenantCode`.
2. Read-only SQL on the production database, expected by the deploy script as `novixa_prod` until the live name is confirmed: migration ledger for 294–300, `NT_XUANHOA` batch mode, CSDL link flags without credentials, open-shift count, aggregate in-date / expired / undated lot counts, counter permission codes, consultation count, and completed-order count.
3. Owner choice of the publish input. The branch tip also ships Content and the core-engine move. A pharmacy-only file set is identifiable and was not cut here.
4. Written acceptance that `0d8568ae` stays off this branch unless the owner later asks to merge it.
5. A separate deploy approval, then a separate approval for the first `NT_XUANHOA` write, only after items 1 and 2 have been read.
6. Keep national stock-out sync off, including the tenant link.
7. Accept the current null-expiry rule, or decide a replacement in a later task.
8. A backup under the owner's existing practice before the first real sale.

### 11.8 Next action

Grant the read-only host and database access in §11.7 and repeat this preflight. Do not deploy, do not run migrations, and do not open a Xuân Hòa sale from this result.

### 11.9 Confirmation

No service restart. No build. No artifact replaced. No migration. No order, consultation, stock, config, feature-flag, role, or tenant write. No national stock-out sync change. No push, merge, cherry-pick, or commit. `E:\KitPlatform` was not modified. The only file written is this dossier in the Phase 5 worktree. SSH credentials were not printed. Customer and patient rows were not read.

## 12. Xuân Hòa go-live decision (2026-10-16, completed read)

**GO-LIVE DECISION: READY FOR OWNER DEPLOY.**

The pharmacy code to publish is commit `76e8177b134017164db5297ffa4b049be713d9d7` on `phase5-novixa-product-proof`. No new deploy branch was cut. Production migrations 294–300 are already applied, so this deploy does not run migrations. National stock-out sync is off. The process catalog mode is `sandbox`, not the `mock` value in the checked-in JSON. The owner deploys during a quiet hour, after a fresh database dump and a copy of the live web roots. This section does not replace §9 or §10.

### 12.1 What was read

Read-only session at 2026-10-16 10:10–10:15 +07. OpenSSH still has no key. The same host and account named by `scripts/deploy-update-vps.ps1` answered through PuTTY. Commands were service status, file times, hashes, allowlisted configuration keys, backup-file headers, and aggregate SQL. No secret value was printed. No customer, patient, order line, or consultation body was selected.

| Check | Result |
|---|---|
| API service | `kit-platform-api` active, running. Last start 2026-09-29 21:00:52 +07. `pharmacore-api` inactive. |
| API binary | `/var/www/kit-platform/api/KitPlatform.Api.dll`, 1,786,368 bytes, mtime 2026-09-27 11:47:15 +07, SHA-256 `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`. The same size and mtime exist under `/var/www/pharmacore/api/`. Nginx `root` for admin and POS is `/var/www/kit-platform`, not the legacy tree. |
| Admin / POS | Disk times match the public files in §11.2. POS `version.json` build `2026-09-25T08:59:48.376Z`. These files are not this branch. |
| Database | `novixa_prod` exists. |
| Migrations 294–300 | All seven filenames are in `kit_schema_migrations`, applied 2026-08-25 12:26 +07. Symptom table has 190 rows. `pharmacy_consultation_sessions` exists. |
| Family 230 and 231 | Already in the ledger (2026-07-26). Removing them from this branch's prod manifest does not uninstall them. |
| `NT_XUANHOA` batch mode | `suggest`. |
| Open shifts | 2 shifts with status open. Latest open time 2026-10-16 09:34:32 +07. The counter was in service during this read. |
| Completed orders | 6,411 completed, 0 drafts, 6,419 orders in total. Latest completed order 2026-10-16 10:12:36 +07. These counts are a snapshot, not the deploy baseline. |
| Consultations | 0 sessions for `NT_XUANHOA`. |
| On-hand lots | 3,098 batches with quantity. 3,095 in-date batches, quantity 3,874,631.001. 3 expired batches, quantity 231. 0 undated batches with quantity. |
| Counter roles | `CASHIER` and `STAFF` have `sales.pos` and do not have `reports.read`. Active users with `sales.pos`: 5. Of those, 3 do not have `reports.read` and are not `ADMIN`. |
| Catalog mode | `api.env` sets `NationalDrugCatalog__Mode=sandbox` and `NationalDrugCatalog__EnableStockOutSync=false`. Base host is `api-sandbox.csdlduoc.com.vn`. Username and password keys are present and non-empty; the values were not printed. `Auth__DefaultTenantCode` is absent from `api.env`. `ASPNETCORE_ENVIRONMENT=Production`. Checked-in `appsettings.Production.json` still says `Mode=mock`; the environment value overrides it. |
| Tenant link | 0 rows in `tenant_csdl_duoc_link` for `NT_XUANHOA`. |
| Stock-out log | 0 rows in `csdl_duoc_sync_log` for `NT_XUANHOA`. |
| Backup files | Cron `/etc/cron.d/kit-platform-backup` runs `/opt/kit-platform/backup-db.sh` at 02:00. Files from 2026-10-10 through 2026-10-15 at 02:00 are 20 bytes and are not dumps. `novixa_prod_20261015_111346.sql.gz` (14,836,182 bytes) and `novixa_prod_20261016_020001.sql.gz` (15,019,608 bytes) are gzip-valid and begin with a PostgreSQL dump header. Neither file was restored. |
| Previous release directory | None. `/var/www/kit-platform.prev` and `/opt/kit-platform/releases` are absent. The update script overwrites the live tree and does not keep the previous binaries. |

### 12.2 Artifact

Publish commit `76e8177b` from worktree `E:\KitPlatform-wt-phase5-novixa-product-proof`. Do not publish `E:\KitPlatform`.

That commit is the expiry refusal. Its parent `bf5dc643` is the server-side OTC block for `stop_sale` and `refer_medical`. Both are patches on `SalesRepository.cs`, `SalesService.cs`, and `BatchResolver.cs` after `c447814d` moved the pharmacy workflow out of Core and after `0896f45a` made that tree compile. `c447814d` is a direct child of `origin/main` `73d3c4a6` and also edits the draft-order and dashboard permission calls. The content commits between `c447814d` and `bf5dc643` are in the same tree. A pharmacy-file cherry-pick onto the September binary was not cut: those files are not independent of the engine move, and a new branch would be an untested reconstruction.

`deploy-production.ps1` sets its root from the script's own directory. Run it from this worktree. `deploy-update-vps.ps1` defaults `-Root` to `E:\KitPlatform`; the owner must pass this worktree. Do not pass `-RunMigrations`.

What else this publish contains, and what it does not:

- Content editorial workflow, production customer ownership, and the `ProductionNotFoundException` mapping in `Program.cs` ship inside the same API binary and admin bundle.
- Staff-app sources on this branch match `origin/main`. The live POS bundle is older (2026-09-25), so the POS bundle still moves forward to that `origin/main` tree.
- `0d8568ae` (low-stock paging on local `main`) is not in this branch and is not in the live binary. It stays out.
- This branch adds no pharmacy migration. Content migrations stay on `migration-files.content.txt`. The prod runner will not apply them.

### 12.3 Owner deploy runbook

Do this only in a quiet hour, after the owner accepts §12.2. The read in §12.1 found two open shifts and a completed sale at 10:12 +07, so that window was not quiet.

1. **Code identity.** Build from `E:\KitPlatform-wt-phase5-novixa-product-proof`. Pharmacy runtime code is `76e8177b`. This dossier edit does not change that code.

2. **Immediately before build and upload**, on the server, read only:
   - `systemctl is-active kit-platform-api` is `active`.
   - `sha256sum /var/www/kit-platform/api/KitPlatform.Api.dll` is still `26afdaa7f123fa36496f31652a01a6d87942e975c4baf72b0b49bfff977a8941`. If it differs, stop. The snapshot below would no longer match this record.
   - `NationalDrugCatalog__Mode` is `sandbox`, `NationalDrugCatalog__EnableStockOutSync` is `false`, and `NT_XUANHOA` still has 0 rows in `tenant_csdl_duoc_link`. If mode is `live`, or stock-out sync is true, or a tenant link row has `enable_stock_out_sync` true, stop.
   - Recount and write down: open shifts, completed orders (`status = 2`), consultation sessions, in-date quantity, expired quantity, undated quantity. The §12.1 numbers are already stale.

3. **Backup.** Run `/opt/kit-platform/backup-db.sh`. The new `novixa_prod_*.sql.gz` must be far larger than 20 bytes, `gzip -t` must pass, and the first bytes must be a PostgreSQL dump header. The 20-byte files from 10–15 October are not backups. Do not restore this file as a drill.

4. **Code snapshot, because the deploy script does not make one.** Copy `api` (exclude `uploads`), `admin`, `staff-app`, `customer-app`, and `prescriber-portal` from `/var/www/kit-platform` to a new directory under `/var/backups/KitPlatform/release-<stamp>/`. The copied DLL hash must equal the live hash from step 2. Also keep the matching `/var/www/pharmacore` API and admin trees in mind: the update script mirrors into that legacy directory when it exists. Nginx is already pointed at `/var/www/kit-platform`.

5. **Build, from the worktree:**

```powershell
Set-Location E:\KitPlatform-wt-phase5-novixa-product-proof
.\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn"
.\scripts\deploy-update-vps.ps1 -Root "E:\KitPlatform-wt-phase5-novixa-product-proof"
```

Do not add `-RunMigrations`. The script rsyncs API without `--delete`, rsyncs admin, customer, staff, and prescriber with `--delete`, rewrites only `Cors__AllowedOrigins__*` in `api.env`, reloads nginx, and restarts `kit-platform-api`. It does not change `NationalDrugCatalog__*`.

6. **Smoke, still without a new sale or consultation.** `systemctl is-active kit-platform-api` is `active`. `GET /api/health` is 200. `GET /api/health/db` reports `database: true`. `GET /api/sales/pos/consultation/symptom-options` with no token is 401. Admin and POS index pages return HTML. `deploy/ubuntu/smoke-test.sh` passes. The new DLL hash differs from `26afdaa7…`. Mode is still `sandbox`, stock-out sync is still `false`, tenant link rows are still 0, and `csdl_duoc_sync_log` for `NT_XUANHOA` is still 0.

7. **Stop if** the API is not active, health or the smoke script fails, the consultation route returns 500, catalog mode is `live`, stock-out sync is true, a tenant link row appears with sync enabled, or the sync log gains a row. Do not open a counter sale after a failed smoke.

8. **Rollback of code.** Stop `kit-platform-api`. Rsync the release snapshot back over `/var/www/kit-platform/api` without `--delete` and without touching `uploads`. Rsync admin, staff-app, customer-app, and prescriber-portal back with `--delete`. Restart the service. Confirm the DLL hash is `26afdaa7…` and health returns 200. No down migration is required. Repeat the mirror into `/var/www/pharmacore` only if that tree was updated by the failed deploy. A database restore from the dump is a separate, destructive choice: it discards every sale written after the dump. Use it only when the owner accepts that loss. A bad sale after a good deploy is corrected in data, not by restoring the morning dump over a later day of trade.

9. **Catalog confirmation.** After restart, read the same three facts: mode `sandbox`, `EnableStockOutSync=false`, zero `tenant_csdl_duoc_link` rows for `NT_XUANHOA`. Do not switch the process to `mock` or `live` as part of this deploy. `sandbox` is the value already serving the store. Stock-out sync stays off.

10. **Baseline before the first real post-deploy sale.** Run the recount in step 2 again and keep the numbers with the clock time. The first completed sale should raise the completed-order count by one and reduce the sold lot. Consultation sessions may move from 0 to 1 only when the owner deliberately saves one. Null-expiry lots remain sellable; this deploy does not change that rule. Expired lots with quantity (3 batches, 231 units at the time of §12.1) become unsellable after this API is the one serving the counter.

### 12.4 Confirmation

No service restart. No build. No artifact replaced. No migration. No order, consultation, stock, config, role, feature-flag, or tenant write. No change to national stock-out sync. No push, merge, or cherry-pick. `E:\KitPlatform` was not modified. Secrets and customer or patient rows were not printed. The dossier update is uncommitted on `phase5-novixa-product-proof`.

## 13. KPA-RELEASE-SAFETY-001 (2026-10-16)

**Decision: NOT READY.**

§12 is a read of the live host and a proposed runbook. It is not a verified production release. This section does not edit the Architecture Freeze register.

The code identity remains `76e8177b134017164db5297ffa4b049be713d9d7`. Branch tip `b3ea8c8c` adds one documentation line and no runtime change. `origin/main` is `44fd5085` and diverged at `73d3c4a6`. Local `main` `8c59ceb4` is dirty and is not a release artifact. Video MVP `45d21801` is a separate line and is **BLOCKED** on `ReadVeoTake` / `ReadFalTake`; it is not part of this pharmacy candidate.

What this session measured, and what is still missing, is in `docs/novixa/03-operations/KPA-RELEASE-SAFETY-001-REPORT.md`. The blockers that keep the decision at NOT READY are: admin `npm run build` fails (173 TypeScript errors), there is no staging host on which binary rollback was executed, the live DLL hash in §12.1 was not re-read in this session, and Content remains a compile-time dependency of the API even though its migrations are outside `migration-files.prod.txt`.

Owner approval waits on those conditions. Do not run the §12.3 commands against production from this result.

## 14. KPA-RELEASE-RECOVERY-001 (2026-10-16)

**Decision: NOT READY — REMEDIATION COMPLETE.**

The admin `npm run build` failure in §13 is closed on this worktree, and the other SPAs named in `scripts/deploy-production.ps1` also build. The API Release publish hash is still `E9A5CC26DB670BFD89E47662210208E2A8E616D4FBAE472D46086D31F7027E0D` because no C# file changed. The recovery edits are uncommitted, so they are not yet a commit-identified candidate. Staging was not used. Production rollback was not executed. Video `ReadVeoTake` / `ReadFalTake` stay blocked and outside this candidate.

Detail, file list, and artifact hashes: `docs/novixa/03-operations/KPA-RELEASE-RECOVERY-001-REPORT.md`. Do not run the §12.3 commands against production from this result.

## 15. KPA-PRODUCTION-READY-001 (2026-10-16)

**Decision: NOT READY.**

The recovery source is now commit `643932c0ba35cac0cad069a789a7197671f4ba24` on `phase5-novixa-product-proof`. Pharmacy C# is unchanged since `76e8177b`. Release publish and the required tests were run from that clean commit. There is still no permitted staging host, so the candidate was not deployed and binary rollback was not executed. A local DEMO sale is not a Xuân Hòa release.

The live host was read again in this session and was not modified. The running DLL is still the 2026-09-27 binary. Catalog mode is still `sandbox`, stock-out sync is still false, and `NT_XUANHOA` still has no tenant-link row. Two shifts were open and completed orders had moved since §12.1. That is not a quiet deploy window.

Detail: `docs/novixa/03-operations/KPA-PRODUCTION-READY-001-REPORT.md`. Do not upload this candidate to production from this result.

