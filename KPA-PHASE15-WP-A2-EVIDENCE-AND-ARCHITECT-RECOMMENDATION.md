# KPA-PHASE15 — WP-A2: Evidence Deepening and Architect Recommendation

Status: **RESEARCH ONLY.** No implementation follows from this file.  
Phase 1.5: **NOT PASS.** Phase 2 is not opened.  
Date of this reading: 2026-10-15.  
This file is the only WP-A2 artifact. No product file, migration, manifest, Register, ADR, or other authority document was edited. No database session was opened. No SQL was run. No fetch, push, merge, reset, clean, stash, cherry-pick, or new branch or worktree was made.

A recommendation in this file is not an approval, and it is not a PASS.

Labels used below:

| Label | Meaning |
|---|---|
| ARCHITECT CAN DECIDE | The technical evidence is sufficient, and the choice stays inside authority already recorded. This file still does not mark the item APPROVED and does not edit the Register. |
| OWNER APPROVAL REQUIRED | The choice changes what a customer sees, which database deploy applies, or whether a Register prohibition may be narrowed. |
| EVIDENCE MISSING | A named source was read and does not contain the fact. The safe way to obtain it is stated. |

The dirty working tree of `E:\KitPlatform` is not a source commit. Register text below is blob `67302fc3ee01b36f35eb612b64f51d62d6ea8348` at `b64f677` and at local `HEAD`. The baseline blob at `d54652d4` is `9065fc9a264f592a19759a6d0d8ef52d95321756`.

## 1. Executive summary

Phase 1.5 is still **NOT PASS**. There is now enough technical evidence to name one source commit and to stop sending the five F-03 design questions back as open product choices. There is not enough authority to write the episode-owner migration or to claim any of F-03 through F-08 closed.

The source to verify against is `b64f6772213dd0e095a49ae9a84983b37116f696`. It is the only commit in this repository that contains the Phase 1.5 scope limit on the Famixa series script and does not also contain a migration that adds a customer column to that script. The later pharmacy fix, the release branch, the detached application fix, and the rewritten replay history are the wrong trees for this phase. Choosing this commit does not publish it and does not move the dirty checkout at `E:\KitPlatform`.

What the Architect can now settle, as recommendations rather than approvals:

- An old episode whose customer cannot be proved is hidden from every customer. It is not assigned by guesswork, and it is not shown as a shared episode.
- Two customers who both use the code `EP01` each get their own episode. They do not share shots, contracts, or generation history. They still share the Famixa project seed and, for this phase, the character and visual bible.
- A request for someone else’s episode, a missing episode, and a hidden old episode all return the same not-found response. The response does not say which of the three happened.
- The new uniqueness rule keeps the project in the key, allows the same episode code for different customers, and keeps at most one hidden old row for each project and code. It can be created beside the current rule and only replaces that rule after both new rules and the “new rows must name a customer” trigger exist.
- Shared character, visual style, and project canon do not gain a customer column in this phase. Routes that edit them are not described as isolated.
- Prefixes 376, 385, and 386 are frozen. Prefixes 368, 369, and 371 are already used by other packs on the source commit. The first prefix that is free on the source commit, on the release tip, and on the untracked disk names is **387** for the episode-owner migration and **388** for the later uniqueness migration. A fresh listing is still required at the moment a file is created.

What is still blocking implementation of F-03 through F-08:

1. Register line 231 still forbids a customer column, a workspace column, row-level security, and an API authorization change. On the recommended source, the episode table and the production-shot table do not have a customer column. The application fix on the detached commit depends on a shot column that this source does not have. Closing F-03, F-06, and F-08 therefore needs one narrow grant, written by the Product Owner, for the tables named in section 8. This file does not write that grant.
2. No count of existing episodes is available. The only session that reached a database stopped before counting, because the role could modify data. The earlier disposable cluster on port 54329 was built from the release history, so its schema is not the schema of `b64f677`.
3. No clean build of `b64f677` is recorded. The isolated build that exists included dirty files and stopped with two missing video-reader methods.

The highest-value next step is the Product Owner’s answer to the single grant in section 8, together with preparation of a throwaway database that is not the application database. Do not write the migration file before the grant and before the counts.

## 2. Source commit comparison

Measured 2026-10-15 with read-only git. No fetch. Cached `origin/main` is the local remote-tracking ref. Whether the host has moved is unknown.

