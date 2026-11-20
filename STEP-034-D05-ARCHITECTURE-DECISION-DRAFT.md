# STEP 034 — D-05 Architecture Decision Draft

Status: **DRAFT AWAITING ARCHITECT APPROVAL.** Decision preparation only. No option in this file is approved.  
Phase 1.5: **NOT PASS.** This draft does not declare PASS and does not open Phase 2.  
Baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate.

The purpose of this draft is to remove one architecture blocker on the way to KIT product verification: a written scope for tenant attribution on `pack_content.series_build`. Completing the architecture is not the goal. The draft proposes the Option B shape from Step 033. That orientation is not permission to implement.

ADR-03 Option 1 stays **DECIDED**. Register line 231 stays binding. This draft does not lift it. It does not close D-05. It does not start D-06. It does not mark any Step 028 gate row PASS. It does not authorize a migration, a manifest edit, an API change, a database session, a deployment, or publication.

No product file, migration, manifest, ADR, Decision Register, Master Architecture, governance file, or application code was edited. Migration 376 was not read. The working-tree copy of the content manifest was not used. No commit, push, merge, reset, clean, deploy, migration, or production database access was performed. No test or build was run. No stash object was read.

## Labels

| Label | Meaning in this draft |
|---|---|
| DECIDED | ADR-03 Option 1, already closed. This draft preserves it. |
| VERIFIED | Read in this step from a baseline blob, or returned by a narrow read-only git query named below. |
| RECORDED | Step 032 or Step 033 states the fact, or Step 028 states a gate status that this step did not re-score. |
| INFERENCE | A conclusion drawn here from verified facts. It is not itself a measurement. |
| PROPOSED | A choice this draft asks the Architect to approve. It is not DECIDED. |
| UNVERIFIED | A tracked sentence claims an implementation fact, and Step 032 records that the cited file is not in the baseline tree. |
| NOT GRANTED | This draft does not give permission to change schema, API, database, deployment, or publication. |
| UNKNOWN | No source used here answers the question. |

Level 4 documents, gitignored files, untracked files, and stash objects do not become implementation evidence by being named here.

## Evidence boundary

