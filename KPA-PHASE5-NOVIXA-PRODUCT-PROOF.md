# KPA-PHASE5 — NOVIXA Product Proof

**Date:** 2026-10-15  
**Controlled pilot decision:** 2026-10-16 — **CONTROLLED PILOT READY** on this branch. Nhà thuốc Xuân Hòa is not yet verified. Deploy and any production write still need the owner. See §10.  
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