| Candidate | Full hash | Phase 1.5 changes still required if this tree is the source | Unrelated changes that come with it | D-05 Option A or line 231 | Clean build and mandatory gates on this tree | How the dirty checkout stays untouched |
|---|---|---|---|---|---|---|
| D-01 baseline | `d54652d4a7734b3116db62bd356a831f9942fc73` | Option A is absent (Register blob `9065fc9a`). F-03–F-08, manifest ownership, and the 376 retraction are all still ahead of this commit. | None of the later pharmacy fix. Also none of the architecture limit that Phase 1.5 must obey. | Line 231 binds. Option A is not in the file. No series-script customer migration is in the tree. | No clean build is recorded. Gates that require Option A cannot pass on this blob. | Do not check this commit out in `E:\KitPlatform`. |
| D-05 commit | `b64f6772213dd0e095a49ae9a84983b37116f696` | Everything in sections 3–6 that is not already in the three documentation files of this commit. Parent is the D-01 baseline. Subject: `docs(architecture): retract unproven tenant claims and bound D-05`. Files: the Master Architecture, the Register, and ADR-03 only. | No pharmacy files. No tenant-boundary migration. | Option A is present at Register lines 754–779. Line 231 is unchanged. `migrations/385_pack_content_series_tenant_boundary.sql` is absent. `migrations/386_pack_content_execution_tenant_boundary.sql` is absent. | Not yet. See section 6. The tree is the right one to build later. It is not a green build today. | Leave `E:\KitPlatform` at `0d8568ae`. A later package adds a new worktree pointing at this hash. |
| Local `HEAD` / `main` | `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` | Same architecture gap as `b64f677`, because this commit’s parent is that hash and its only delta is the pharmacy fix. | `fix(novixa): page low-stock without dropping tied rows`: 21 files across pharmacy inventory, dashboard, reports, admin low-stock UI, OpenAPI export, and `NovixaFixImplement01Tests.cs`. | Same Register blob as `b64f677`. No series-script customer migration. | A green build here would also be a green build of an unrelated pharmacy change. No such clean build is recorded. | This commit is already `HEAD`. Do not reset it to make the tree clean. |
| Cached `origin/main` | `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` | The five local commits are absent. `git rev-list --left-right --count origin/main...HEAD` is `0` `5`. The Register file is not in this commit. | The published history as last cached. It is an ancestor, not a superset. | Option A cannot be checked: the Register path does not exist here. Line 231 does not exist here. | Authority gates fail because the authority files are absent. | Do not reset local `main` onto this ref. |
| Release tip | `ec452bda2143756ad0c09843548e7f5f94fd1f5a` | Would still need F-03–F-08. Merge-base with local `HEAD` is local `HEAD`, so the release commits sit on top of the pharmacy fix. | Pharmacy fix, plus `bfe51e14` (series and execution customer migrations), `f86655e1` (local Veo/Fal reads), and `ec452bda` (compile shims). | **Violates Option A.** Tracked `385_pack_content_series_tenant_boundary.sql` adds a nullable customer column to `series_build` and `series_pilot`. Tracked `386_pack_content_execution_tenant_boundary.sql` adds customer columns to shot and execution tables. That is a TenantId change while line 231 is still in force on the mainline Register. | WP-1 reported a green content build on this tip, on a different worktree. That result is not a clean build of the Phase 1.5 source, and it does not make the series migration legal. | Do not merge this tip into `E:\KitPlatform`. |
| WP-1 detached commit | `121553e4b654f501df50b3217df2bcbf76795b3a` | Child of the release tip. It changes child contract and prompt repositories. It does not change `video_production`, `video_visual_contract`, or `KitVideoVisionRepository`. F-03, F-04, F-05, F-07, and F-08 stay open even on its own report. | Everything in the release tip, plus the child-contract patch. `git branch -a --contains` from `E:\KitPlatform` does not print a branch tip for it. | **Violates Option A** by inheritance of `bfe51e14`. The filter type on this commit checks that the caller has an active tenant id. It does not check that the series build or the episode belongs to that tenant. | Its PostgreSQL facts were run on database `kit_tb_pgate01`, port `54329`. They are evidence for that worktree only. | Do not cherry-pick it onto the dirty checkout. |
| Replay history | See the hashes below. Not one commit. | The rewritten history in `E:\KitPlatform-isolated-patch-replay-01` is a different object store. `git cat-file -t 2a88d72cf806e99f647bb65a481c6bc1a073d1ba` from `E:\KitPlatform` fails. | The tip `4f97fd1a0e9efd4f2489fb41dad5a877a561cf94` adds `fix: complete pricing extraction and narrative quality gate` (7 files, pharmacy pricing and content quality). That commit is not one of the named candidates and is not in this repository’s object database. | The replay of the series isolation commit is `084e6b059dd9b71b7fb3c957e4fa8f358d7fceaa`. It adds the same two forbidden migrations. The tip contains `migrations/385_pack_content_series_tenant_boundary.sql`. | A build of that directory would not be a build of a commit `E:\KitPlatform` can check out. | Do not copy that directory into `E:\KitPlatform`. |

Patch-id check, stable, read-only, so the replay can be identified exactly and then rejected:

| Replay hash | Same patch-id as | Why it is still not the source |
|---|---|---|
| `2a88d72cf806e99f647bb65a481c6bc1a073d1ba` | `b64f677` (`d90029c7e07ec7119e96f961b5d8f573ada9697e`) | Equivalent documentation patch, but the object is not in `E:\KitPlatform`. Later replay commits put the forbidden series migration on top of it. |
| `cb090fab59de808e224b2002be9d6d1cdf27ff25` | `0d8568ae` (`8a09300ec3a332c92bb52e0a95fcf01ea325705b`) | Equivalent pharmacy patch, outside this phase. |
| `e3e3bf68277ff2bb91e20cc0881fabdd74359217` | `121553e4` (`9568aead3aa492842198564d987d0a3cb39410f7`) | Equivalent child-contract patch, sitting on the forbidden series migration. |

No file in this repository assigns the words “replay commit” to a single hash. The worktree name and the patch-ids above are the evidence. They do not produce a safe source.

### Recommendation

**ARCHITECT CAN DECIDE:** the Phase 1.5 verification source is `b64f6772213dd0e095a49ae9a84983b37116f696`.

Why this one:

- D-05 Option A is in its Register. The D-01 baseline does not contain that limit, so implementing from `d54652d4` would be implementing against a Register that does not yet state the limit.
- It does not contain the series-script customer migration. The release tip, the WP-1 commit, and the replay tip do.
- It does not contain the 21-file pharmacy low-stock fix. That fix can stay on local `main`. Phase 1.5 gates must not depend on it.
- Cached `origin/main` does not contain the Register at all.
- The replay hash that matches this commit is not an object in this repository, so it cannot be checked out from `E:\KitPlatform`.

What this choice does not do: it does not push, it does not declare PASS, it does not lift line 231, and it does not authorize a migration.

**OWNER APPROVAL REQUIRED** only if the same source must also be published. Local verification does not need a push. Publication remains a separate yes or no and is not the blocker for a later worktree.

Preserving the dirty tree:

- Do not run checkout, reset, clean, stash, or merge in `E:\KitPlatform`. Local `HEAD` stays `0d8568ae`. Dirty files stay dirty, including the content manifest, `KitPlatform.Api.csproj`, migration `281`, and the untracked migration files listed in section 6.
- Do not use `E:\KitPlatform-wt-phase15-accept-b64f677` as the clean tree. On 2026-10-15 it is at `b64f677` and porcelain shows modified `CustomerDraftOrderService.cs`, modified `DashboardService.cs`, and untracked `phase15-clean-build.log`.
- Do not use `E:\KitPlatform-wt-phase15-clean-acceptance` as the clean tree. Porcelain count was 7.
- A later package may add a new worktree at `b64f677`. Adding a worktree does not change `HEAD` or the dirty files in `E:\KitPlatform`. This research step did not add one.

There is no remaining ambiguity among the seven candidates. The blocker that still stops schema work is the line 231 grant and the missing episode counts, not the choice of hash.

## 3. Architecture recommendation for F-03 through F-08

The four sentences already accepted stay closed and are not reopened: an episode (`video_production`) belongs to one customer; a code such as `EP01` may be used by more than one customer; the visual contract follows the episode; project, universe, and canon do not automatically belong to a customer. F-04 remains a separate finding. Closing one finding does not close the others.

Shared studio material and a customer’s own episode are different things.

