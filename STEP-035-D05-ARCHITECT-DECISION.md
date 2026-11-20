# STEP 035 — D-05 Architect Decision

Status: **AWAITING ARCHITECT APPROVAL.** Decision finalization only. Every schema item below is **PROPOSED**. None is **APPROVED**.  
Phase 1.5: **NOT PASS.** This file does not declare PASS and does not open Phase 2.  
Baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate.

This file asks the Architect to approve or reject a bounded schema decision for tenant attribution on `pack_content.series_build`. The proposed scope is Step 033 Option B, limited to that table. Publication of this file does not close D-05, does not lift Register line 231, and does not authorize a migration.

ADR-03 Option 1 stays **DECIDED**. D-06 stays unstarted. No Step 028 gate row is marked PASS.

No product file, migration, manifest, ADR, Decision Register, Master Architecture, governance file, or application code was edited. Migration 376 was not read. The working-tree copy of the content manifest was not used. No commit, push, merge, reset, clean, deploy, migration, or production database access was performed. No test or build was run. No stash object was read. No repository-wide audit was run.

## Labels

| Label | Meaning in this decision |
|---|---|
| DECIDED | ADR-03 Option 1, already closed. This file preserves it. |
| VERIFIED | Confirmed in this step by the single read-only check named below. |
| RECORDED | Step 032, Step 033, or Step 034 states the fact. This step did not re-score it. |
| INFERENCE | A conclusion Step 034 drew from verified facts. It is not itself a measurement. |
| PROPOSED | A choice this file asks the Architect to approve. It is not DECIDED and not APPROVED. |
| UNVERIFIED | A tracked sentence claims an implementation fact, and Step 032 records that the cited file is not in the baseline tree. |
| NOT GRANTED | This file does not give permission to change schema, API, database, deployment, or publication. |
| UNKNOWN | No source used in Steps 032–034, and no check in this step, answers the question. |
| BLOCKED | A later action cannot proceed on the evidence and authority this file has. |

Level 4 documents, gitignored files, untracked files, and stash objects do not become implementation evidence by being named here. This file is an untracked report. It is not an authority rank under Step 030 D-02.

## Evidence boundary

This step did not reopen Steps 027–031 and did not repeat the schema searches in Steps 032–034.

| Source | Role here | Limit |
|---|---|---|
| `git rev-parse HEAD` in this step | Confirms the working commit is still the baseline. | **VERIFIED** equal to `d54652d4a7734b3116db62bd356a831f9942fc73`. No other git query was run. |
| `STEP-032-ARCHITECT-DISPOSITION-D04-LINE-231.md` | Line 231 still binds. D-05 was left open. | Not re-audited. |
| `STEP-033-D05-TENANT-SCHEMA-SCOPE-OPTIONS.md` | Option A and Option B. Seventeen items left **NOT CHOSEN**. | Not an approval. |
| `STEP-034-D05-ARCHITECTURE-DECISION-DRAFT.md` | Proposed Option B column shape, permission path, and remaining unknowns. | Not an approval. This file adopts that shape except where §2 item 7 narrows the index. |
| `STEP-030-ARCHITECT-DISPOSITION-D01-D02.md` | Authority rank used to name where a grant must be stored. | Hierarchy left as D-02 decided it. |
| `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md` | Gate identifiers this file must not re-score. | Statuses stay as Step 034 recorded them. |

The current table used below is migration 315, as Step 033 §1.1 and Step 034 §1.1 verified at blob `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d`. This step did not re-read that blob.

### Already-bounded tension

ADR-03 lines 156–160, Register lines 164, 604, 630, 677, and 693, and Master Architecture line 433 claim that `migrations/376_pack_content_production_tenant_boundary.sql` already added nullable `tenant_id`. Step 032 records that claim as **UNVERIFIED**. This file does not edit those sentences, does not withdraw them, and does not use them as the column design. AG-G2-02 stays **FAIL**, as Step 034 recorded it.

## 1. Scope

**PROPOSED.** Option B, and only Option B, for `pack_content.series_build`.

One later migration, if a later authoritative grant creates it, changes only that table by adding tenant attribution. One later line on `deploy/ubuntu/migration-files.content.txt` names that migration. No other table is in the migration. Option A remains available to the Architect: selecting Option A stops the path and adds no migration.

