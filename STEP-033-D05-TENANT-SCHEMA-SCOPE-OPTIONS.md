# STEP 033 — D-05 Tenant Attribution Schema Scope

Status: **OPTIONS REPORT.** Decision preparation only. No option is selected.  
Phase 1.5: **NOT PASS.**  
Baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate. Phase 2 is not opened.

This file compares two schema-scope choices for `pack_content.series_build`. It preserves ADR-03 Option 1. It preserves Register line 231 as the binding authorization limit recorded in Step 032. It does not choose a column type, nullability, backfill strategy, or enforcement mechanism.

It does not close D-05. It does not start D-06. It does not mark any Step 028 gate row PASS. It does not lift line 231. It does not authorize a migration, an API change, a database session, a deployment, or publication.

No product file, migration, manifest, ADR, Decision Register, Master Architecture, governance file, or application code was edited. Migration 376 was not read. The working-tree copy of the content manifest was not used. No commit, push, merge, reset, clean, deploy, migration, or production database access was performed. No test or build was run. No stash object was read.

## Labels

| Label | Meaning in this report |
|---|---|
| DECIDED | ADR-03 Option 1, already closed. This step preserves it. |
| VERIFIED | Read in this step from the baseline blob of migration 315, the tracked content manifest, or ADR-03. |
| RECORDED | Step 032 states the fact. This step did not re-open the Decision Register, Master Architecture, Step 028, or the controller. |
| UNVERIFIED | A tracked sentence claims an implementation fact, and Step 032 records that the cited file is not in the baseline tree. |
| NOT GRANTED | This step does not give permission to change schema, API, database, deployment, or publication. |
| UNKNOWN | No source used here answers the question. |
| NOT CHOSEN | An open decision this step is required to name and forbidden to answer. |

Level 4 documents, gitignored files, untracked files, and stash objects do not become implementation evidence by being named here.

## Evidence boundary