| | Shared Famixa studio | One customer’s episode |
|---|---|---|
| What it is | The brand project `FAMIXA`, its universe, visual style, character bible, master reference, and the audit of those rows. Migration 333 seeds the project. There is no API that creates a second project per customer. | The episode row, its visual contract, keyframe attempts, shot state, jobs, story graph, and motion takes. |
| If it stays shared | Every content administrator sees and can change the same character and style rules. A second customer does not get a private Minh, a private visual system, or a private `FAMIXA` project. Future customers can still have private episodes, contracts, and renders. | Customer B does not read or replace customer A’s contract, prompts, attempts, or video. |
| If each customer gets a private copy | Every new customer needs a copy of the character bible before they can generate, and a lock by one customer must not change another customer’s rules. That is a new product: Famixa stops being one studio catalog. It also needs customer columns on tables line 231 does not allow. | This is the model already accepted for the episode itself. It does not require private copies of the character bible. |

**ARCHITECT CAN DECIDE** for this phase: keep the studio catalog shared, and make the episode private. Do not add a customer column to project, universe, visual system, asset canon, character DNA, master reference, or their event tables. Do not describe those routes as isolated. Do not close F-04, F-05, or F-07 by adding a column to `video_production_event`.

**OWNER APPROVAL REQUIRED:** accept the exposure that comes with a shared studio. Any administrator of the content module can lock or edit the Famixa character and visual rules that every other customer of this studio also sees. If that exposure is not acceptable, Phase 1.5 cannot close F-04, F-05, or F-07, and a separate grant would be required before any private-canon schema. This file does not request a private-canon design.

### F-03 — one customer’s episode can be overwritten by another

**Problem.** The visual contract is stored per episode and shot code. The upsert replaces the row on that pair. The episode table has no customer column in migration 333. Listing episodes and opening one by id do not check the caller. `EnsureProductionAsync` at `b64f677` (`KitVideoEngineService.cs`) returns the existing row when the project and the code match, so two customers who both attach `EP01` receive the same episode id and can replace each other’s contract.

**If it is not fixed.** Customer B, signed in as a content administrator, can open the episode list, take customer A’s episode id, and replace A’s shot contract, prompt, and fingerprint. B can also read A’s keyframe review.

**Feasible fix.** Add a nullable customer id on `video_production` only. Child rows keep their current keys and are reached with “this episode id belongs to the caller.” New inserts must name the caller. Old rows stay null and are not updated. The contract unique key `(production_id, shot_code)` stays. The upsert runs only when the episode belongs to the caller, and it does not leave an attempt behind when the check fails.

**Files and surfaces.** `migrations/333_pack_content_kit_video_engine.sql` is not edited. A new file at prefix 387, after a fresh listing. `KitVideoEngineService.EnsureProductionAsync`, `ContentRepository` list/get/insert/transition, `KitVideoVisionRepository.UpsertContractAsync`, attempt reads and writes, job, story, and motion paths that take an episode id, and `ContentController` routes `video-engine/productions`, `visual/attempts`, `visual/generate`, `visual/decide`, jobs, story, and motion. Schema `pack_content`. Tests are the matrix in the WP-3A specification, rewritten so they do not depend on the release migrations. UI `ContentKitVideoEngineCard.attachEp01` sends `FAMIXA` / `FAMILY_A` / `EP01` and does not send a series build. After the change, that button creates the caller’s episode instead of joining a shared one.

**Best fit.** One nullable column on the episode, children checked through that episode. This matches the accepted model and does not turn the shared project into a tenant.

**Register line 231.** The column is a TenantId change. It is not authorized until the grant in section 8 exists. No row-level security. No workspace id. No customer column on `series_build`. Do not use prefix 385 or 386. Do not commit the untracked 376 file.

**Proof that A and B stay apart.** On the throwaway database: A and B each attach `EP01` and receive two ids. A’s list does not contain B. A’s read or write of B’s id returns the same not-found body and does not change B’s contract, fingerprint, `updated_at`, attempt status, job, story, or take. A second attach by A returns A’s id and does not change `series_build_id`. A hidden null row with the same code remains null.

**Residual risk.** A database role that can `SELECT` the table can still read the contract directly. Application checks are not row-level security. Idempotency keys that are unique across all customers remain F-08. Two customers still share the Famixa project and canon.

### F-04 — shared visual system and project canon

**Problem.** `video_project.project_code` is unique across the whole table. `video_visual_system` is updated by id or by that project code. Lock, revision, and calibration routes have no customer check.

**If it is not fixed, and if the product treats this as private.** Customer B locks the Famixa visual system or approves a revision, and customer A’s rules, status, and `locked_by` change.

**Feasible fixes.** Private projects, which need new columns and a new product. Or a written shared-studio boundary and tests that two customers see the same canon, with no isolation claim.

**Best fit.** The shared-studio boundary. Files stay the visual-system repository and the routes `video-engine/visual-system`, `project-visual-style`, `character-studio/project-visual-universe`, and `visual-calibration`. No schema change.

**Line 231.** No change if the shared boundary is the choice. A private project would need a further grant this phase does not request.

**Proof.** Two customers read the same `FAMIXA` visual system. The package’s test says the route is shared. It does not expect B to receive 404 for A’s project, because there is no private project.

**Residual risk.** The lock exposure in the owner decision above. A later private-canon phase has to start from that decision, not from this boundary text.

### F-05 — image prompt compile reads canon by project code

**Problem.** `POST api/content/video-engine/compile-image-prompt` loads assets by `project_code` with no customer predicate. It does not write the shot prompt table.

**If the canon is private and this stays as it is.** Customer B’s compile response contains A’s character, location, and the dialogue B sent.

**Best fit.** Same decision as F-04. Under the shared studio, the route is documented as a shared canon read. It is not retargeted at the shot prompt compiler. A test shows two customers compiling `FAMIXA` can both receive the studio canon, and the test does not award an isolation pass. No row is written to `video_production_prompt`.

**Residual risk.** Asset PUT and POST version edit the same canon. They stay in this finding, not in F-03.

### F-06 — contract and prompt events can be written for someone else’s shot

**Problem.** On the release audit, three event inserts did not prove the shot belonged to the caller. The detached commit `121553e4` reports that the application path now inserts only when the shot belongs to the caller. A SQL role can still read `payload_json`. There is no row-level security and no customer column on those event tables.