Basis: Step 032 question 2 names `series_build` as D-05. Step 033 compares Option A and Option B and selects neither. Step 034 §1.1 and §7 item 1 propose Option B and leave approval to the Architect.

## 2. Decisions presented for approval

Each row is **PROPOSED**. This file does not change any row to **APPROVED**. Approval of a row is still not permission to write a migration. The conditions in §4 apply before any file may be created.

| # | Proposed decision | Status | Basis in Steps 032–034 | Still open |
|---|---|---|---|---|
| 1 | Add `tenant_id UUID NULL`, with no default value. | **PROPOSED** | Step 034 §2.1–§2.3. Migration 002 defines `tenants.id` as `UUID PRIMARY KEY`. ADR-03 line 70: existing Production rows are not already attributable to tenants. A `NOT NULL` column with no default fails if any row exists. A default tenant UUID would assign a tenant inside the migration. Row existence is **UNKNOWN**, so the column that can be added without inventing attribution is nullable and has no default. A nullable column does not make the row attributable. | Live catalogs were not read. Whether any database has applied migration 315 is **UNKNOWN**. |
| 2 | Foreign key to `public.tenants(id)`, with no `ON DELETE` clause. | **PROPOSED** | Step 034 §2.2 and §2.4. Migration 002’s foreign keys from `branches`, `employees`, `users`, `roles`, and `audit_logs` reference `tenants(id)` and omit `ON DELETE`. In PostgreSQL that omission is `NO ACTION`. Step 034 found one `CREATE TABLE tenants` and no `CREATE TABLE public.tenants`. The unqualified name in migration 002 is **INFERENCE** for `public.tenants`. `ON DELETE CASCADE`, `SET NULL`, and `RESTRICT` are not proposed. | Whether every database that applied migration 002 created `tenants` in `public` is **UNKNOWN**. What should happen to a `series_build` row when a tenant row is deleted, rather than soft-deleted through `tenants.deleted_at`, is **UNKNOWN**. |
| 3 | Keep primary key `id`. Add no Workspace column. | **PROPOSED** | Step 034 §2.1. `series_build.id` is `UUID PRIMARY KEY` in migration 315. `active_build_id` and `video_production.series_build_id` each store a single UUID. ADR-03 Option 1 is **DECIDED**: Workspace is not a mandatory Production boundary. A missing Workspace column is not the defect this decision answers. | None for this row. A composite primary key `(tenant_id, id)` would be a different schema decision and is outside this proposal. |
| 4 | The attribution migration performs no historical backfill. It contains no `UPDATE` of `pack_content.series_build`. | **PROPOSED** | Step 034 §3. Existing rows, if any exist, stay null until a separate decision. Step 034 §3.2 lists seven facts that must exist before any later `UPDATE` may be authorized. None of those seven was measured. Approving this row does not approve a later backfill. | Row counts, a named database list, orphan pointer counts, a source that maps each historical `series_build.id` to exactly one `public.tenants.id`, and the treatment of a row that still cannot be attributed are **UNKNOWN**. The standing rule if a later backfill is ever approved: leave an unattributable row `NULL`. |
| 5 | Do not alter `pack_content.series_pilot.active_build_id`. Do not alter `pack_content.video_production.series_build_id`. | **PROPOSED** | Step 034 §4 and §1.2. `active_build_id` is `UUID` with no foreign key, added in migration 315. `series_pilot` has no tenant column in migration 313, and no later tracked migration adds one. Migration 333 line 41 defines `video_production.series_build_id UUID` with no foreign key. Neither pointer can supply a tenant id. ADR-03 lines 34–35: a BuildId is not an authorization identity. | Counts of non-null pointers and of orphans are **UNKNOWN**. A foreign key on either pointer waits on that evidence and is not requested here. |
| 6 | Do not enable RLS. Do not change grants in this schema decision. | **PROPOSED** | Step 034 §1.2 and §2.4. Migration 315 grants `SELECT, INSERT, UPDATE, DELETE` to `kitplatform` and to `pharmacore` only when that role exists. It has no `ROW LEVEL SECURITY` and no `CREATE POLICY`. RLS is D-06. Register line 231 does not authorize RLS. Evidence for changing the `pharmacore` grant was not gathered. The new column, if a later migration adds it, stays under the table grants migration 315 already issued. | Whether `pharmacore` should keep DML after attribution is a separate decision. This row does not make that decision. |
| 7 | A non-unique index on `tenant_id` alone is an independent choice. It is not part of rows 1–6. This decision does not require the index. | **PROPOSED** | Step 034 §2.4 proposed the index as a separate yes, from an **INFERENCE** that migration 002 indexes `tenant_id` on `branches`, `employees`, and `users`. The same section records that no query shape has been decided, that D-06 has not named the predicate, and that the Architect can approve the column and reject the index. Step 034 §7 item 5 says the column decision does not depend on the index. Because that query basis is absent, this file does not treat the index as required. The proposed migration body omits the index. | A query predicate that would justify the index is **UNKNOWN**. The Architect may approve inclusion only as a separate vote on this row. Inclusion is not the default of rows 1–6. |
| 8 | Do not start D-06. Do not treat the tenant column as satisfaction of GATE A. | **PROPOSED** | Step 032 question 3 leaves enforcement location open. Step 034 §6.1: a nullable `tenant_id` does not refuse a load or a store. The six `[ServiceFilter(typeof(ProductionTenantActionFilter))]` attributes recorded in Step 032 §3.4 still have no tracked implementing type. GATE A, ADR-03 lines 118–127, remains the requirement. Step 032: decision closed is not implementation complete. | Enforcement location, whether the six actions are the complete future API surface, and a named isolation check with a recorded result remain open. Those questions are D-06 and AG-G4-02. This file does not answer them. |

