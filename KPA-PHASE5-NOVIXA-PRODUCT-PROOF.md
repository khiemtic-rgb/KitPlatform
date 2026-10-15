# KPA-PHASE5 — NOVIXA Product Proof

**Date:** 2026-10-15  
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