**Fact that changes the port.** On `b64f677`, `migrations/356_pack_content_kit_video_production_shot.sql` creates `video_production_shot` with unique `(shot_code, shot_version)` and unique `(character_id, era_id, shot_seq, shot_version)`. A search for `tenant_id` in that file and in migrations 362 and 365 finds no customer column. The WP-1 predicate compares `video_production_shot.tenant_id` to the caller. That column is not on this source. Copying the detached patch here does not compile against this schema, and merging the release branch would bring the forbidden series migration.

**If it is not fixed.** Customer B can reject customer A’s shot contract. The HTTP response can be 404 while A’s status is already `REJECTED` and a new event row exists.

**Best fit.** After the grant adds a nullable customer id on `video_production_shot` only, replay the WP-1 behavior as a new patch on `b64f677`: service guard before any reject or validate write, and the insert itself requires the shot to belong to the caller. Do not add a customer column to the three event tables. Do not enable row-level security. Do not treat the old port-54329 result as the result of this source. Null shot rows match nobody.

**Proof.** Under caller A, insert or reject for B’s shot and for a null shot adds no event row and does not change status. Caller A can still reject A’s own draft. A direct SQL read of `payload_json` is recorded as residual role risk and is not a pass criterion of the application package, which is what the remediation audit already said.

**Residual risk.** Anyone with SQL `SELECT` on the event tables can read payloads. Triggers that block update and delete do not block select or insert.

### F-07 — shared canon events

**Problem.** Master, DNA, asset, reference, and episode events have no customer. Two content administrators write and read the same `CHAR-001` audit. `GET video-engine/master-reference/events` is fixed to `FAMIXA` / `CHAR-001`.

**Best fit.** Follow F-04. If the studio stays shared, a test shows two customers see the same canon event, and the package does not take an isolation pass. Do not add `tenant_id` to `video_production_event` and call F-07 closed. Episode-event inserts that happen after an F-03 owner check are still not F-07.

**If the owner rejects the shared exposure.** Stop. Do not invent per-customer DNA inside the F-03 migration.

**Residual risk.** Some of these event tables have no immutability trigger, and older grants include update and delete. This phase does not revoke grants and does not turn them into row-level security.

### F-08 — global unique keys reveal that a value exists, or overwrite a row

**Problem.** On the source commit the shot code and shot sequence uniques do not include a customer, and the shot table has no customer column. The audit on the release tip also names global fingerprints on image and video execution. The visual-contract unique is F-03’s overwrite and is not fixed by changing that unique alone. Global idempotency keys on attempts and jobs are the same class.

**If it is not fixed.** After shots gain a customer, customer B still cannot create the same shot code and version as A, and the database error tells B the value is taken. B does not receive A’s payload. For the visual contract, the failure mode is overwrite, which F-03 handles. Fingerprint collisions block B without returning A’s artifact, but B still cannot use that fingerprint.

**Best fit.** A separate migration at prefix 388, not 385 and not 386, and not the F-03 file. Only after the granted customer columns exist on the shot and execution tables, and only after the preflight counts in section 5. Shape: a partial unique for rows that already have a customer, plus a partial unique for rows that do not, then drop the old global unique. Do not drop the old unique first. Do not assign a customer to a null row. A duplicate inside one customer stays one row.

**Line 231.** Widening these uniques is pointless until the columns exist, and the columns need the grant. F-03 does not drop these uniques.

**Proof.** A and B both insert the same shot code and version, each row keeping its own customer. The same fingerprint can exist once per customer. A null row is not updated to a customer. If the preflight finds two rows that already share a key the new rule cannot hold, the migration is not written until the owner says how those existing rows are treated. That treatment is not a guessed assignment.

**Residual risk.** Until 388 is applied, existence of a shot code leaks across customers. After it is applied, SQL `SELECT` can still read the rows. Idempotency of attempts and jobs stays in this finding if those uniques are not in the same reviewed preflight.

## 4. F03-O1 through F03-O5

These five questions are design consequences of the accepted model, of migration 333, and of `EnsureProductionAsync`. They are not marked APPROVED. Schema files are still not written, because line 231 and the episode counts are still open.

### F03-O1 — old episodes with no proved customer

**ARCHITECT CAN DECIDE.**

Migration 333 has no customer column, and `series_build_id` has no foreign key and is not an owner. The accepted rules forbid guessing an owner from the series, the project, the universe, or the actor. Showing the row to every customer would make it a shared episode, which the accepted model rejects. Assigning it to whoever clicks “Gắn EP01” would attach the hidden work to the wrong customer.

The safe behavior is:

- The migration adds the column as null and does not update existing rows.
- Every read and write used by the application treats a null customer as not owned. List omits the row. Get, attach, transition, contract, attempt, job, story, and motion do not return it and do not change it.
- A later human assignment, if the studio still needs that old episode, is a separate decision after the counts exist. It is not part of this migration.

**Consequence.** Until that later decision, the existing Famixa episode disappears from every account, including the account that created it. Nobody can open it by remembering the id. The pictures and prompts stay in the database. They are not deleted.

**Condition.** The inventory must record how many such rows exist. The number does not change this rule. A non-zero count is not a reason to guess the customer.

### F03-O2 — two customers both use `EP01`

**ARCHITECT CAN DECIDE.**

`EnsureProductionAsync` today loads productions for the project and returns the first row with the same code. Unique `uq_video_production` is `(project_id, production_code)` in migration 333, and both columns are `NOT NULL`. The accepted model says the episode belongs to one customer and the code may repeat. One shared row cannot satisfy both sentences.

The behavior is:

- Resolve the shared project and universe exactly as today. A missing project and a universe that is not in that project keep their current errors.
- Look up project + code + caller. If that row exists, return it. Do not update `series_build_id`.
- If it does not exist, insert a new id in `DRAFT` with the caller’s customer id. Do not return the hidden null row and do not update it.
- Two simultaneous requests from the same customer produce one row. The loser of the unique race reads again with the caller predicate and returns that same id.
- Two simultaneous requests from two customers produce two ids.

**Consequence.** The button “Gắn EP01” no longer means “open the studio’s one episode.” It means “open my episode called EP01.” Customer A and customer B can both have an episode called EP01 under the Famixa project. Their contracts and renders stay apart. Their character bible stays the shared studio bible.

### F03-O3 — error that does not reveal someone else’s episode

**ARCHITECT CAN DECIDE.**

Use HTTP 404 and one fixed message, the same message already used for a missing episode (`Production không tồn tại.`), for all three cases: no row, a row owned by someone else, and a row with no owner. The body does not say “unowned,” does not say “another customer,” and does not include contract JSON, prompts, review scores, or file bytes.

