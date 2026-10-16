# KPA-PHASE5 — NOVIXA Product Proof

**Date:** 2026-10-15  
**Pilot data validation:** 2026-10-16 — **PARTIAL** (valid in-date sale recorded; expired-lot exclusion and a live read-only denial are not verified)  
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