Rejecting row 1, 2, or 3, or rejecting Option B in §1, stops the migration path. Rows 4, 5, 6, and 8 are limits on that path. Row 7 can be rejected, or left omitted, while rows 1–6 and 8 stay proposed.

## 3. Register line 231

**Line 231 stays in force. No exception is in effect.**

**VERIFIED** text, as Step 032 quoted it from blob `9065fc9a264f592a19759a6d0d8ef52d95321756` at the baseline:

> **DECISION CLOSED ≠ IMPLEMENTATION COMPLETE.** Production is **not** tenant-safe today. GATE A is not executed. No TenantId/WorkspaceId/RLS/API change is authorized by this record.

Step 032 disposed the authorization half of D-04: that sentence still binds. Step 033 and Step 034 preserved it. Paragraphs that call the sentence stale depend on Migration 376 and on forensic files that are not in the baseline tree. Those paragraphs do not lift the line. This file does not lift it.

### Proposed exception, not granted

**PROPOSED** text for a later entry in the Decision Register. It is not in force. This file does not write it into the Register.

If the Architect approves §1 and §2, the only exception that a later Register entry may grant is this one:

The rest of line 231 remains binding: Production is not tenant-safe, GATE A is not executed, no WorkspaceId change is authorized, no RLS change is authorized, and no API change is authorized. The sole additional authorization, and only when that Register entry also contains the implementation sentences in §4, is one new migration whose only table change is the approved attribution column on `pack_content.series_build`, plus one new line on `deploy/ubuntu/migration-files.content.txt`.

Until that Register entry exists on the agreed commit, line 231 has no D-05 exception. Step 032, Step 033, Step 034, and this file are not that entry. None of them is committed.

## 4. Authority required before a migration file may be created

Step 030 D-02, **DECIDED**, ranks authority as:

| Rank | Document |
|---|---|
| Level 1 | `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` |
| Level 2 | `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` |
| Level 3 | ADR decision files, including `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` |

`docs/architecture/GOVERNANCE.md` states the hierarchy and is not itself an authority rank. `docs/architecture/INDEX.md` is navigation. This report is outside that rank.

The decision that can disposition line 231 is an entry in the Level 2 Decision Register, on the agreed commit. ADR-03 stays the policy record for Option 1. This step does not edit ADR-03, the Register, or Master Architecture, including their Migration 376 sentences.

**BLOCKED** until that Register entry exists and says, in its own words, all of the following:

1. The line 231 disposition: either the limited exception in §3, word for word as the Architect approves it, or a statement that line 231 stays without exception. A statement without exception authorizes no migration.
2. An explicit implementation grant. Approval of the column shape is not that grant.
3. One new migration filename. The name is not `migrations/376_pack_content_production_tenant_boundary.sql`. It is not an edit of migration 315. It is not a reconstruction of an untracked or stashed file. The entry names the prefix only after a fresh `git ls-tree` of `migrations/` on the agreed implementation commit. Step 034’s inventory at `d54652d4` is evidence for that baseline only. It does not reserve a number. This file selects no prefix.
4. One line on `deploy/ubuntu/migration-files.content.txt`, naming that filename and its position. The baseline list ends at line 62 with `367_pack_content_character_reference_pack_contract.sql`. The new line is not added to `migration-files.prod.txt`, `migration-files.family-os.txt`, or `migration-files.local-os.txt`.
5. The column, nullability, foreign key, backfill limit, pointer limit, RLS limit, grant limit, and index choice, matching the rows in §2 that the Architect has marked **APPROVED**, or a recorded revision of those rows.
6. A citation the implementation can point to that is newer than the Step 028 checklist. Step 028 AG-G3-05 requires that citation. This file is not that approval while it remains an untracked report, and it is not that approval merely by being cited.

The Register entry has to stand without Migration 376, without the three gitignored forensic paths named in Step 032, and without stash `c170af39`.

`migrations/383_pack_content_article_series.sql` and `migrations/384_pack_content_brand_narrative_dna.sql` stay outside this decision. Their manifest fate is AG-G3-04. The one D-05 line does not decide them.

Applying the migration to any database, including a production database, is a further approval after the file exists. Passing a later gate does not apply SQL.

## 5. Points still BLOCKED or UNKNOWN

### BLOCKED

| Item | Why it stays blocked |
|---|---|
| Creating the migration and the content-manifest line | §4 is not satisfied. Line 231 has no exception in force. |
| Choosing a migration prefix | The namespace check belongs to the agreed implementation commit. This step did not list the working tree. Step 028 recorded untracked on-disk files in prefixes `368`–`382`, `385`, and `386`. A number free at `d54652d4` can still belong to an untracked file. |
| D-05 closure | The authoritative decision is not recorded in the Decision Register. |
| D-06 | Not started. Row 8 proposes that it stay unstarted. |
| AG-G3-02 | **RECORDED** from Step 034 as **BLOCKED**. The migration and the manifest line do not exist. |
| AG-G3-05 | **RECORDED** as **BLOCKED**. This file is not an approval newer than Step 028 stored as authority. |
| AG-G2-03 and AG-G4-03 | **RECORDED** as **BLOCKED**. The line 231 disposition is not in a tracked decision. |
| Phase 1.5 | **NOT PASS** while any mandatory row is FAIL, BLOCKED, or UNKNOWN. |
| Phase 2 | Not opened. |

### UNKNOWN

These are the measurements still missing. This step did not collect them.

| Gap | Label |
|---|---|
| Whether any database has applied migration 002, 313, 315, or a later file. | **UNKNOWN** |
| Whether migration 002’s `tenants` is `public.tenants` on each such database. | **UNKNOWN** on a live catalog. The tracked SQL supports the reading as **INFERENCE** only. |
| Row counts for `pack_content.series_build`. | **UNKNOWN** |
| Whether any live `graph_json` holds a tenant identifier. | **UNKNOWN** |
| Which `public.tenants.id` would be correct for any historical row. | **UNKNOWN** |
| Counts and orphans for `series_pilot.active_build_id` and `video_production.series_build_id`. | **UNKNOWN** |
| Delete versus soft-delete behavior for a tenant row. | **UNKNOWN**. Row 2 therefore keeps the migration 002 form. |
| A free migration prefix on the future agreed commit. | Not reserved. |
| The position of a future content-manifest line relative to files 383 and 384. | Not proposed. Those files are AG-G3-04, outside D-05. |
| The D-06 enforcement location. | Unstarted. |
| A named tenant-isolation check and its result. | **UNKNOWN**. AG-G4-02. |
| Whether the six controller actions in Step 032 §3.4 are the complete future API surface. | **RECORDED** as still unanswered. |

### Gate rows left as recorded

Step 034 §6.3 statuses stay in place. This file does not re-score them.