Use HTTP 401 only when the caller has no active tenant. A list of the caller’s own episodes returns 200 and an empty list when they have none. Do not use 400 or 409 for these three cases. Today several `catch (InvalidOperationException)` paths turn a not-found into 400 or 409, and motion preflight can turn it into HTTP 200 with `BLOCKED`. Those catches must recognize not-found first. A 409 on a cross-customer id tells the caller that a row exists.

### F03-O4 and F03-O5 — new unique rules versus the current unique and old rows

**ARCHITECT CAN DECIDE** the shape. **EVIDENCE MISSING** before the file is written: the counts in section 9.

Current rule: `CONSTRAINT uq_video_production UNIQUE (project_id, production_code)` with both columns `NOT NULL`. PostgreSQL will not store two rows with the same project and code while this constraint exists. A partial unique that ignores nulls is not required to explain the current table, because the columns are not nullable.

Recommended pair, created while the old constraint still exists:

1. Unique `(project_id, production_code)` where the customer id is null. This matches every current row, because every current row will have a null customer id. It keeps at most one hidden old episode per project and code.
2. Unique `(tenant_id, project_id, production_code)` where the customer id is not null. This index is empty on the day of the migration if nothing is backfilled. The old constraint already makes that subset unique.
3. A trigger: new inserts must name a customer; updates of a null row are rejected; changing the customer of a row that already has one is rejected. The trigger does not fill the column.
4. Drop `uq_video_production` only after steps 1–3 exist.

That order is required because `deploy/ubuntu/run-kit-migrations-content.sh` at `b64f677` runs `psql -v ON_ERROR_STOP=1 -f` and does not pass `--single-transaction`. Each statement commits on its own. The previous unknown about the runner is closed by that script. The order above does not depend on a wrapping transaction. Do not use `CREATE INDEX CONCURRENTLY`.

Keeping `project_id` in the key is the compatible choice. The current key has it, and the project remains the shared catalog. A key of only customer + code would forbid one customer from having `EP01` on two projects. That would be a product change. It is not recommended.

**Condition.** If the inventory finds any duplicate `(project_id, production_code)`, stop and do not write the file. Do not delete or merge episodes to make the count zero. After customers have created two legal `EP01` rows, the old single-key unique cannot be restored. Rollback stops in that case. It does not assign customers and does not delete rows.

### What must be counted before a migration file exists

**EVIDENCE MISSING.** Counts, not samples of prompt text. On a copy that matches the source commit’s catalog:

- Column list and unique names for `video_production`, `video_visual_contract`, `video_keyframe_attempt`, and `video_shot_state`, compared with migrations 333, 337, and 338.
- Number of episode rows.
- Number of groups that already violate `(project_id, production_code)`. Expected is zero while the old unique exists. Non-zero means the catalog has drifted. Stop.
- How many episodes have a null `series_build_id`, and how many have some id. The id is not an owner.
- Contract rows, contract groups on `(production_id, shot_code)`, attempts, and child-row counts for shot state, episode events, jobs, story, and takes.
- For F-08 later, not inside the F-03 file: collisions on shot code and version, collisions on character, era, sequence, and version, and fingerprint counts.

The inventory does not choose an owner and does not print contract or prompt bodies into the evidence note. Ids and counts are enough.

## 5. PostgreSQL environment

**EVIDENCE MISSING** for a database this phase is allowed to count or write. The configurations below were read. None of them is the approved disposable environment. No connection was opened in this step.

| What was read | Result |
|---|---|
| `docker-compose.yml` | PostgreSQL 16, database name `kitplatform`, port `5432`, role `kitplatform`, volume `kitplatform_pgdata`. The volume persists. This is the application database, not a throwaway. The password in that file is not repeated here. |
| `apply-test-migrations.ps1` | Targets database `kitplatform_test` on port `5432` with the same application role, and only migrations 001 through 006. It is not a content schema harness and not a select-only role. |
| `deploy/ubuntu/run-kit-migrations-content.sh` at `b64f677` | Applies each content manifest file with `psql -f` and `ON_ERROR_STOP`. No `--single-transaction`. The VPS wrapper reads a connection string from an env file. That file was not opened. |
| Inventory report, 2026-10-14 | One read-only transaction on database `kitplatform`, port `5432`, address `::1`, role `kitplatform`, PostgreSQL 18.4. The role has `CREATE` on `pack_content` and insert, update, delete, and truncate on all 16 checked tables. The session stopped. No business counts. |
| WP-1 report | Cluster data directory `E:\tmp\kit-tb-pgate01\data`, port `54329`, database `kit_tb_pgate01`, listen `127.0.0.1`. `postmaster.opts` in that directory matches those arguments. The directory still exists. That cluster was built for the release history, which includes the series customer migration. It is not a catalog match for `b64f677`. Its 19 passing tests are not F-03 evidence and are not evidence for this source. |
| Test project | `ContentChildContractTenantBoundaryPostgresTests.cs` exists at `121553e4` and does not exist at `b64f677`. |

**ARCHITECT CAN DECIDE** the shape of a later throwaway cluster. Do not create it in this step.

- Data directory outside the repository and outside `kitplatform_pgdata`. Port is not `5432`. Database name is not `kitplatform` and not `kitplatform_test`. Listen address `127.0.0.1` only.
- Schema is produced by applying the content manifest of `b64f677`, which ends at `367_pack_content_character_reference_pack_contract.sql`, plus the tracked files that are not in that manifest only when a package has explicitly named them. Do not apply untracked 376, either 385, or either 386.
- Three roles, not one. The inventory role has `SELECT` and schema `USAGE` only. It is not a superuser, cannot create roles, databases, or objects, cannot replicate, cannot bypass row-level security, inherits no other role, and is not named `kitplatform`, `pharmacore`, or `postgres`. The test-write role can insert, update, and delete only in this database, and only after the inventory session has ended. The migration role can apply one named file and is not reused for the counts.
- Before any business query, record `current_database()`, `inet_server_port()`, `inet_server_addr()`, and `current_user`. If the port is 5432 or the database name is `kitplatform`, stop.
- Disposable means the process can be stopped and the data directory deleted without affecting the application database, the pharmacy tenant `NT_XUANHOA`, or `DEMO_PHARMACY`. A named Docker volume and a database the API uses for daily work are not disposable.
- Existing episode rows, if they must be counted, are copied into this cluster by a dump of the content tables. The copy is the thing that is counted. The application database is not given a new select-only role in this design, because creating a role there writes to that server. The dump itself waits for the owner grant in section 8, because the tables may contain real episode content.

