# STEP 032 — Architect Disposition: D-04, Register line 231

Status: **ARCHITECT DECISION RECORD.** D-04 only.  
Phase 1.5: **NOT PASS.**  
Baseline for acceptance checks: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate. Phase 2 is not opened.

This file records the D-04 disposition for Decision Register line 231. It preserves ADR-03 Option 1. It separates architecture policy, implementation evidence, and permission to implement. It prepares a bounded proposal of the questions that must be answered before any implementation authorization.

It does not close D-05 or D-06. It does not mark any Step 028 gate row PASS. It does not lift line 231. It does not authorize a migration, an API change, a database session, a deployment, or publication.

No product file, migration, manifest, ADR, Decision Register, Master Architecture, governance file, or application code was edited. Migration 376 was not created, reconstructed, or read. `ProductionTenantActionFilter` was not implemented and was not removed. No commit, push, merge, reset, clean, deploy, migration, or production database access was performed. No test or build was run. No stash object was read. No broad repository audit was run.

## Labels

| Label | Meaning in this record |
|---|---|
| DECIDED | ADR-03 Option 1, already closed in the tracked decision file. This step preserves it. |
| VERIFIED | A path check in this step, limited to the named paths at baseline `d54652d4`. |
| RECORDED | Step 027, Step 028, Step 029, Step 030, or Step 031 states the fact. This step did not re-count it. |
| UNVERIFIED | A tracked sentence claims an implementation fact, and the cited file is not in the baseline tree. |
| NOT GRANTED | This step does not give permission to change schema, API, database, deployment, or publication. |
| UNKNOWN | No source used here answers the question. |

Level 4 documents, gitignored files, untracked files, and stash objects do not become implementation evidence by being named here.

## Evidence boundary

| Source | Role here | Limit |
|---|---|---|
| Architect writing in the Step 032 task | Closes the D-04 disposition: policy stays DECIDED, line 231’s authorization sentence stays in force, and a question list is the bounded proposal. | Does not authorize implementation. Does not close D-05 or D-06. |
| `STEP-027-AUTHORITY-GAP-CLOSURE-DECISION.md` | Blobs, line 231 text, migration 315 shape, migration 376 absence, six-attribute gap. | Not re-opened as an audit. Stash was not read there and is not read here. |
| `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md` | AG-G2-03, AG-G3-02, AG-G4-01, AG-G4-02, AG-G4-03. Blocker B-06. | No row is marked PASS. |
| `STEP-029-ARCHITECT-DISPOSITION-REGISTER.md` | D-04 was open. Options D-04-A, D-04-B, and D-04-C were proposals. | Not edited. Those proposals are not reused as a grant. |
| `STEP-030-ARCHITECT-DISPOSITION-D01-D02.md` | Baseline `d54652d4` and the authority hierarchy. | Left in place. |
| `STEP-031-ARCHITECT-DISPOSITION-D03-C01-C04.md` | C-01 through C-04. Phase 1.5 **NOT PASS**. | D-04 was still open there. |
| This step, path checks only | `git rev-parse` of HEAD and of the local ref `origin/main`. `git show` line extracts and `git rev-parse <commit>:<path>` for the paths below. `git grep` for `ProductionTenantActionFilter`, `pack_content.series_build`, and `ALTER TABLE pack_content.series_build` at the baseline. `git log -1` for the last commit that touched each cited path. `git status --short` and `git check-ignore -v` for the migration 376 path and the three forensic paths named by Step 027. | No fetch. No stash read. No file contents taken from an untracked or gitignored path. |