Local HEAD is the baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`.

| Source | Role here | Limit |
|---|---|---|
| `migrations/315_pack_content_series_build.sql` | Current tracked schema of `pack_content.series_build`. | Baseline blob `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d`. Working-tree hash matches that blob. |
| `deploy/ubuntu/migration-files.content.txt` at `d54652d4` | Tracked content-manifest ownership of migration 315. | Baseline blob `10053f6ae9043c955f0fa208a9d499e78482df65`. The working-tree hash is `6bc5f545035c62d4a9ce9f45e1fe9ab2380ad839` and `git status --short` reports the path modified. This step uses the baseline blob only. |
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | Decided tenant-only policy and GATE A requirements. | Baseline blob `e199ed02a4b119686a7a3f8f934e49dbba1502d4`. Working-tree hash matches that blob. |
| `STEP-032-ARCHITECT-DISPOSITION-D04-LINE-231.md` | D-04 disposition, line 231 text, gate identifiers, and the recorded absence of a tracked tenant migration. | Not re-audited. Stash was not read there and is not read here. |

Step 032 records that `git grep` for `ALTER TABLE pack_content.series_build` at the baseline, across `migrations/*.sql`, finds no match, and that `pack_content.series_build` appears in tracked migrations only in `migrations/315_pack_content_series_build.sql`. This step does not repeat that search. Those two sentences stay **RECORDED**.

## 1. Verified current schema

**VERIFIED** from migration 315 at the baseline blob above.

The file header states that each row is one Famixa Series production snapshot, that the migration belongs on `deploy/ubuntu/migration-files.content.txt` only, and that `graph_json` holds script, shots, locks, and take URLs, with image and audio bytes staying on the operator machine.

### 1.1 `pack_content.series_build`

`CREATE TABLE IF NOT EXISTS pack_content.series_build` defines these columns:

| Column | Definition in migration 315 |
|---|---|
| `id` | `UUID PRIMARY KEY` |
| `series_code` | `VARCHAR(64) NOT NULL` |
| `episode_code` | `VARCHAR(32) NOT NULL DEFAULT ''` |
| `title` | `VARCHAR(240) NOT NULL DEFAULT ''` |
| `status` | `VARCHAR(32) NOT NULL DEFAULT 'draft'` |
| `shot_count` | `INTEGER NOT NULL DEFAULT 0` |
| `voice_lines` | `INTEGER NOT NULL DEFAULT 0` |
| `kf_count` | `INTEGER NOT NULL DEFAULT 0` |
| `video_count` | `INTEGER NOT NULL DEFAULT 0` |
| `graph_json` | `JSONB NOT NULL DEFAULT '{}'::jsonb` |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT NOW()` |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT NOW()` |

Constraints in the same `CREATE TABLE`:

| Name | Rule |
|---|---|
| `ck_series_build_code` | `series_code ~ '^[A-Z][A-Z0-9_]{1,62}$'` |
| `ck_series_build_status` | `status IN ('draft', 'script_locked', 'voice_locked', 'in_prod', 'final')` |

Other statements in the same file that touch this table:

| Statement | Effect |
|---|---|
| `CREATE INDEX IF NOT EXISTS ix_series_build_series_updated` | Index on `(series_code, updated_at DESC)` |
| `COMMENT ON TABLE pack_content.series_build` | Describes a Famixa Series production snapshot whose graph is stored and whose KF, TTS, and take files stay local |
| `GRANT SELECT, INSERT, UPDATE, DELETE` | Granted to `kitplatform` and to `pharmacore` only when that role exists |

The `CREATE TABLE` contains no tenant attribution column and no workspace column. The file contains no `REFERENCES public.tenants`, no `ROW LEVEL SECURITY`, and no `CREATE POLICY`. Step 032 recorded the same RLS absence from a baseline `git grep` of this file. That record agrees with the file text read here.

`CREATE TABLE IF NOT EXISTS` does not add a column when the table already exists. The only `ALTER TABLE` in this file targets `pack_content.series_pilot`, adding `active_build_id UUID` when that column is absent. It does not declare a foreign key to `series_build.id`.

### 1.2 Tracked manifest ownership of this schema

**VERIFIED** on blob `10053f6ae9043c955f0fa208a9d499e78482df65`.

| Item | Baseline fact |
|---|---|
| Line 24 | `315_pack_content_series_build.sql` |
| Line 62, last line | `367_pack_content_character_reference_pack_contract.sql` |
| Line count | 62 |
| Header | Content park list. It says not to merge the list into `migration-files.prod.txt` until GA is explicitly approved. |

The baseline manifest does not name a tenant-boundary migration. Step 032 recorded the same absence, including the absence of `migrations/376_pack_content_production_tenant_boundary.sql` from the baseline tree. That absence stays **RECORDED**. This step did not read that path.

### 1.3 Policy that this schema does not implement

**DECIDED.** ADR-03 Option 1 remains tenant-only Production scope.

Formal statement, lines 22–26: Production execution must be attributable to the platform Tenant represented by `public.tenants`. Workspace is not a mandatory Production boundary.

Rules that set the isolation requirement, lines 32–35:

- Tenant is the mandatory isolation boundary for Production.
- A Production work item cannot be authorized merely because the caller knows a BuildId, TaskId, ArtifactId, or StorageKey.
- Production identifiers such as BuildId, ArtifactId, and TaskId must not become global authorization identities.

GATE A, lines 118–127, remains the requirement and still withholds the mechanism. Load and store paths must refuse work that is not attributable to the caller’s `public.tenants` tenant. BuildId, TaskId, ArtifactId, and StorageKey must not bypass that authorization. Physical artifact location must not permit one tenant to overwrite another tenant’s logical object. Actor provenance should use platform identity rather than the current VARCHAR-only actor where the future execution model requires it. Line 127: do not design how this is implemented in the ADR-03 record.

Lines 64–70 and line 79 say what the decision does not mean, including: TenantId is not already present on Production tables; Production is not already tenant-safe; RLS is not already implemented; storage is not already tenant-isolated; existing Production rows are not already attributable to tenants; current implementation remains unchanged.

ADR-03 lines 156–160 claim that `migrations/376_pack_content_production_tenant_boundary.sql` already added nullable `tenant_id` and did not backfill. Step 032 records that claim as **UNVERIFIED**. This step does not use it as the current schema. The current schema is §1.1.

Register line 231, as Step 032 verified and quoted in full:

> **DECISION CLOSED ≠ IMPLEMENTATION COMPLETE.** Production is **not** tenant-safe today. GATE A is not executed. No TenantId/WorkspaceId/RLS/API change is authorized by this record.

That sentence stays **RECORDED** and binding. This step does not lift it.

## 2. Option A — keep the schema change out of the current phase

Option A leaves `pack_content.series_build` as migration 315 defines it through the current phase. No new tenant-attribution migration is in scope while Phase 1.5 remains the open phase and line 231 remains in force. Workspace stays outside the Production isolation contract under ADR-03 Option 1. A missing workspace column is not treated here as a tenant-isolation defect.

This report does not select Option A.

### Tenant-isolation requirements that remain unmet

These requirements come from ADR-03. Option A leaves each of them unsatisfied for `series_build` in the current phase.

| Requirement | Source | Why it remains unmet under Option A |
|---|---|---|
| A Production row must be attributable to `public.tenants`. | ADR-03 lines 22 and 32 | The verified column list has no attribute that stores or references that tenant. |
| A work item must not be authorized merely because the caller knows a BuildId, TaskId, ArtifactId, or StorageKey. | ADR-03 lines 34 and 122–123 | The table’s only key is `id UUID`. The schema carries no tenant attribute a refusal could compare to the caller’s tenant. The refusal behavior itself remains D-06. |
| BuildId, ArtifactId, and TaskId must not become global authorization identities. | ADR-03 line 35 | The tracked primary key is a single UUID with no tenant component. |
| RLS on this table. | ADR-03 line 68; migration 315 | The migration has no `ROW LEVEL SECURITY` and no `CREATE POLICY`. Step 032 recorded the same absence. Line 231 also withholds authorization to add RLS. |
| Existing Production rows attributable to a tenant. | ADR-03 line 70 | The verified schema has no column in which that attribution can be stored. |
| GATE A as a whole, including load/store refusal. | ADR-03 lines 118–129; Register line 231 as recorded in Step 032 | Decision closed remains separate from implementation complete. Step 032 records six `[ServiceFilter(typeof(ProductionTenantActionFilter))]` attributes and no tracked type that implements the filter. Option A does not add that type. |
| Isolation tests that show the refusal. | Step 032 on AG-G4-02 | Step 032 records the test list as **UNKNOWN**. Option A does not create the list and does not run it. |

Two GATE A sentences stay short of a new measurement in this step:

| GATE A sentence | Status in this step |
|---|---|
| Physical artifact location must not permit one tenant to overwrite another tenant’s logical object. ADR-03 line 124. | ADR-03 line 69 states that storage is not already tenant-isolated. Step 032 did not measure disk layout. This step did not measure it. Satisfaction on disk is **UNKNOWN** here. The requirement stays open. |
| Actor provenance should use platform identity where the future execution model requires it. ADR-03 line 125. | `series_build` as created in migration 315 has no actor column. Whether an actor is stored inside `graph_json`, or on another table, is **UNKNOWN**. This step does not decide that `series_build` must gain an actor column. |

Option A also leaves these Step 032 facts in place: no tracked `ALTER` of `pack_content.series_build`, no tracked implementation of the six filter attributes, and Phase 1.5 **NOT PASS**.

## 3. Option B — a later checkpoint may authorize one new migration

Option B is a shape for a later Architect writing. That writing would authorize one new, separately named migration whose subject is tenant attribution on `pack_content.series_build`, plus one new entry on the tracked content manifest. The writing would be its own checkpoint. It would not be this file. It would not be a reconstruction of the untracked 376 path. Step 032 question 2 already named that shape and left it unanswered.

This report does not select Option B. Describing it does not authorize the migration.

### Decisions still required

Each item below is **NOT CHOSEN**.

| # | Decision the later writing would have to make | Bound recorded here |
|---|---|---|
| 1 | Whether Option B is the selected scope. | This step does not select it. |
| 2 | Which later tracked decision lifts Register line 231, or grants the schema change in its own words. | Until that writing exists, line 231 remains in force. Step 032 requires that writing to stand without Migration 376, without the three gitignored forensic paths, and without stash `c170af39`. |
| 3 | Column name. | Not chosen. |
| 4 | Column type. | Not chosen. |
| 5 | Nullability. | Not chosen. |
| 6 | Whether the column references `public.tenants`, and what constraint form is used. | Not chosen. |
| 7 | Whether a new index includes the attribution column. | Not chosen. |
| 8 | Enforcement mechanism: RLS in that migration, RLS in another migration, a tracked filter type, named service methods, a named repository boundary, removal of the six attributes, or some other mechanism. | Not chosen. ADR-03 line 127 does not choose. This item is D-06. This step does not start D-06. |
| 9 | New migration filename. | Not chosen. Step 032 requires a new name. The name is not the untracked 376 path. |
| 10 | The single content-manifest line: filename and position in the ordered content list. | Not chosen. The baseline list ends at line 62 with `367_pack_content_character_reference_pack_contract.sql`. |
| 11 | Whether any table other than `pack_content.series_build` is in that migration. | Not chosen. The verified subject of D-05 in Step 032 question 2 is `series_build`. ADR-03 line 156 names other production tables inside an **UNVERIFIED** implementation claim. That sentence is not a scope decision. |
| 12 | Historical backfill: whether existing rows are in scope, which source would supply a tenant, and what happens to a row that cannot be attributed. | Not chosen. |
| 13 | Whether role grants from migration 315 change. | Not chosen. |
| 14 | Whether a workspace column is added. | Not chosen. ADR-03 Option 1 does not require one. |
| 15 | How `pack_content.series_pilot.active_build_id` relates to a tenant-attributed build. | Not chosen. |
| 16 | Which named isolation checks accompany the migration, and on which commit they are run. | Not chosen. Step 032 records AG-G4-02 as **UNKNOWN**. |
| 17 | Which later checkpoint receives the grant, and which Step 028 rows may be re-checked only after that grant is on the agreed commit. | Not chosen. |

A column, a nullable column, a backfill, and an enforcement mechanism are all inside that list. None is selected here.

`CREATE TABLE IF NOT EXISTS` in migration 315 will not add an attribution column if the statement is applied again. A later change to an already-created table needs its own migration statement. The form of that statement is **NOT CHOSEN**. Whether any database has already applied migration 315 is **UNKNOWN**, because this step did not connect to a database.

## 4. Effects on historical rows, manifest ownership, and tests

### 4.1 Historical rows

| Known | Unknown |
|---|---|
| Migration 315’s `series_build` definition has no column that can store a `public.tenants` identity. | Whether any database has applied migration 315. |
| Re-applying that `CREATE TABLE IF NOT EXISTS` does not add a column to an existing table. | Whether any row exists. Row counts are unknown. |
| Step 032 records no tracked `ALTER TABLE pack_content.series_build` at the baseline. | Whether `graph_json` on any live row contains a tenant identifier. This step did not read row data or application writers. |
| ADR-03 line 70 states that existing Production rows are not already attributable to tenants. Lines 156–157 claim a later nullable column with no backfill. Step 032 records that claim as **UNVERIFIED**. | Which tenant, if any, would be the correct attribution for a historical row. |
| Option A adds no column, so it performs no backfill. | Under Option B, backfill scope, source, and treatment of an unattributable row remain **NOT CHOSEN**. |
| Option B, if a later writing selects it, would be the first tracked schema place where historical `series_build` rows could receive an attribution column. This step does not create that place. | Whether rows in `series_pilot`, or in any other table, point at `series_build.id` in a way that would have to move with the attribution. `active_build_id` is only a nullable UUID in migration 315, with no foreign key declared there. |

### 4.2 Manifest ownership

| Known | Unknown |
|---|---|
| Baseline content manifest blob `10053f6ae9043c955f0fa208a9d499e78482df65` lists `315_pack_content_series_build.sql` on line 24. | The contents of the modified working-tree manifest. This step did not use them. |
| That baseline file ends at line 62 with `367_pack_content_character_reference_pack_contract.sql`. | Which filename and which line position a later writing would choose. Both are **NOT CHOSEN**. |
| The header places this list on the content runner and withholds a merge into `migration-files.prod.txt` until GA is explicitly approved. | Whether a future content-manifest edit would also be proposed for another manifest. Option B, as Step 032 question 2 stated it, names a single manifest entry. This step does not edit any manifest. |
| Step 032 records that the baseline manifest does not list a 376 file, and that the 376 path is not in the baseline tree. | Nothing in the sources used here proves that a number after 367 is free in every other tracked list. This step did not search other manifests. |
| Option A leaves line 24 as the tracked schema owner of `series_build` and adds no entry. | |
| Option B would add one new content-manifest line for one new filename, in a later checkpoint, only if that checkpoint grants it. | |

The working-tree manifest hash differs from the baseline blob. That difference is not a schema decision and is not implementation evidence.

### 4.3 Tests

| Known | Unknown |
|---|---|
| Step 032 records AG-G4-02 as **UNKNOWN**: no tracked Phase 1.5 isolation-test list was established there, and that step did not run the checks. | Which existing tests assume the migration 315 column list. This step did not search tests and did not run them. |
| This step did not run a test or a build. | What a future check would assert about historical rows, null attribution, or cross-tenant refusal. Those assertions are **NOT CHOSEN**. |
| Step 032 question 5 requires any future check list to say that the run uses no production database, no deploy, and no paid provider. | Whether any current test fixture inserts into `pack_content.series_build`. |
| Option A adds no schema, so it adds no new test obligation of its own. The unmet isolation requirement in §2 stays open, and AG-G4-02 stays **UNKNOWN**. | |
| Option B does not, by itself, name or satisfy the checks. A later grant would still need decision 16 in §3. | |

## 5. Dependencies

### 5.1 D-04 authorization

**RECORDED** from Step 032. D-04’s authorization result is that Register line 231 still binds. ADR-03 Option 1 stays **DECIDED**. Implementation permission stays **NOT GRANTED**.

Step 032 question 2 is this step’s subject: whether a tenant column on `pack_content.series_build` is in scope for a later phase, and, if so, the column, the nullability, the new migration filename, and the single manifest entry. Step 032 did not answer it. This step compares Option A and Option B and still does not answer it.

Consequences for the two options:

| Option | Relation to D-04 |
|---|---|
| A | Compatible with leaving line 231 in force through the current phase. The schema gap in §1 remains. The unmet requirements in §2 remain. |
| B | Can become permission only in a later writing that grants it in its own words, including a disposition of line 231. This file is not that writing. |

AG-G2-03 and AG-G4-03 stay as Step 028 left them. Step 032 records that Step 028 requires the disposition to be in a tracked decision before those rows can pass. Step 032 was not committed. This file is not committed. Neither file marks those rows PASS.

### 5.2 D-06 enforcement

**RECORDED** as not started. This step does not start it.

ADR-03 line 127 withholds the implementation design. Step 032 question 3 leaves the enforcement location open: a tracked filter type, named service methods, a named repository boundary, or a written removal of the six attributes. Step 032 §3.4 names the six actions. Naming them is not an API change.

A schema option does not place the refusal. Option A leaves enforcement unmet. Option B’s later migration would still leave enforcement **NOT CHOSEN** unless the later writing also answers D-06. AG-G4-01 stays as Step 028 left it.

### 5.3 Step 028 acceptance gates

This step did not re-open `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md`. Gate bodies are not restated beyond what Step 032 recorded.

| Step 032 record | Dependency of this schema-scope report |
|---|---|
| AG-G3-02 stays as Step 028 left it, on the open tenant-attribution schema. | Option A leaves that schema gap in place. Option B would still require the granted migration to exist on the agreed commit before any re-check. This step does not re-score the row. |
| AG-G4-01 stays as Step 028 left it, on the open enforcement location. | Unchanged by a paper comparison of schema scope. D-06 remains unstarted. |
| AG-G4-02 is **UNKNOWN**. | Neither option invents or runs the isolation tests. |
| AG-G2-03 and AG-G4-03 wait on a tracked decision. | This file is an options report. It is not committed. It does not mark either row PASS. |
| Blocker B-06 is named from Step 028. | The blocker body was not re-opened. This step does not clear it. |
| Mandatory rows other than AG-G1-01 are FAIL, BLOCKED, or UNKNOWN. | This step does not re-score AG-G1-01 and does not mark any other mandatory row PASS. |
| Phase 1.5 is **NOT PASS**. | Both options leave Phase 1.5 **NOT PASS**. |

Step 032 question 6 asks which mandatory Step 028 rows may be re-checked after the D-05 and D-06 answers exist on the agreed commit. That question stays unanswered.

## 6. Permission

**No schema permission and no implementation permission are granted by this step.**

Register line 231 remains binding. ADR-03 Option 1 remains **DECIDED** and does not supply permission. Option A is not a grant to freeze the schema in a migration. Option B is not a grant to add a migration. Neither option chooses a column type, nullability, backfill strategy, or enforcement mechanism.

This step does not authorize a new file under `migrations/`, an edit to migration 315, an edit to `deploy/ubuntu/migration-files.content.txt`, an edit to ADR-03, an edit to the Decision Register, or an edit to application code. It does not authorize creating or reading `migrations/376_pack_content_production_tenant_boundary.sql`. It does not authorize implementing or removing `ProductionTenantActionFilter`. It does not authorize push, commit, merge, reset, clean, deploy, a production database session, or a migration run.

Phase 1.5 remains **NOT PASS**.

## Stop

This step created only `STEP-033-D05-TENANT-SCHEMA-SCOPE-OPTIONS.md`.

No product file, migration, manifest, solution, ADR, Decision Register, or Master Architecture file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

D-05 is not closed. D-06 was not started. No option was selected. No implementation follows this report. Phase 1.5 remains **NOT PASS**.