The old port-54329 directory is evidence that a throwaway cluster was possible. It is not reused until it is rebuilt from the source commit. Rebuilding it is a later package.

## 6. Closing the open platform gates

Statuses are the WP-A reading unless this section cites a new read. Historical PASS on `d54652d4` is not PASS on the current `HEAD`.

### Authority files, C-01–C-04, and D-09

**ARCHITECT CAN DECIDE** the disposition text. Step 031 already decided C-01 through C-04: work lists do not change authority rank; “Primary SSOT” does not create a second Level 1; Documentation State V1 is not declared wholly invalid; the 2026-10-08 consolidation report does not override the later NOT PASS. `b64f677` does not contain those paragraphs. A later commit can record them without changing rank and without editing line 231.

**ARCHITECT CAN DECIDE** D-09 on the evidence in hand. `git grep` for `RESET EXECUTION ORDER` on `b64f677` under `docs` found no file. Step 028 did not find the document under `docs/`, `docs/architecture/`, `docs/forensics/`, or `docs/00-control/`. A mandatory row whose document is not in the repository cannot be implemented. The disposition is: reset-order text is out of Phase 1.5 unless a later path is supplied on the source commit. This is a scope note, not a claim that a missing document passed.

**OWNER APPROVAL REQUIRED** only if the Product Owner still wants that missing document to block the phase. Leaving the row unknown blocks PASS forever. The recommendation is to take it out of the phase.

### Migrations 376, 383/384, the 385/386 pairs, and manifest lines 228–231

Tracked prefixes on `b64f677` from 360 upward are 360–367 (content), `368_pack_local_os_job_public_45d.sql`, `369_pack_local_os_room_keep_public.sql`, `371_inventory_stock_review.sql`, `383_pack_content_article_series.sql`, and `384_pack_content_brand_narrative_dna.sql`. The content manifest in that commit ends at 367. Files 383 and 384 are tracked and are not on the manifest. The Register already says to add those two exact names and not to rename them to 370 or 371.

Untracked names on the dirty disk, which must not be committed as part of becoming clean, include content files at 368–382, `385_pack_personal_intelligence_trading_ledger_v1.sql`, and `386_pack_content_famixa_canon_administer.sql`. Prefixes 368, 369, and 371 therefore collide with tracked local-os and inventory files. Prefixes 385 and 386 also collide with the release pair.

**ARCHITECT CAN DECIDE:**

- Do not commit untracked 376. Do not treat it as proof. The retraction in `b64f677` is the disposition.
- Freeze both 385/386 pairs. Do not copy either pair onto the source. Do not edit them to satisfy a checklist.
- Do not commit the dirty content manifest. It would schedule 376 and the on-disk 386, and it would put content files on prefixes owned by local-os and inventory.
- When a manifest edit is made, add exactly `383_pack_content_article_series.sql` and `384_pack_content_brand_narrative_dna.sql`. The Register decision is already recorded and is not a new product choice. The edit is the implementation that decision withheld from itself.
- New F-03 and F-08 files use 387 and 388 only after `git ls-tree -r` on the source commit and a disk listing show those names still absent.

**OWNER APPROVAL REQUIRED** for lines 228–231. At `b64f677`, both `migration-files.prod.txt` (lines 194–197) and `migration-files.family-os.txt` (lines 32–35) list all four files. Recommended split by filename, not by a guess about data: pharmacy prod keeps `228_kit_accounts_memberships.sql` and `229_kit_accounts_prefer_pharmacy.sql`; the Family pilot keeps `230_pack_family_parent_emotion_push.sql` and `231_pack_family_memory.sql`. Each manifest drops the other pair. They are not merged into one file. This changes what a pharmacy apply and a Family apply will run. That is an operations decision.

### Confirmed build defects and TestPack

On `b64f677`, `IContentSeriesTakeProxyService` requires `ReadVeoTake` and `ReadFalTake`. `ContentSeriesTakeProxyService` in that commit does not implement them. The isolated build recorded in `KPA-PHASE15-WHOLE-SCOPE-CLOSURE-PLAN-V1.md` exited 1 with CS0535 on those two methods, included five dirty files, and never compiled `KitPlatform.Api`. That build is not evidence for a clean commit.

`WorkflowController` still imports core engine types that `c447814d` removed. The fix is the pharmacy workflow namespace, not a content change.

`KitPlatform.Api.csproj` in the dirty tree has one extra line: a project reference to `KitPlatform.Packs.TestPack.Infrastructure`. That line is not in `b64f677`.

Commit `f86655e1` shows the intended byte source: read a named file under the video store (`TryReadNamed` for `veo` and `fal`), and do not accept a path outside that store. `TryReadNamed` does not exist on `b64f677`. The parent of `f86655e1` is the forbidden series migration, so the patch is a design source, not a commit to merge.

**ARCHITECT CAN DECIDE:** revert the TestPack reference in the implementation worktree; retarget the workflow using; implement the two readers from the `f86655e1` behavior without taking migrations 385 or 386. NU1903 for `Newtonsoft.Json` 10.0.3 stays unknown, not a new mandatory failure. A clean `dotnet build` of `KitPlatform.slnx` on a worktree whose porcelain is empty, at a commit whose ancestor is `b64f677`, is the gate. The dirty tree is not that worktree.

**EVIDENCE MISSING:** the compiler log of Api, Api.Tests, and Platform.Tests after the content project actually builds. The previous log never reached them. The six filter attributes are predicted to fail there because `class ProductionTenantActionFilter` does not exist on `b64f677`.

### Six actions that name a filter and do not check resource ownership

At `b64f677`, `ContentController.cs` applies `ProductionTenantActionFilter` and no such class exists:

| Line | Action | Route |
|---|---|---|
| 449 | `ListSeriesBuilds` | `GET series/builds` |
| 465 | `GetSeriesBuild` | `GET series/builds/{id}` |
| 485 | `PutSeriesBuild` | `PUT series/builds` |
| 505 | `DeleteSeriesBuild` | `DELETE series/builds/{id}` |
| 1969 | `ListProductionProgress` | `GET video-engine/production-progress` |
| 1985 | `GetProductionProgress` | `GET video-engine/series/builds/{id}/production-progress` |