Local HEAD was read with `git rev-parse HEAD`. It matches the baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`.

| Source | Role here | Limit |
|---|---|---|
| `migrations/315_pack_content_series_build.sql` | Current tracked schema of `pack_content.series_build` and of `active_build_id`. | Baseline blob, via `git show d54652d4:migrations/315_pack_content_series_build.sql`. Same blob Step 033 recorded: `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d`. |
| `migrations/313_pack_content_series_pilot.sql` | Tracked create of `pack_content.series_pilot`. | `git show` of that path at the baseline. |
| `migrations/002_identity.sql` | Tracked create of `tenants` and the type of `tenants.id`. | `git show` of that path at the baseline. The statement is unqualified `CREATE TABLE tenants`. |
| `deploy/ubuntu/migration-files.content.txt` | Tracked content-manifest end. | Step 033 verified blob `10053f6ae9043c955f0fa208a9d499e78482df65`, line 62 = `367_pack_content_character_reference_pack_contract.sql`. This step re-read the header only. The working-tree file was not used. |
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | Decided tenant-only policy and GATE A. | Baseline text already quoted by Step 032 and Step 033. Blob `e199ed02a4b119686a7a3f8f934e49dbba1502d4`. |
| `STEP-032-ARCHITECT-DISPOSITION-D04-LINE-231.md` | Line 231 still binds. D-05 question left open. | Not re-audited. |
| `STEP-033-D05-TENANT-SCHEMA-SCOPE-OPTIONS.md` | Option A and Option B. Seventeen items left **NOT CHOSEN**. | This draft proposes answers. It does not treat Step 033 as an approval. |
| `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md` | Gate identifiers this draft must not re-score. | Statuses stay as that proposal left them. |

Narrow read-only queries in this step, all against `d54652d4`:

| Query | Result used below |
|---|---|
| `git grep active_build_id -- migrations/*.sql` | One match: `migrations/315_pack_content_series_build.sql` line 29. |
| `git grep series_pilot -- migrations/*.sql` | Definitions in 313 and the `active_build_id` alter in 315. Migrations 333 and 383 name the table only in comments. |
| `git grep -l series_build` | Thirteen tracked paths, listed in §6.2. None is under `tests/`. |
| `git grep tenant` on the three non-document code paths from that list | No match. |
| `git grep series_build` on migrations 333 and 383 | Comment text, plus `video_production.series_build_id UUID` in 333. No `ALTER` of `pack_content.series_build`. |
| `git ls-tree` of `migrations/` prefixes from 367 upward | Tracked files: 367, 368, 369, 371, 383, 384. No tracked `370_`, `372_`–`382_`, `376_`, `385_`, or `386_`. |
| `git grep` of those prefixes in `deploy/ubuntu/migration-files*.txt` | 367 is on content line 62. 368 and 369 are on `migration-files.local-os.txt`. 371 is on `migration-files.prod.txt`. 383 and 384 are not named in those manifests. |
| `git grep` of the 383 and 384 filenames under `deploy/ubuntu` | No match. |
| `git grep "CREATE TABLE … tenants" -- migrations/*.sql` | One match: `migrations/002_identity.sql` line 3, `CREATE TABLE tenants`. No `CREATE TABLE public.tenants`. |

This step did not connect to a database. Whether any database has applied migration 002, 313, 315, or any later file is **UNKNOWN**. Production is not assumed to have applied, or to have skipped, any migration.

### Already-bounded tension

ADR-03 lines 156–160 claim that `migrations/376_pack_content_production_tenant_boundary.sql` already added nullable `tenant_id` and did not backfill. Step 032 records that claim as **UNVERIFIED**. Register line 231 still binds. The current schema used here is migration 315, as Step 033 §1.1 verified. This step did not find a new contradiction between that disposition, the baseline schema, and the tracked code it read. It does not reopen the 376 claim and does not use it as a column design.

## 1. Scope

### 1.1 In scope

**PROPOSED.** One later schema subject, and only that subject: tenant attribution on `pack_content.series_build`.

The proposed direction is Step 033 Option B. A later checkpoint, not this file, would authorize one new migration and one new line on the tracked content manifest. The migration’s only table change is an attribution column on `pack_content.series_build`. Section 2 states the proposed column. Section 3 forbids backfill inside that migration. Section 5 states what that later checkpoint still has to say before anyone may create the file.

**VERIFIED** current table, from migration 315 at the baseline:

| Column | Definition |
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

Constraints in that `CREATE TABLE`: `ck_series_build_code` and `ck_series_build_status`. Index: `ix_series_build_series_updated` on `(series_code, updated_at DESC)`. Grants: `SELECT, INSERT, UPDATE, DELETE` to `kitplatform` and to `pharmacore` only when that role exists. No tenant column. No workspace column. No `REFERENCES public.tenants`. No `ROW LEVEL SECURITY`. No `CREATE POLICY`.

`CREATE TABLE IF NOT EXISTS` does not add a column when the table already exists. A later attribution change has to be its own statement. Editing migration 315 is out of scope.

### 1.2 Out of scope

| Item | Why it is outside this draft |
|---|---|
| Option A as the selected scope | Not proposed. The Architect can still reject Option B and choose Option A. Choosing Option A would leave the table as migration 315 defines it and would add no migration. |
| Any table other than `pack_content.series_build` | Step 032 question 2 names `series_build`. ADR-03 line 156 names other production tables only inside an **UNVERIFIED** implementation claim. That sentence is not a scope decision. |
| `pack_content.series_pilot`, including `active_build_id` | Relationship is decided in §4 as “do not alter.” The column is not a tenant source. |
| `pack_content.video_production.series_build_id` | **VERIFIED** in migration 333 line 41: `series_build_id UUID`, with no foreign key on that line, and the comment calls it an optional Famixa adapter hook. This draft does not alter `video_production`. |
| `migrations/383_pack_content_article_series.sql` and `migrations/384_pack_content_brand_narrative_dna.sql` | **VERIFIED** tracked at the baseline, content-named, and not listed under `deploy/ubuntu`. Their manifest fate is Step 028 AG-G3-04. This draft does not edit them and does not list them. |
| A workspace column | ADR-03 Option 1. Workspace is not a mandatory Production boundary. A missing workspace column is not treated as the defect this draft answers. |
| RLS, `CREATE POLICY`, and the refusal mechanism | D-06. ADR-03 line 127 withholds the mechanism. |
| The six `ProductionTenantActionFilter` attributes and any change to `ContentController.cs` | **RECORDED** in Step 032 §3.4. This draft does not add a type and does not remove the attributes. |
| Role grants from migration 315 | Left unchanged. Whether `pharmacore` should keep DML after attribution is a separate decision. Evidence for that access change was not gathered. |
| Historical `UPDATE` backfill | Forbidden inside the proposed migration. §3 states the evidence still required before any later backfill. |
| A migration filename and a manifest line number | Not chosen. §5 says how a later writing chooses them. |
| Editing ADR-03, the Decision Register, or Master Architecture to withdraw the **UNVERIFIED** 376 sentences | AG-G2-02 still requires Architect approval before those edits. This draft does not edit them. |
| Phase 1.5 PASS, and every Step 028 row other than the re-check candidates named in §6 | Unchanged. |

## 2. Column, constraint, and nullability

Each recommended row is **PROPOSED**. Approval of the row is still not permission to write a migration. Register line 231 remains in force until the later writing in §5 grants the exception in its own words.

### 2.1 Column

| Choice | Draft | Evidence | What is still missing |
|---|---|---|---|
| Name `tenant_id` | **PROPOSED** | ADR-03 requires attribution to the platform Tenant represented by `public.tenants`. Migration 002 defines `tenants.id` as `UUID PRIMARY KEY`. Later tracked migrations, including `migrations/072_kit_core_iam_tenant.sql`, declare `tenant_id UUID … REFERENCES public.tenants(id)`. The name is taken from that platform pattern. It is not taken from the **UNVERIFIED** 376 claim. | None for the name itself. Live catalogs were not read. |
| Any other column name | Not proposed | A second name would not match the platform column already used for `public.tenants`. | |
| A workspace column beside it | Not proposed | ADR-03 Option 1. | |

`series_build.id` stays the primary key. A composite primary key `(tenant_id, id)` is not proposed. `active_build_id` and `video_production.series_build_id` store a single UUID. Changing the primary key would make those pointers a different schema decision. That decision is out of scope.

### 2.2 Type and target

| Choice | Draft | Evidence | What is still missing |
|---|---|---|---|
| `UUID`, referencing `public.tenants(id)` | **PROPOSED** | **VERIFIED:** `tenants.id` is `UUID PRIMARY KEY` in migration 002. **VERIFIED:** later migrations reference `public.tenants(id)` with type UUID. **INFERENCE:** migration 002’s unqualified `tenants` is that same `public.tenants`. This step found no second `CREATE TABLE tenants` and no `CREATE TABLE public.tenants`. | No database catalog was read. Whether every database that applied migration 002 created the table in `public` is **UNKNOWN**. |
| `tenant_code VARCHAR` instead of the UUID | Not proposed | Migration 002 also has `UNIQUE (tenant_code)`. The primary key is `id`. No source here says `tenant_code` is immutable. | |
| UUID with no foreign key | Not proposed | A bare UUID can store a value that is not a row in `public.tenants`. ADR-03 names that table as the tenant. | |

### 2.3 Nullability

The proposed migration does not backfill. Row existence is **UNKNOWN**. Those two facts pick the nullability.

| Choice | Draft | Why |
|---|---|---|
| `NULL` allowed | **PROPOSED** | Adding `NOT NULL` with no default fails if any row already exists. A default tenant UUID would assign a tenant without the decision in §3. ADR-03 line 70 states that existing Production rows are not already attributable to tenants. Nullable storage is the column that can be added without inventing that attribution. |
| `NOT NULL`, no default | Not proposed for this migration | Available only after evidence that every database which will apply the migration has zero `series_build` rows, or after a separate backfill has already filled every row. Neither fact exists. See §3. |
| `NOT NULL DEFAULT <tenant uuid>` | Not proposed | That default is an automatic backfill. This draft forbids it. |

A nullable column does not make the row attributable. It does not satisfy GATE A. It does not make Production tenant-safe. Step 032’s reading of line 231 remains: decision closed is not implementation complete.

### 2.4 Constraint and index

| Choice | Draft | Evidence | What is still missing |
|---|---|---|---|
| Foreign key to `public.tenants(id)`, with no `ON DELETE` clause | **PROPOSED** | Migration 002’s own foreign keys from `branches`, `employees`, `users`, `roles`, and `audit_logs` reference `tenants(id)` and omit `ON DELETE`. In PostgreSQL that omission is `NO ACTION`. The proposed column follows that form. | No tracked rule says what should happen to a `series_build` row when a tenant row is deleted. `tenants.deleted_at` exists in migration 002. Whether application code deletes tenant rows, or only sets `deleted_at`, was not searched. **UNKNOWN**. |
| `ON DELETE CASCADE` | Not proposed | Would delete production snapshots because a tenant row was deleted. No policy for that was found. | |
| `ON DELETE SET NULL` | Not proposed | Would clear attribution on tenant deletion. That is a later behavior decision, not part of adding the column. | |
| `ON DELETE RESTRICT` | Not proposed as a change from the 002 form | Effect is close to `NO ACTION`, with different timing. Not required to store attribution. | |
| Non-unique index on `tenant_id` only | **PROPOSED** as a separate approval item | **INFERENCE** from migration 002, which indexes `tenant_id` on `branches`, `employees`, and `users`. The index would not implement refusal. The existing index on `(series_code, updated_at DESC)` does not lead with a tenant column. | No query shape has been decided. D-06 has not named the predicate. The Architect can approve the column and reject the index. |
| `CHECK (tenant_id IS NOT NULL)`, or a status-based check | Not proposed | Same effect as forbidding null, which §2.3 does not propose. Enforcement of “new writes must carry a tenant” is D-06. | |
| RLS in this migration | Not proposed | D-06. Line 231 does not authorize RLS. | |

Grants are not restated and not expanded. The new column, if a later migration adds it, is covered by the table grants migration 315 already issued. This draft does not add a `GRANT` and does not revoke one.

## 3. Historical rows

**PROPOSED policy:** the attribution migration does not backfill. It contains no `UPDATE` of `pack_content.series_build`. Existing rows, if any exist, stay without a tenant until a separate decision.

That policy is not evidence that rows exist. It is the rule that follows from **UNKNOWN** row counts and from ADR-03 line 70.

### 3.1 What is known without a database

| Fact | Label |
|---|---|
| Migration 315’s `series_build` has no column that can store `public.tenants.id`. | **VERIFIED** |
| Re-applying that `CREATE TABLE IF NOT EXISTS` does not add a column. | **VERIFIED** |
| No tracked `ALTER TABLE pack_content.series_build` at the baseline. | **RECORDED**, Step 032. Not repeated as a new search. |
| ADR-03 line 70: existing Production rows are not already attributable to tenants. | **DECIDED** as policy meaning. It is not a row count. |
| The three tracked code files whose text contains `series_build`, other than docs, rules, and migrations, do not contain the string `tenant`. Paths: `client/admin/src/modules/content/content-famixa-series.ts`, `client/admin/src/modules/content/kit-video-famixa-adapter.ts`, `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentRepository.cs`. | **VERIFIED** absence of that string. It does not prove what a live `graph_json` contains. Other code can touch the table without using the string `series_build`. |
| `series_pilot` has no tenant column in migration 313, and no later tracked migration adds one. | **VERIFIED** by the `series_pilot` grep in §Evidence. The pilot cannot supply a tenant id. |
| `video_production.series_build_id` is a nullable UUID with no foreign key in migration 333. | **VERIFIED**. It also cannot supply a tenant id. |

### 3.2 Evidence and decisions required before any backfill

A backfill is a later migration, a later content-manifest line, and a later Architect approval. Approving this draft does not approve that later migration.

Before that later writing may authorize an `UPDATE`, all of the following have to exist:

1. A named list of databases that have applied migration 315. This step did not open a session. The list is **UNKNOWN**.
2. A row count of `pack_content.series_build` on each of those databases. **UNKNOWN**.
3. A count of `series_pilot.active_build_id` values that are not null, and whether each value equals some `series_build.id`. **UNKNOWN**. Orphan pointers are possible on the schema as written, because no foreign key is declared. This draft does not measure them.
4. The same check for `pack_content.video_production.series_build_id`, if a backfill author intends to use that hook. Using it is not proposed here.
5. A named source that maps each historical `series_build.id` to exactly one `public.tenants.id`. `graph_json` was not read. The three code files above are not that source. **UNKNOWN** whether any live document contains a tenant key.
6. A written decision for a row that still cannot be attributed. This draft’s standing rule is: leave it `NULL`. Do not invent a tenant, do not use a default UUID, and do not delete the row inside a backfill that has not been separately approved.
7. A statement that the backfill does not run against a production database unless a separate approval says so. This draft does not give that approval.

Until those seven items exist, the only historical action consistent with this draft is to add the nullable column and leave every existing value null.

## 4. `series_pilot.active_build_id`

**VERIFIED** from migrations 313 and 315:

| Fact | Text |
|---|---|
| `series_pilot` primary key | `series_code VARCHAR(64) PRIMARY KEY` |
| Other columns at create | `graph_json JSONB NOT NULL`, `updated_at TIMESTAMPTZ NOT NULL` |
| Tenant column on `series_pilot` | None in 313. No later tracked migration adds one. |
| `active_build_id` | `ALTER TABLE pack_content.series_pilot ADD COLUMN IF NOT EXISTS active_build_id UUID` |
| Foreign key | Not declared. The only tracked mention of the name is that one statement. |
| Workspace column | None. |

**PROPOSED** relationship:

- The future `series_build` attribution migration does not `ALTER` `pack_content.series_pilot`.
- It does not add a foreign key from `active_build_id` to `series_build.id`.
- It does not copy a tenant onto `series_pilot`, and it does not read `series_pilot` to fill `series_build.tenant_id`.
- A non-null `active_build_id` does not establish that the pilot and the build share a tenant. The pilot has nowhere to store one.
- A non-null `active_build_id` does not authorize access. ADR-03 lines 34–35 already say a BuildId is not an authorization identity. This draft does not add a new rule; it applies that one to this pointer.
- `video_production.series_build_id` is the same kind of pointer on a different table. It stays out of the migration for the same reasons.

A foreign key on `active_build_id` would be a second schema decision. It waits on the orphan evidence in §3.2 item 3. This draft does not request that foreign key.

## 5. Later steps before a migration and a content-manifest line may be created

This section is the permission path. Walking through it on paper does not grant the permission. **No implementation permission granted** by completing, approving, or citing this draft alone.

The Architect still has to approve the choices in §7 first. If the Architect rejects Option B and selects Option A, the path below stops: no migration is authorized, and AG-G3-02’s in-scope branch is not taken.

If the Architect approves the proposed choices, a **later tracked decision** is still required. That writing is not this file, not Step 032, and not Step 033. It has to stand without Migration 376, without the three gitignored forensic paths named in Step 032, and without stash `c170af39`. It has to say, in its own words, all of the following before any file is created:

1. **Line 231.** Either this one schema change is granted as a named exception while the rest of line 231 stays in force, or line 231 is lifted for this change only. Until that sentence exists in a tracked decision, line 231 remains the binding authorization limit. Step 032 recorded that its own file was not committed. This file is not committed. Neither one is that tracked decision.
2. **Filename.** One new migration name. The name is not `migrations/376_pack_content_production_tenant_boundary.sql`. It is not an edit of migration 315. It is not a reconstruction of an untracked or stashed file.
3. **Namespace check on the agreed commit.** The writing names the prefix only after a fresh `git ls-tree` of `migrations/` on that commit. This step’s inventory is evidence for the baseline only, not a reserved number. At `d54652d4`, tracked prefixes from 367 up are `367`, `368`, `369`, `371`, `383`, and `384`. Prefixes with no tracked file in that listing include `370`, `372`–`382`, `376`, `385`, and `386`. Step 028 **RECORDED** untracked on-disk files in `368`–`382`, `385`, and `386`. This step did not inventory the working tree. A number that is free in the baseline tree can still belong to an untracked file. The later writing must not adopt that file’s name.
4. **One content-manifest line.** The writing names the filename and the position on `deploy/ubuntu/migration-files.content.txt`. The baseline list ends at line 62 with `367_pack_content_character_reference_pack_contract.sql`. The header, re-read here, says this list stays on the content runner and is not merged into `migration-files.prod.txt` until GA is explicitly approved. The new line goes on the content list only. It is not added to `migration-files.prod.txt`, `migration-files.family-os.txt`, or `migration-files.local-os.txt`.
5. **Tracked content files already outside that list.** `383_pack_content_article_series.sql` and `384_pack_content_brand_narrative_dna.sql` are in the baseline tree and are not named under `deploy/ubuntu`. Migration 383’s first comment says “KitPlatform 370” while the filename prefix is 383. That comment is not a second migration and is not a free prefix. Their inclusion or exclusion is AG-G3-04. The one new D-05 line does not decide them, does not edit them, and does not use them as a template.
6. **Body limits.** The writing repeats the approved column, nullability, foreign key, and index choice from §7, or records a revision. It states that the migration does not `UPDATE` existing rows, does not enable RLS, does not alter `series_pilot`, does not alter `video_production`, does not change grants, and does not edit migration 315.
7. **Citation.** Step 028 AG-G3-05: the implementation cites an Architect approval newer than the Step 028 checklist. This draft is not that approval.
8. **Only then** may a separate implementation step add the one migration file and the one manifest line on the agreed commit. Applying the migration to a database, including a production database, is still a further approval. Passing a later gate does not apply SQL.

The working-tree manifest hash recorded in Step 033 (`6bc5f545035c62d4a9ce9f45e1fe9ab2380ad839`, status modified) is not a line position and is not implementation evidence. This step did not read that file.

## 6. D-06 and the Step 028 gates

### 6.1 D-06 stays unstarted

**RECORDED.** Step 032 question 3 leaves the enforcement location open: a tracked filter type, named service methods, a named repository boundary, or a written removal of the six attributes. ADR-03 line 127 does not choose. This draft does not choose.

A nullable `tenant_id` does not refuse a load or a store. The six `[ServiceFilter(typeof(ProductionTenantActionFilter))]` attributes **RECORDED** in Step 032 §3.4 still have no tracked implementing type. This draft does not add that type and does not remove the attributes.

GATE A, ADR-03 lines 118–127, remains the requirement. The physical-storage sentence and the actor-provenance sentence stay as Step 033 left them: satisfaction on disk is **UNKNOWN**, and this draft does not add an actor column to `series_build`.

### 6.2 Tests this step inventoried

`git grep -l series_build` at the baseline returned these tracked paths and no others:

- `.cursor/rules/content-park.mdc`
- `client/admin/src/modules/content/content-famixa-series.ts`
- `client/admin/src/modules/content/kit-video-famixa-adapter.ts`
- `deploy/ubuntu/migration-files.content.txt`
- `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md`
- `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`
- `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`
- `docs/novixa/03-solution/famixa-current-implementation-ux-readiness.md`
- `docs/novixa/03-solution/famixa-video-audio-lipsync-pipeline-audit-v1.md`
- `migrations/315_pack_content_series_build.sql`
- `migrations/333_pack_content_kit_video_engine.sql`
- `migrations/383_pack_content_article_series.sql`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentRepository.cs`

**VERIFIED:** none of those paths is under `tests/`, and none is a `*.smoke.ts` or `*Test*.cs` file. No test was run. AG-G4-02 stays **UNKNOWN**. Absence of the string in tracked tests is not a passing isolation check. Step 032 question 5 still requires a future check list to say that the run uses no production database, no deploy, and no paid provider. This draft does not write that list.

### 6.3 Gate rows

Statuses below are **RECORDED**. This draft does not re-score them. Phase 1.5 stays **NOT PASS** while any mandatory row is FAIL, BLOCKED, or UNKNOWN.

| Row | After this draft | When a re-check is even eligible |
|---|---|---|
| AG-G3-02 | Stays **BLOCKED**. The proposed scope is Option B. The migration and the manifest line do not exist. | Only after the §5 writing is on the agreed commit and the tracked migration plus the one content-manifest line match that writing. If the Architect instead selects Option A, the eligible check is the written out-of-scope decision and the absence of a new migration. This draft does not take that branch. |
| AG-G3-01 | Stays **FAIL**. Acceptance still must not depend on the missing 376 file. | A re-check of the “no acceptance claim depends on that filename” branch is eligible only when a tracked decision says the D-05 claim does not depend on 376. This draft says that, and it is not tracked. |
| AG-G3-04 | Stays **FAIL**. Files 383 and 384 are a manifest gap this step verified. They are not the D-05 change. | Unchanged by D-05. Do not edit migration 386, 383, or 384 in order to pass. |
| AG-G3-05 | Stays **BLOCKED**. | A schema change still needs an approval newer than Step 028. This draft is not that approval. |
| AG-G2-03 and AG-G4-03 | Stay **BLOCKED**. Line 231 still binds. | Eligible when the line 231 disposition is in a tracked decision and does not cite the missing forensic files. Step 032 was not committed. This file is not committed. |
| AG-G2-02 | Stays **FAIL**. The **UNVERIFIED** 376 sentences remain in the tracked authority files. | Eligible only after an Architect decision withdraws them or the cited path exists in the agreed commit. This draft does not edit those files and does not restore 376. |
| AG-G4-01 | Stays **FAIL**. D-06 is unstarted. | Eligible after D-06’s location, or a written removal of the six attributes, is on the agreed commit. |
| AG-G4-02 | Stays **UNKNOWN**. | Eligible after the Architect names the checks and they have a recorded result on the agreed commit, with no production database, no deploy, and no paid provider. |
| AG-G4-03 | Same as AG-G2-03. | A nullable column is not an API authorization. |
| Every other mandatory row | Unchanged. AG-G1-01’s carried PASS does not pass the phase. | D-05 does not clear publish, working tree, file set, C-01–C-04, reset order, manifest ownership collisions, build, TestPack, or reproducibility. |

Step 032 question 6 asked which mandatory rows may be re-checked after D-05 and D-06 exist on the agreed commit. The eligible rows are the ones in the table above whose “when” clause is satisfied. Eligibility is not a PASS. Blocker B-06, Register line 231 not lifted, stays open.

## 7. Decisions the Architect is asked to approve

These are **PROPOSED**. None is granted by publication of this file.

1. Select Option B as the scope direction for `pack_content.series_build` only: one later migration and one later content-manifest line, under the path in §5. Rejecting this item and selecting Option A stops the path. No migration is then in scope.
2. Column name `tenant_id`. Type `UUID`. Primary key of `series_build` stays `id`. No workspace column. No other table.
3. Nullability: `NULL` allowed. No default tenant value.
4. Foreign key to `public.tenants(id)`, with no `ON DELETE` clause. No RLS in that migration.
5. Whether the same migration also creates a non-unique index on `tenant_id` alone. The draft proposes yes. The column decision does not depend on it.
6. No `UPDATE` backfill in that migration. Historical rows, if any, remain null until the seven items in §3.2 exist and a separate approval authorizes a backfill.
7. Do not alter `pack_content.series_pilot`. Do not add a foreign key on `active_build_id`. Do not alter `pack_content.video_production`.
8. Do not change the grants from migration 315 in that migration.
9. Do not treat this draft as a lift of Register line 231. The §5 tracked writing is still required before a file may be created.
10. Leave D-06 unstarted. Do not implement or remove `ProductionTenantActionFilter` from this decision.
11. Leave Phase 1.5 **NOT PASS**. Do not mark a Step 028 row PASS from this draft. Do not open Phase 2.

## 8. Evidence still missing

| Gap | Label |
|---|---|
| Whether any database has applied migration 002, 313, 315, or later files. | **UNKNOWN**. No session was opened. |
| Whether migration 002’s `tenants` is `public.tenants` on each such database. The tracked SQL supports that reading as **INFERENCE** only. | **UNKNOWN** on a live catalog. |
| Row counts for `series_build`. | **UNKNOWN** |
| Whether any live `graph_json` holds a tenant identifier. | **UNKNOWN** |
| Which `public.tenants.id` would be correct for any historical row. | **UNKNOWN** |
| Counts and orphans for `active_build_id` and for `video_production.series_build_id`. | **UNKNOWN** |
| What should happen to a build when a tenant row is deleted rather than soft-deleted. | **UNKNOWN**. `ON DELETE` was therefore left at the migration 002 form, not invented. |
| A free migration prefix on the future agreed commit, and the working-tree names Step 028 recorded as untracked. | Not reserved. This step did not list the working tree. |
| The position of the future content-manifest line relative to 383 and 384, once AG-G3-04 lists them or excludes them. | Not proposed. Those two files are outside D-05. |
| The D-06 enforcement location. | Unstarted. |
| A named tenant-isolation check and its result. | **UNKNOWN**. AG-G4-02. |
| Whether the six controller actions are the complete future API surface. | **RECORDED** as Step 032 question 4, still unanswered. This step did not reopen the controller. |

## 9. Permission

**No implementation permission granted.**

Register line 231 remains binding. ADR-03 Option 1 remains **DECIDED** and does not supply permission. Option B as proposed here is not a grant to add a migration. Approving §7 in a later writing is still not this file, and even that writing must contain the sentences in §5 before a file may be created.

This draft does not authorize a new file under `migrations/`, an edit to migration 315, an edit to `deploy/ubuntu/migration-files.content.txt`, an edit to ADR-03, an edit to the Decision Register, an edit to Master Architecture, or an edit to application code. It does not authorize creating or reading `migrations/376_pack_content_production_tenant_boundary.sql`. It does not authorize implementing or removing `ProductionTenantActionFilter`. It does not authorize push, commit, merge, reset, clean, deploy, a production database session, or a migration run.

Phase 1.5 remains **NOT PASS**.

## Stop

This step created only `STEP-034-D05-ARCHITECTURE-DECISION-DRAFT.md`.

No product file, migration, manifest, solution, ADR, Decision Register, or Master Architecture file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

D-05 is not closed. D-06 was not started. No Step 028 row was marked PASS. Phase 2 was not opened.

**No implementation permission granted.**