| Row | Status this file leaves |
|---|---|
| AG-G3-02 | **BLOCKED** |
| AG-G3-01 | **FAIL** |
| AG-G3-04 | **FAIL** |
| AG-G3-05 | **BLOCKED** |
| AG-G2-02 | **FAIL** |
| AG-G2-03 and AG-G4-03 | **BLOCKED** |
| AG-G4-01 | **FAIL** |
| AG-G4-02 | **UNKNOWN** |
| Every other mandatory row | Unchanged. AG-G1-01’s carried PASS does not pass the phase. |

Blocker B-06, Register line 231 not lifted, stays open.

## 6. Permissions not granted

**No implementation permission granted.**

| Permission | State |
|---|---|
| Schema change on disk, including a new file under `migrations/` and any edit of migration 315 | **NOT GRANTED** |
| Edit of `deploy/ubuntu/migration-files.content.txt` or of any other manifest | **NOT GRANTED** |
| Choice of a migration prefix | **NOT GRANTED** |
| API change, including a change to `ContentController.cs` | **NOT GRANTED** |
| Creating or removing `ProductionTenantActionFilter` | **NOT GRANTED** |
| Historical backfill, including any `UPDATE` of `series_build` | **NOT GRANTED** |
| RLS, `CREATE POLICY`, or a grant change | **NOT GRANTED** |
| Running a migration, opening a database session, or touching Production | **NOT GRANTED** |
| Edit of ADR-03, the Decision Register, or Master Architecture, including the Migration 376 sentences | **NOT GRANTED** |
| Reading Migration 376, the three gitignored forensic paths, or stash `c170af39` as proof | **NOT GRANTED** |
| Commit, push, merge, reset, clean, or deploy | **NOT GRANTED** |
| Marking any Step 028 row PASS, passing Phase 1.5, or opening Phase 2 | **NOT GRANTED** |
| Closing D-05 or starting D-06 | **NOT GRANTED** |

ADR-03 Option 1 does not supply these permissions. Approving §2 in the section below does not supply them either. The grant exists only when §4 is satisfied on the agreed commit.

## 7. Architect approval

This section is reserved for the Architect. The preparer leaves every status **PROPOSED**. The Architect writes one of `APPROVED`, `REJECTED`, or `BLOCKED` on each row, or leaves `PROPOSED`.

D-05 closes only when the approved text is stored in `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` on the agreed commit and that entry contains the implementation grant, the new filename, and the one content-manifest line required by §4. A mark in this table, while this file is the only copy, does not close D-05 and does not authorize implementation.

| # | Subject | Status entering this section | Architect status | Architect note |
|---|---|---|---|---|
| Scope | Option B for `pack_content.series_build` only | **PROPOSED** | | |
| 1 | `tenant_id UUID NULL`, no default | **PROPOSED** | | |
| 2 | Foreign key to `public.tenants(id)`, no `ON DELETE` | **PROPOSED** | | |
| 3 | Primary key stays `id`; no Workspace column | **PROPOSED** | | |
| 4 | No historical backfill in the attribution migration | **PROPOSED** | | |
| 5 | No change to `active_build_id` or `video_production.series_build_id` | **PROPOSED** | | |
| 6 | No RLS and no grant change | **PROPOSED** | | |
| 7 | Index on `tenant_id` is independent and is not required | **PROPOSED** | | |
| 8 | D-06 stays unstarted; the column does not satisfy GATE A | **PROPOSED** | | |
| Line 231 | Kept in full. The limited exception in §3 is proposed and is not in force | **PROPOSED** | | |

Architect decision:

- Phase 1.5 remains **NOT PASS**.
- Phase 2 remains closed.
- D-06 remains unstarted.
- Implementation remains **NOT GRANTED** until §4 is satisfied.
- D-05 remains open until the Register entry described in §4 exists.

| Field | Architect entry |
|---|---|
| Name | |
| Date | |
| Disposition | `APPROVED` / `REJECTED` / `RETURNED` |
| Line 231 | `KEPT` / `KEPT WITH THE §3 EXCEPTION` |
| Register entry required before any migration file | Yes. This table does not replace it. |

## Stop

This step created only `STEP-035-D05-ARCHITECT-DECISION.md`.

No product file, migration, manifest, solution, ADR, Decision Register, or Master Architecture file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

D-05 is not closed. D-06 was not started. No Step 028 row was marked PASS. Phase 1.5 remains **NOT PASS**. Phase 2 was not opened.

**No implementation permission granted.**