`ContentSeriesPilotService` and `ProductionProgressService` on this commit do not mention a tenant. Migration 315 creates `series_build` without a customer column. Option A forbids adding one in this phase.

**ARCHITECT CAN DECIDE:** do not delete the six attributes to make a compile succeed. Do not copy the claim-only class from `121553e4` and call the gate passed. A type may be added so the attributes resolve, and tests must show that a caller who merely has an active tenant is not proved to own the build id. These six routes stay a shared series surface for Phase 1.5. They do not receive an isolation pass. Resource refusal for a series build would require the column Option A forbids.

Episode routes in F-03 are a different set. They do not have this attribute today. Their check is the episode owner predicate, after the grant. That predicate is not a reason to add a customer column to `series_build`.

### Tests and being able to reproduce them on one commit

**EVIDENCE MISSING** on the source commit. There is no tracked Phase 1.5 test list with a result at `b64f677`. WP-1’s 19 passed and 1 skipped were on the release worktree and the port-54329 cluster. The architect review of WP-1 did not re-run them.

The list to run later, on one hash, after the schema grant and the throwaway database:

- F-03 matrix rows 1–18 from the WP-3A specification, including the two concurrent attach cases.
- F-06: rejected cross-customer insert adds no event; the caller’s own draft still rejects.
- F-04/F-05/F-07: two customers see the same canon, and the test name does not say isolated.
- F-08: two customers, same shot code and version, after migration 388, each keeping its own customer id.
- The six series routes: active tenant does not imply ownership of the build id.
- `dotnet build KitPlatform.slnx` and the named tests, with the commit hash and a statement that the cited tree was clean.

A green build is not a substitute for these tests. Results from another commit are cited as prior evidence, not as this commit’s result.

## 7. Decisions the Architect can settle

These are recommendations. They are not Register edits and not APPROVED stamps.

1. Verification source is `b64f6772213dd0e095a49ae9a84983b37116f696`. Do not push as part of that choice.
2. Preserve `E:\KitPlatform` by using a new worktree later. Do not reuse the dirty phase15 worktrees.
3. F03-O1: hide unowned episodes. Do not guess a customer. Do not delete them.
4. F03-O2: each customer gets a distinct episode when they attach `EP01` on the shared Famixa project.
5. F03-O3: one 404 message for missing, other-customer, and unowned. 401 only for a missing tenant claim.
6. F03-O4 and F03-O5: the two partial uniques in section 4, project kept in the key, old unique dropped only after both partials and the trigger.
7. F-04, F-05, and F-07 stay a shared studio surface. No customer columns on those tables. No isolation pass.
8. F-06 is ported as new code after the shot has a customer column. Event tables do not gain a column. Direct SQL read stays a recorded residual.
9. F-08 is a separate migration at 388. It does not drop a global unique while null rows could overwrite each other.
10. Freeze 376 and both 385/386 pairs. Do not commit the dirty manifest. Add the already-decided 383 and 384 manifest lines when the manifest package runs. Reserve 387 and 388.
11. Content migration files are not wrapped in one transaction by the current runner. DDL order must be safe statement by statement.
12. D-09 reset-order text is out of Phase 1.5 unless a path appears on the source commit.
13. C-01–C-04 text from Step 031 can be recorded on the source without changing authority rank.
14. TestPack reference is reverted. Veo and Fal readers are implemented from the local video-store behavior in `f86655e1`, without merging that commit’s parent.
15. The six series filters are not an isolation mechanism in this phase.
16. The port-54329 cluster and the port-5432 application database are not the proof database.

## 8. Decisions that still need the Product Owner

Four decisions. Each one changes a customer-visible rule, a deploy, or a Register prohibition. Technical questions from section 7 are not in this list.

1. **May an episode, a production shot, and the two generation records carry an optional customer owner?**  
   Today two customers who both work on “EP01” can open the same episode and replace each other’s pictures, prompts, and review. The fix stores the customer on the episode, and on the production shot and the two execution records that otherwise cannot be separated. Old rows stay unowned and hidden. The Famixa series script does not gain a customer. Row-level security stays off. Nothing is backfilled.  
   If you say yes, a later package may add only those nullable columns, in new files 387 and 388, after the counts. If you say no, F-03, F-06, and F-08 stay open and Phase 1.5 stays NOT PASS.  
   This is the line 231 grant. This file does not write it into the Register.

2. **Is it acceptable that every content administrator still shares one Famixa character and visual bible?**  
   Customer A and customer B will have separate episodes. They will still share the Famixa project, the character rules, and the visual style. An administrator of one customer can lock or edit rules the other customer also sees.  
   If you say yes, F-04, F-05, and F-07 close as a documented shared surface, not as isolation. If you say no, those three findings stay open and this phase does not design private canon.

3. **When pharmacy is updated, should it stop also applying the two Family memory files, and should the Family pilot stop also applying the two account files?**  
   The recommended split is in section 6. Saying yes changes the next pharmacy apply and the next Family apply. Saying nothing leaves both deploys applying all four files.

4. **May a copy of the existing content tables be taken into a throwaway database so the old episodes can be counted?**  
   The count has to see the real rows. The application database on port 5432 must not be queried with the application role, and it must not receive the new migration. A dump into the throwaway cluster is a read of episode content. If you say yes, the next package creates the cluster in section 5 and counts. If the studio has no episodes that must be preserved, say that instead, and the migration may be rehearsed on an empty cluster only. An empty cluster does not prove there are no duplicates in the database you actually use.

Publication of `b64f677` is optional and is not required to start the local worktree. D-09 does not need a separate product answer if you accept the Architect disposition that a missing document is out of the phase.

## 9. Evidence still missing, and how to collect it safely

| Gap | Why it matters | Safe collection |
|---|---|---|
| Episode and contract counts | A duplicate `(project_id, production_code)` stops the migration. A large hidden set tells the studio that today’s episode will disappear until a later assignment. | After decision 4, dump content tables into the throwaway cluster. Run the count queries from section 4 as the select-only role. Store counts and ids. Do not store prompt or contract bodies in the note. |
| Catalog match to `b64f677` | The port-54329 cluster has the release migrations. Counting it would describe the wrong schema. | `information_schema` and `pg_constraint` on the new cluster, compared with migrations 333, 337, 338, and 356. Drift stops the work. |
| Clean compiler log of Api and the two test projects | Predicted filter failure is not yet a compiler line. Other errors may appear after CS0535 is gone. | `dotnet build KitPlatform.slnx` in the new worktree, porcelain empty, no TestPack reference. |
| F-03 and F-08 facts on one hash | WP-1’s tests do not cover them and were not run on this source. | The test list in section 6, on the throwaway database, after the granted migration. |
| Whether `KitVideoVisionService.DecideAsync` has an HTTP route besides `visual/decide` | The specification left this unknown. The known action calls the pixel service and is already in the F-03 path. | One search of controller actions on the source commit during the F-03 package. If another route calls it, the same owner predicate applies. Do not open a new audit. |
| Live host tip of `origin/main` | The cached ref may be stale. | Not required for local verification. A fetch only if publication is requested. |