Local HEAD matches the baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`.

The local remote-tracking ref `origin/main` is `73d3c4a6c509ab25229d07a475229a6c8c1cf79d`. That ref is an ancestor of the baseline. No fetch was performed. Whether the remote host has moved since this ref was last updated is **UNKNOWN**.

Working-tree hashes of the three authority files match the baseline blobs. The lines quoted below are the baseline text.

| Path | Blob on `d54652d4` | Last commit touching the path, reachable from the baseline |
|---|---|---|
| `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` | `9065fc9a264f592a19759a6d0d8ef52d95321756` | `d54652d4a7734b3116db62bd356a831f9942fc73` |
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | `e199ed02a4b119686a7a3f8f934e49dbba1502d4` | `d54652d4a7734b3116db62bd356a831f9942fc73` |
| `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` | `128a7996fb2007bd2a595af3f15391b25087450f` | `d54652d4a7734b3116db62bd356a831f9942fc73` |

## 1. Register line 231

**Path:** `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`  
**Blob:** `9065fc9a264f592a19759a6d0d8ef52d95321756`  
**Commit:** `d54652d4a7734b3116db62bd356a831f9942fc73`

**VERIFIED** text. Line 231, in full:

> **DECISION CLOSED ≠ IMPLEMENTATION COMPLETE.** Production is **not** tenant-safe today. GATE A is not executed. No TenantId/WorkspaceId/RLS/API change is authorized by this record.

Step 027 and Step 028 quoted the authorization sentence inside that line. The full line is the sentence this step uses.

That line is the Decision Register’s authorization limit for this gate. It states three facts together:

1. Closing the architecture decision is not the same thing as finishing implementation.
2. GATE A is not executed.
3. The Decision Register record does not authorize a TenantId, WorkspaceId, RLS, or API change.

### Later paragraphs do not lift the line

These tracked sentences say the “GATE A not executed” wording is stale because Migration 376 and fail-closed writes exist. Their factual premise is **UNVERIFIED**. The files they cite are not in the baseline tree. The paragraphs do not authorize a schema or API change.

| Location | Claim that depends on missing files |
|---|---|
| Register line 164 | Migration 376 adds nullable `tenant_id` and does not backfill. Cited forensic §3 and Master Architecture §28.1 record fail-closed writes. |
| Register line 604 | “GATE A not executed” is stale for the tenant column. |
| Register line 630 | “GATE A has not been executed” is stale for migration 376 and for fail-closed new Production writes. |
| Register line 677 | “GATE A not started” is stale for migration 376. |
| Register line 693 | Migration 376 added nullable `tenant_id` without backfill. The row marks documentation drift on “GATE A not executed” and “Production is not tenant-safe today.” |
| ADR-03 line 156 | The same migration adds nullable `tenant_id` on `series_build` and production tables, and new official Production OS writes fail closed. |

Step 027 recorded the same premise as **UNVERIFIED**. This step does not treat those paragraphs as a lift of line 231.

### Disposition of the line

**The authorization sentence still binds.**

Register line 231 remains the binding authorization limit. This step does not lift it. ADR-03 Option 1 does not lift it. A paragraph that calls the sentence stale does not lift it.

This result is the authorization half of D-04: no TenantId, WorkspaceId, RLS, or API change is authorized. It is not D-04-B. It is not an authorization of a named schema or API edit. The bounded proposal in §4 names questions only.

AG-G2-03 and AG-G4-03 stay as Step 028 left them. This file does not mark either row PASS. Step 028 requires the disposition to be in a tracked decision before those rows can pass. This file is the Step 032 record. It was not committed. Phase 1.5 remains **NOT PASS**.

## 2. ADR-03 policy that remains DECIDED

**Path:** `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md`  
**Blob:** `e199ed02a4b119686a7a3f8f934e49dbba1502d4`  
**Commit:** `d54652d4a7734b3116db62bd356a831f9942fc73`

**DECIDED.** Option 1 remains tenant-only Production scope. This step does not reopen it.

Line 16: ADR-03 is **DECIDED**.  
Line 18: **OPTION 1 — TENANT-ONLY PRODUCTION SCOPE**.

Formal statement, lines 22–26:

> Production execution must be attributable to the platform Tenant represented by `public.tenants`.
>
> Workspace is NOT a mandatory Production boundary.
>
> Workspace remains a separate KIT Platform primitive and may be used by products that have a genuine workspace concept, but Production Engine scope must not inherit Workspace merely because `kit_workspace` exists.

Architectural rules that this disposition preserves:

- Line 32. Tenant is the mandatory isolation boundary for Production.
- Line 34. A Production work item cannot be authorized merely because the caller knows a BuildId, TaskId, ArtifactId, or StorageKey.
- Line 35. Production identifiers such as BuildId, ArtifactId, and TaskId must not become global authorization identities.
- Lines 36–37. Workspace is optional and product-dependent. Workspace is not required for Famixa Production.

Register line 229 states the same policy: Production execution must be attributable to the platform Tenant represented by `public.tenants`, and Workspace is not a mandatory Production boundary.

GATE A, lines 118–127, remains the requirement and still withholds the mechanism:

- Production load/store paths must refuse work that is not attributable to the caller’s `public.tenants` tenant.
- BuildId / TaskId / ArtifactId / StorageKey must not bypass tenant authorization.
- Physical artifact location must not permit one tenant to overwrite another tenant’s logical object.
- Actor provenance should use platform identity rather than the current VARCHAR-only actor where required by the future execution model.
- Line 127: do not design how this is implemented in the ADR-03 record.
- Line 129: **DECISION CLOSED ≠ IMPLEMENTATION COMPLETE.**

Lines 64–79 say what the decision does not mean. The lines that matter here: TenantId is not already present on Production tables; Production is not already tenant-safe; RLS is not already implemented; existing Production rows are not already attributable to tenants; current implementation remains unchanged.

Those “does not mean” sentences and line 231 agree. ADR-03 lines 156–160 claim a later implemented state. That claim is **UNVERIFIED**, as recorded in §1 and §3. The unverified claim does not replace Option 1, and it does not authorize implementation.

## 3. Verified schema and tracked-code facts

### 3.1 `pack_content.series_build`

**VERIFIED** shape at the baseline.

| Item | Evidence |
|---|---|
| Path | `migrations/315_pack_content_series_build.sql` |
| Blob | `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d` |
| Last commit touching the path | `d35a347754c99fb321856d0284a898a100165d75` |
| Manifest | `deploy/ubuntu/migration-files.content.txt` line 24 lists `315_pack_content_series_build.sql` |
| Manifest blob | `10053f6ae9043c955f0fa208a9d499e78482df65` |
| Manifest last commit | `ceb521ff552c964c805ddbdb2b7a27204aab5bef` |

The `CREATE TABLE` at line 5 defines `pack_content.series_build` with `id`, `series_code`, `episode_code`, `title`, `status`, `shot_count`, `voice_lines`, `kf_count`, `video_count`, `graph_json`, `created_at`, and `updated_at`. There is no tenant attribution column and no workspace column.

The same file grants `SELECT, INSERT, UPDATE, DELETE` to roles `kitplatform` and `pharmacore` when those roles exist. `git grep` for `ROW LEVEL SECURITY` and `CREATE POLICY` in that file at the baseline finds no match. Tracked RLS on `series_build` is absent from this migration.

`git grep` for `ALTER TABLE pack_content.series_build` at the baseline, across `migrations/*.sql`, finds no match. `git grep` for `pack_content.series_build` in tracked migrations finds only `migrations/315_pack_content_series_build.sql`.

The content manifest does not list a 376 file.

### 3.2 Migration 376 and the cited forensic paths

**VERIFIED** absence from the baseline tree.

`git cat-file -e d54652d4:migrations/376_pack_content_production_tenant_boundary.sql` fails. `git status --short` reports the path as untracked. `git check-ignore -v` does not match it. The on-disk file was not read. An untracked file is not implementation evidence. Step 027 recorded that the path also appears on `refs/stash` and `stash^3` (`c170af39`), which are not ancestors of HEAD. That object was not read in Step 027 and was not read here.

These three paths are absent from the baseline. `git check-ignore -v` reports `.gitignore:12:docs/*` for each. `git cat-file -e` for `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md` reports that the path exists on disk and is not in `d54652d4`. That file was not read. Gitignored contents are not implementation evidence.

- `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md`
- `docs/forensics/PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01.md`
- `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md`

ADR-03 line 156, Register line 164, and Master Architecture line 433 claim Migration 376 already added nullable `tenant_id`. That claim is **UNVERIFIED**. This step does not recreate the migration in order to test the claim.

### 3.3 Master Architecture sentences about the same gap

Blob `128a7996fb2007bd2a595af3f15391b25087450f`. These lines were read from the baseline blob. The file was not edited.

| Line | Tracked sentence | Relation to the checks above |
|---|---|---|
| 179 | Current Famixa Production OS implements a product-shaped subset without tenant/workspace. | Consistent with the verified `series_build` shape. A full inventory of every Production OS table was not performed in this step. |
| 199 | Cross-product generic execution is architecturally unsafe because there is no tenant/workspace on Production OS or `series_build`. | Same limit. `series_build` is the table checked here. |
| 433 | Migration 376 added the nullable column and did not backfill. | **UNVERIFIED.** The cited migration is not in the baseline tree. |
| 590 | Tenant isolation is **ABSENT** on Production OS rows/disk. | Tracked requirement statement. This step did not measure disk layout. |
| 666 | Production OS and `series_build` lack TenantId / WorkspaceId. | The `series_build` half is **VERIFIED** by migration 315. |

### 3.4 Six `ProductionTenantActionFilter` attributes

**VERIFIED** gap at the baseline.

| Item | Evidence |
|---|---|
| Path | `src/KitPlatform.Api/Controllers/Content/ContentController.cs` |
| Blob | `a2895644572b109994444dd5d05b05f9010593d8` |
| Route | `[Route("api/content")]` at line 15 |
| Last commit touching the path | `98f0632cab983750edb032eb365c91b15db5eb44` |

`git grep` for `ProductionTenantActionFilter` at the baseline finds exactly six lines, all in that file. `git grep` for `class ProductionTenantActionFilter` finds no match in the baseline tree.

| Attribute line | Action | HTTP route under `api/content` |
|---|---|---|
| 449 | `ListSeriesBuilds` | `GET series/builds` |
| 465 | `GetSeriesBuild` | `GET series/builds/{id:guid}` |
| 485 | `PutSeriesBuild` | `PUT series/builds` |
| 505 | `DeleteSeriesBuild` | `DELETE series/builds/{id:guid}` |
| 1969 | `ListProductionProgress` | `GET video-engine/production-progress` |
| 1985 | `GetProductionProgress` | `GET video-engine/series/builds/{id:guid}/production-progress` |

Each attribute is `[ServiceFilter(typeof(ProductionTenantActionFilter))]`. No tracked type implements that filter. ADR-03 line 127 does not choose filter, service, or repository. Line 231 does not authorize adding the type or removing the attributes.

Step 029 left the six action names **UNKNOWN** because that register did not reopen the controller. The names above are the targeted check in this step. Naming them does not authorize an API change.

## 4. Bounded proposal

This proposal identifies the minimum questions that must be answered in a later Architect writing before implementation can be authorized. Answering them is not done here. A future writing that answers them still has to grant permission in its own words. Policy, including ADR-03 Option 1, does not supply that permission.

1. **Authorization sentence.** Which later tracked decision, if any, will lift Register line 231? The writing has to stand without Migration 376, without the three gitignored forensic paths, and without stash `c170af39` as proof. Until that writing exists, line 231 remains in force.
2. **Tenant attribution schema.** Is a tenant column on `pack_content.series_build` in scope for a later phase? If it is, the writing names the column, the nullability, the new tracked migration filename, and the single manifest entry. The new file is a new migration. It is not a copy of the untracked 376 path. Historical backfill is its own question. This question is D-05. This step does not answer it.
3. **Enforcement location.** Where would tenant refusal run if an API change is later authorized: a tracked filter type, named service methods, a named repository boundary, or a written removal of the six attributes? ADR-03 does not choose. Removal without that writing is not a passing disposition. This question is D-06. This step does not answer it.
4. **Six API attributes.** Are the six actions in §3.4 the complete surface a later change may touch? Any action omitted from that later writing stays unchanged.
5. **Isolation tests.** Which named checks show that a caller cannot load or store Production work outside the caller’s `public.tenants` tenant, and that BuildId, TaskId, ArtifactId, and StorageKey do not bypass that refusal? The check list has to say that the run uses no production database, no deploy, and no paid provider. Step 028 AG-G4-02 records the test list as **UNKNOWN**. This step does not invent the list and does not run it.
6. **Phase 1.5 acceptance.** Which mandatory Step 028 rows may be re-checked after the answers above exist on the agreed commit? This step does not re-score them. Phase 1.5 stays **NOT PASS** while any mandatory row is unsatisfied.

Anything outside those six questions stays unauthorized. This proposal does not name a column type, a filter body, a test body, or a migration body.

## 5. Separate statuses

| State | Status | What that status allows |
|---|---|---|
| Architecture policy | **DECIDED.** ADR-03 Option 1. Tenant is the mandatory Production isolation boundary. Workspace is not a substitute for Tenant. BuildId, TaskId, ArtifactId, and StorageKey do not independently authorize access. | The policy stands. It does not authorize a file change. |
| Implementation | **VERIFIED** only for the facts in §3. `series_build` has no tenant column in migration 315. Migration 376 is not in the baseline. No tracked type implements the six attributes. Claims that tenant attribution is already implemented are **UNVERIFIED**. | A verified gap is evidence. It is not permission to close the gap. |
| Implementation permission | **NOT GRANTED.** Line 231 still binds. This step does not grant a schema, API, RLS, database, deployment, or publication change. | No migration, filter, attribute removal, or test run follows from this file. |

GATE A’s own sentence applies: decision closed is not implementation complete.

## 6. Remaining dependencies

These stay open. This step does not start them.

| Dependency | State after this step | Why it remains |
|---|---|---|
| Tenant attribution schema | Open. D-05 was not started. | Migration 315 has no tenant column. No tracked `ALTER` adds one. Line 231 does not authorize a TenantId change. AG-G3-02 stays as Step 028 left it. |
| Enforcement location | Open. D-06 was not started. | ADR-03 line 127 does not choose filter, service, or repository. The six attributes have no tracked type. AG-G4-01 stays as Step 028 left it. |
| Six API attributes | Identified, unimplemented. | The six actions are listed in §3.4. Identification is not an edit. Adding a type, or removing an attribute, waits on a later authorization and on the enforcement-location decision. |
| Isolation tests | **UNKNOWN.** | Step 027 and Step 028 record no tracked Phase 1.5 test list, and neither step ran the checks. AG-G4-02 stays **UNKNOWN**. This step did not run tests. |
| Phase 1.5 acceptance | **NOT PASS.** | Step 028 records mandatory rows other than AG-G1-01 as FAIL, BLOCKED, or UNKNOWN. Step 030 and Step 031 did not mark those rows PASS. This step does not mark them PASS. |

D-07, D-08, and D-09 were not started.

## 7. Permission

This step grants no migration permission, no API permission, no database permission, no deployment permission, and no publication permission.

It does not authorize push, commit, merge, reset, clean, deploy, a production database session, or a migration run. It does not authorize creating or reconstructing `migrations/376_pack_content_production_tenant_boundary.sql`. It does not authorize implementing or removing `ProductionTenantActionFilter`. It does not authorize an edit to ADR-03, the Decision Register, Master Architecture, any manifest, or application code. It does not authorize reading stash or an untracked file as proof that implementation already exists.

## Stop

This step created only `STEP-032-ARCHITECT-DISPOSITION-D04-LINE-231.md`.

No product file, migration, manifest, solution, ADR, Decision Register, or Master Architecture file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

D-05 and D-06 were not started. No implementation follows this report. Phase 1.5 remains **NOT PASS**.