Do not collect any of this by connecting to port 5432 as `kitplatform`, by applying a migration to the application database, or by reading the untracked 376 file as if it were history.

## 10. Implementation plan

Four packages. F-03 through F-08 stay mandatory. A package stops at its stop condition even if later packages are already described.

### WP-B — Clean source worktree

- **Blocker.** Gates cannot be scored on a dirty tree or on two hashes.
- **Scope.** A new worktree at `b64f677` only. No files from `E:\KitPlatform` are copied in. No edit in this package.
- **Depends on.** The source recommendation in section 2. Does not depend on the owner grant.
- **Acceptance.** `git rev-parse HEAD` prints `b64f6772213dd0e095a49ae9a84983b37116f696`. Porcelain of that worktree is empty. `E:\KitPlatform` is still `0d8568ae` and still dirty.
- **Evidence.** The two hashes and the two porcelain results.
- **Stop.** Any command would reset, clean, or stash `E:\KitPlatform`, or the new worktree is not empty.
- **Opens.** WP-C and WP-D may be scheduled. It does not open schema.

### WP-C — Compile, without tenant schema

- **Blocker.** CS0535, the workflow using, the uncommitted TestPack reference, and the unknown Api errors behind them.
- **Scope.** Workflow controller using. Veo and Fal readers limited to named files inside the video store. Remove the TestPack reference in this worktree. Add the filter type only so the six attributes resolve, with tests that an active tenant does not prove build ownership. No migration. No customer column. No edit of the six attribute lines.
- **Depends on.** WP-B. Independent of the owner grant.
- **Acceptance.** `dotnet build KitPlatform.slnx` exits 0 on that clean worktree. The two reader methods return null or bytes from inside the store and do not accept an outside path. The series tests show the limitation in section 6.
- **Evidence.** Command, exit code, commit hash, porcelain.
- **Stop.** The reader patch requires a type or a column that exists only on the release branch. Do not merge `bfe51e14` to get past that. Stop and report the missing type.
- **Opens.** A clean build can be re-read. It does not pass AG-G3, AG-G4 resource ownership, or F-03–F-08.

### WP-D — Authority notes and the safe manifest lines

- **Blocker.** C-01–C-04 are not on the commit. 383 and 384 are not on the content manifest. 228–231 have two owners. 376 and both 385/386 pairs must stay out.
- **Scope.** Record the Step 031 dispositions and the D-09 out-of-scope sentence without changing rank or line 231. Add the two 383/384 manifest lines. Exclude 376, both 385 files, and both 386 files in the commit message and by not adding them. Apply the 228–231 split only if decision 3 is yes.
- **Depends on.** WP-B. Decision 3 for the four-file split. The 383/384 lines do not wait on that decision.
- **Acceptance.** The content manifest diff is only the two named lines, unless decision 3 also changes the four names in the two other manifests. `git ls-tree` does not show a series-script customer migration. Line 231 text is unchanged.
- **Evidence.** The manifest diff and the new commit hash.
- **Stop.** The dirty manifest from `E:\KitPlatform` is the file being committed, or a 368/369/371 content file would replace a tracked local-os or inventory file.
- **Opens.** Manifest gates can be re-read. They are not a phase PASS.

### WP-E — F-03 through F-08, one schema sequence

- **Blocker.** Customer columns are unauthorized. Counts do not exist. F-06’s predicate has no column on this source. F-08’s uniques cannot include a customer the table does not have.
- **Scope.** After decisions 1, 2, and 4: create the throwaway cluster; count; stop if the catalog drifted or `(project_id, production_code)` is duplicated. Then one migration 387 for the granted nullable columns and the F-03 unique pair, application predicates for episode, contract, attempt, job, story, and motion, and the F-06 shot predicate. Shared-canon tests for F-04, F-05, and F-07 with no schema change on those tables. Preflight, then migration 388 for F-08. No row-level security, no backfill, no `series_build` column, no edit of 385 or 386.
- **Depends on.** WP-B, decisions 1, 2, and 4, and the counts. WP-C should land on the same hash before the final test run so the build and the tests are one commit. WP-D’s manifest lines must include 387 and 388 only in the same commit that adds those files, not before the files exist.
- **Acceptance.** Every F-03 criterion in section 4, the F-06 event count, the shared-canon tests, and the F-08 two-customer insert, all on one hash and on the throwaway database. Null rows are still null.
- **Evidence.** Count output, migration file hashes, test command and output, database identity, commit hash. Before-and-after snapshots only if an apply on that throwaway database was part of the same package. No snapshot from port 5432.
- **Stop.** Decision 1 is no. The count finds duplicates. The catalog does not match `b64f677`. A statement would update a null customer id. The package is about to edit `series_build` or enable row-level security.
- **Opens.** A final acceptance read of every mandatory row on that one hash. That read is a later package. It does not start inside WP-E, and it does not open Phase 2. Deploy and the application database still need their own approval.

## 11. Conclusion

There is enough evidence to name the source and to close the five F-03 design questions as Architect recommendations. There is not enough authority to start the schema package.

Still required before any migration or authorization code:

- The Product Owner’s grant for optional customer owners on the episode, the production shot, and the two generation records, with old rows left hidden and the series script left unchanged.
- The Product Owner’s acceptance that the Famixa character and visual bible stay shared in this phase.
- A throwaway database, and permission to count a copy of the existing content tables. The port-5432 application database and the old port-54329 release cluster do not qualify.
- The 228–231 deploy split, before those manifest lines change.

Not required before a non-schema start: publication, a replay hash, or another full-repository audit. WP-B and WP-C can proceed from the source recommendation. They do not close Phase 1.5.

The highest-value next step is the grant in section 8 together with the throwaway database. Writing another decision package, or writing SQL before the counts, does not move the phase.

Phase 1.5 remains **NOT PASS**. Phase 2 is not opened.
