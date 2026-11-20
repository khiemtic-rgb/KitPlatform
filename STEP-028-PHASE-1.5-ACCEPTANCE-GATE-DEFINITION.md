# STEP 028 — Phase 1.5 Acceptance Gate Definition

Status: **PROPOSAL AWAITING ARCHITECT APPROVAL.** Not canonical policy.  
Phase 1.5: **NOT PASS.** This step does not change that verdict.  
HEAD baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate. Phase 2 is not opened.

This file defines a proposed acceptance checklist. It does not implement schema, API, filters, migrations, or tests. It does not amend ADR-03, the Decision Register, or Master Architecture. It does not lift any sentence in those documents. Stash contents and untracked files are not implementation evidence.

No product file, migration, ADR, Decision Register, or Master Architecture file was edited. No database, deploy, migration, commit, push, merge, reset, or clean was run.

## Labels

| Label | Meaning in this brief |
|---|---|
| DECIDED | A tracked authority document states a closed decision. |
| VERIFIED | Step 027 recorded the fact from git history or a tracked file. This step did not re-run that check. |
| RECORDED | A prior Level 4 report states the fact. That report is not in HEAD. This step did not re-measure it. |
| UNVERIFIED | A tracked sentence claims a fact, and the cited evidence is not in repository history. |
| REQUIRES APPROVAL | Only the Architect can close this. This brief does not close it. |
| UNKNOWN | No located source answers the question. |

Level 4 and Level 5 documents do not become architecture authority by being cited here. `docs/architecture/GOVERNANCE.md` §8: promotion is explicit, and automatic promotion is none. `GOVERNANCE.md` §9: forensics support decisions and do not become authority on their own.

## Sources located

| Path | Role | In HEAD? |
|---|---|---|
| `STEP-027-AUTHORITY-GAP-CLOSURE-DECISION.md` | Prior step artifact. Authority-gap facts reused below. | No. Untracked step file. |
| `docs/architecture/KIT-PLATFORM-PHASE-1.5-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md` | Prior verification, 2026-10-11, verdict NOT PASS. Git, manifest, build, and test observations reused below. | No. `docs/architecture/*` leaves it untracked. Level 4. |
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | Blob `e199ed02a4b119686a7a3f8f934e49dbba1502d4` at `d54652d4`, as cited by Step 027. | Yes, on local HEAD. |
| `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` | Blob `9065fc9a264f592a19759a6d0d8ef52d95321756` at `d54652d4`, as cited by Step 027. | Yes, on local HEAD. |
| `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` | Blob `128a7996fb2007bd2a595af3f15391b25087450f` at `d54652d4`, as cited by Step 027. | Yes, on local HEAD. |
| `docs/architecture/GOVERNANCE.md` §3, §4, §8, §9 | Authority levels, change rule, promotion, evidence. Present on local HEAD per the Phase 1.5 report. | Local HEAD. Absent from `origin/main` per that report. |
| `migrations/315_pack_content_series_build.sql` | Blob `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d`, as cited by Step 027. | Yes. |
| `deploy/ubuntu/migration-files.content.txt` | Blob `10053f6ae9043c955f0fa208a9d499e78482df65` at `ceb521ff`, as cited by Step 027. | Yes, at that blob. A dirty working copy is a separate fact, RECORDED by the Phase 1.5 report. |

Named files `STEP-023` through `STEP-026` were not found at the workspace root or in the worktrees listed by `git worktree list`. Their separate texts are **UNKNOWN**. The blockers named in the Step 028 task are carried from Step 027 and from the Phase 1.5 verification above. Nothing in this brief treats those missing filenames as resolved work.

`RESET EXECUTION ORDER V1` was not found by the Phase 1.5 report. Its text remains **UNKNOWN**.

## A. Gate checklist

Every row is **proposed** and **mandatory** unless a later Architect decision removes it. One PASS row does not pass the phase.

Status values: PASS, FAIL, BLOCKED, UNKNOWN.  
Approval values: none, or REQUIRES APPROVAL.

Check method for every row is a read of the named evidence. This step does not authorize a new repository audit, a build, a test run, a migration, or a database session.

### 1. Git state and physical consolidation

#### AG-G1-01 — Repository identity

- PASS condition: The inspection root is the agreed git toplevel, the branch is the agreed branch, and HEAD equals the baseline named for this gate.
- Evidence: `git rev-parse` output recorded against `E:/KitPlatform`, branch `main`, HEAD `d54652d4a7734b3116db62bd356a831f9942fc73`.
- Check: Compare those three values to the baseline in this brief.
- Status: **PASS** (carried; not re-measured in Step 028).
- Approval: none for identity. This row does not accept the three local commits and does not pass Phase 1.5.

#### AG-G1-02 — Agreed published source for the architecture commits

- PASS condition: The Architect has named the remote commit that is the Phase 1.5 source of truth, and that commit contains the Level 1–3 files the gate relies on. Local-only commits are either absent, or explicitly accepted in writing as that source.
- Evidence: Phase 1.5 report §4 and §6. Local `main` recorded as three commits ahead of `origin/main` `73d3c4a6c509ab25229d07a475229a6c8c1cf79d`: `c447814d`, `98f0632c`, `d54652d4`. `origin/main` does not contain `docs/architecture/INDEX.md`. Those commits are not recorded as verified safe to publish.
- Check: `git log origin/main..HEAD` and `git cat-file -e` for each required path on the named source. Do not push as part of the check.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL** to publish `d54652d4` and its two ancestors, or to name a different source. This brief does not approve a push.

#### AG-G1-03 — Working tree does not carry the result

- PASS condition: The commit accepted in AG-G1-02 has a clean tree for every path the gate cites. Phase 1.5 evidence does not depend on unstaged, staged-but-uncommitted, or untracked product, migration, or authority files.
- Evidence: Phase 1.5 report §4 Git row. Recorded porcelain: 282 untracked, 166 modified, 2 staged, count 450. That snapshot was already not the earlier “~130 modified files” note. Step 027 and this file were added later and were not inventoried here. The tree was not clean, and this step does not claim it became clean.
- Check: `git status --porcelain` on the accepted commit, after the Architect names which paths must be clean. No reset, clean, or commit in the check.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** before any later commit that would clear this row. Defining the row grants no commit.

#### AG-G1-04 — Required architecture file set is tracked on the agreed source

- PASS condition: The Architect names the file set. Each named file is in the agreed commit and on the agreed remote. Gitignored copies do not satisfy the row.
- Evidence: Phase 1.5 report §4 Files row and §6. Level 1–3 files are on local HEAD and absent from `origin/main`. Completion gate, control-plane review, documentation state, consolidation matrix, knowledge-routing model, pack/module registry, and several forensic reports were present on disk and ignored. The 2026-10-08 physical-consolidation baseline was staged and not in HEAD.
- Check: For each Architect-named path, `git cat-file -e <agreed-commit>:<path>` succeeds.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL** of the set, then a separate promotion under `GOVERNANCE.md` §8 (gitignore exception, commit, index update). This brief does not promote any file.

### 2. Architecture authority and evidence consistency

#### AG-G2-01 — Authority rank stays the tracked rank

- PASS condition: Level 1 remains `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`. Level 2 remains the Decision Register. Level 3 remains the ADR decision files. INDEX is navigation, not a rank above Level 1. No Phase 1.5 task silently changes that rank.
- Evidence: `GOVERNANCE.md` §3. Phase 1.5 conflicts C-01 and C-02 are unresolved. C-01: task reading order versus INDEX §2 and GOVERNANCE §3. C-02: `docs/00-control/KIT-WORK-INDEX.md` §6.1 names another primary SSOT beside the Master Architecture. A rank change would be BREAKING under GOVERNANCE §5.
- Check: Architect writes a disposition that C-01 is reading order only, and a disposition for C-02 that does not install a new Level 1. Absence of that writing leaves the row closed.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL**. This brief does not pick a winner.

#### AG-G2-02 — Implementation claims match tracked history

- PASS condition: Every tracked sentence that says Migration 376 or a named forensic record already implemented tenant attribution is either withdrawn by an Architect decision, or the cited path exists in the history of the agreed commit.
- Evidence: **UNVERIFIED** claims, carried from Step 027. ADR-03 line 156, Register lines 164, 604, 630, 677, and 693, and Master Architecture line 433 say Migration 376 added nullable `tenant_id`. Register line 693 and the synchronization section cite forensic records. Step 027 recorded no commit reachable from HEAD for:
  - `migrations/376_pack_content_production_tenant_boundary.sql`
  - `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md`
  - `docs/forensics/PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01.md`
  - `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md`
  The migration path was recorded only on `refs/stash` and `stash^3` (`c170af39`), which are not ancestors of HEAD. That object was not read and is not implementation evidence. An on-disk copy, if one exists outside history, is the same: not implementation evidence.
- Check: `git log --all --full-history -- <path>` on the agreed commit, excluding stash. Do not restore the file in order to pass the check.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** before any edit to ADR-03, the Register, or Master Architecture. This brief does not edit them and does not treat the claims as false by rewriting them.

#### AG-G2-03 — Register line 231 disposition

- PASS condition: The Architect records, in a decision this brief does not substitute, whether this sentence still binds: “Production is **not** tenant-safe today. GATE A is not executed. No TenantId/WorkspaceId/RLS/API change is authorized by this record.” (Register line 231, blob `9065fc9a264f592a19759a6d0d8ef52d95321756`).
- Evidence: **VERIFIED** text of line 231, carried from Step 027. Later paragraphs that call the wording stale rely on files that are not in history (**UNVERIFIED** premise). Those paragraphs do not authorize an API or schema change.
- Check: A written Architect disposition exists, and it does not cite missing forensic files as proof.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL**. Line 231 stays in force for this gate until that disposition exists.

**DECIDED, and not reopened here.** ADR-03 Option 1 remains tenant-only Production scope. Workspace is not mandatory. GATE A (ADR-03 lines 118–127) still states the requirement and still says not to design the mechanism in that record.

#### AG-G2-04 — Conflicts C-01 through C-04 have a written disposition

- PASS condition: Each conflict recorded in the Phase 1.5 report §3 has an Architect disposition. The 2026-10-08 physical-consolidation conclusion is not accepted as Phase 1.5 PASS (C-04) unless a new check replaces it.
- Evidence: **RECORDED** conflicts C-01, C-02, C-03, C-04. C-03: Documentation State V1 versus files that are in local HEAD. C-04: that older report’s tracking and unique-migration-ID claims versus the inventory in the Phase 1.5 report. Completion-gate `architect_acceptance` was recorded empty. This step did not re-open those files to re-audit them.
- Check: Four written dispositions, each naming the conflict ID.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL**.

#### AG-G2-05 — Reset execution order

- PASS condition: Either the Architect supplies the path of `RESET EXECUTION ORDER V1` and the gate cites that text, or the Architect records that the document is out of scope for Phase 1.5.
- Evidence: Phase 1.5 report §3. Not found under `docs/`, `docs/architecture/`, `docs/forensics/`, or `docs/00-control/`.
- Check: Path exists in the agreed commit, or a written out-of-scope decision exists.
- Status: **UNKNOWN**.
- Approval: **REQUIRES APPROVAL** to declare it out of scope. Inventing its text is not allowed.

### 3. Migration manifest and schema evidence

#### AG-G3-01 — Migration 376 citation is either in history or not used as proof

- PASS condition: The agreed commit contains `migrations/376_pack_content_production_tenant_boundary.sql`, and the content manifest lists it, or no acceptance claim depends on that filename.
- Evidence: **VERIFIED** absence from HEAD history, carried from Step 027. The content manifest blob cited by Step 027 does not list a 376 file. Reconstructing the file from its name, from citations, or from stash is not a PASS.
- Check: `git cat-file -e <agreed-commit>:migrations/376_pack_content_production_tenant_boundary.sql` and the corresponding manifest line. A stash hit is a fail of this check.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** before anyone adds a migration file. See AG-G3-02. This brief does not recreate 376.

#### AG-G3-02 — `series_build` tenant attribution matches an approved scope

- PASS condition: The Architect decides whether `pack_content.series_build` tenant attribution is in Phase 1.5 scope. If it is out of scope, the decision says so and no migration is added. If it is in scope, one new tracked migration and one manifest entry exist, authored as their own change, and the column definition matches that decision. Historical backfill is a separate decision and is not implied.
- Evidence: **VERIFIED** schema, carried from Step 027. Migration 315 creates `pack_content.series_build` with `id`, `series_code`, `episode_code`, `title`, `status`, counts, `graph_json`, and timestamps. No tenant attribution column. No tracked migration runs `ALTER TABLE pack_content.series_build`. Migrations 333, 383, and 286 do not add that column. Register line 231 does not authorize a TenantId change.
- Check: Read the Architect scope decision, then the tracked migration and manifest entry only if the decision includes them.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL**. No tracked document located by Step 027 authorizes writing the column now.

#### AG-G3-03 — Manifest ownership is exclusive for the recorded collisions

- PASS condition: The Architect assigns one manifest owner for each colliding filename, and the agreed commit shows that assignment. Overlap is not cleared by merging manifests inside this gate.
- Evidence: **RECORDED**, Phase 1.5 report §4 and §6. 407 `.sql` files, prefixes 0–386, 379 unique numbers, 23 duplicated prefixes, gaps 202–209, plus unnumbered `192a_pack_family_os_vertical_owner.sql`. Example duplicates in HEAD: `294_pack_content_gemini_36.sql`, `294_pack_local_os_v1.sql`, `294_pharmacy_consultation_mvp1.sql`. Filenames listed in both `migration-files.prod.txt` and `migration-files.family-os.txt`: `228_kit_accounts_memberships.sql`, `229_kit_accounts_prefer_pharmacy.sql`, `230_pack_family_parent_emotion_push.sql`, `231_pack_family_memory.sql`. Not re-counted in Step 028.
- Check: Architect ownership table, then a manifest diff limited to those names.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** of exclusive ownership. Do not merge manifests under this definition.

#### AG-G3-04 — Content migrations after 367 are in the agreed commit, or excluded by decision

- PASS condition: The Architect decides the fate of content migrations after the tracked manifest end. Files that the gate relies on are committed as they already exist. `migrations/386_pack_content_famixa_canon_administer.sql` is not edited to satisfy the gate.
- Evidence: **RECORDED**, Phase 1.5 report §4. HEAD `migration-files.content.txt` ends at `367_pack_content_character_reference_pack_contract.sql` (58 entries). The dirty working copy lists through 386 and skips 381 and 385. Untracked and unmodified by that task: `migrations/368`–`382` content/test files, `385_pack_personal_intelligence_trading_ledger_v1.sql`, and `386_pack_content_famixa_canon_administer.sql`. Untracked manifests: `migration-files.personal-intelligence.txt`, `migration-files.test-pack.txt`. `migrations/281_pack_content_wave0.sql` had an unstaged one-line change.
- Check: `git ls-files` for the Architect-named set on the agreed commit. Confirm migration 386 has no content edit in that change.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** to commit existing files. No approval is granted here. Do not edit migration 386 in order to pass.

#### AG-G3-05 — Schema authority is not implied by this checklist

- PASS condition: No schema change is treated as authorized merely because a row above describes it. A later implementation step cites a separate Architect approval.
- Evidence: Register line 231 (**VERIFIED** text). `GOVERNANCE.md` §4: a decision is recorded as DECIDED before implementation. This file is Level 5 task text until promoted.
- Check: The implementation commit message or decision record cites an approval newer than this file. This row cannot PASS inside Step 028.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL** for every schema change, including any future `series_build` migration.

### 4. API enforcement and tenant isolation

#### AG-G4-01 — `ProductionTenantActionFilter` has a tracked disposition

- PASS condition: The agreed commit contains a tracked type that implements the six `ProductionTenantActionFilter` attributes on `ContentController.cs`, at the location the Architect names, or the Architect decides in writing that those attributes are removed. Removal as a compile shortcut, without that decision, is not a PASS.
- Evidence: **VERIFIED** gap, carried from Step 027. Six attributes exist. No tracked type implements them. ADR-03 does not choose filter, service, or repository. Line 231 has not been lifted.
- Check: `git grep` for the type and the six attributes on the agreed commit, plus the written location or removal decision.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** of the location, or of removal. This brief does not add or delete the filter.

#### AG-G4-02 — Tenant-isolation behavior is shown by a named check

- PASS condition: The Architect names the checks that demonstrate caller-tenant refusal for the surfaces in scope. Those checks have a recorded result on the agreed commit. A compile of test projects is not that result. A dirty-tree build is not that result.
- Evidence: Step 027: no tracked Phase 1.5 test list, and that step ran no tests (**UNKNOWN**). Phase 1.5 report §4 Test row: tests were not executed (**UNKNOWN**). GATE A’s mechanism remains undesigned in ADR-03.
- Check: Run only the named checks, with no production database, no deploy, and no paid provider, after AG-G2-03 and AG-G4-01 are no longer blocking.
- Status: **UNKNOWN**.
- Approval: **REQUIRES APPROVAL** of the check list before any run. No list is proposed here, because none is in the tracked sources.

#### AG-G4-03 — API change is not authorized by stale-wording paragraphs

- PASS condition: An Architect disposition says whether any TenantId, WorkspaceId, RLS, or API change is authorized. Until that exists, line 231 remains the binding sentence.
- Evidence: Same as AG-G2-03. Paragraphs that call “GATE A not executed” stale are **UNVERIFIED** in their factual premise.
- Check: The disposition is in a tracked decision. This checklist is not that decision.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL**.

### 5. Build, test, and test evidence

#### AG-G5-01 — Build evidence is for the agreed commit

- PASS condition: `dotnet build` of the agreed solution file, on a clean tree of the agreed commit, exits 0. The recorded dirty-tree build does not meet this row.
- Evidence: **RECORDED**, Phase 1.5 report §4. Command `dotnet build e:\KitPlatform\KitPlatform.slnx`, 2026-10-11, exit 0, 0 errors, 63 warnings, 00:00:41.72. That compile included the uncommitted TestPack reference. It does not prove `origin/main` or a clean HEAD. NU1903 for `Newtonsoft.Json` 10.0.3 was reported. Whether NU1903 itself blocks acceptance is **UNKNOWN**; it is not added as a separate mandatory row.
- Check: Clean checkout of the agreed commit, then the Architect-named build command. No product edit to make the build green during the check.
- Status: **FAIL**. Accepted build evidence is absent. The result of a future clean build is **UNKNOWN**.
- Approval: none to define the row. A later clean build still does not replace AG-G3 or AG-G4.

#### AG-G5-02 — Phase 1.5 test list exists in a tracked document

- PASS condition: A tracked document, accepted by the Architect, lists the Phase 1.5 tests by command or by test name, and states that they do not open a production database.
- Evidence: Step 027 found no tracked Phase 1.5 test list. No tracked file under `docs/` at HEAD contained the text “Phase 1.5”.
- Check: `git cat-file -e` on the agreed commit for that document, then read the list.
- Status: **UNKNOWN**.
- Approval: **REQUIRES APPROVAL** of the list. This brief intentionally does not invent one.

#### AG-G5-03 — Named tests have a current result

- PASS condition: The commands in the approved AG-G5-02 list have been run on the agreed commit, and the recorded result is a pass of that list.
- Evidence: Phase 1.5 report: test execution not performed. Step 027: no tests run.
- Check: Run the approved list only. Compilation during build is not execution.
- Status: **UNKNOWN**.
- Approval: **REQUIRES APPROVAL** before the run. Production DB, deploy, and paid providers stay out of scope.

#### AG-G5-04 — Dependency source of truth is the agreed commit

- PASS condition: The Architect decides whether `KitPlatform.slnx` plus HEAD project references are the dependency source of truth. The uncommitted TestPack `ProjectReference` is either committed as its own approved change or reverted. Solution membership of projects that exist on disk but are outside the solution is either decided or explicitly deferred.
- Evidence: **RECORDED**, Phase 1.5 report §4 Dependencies. `KitPlatform.slnx` clean, 17 `.csproj` entries. Filesystem has 30 `.csproj` files under `src/` and `tests/`. HEAD `KitPlatform.Api.csproj` does not reference TestPack. The working tree adds that reference (one-line unstaged diff). No `Directory.Packages.props`, `nuget.config`, or `global.json`.
- Check: `git diff` empty for the Api project reference on the agreed commit, and a written membership decision.
- Status: **FAIL**.
- Approval: **REQUIRES APPROVAL** to commit or revert the reference, and to decide solution membership.

### 6. Working-tree safety and reproducibility

#### AG-G6-01 — The same verdict can be reproduced from the agreed commit

- PASS condition: A second reader, using only the agreed commit and the commands named in this checklist, can reach the same PASS or FAIL for every mandatory row without the current working tree, stash, or gitignored copies.
- Evidence: Phase 1.5 verdict NOT PASS depended on a dirty tree, gitignored architecture files, and untracked migrations. Step 027’s verified gaps are in history; the phase checklist itself is not in history. This file is a new untracked artifact and is not that reproducible baseline.
- Check: Repeat the named checks on a clean checkout. Do not copy the working tree into the checkout to make them pass.
- Status: **FAIL**.
- Approval: none to define the row. Clearing it requires the approvals already listed.

#### AG-G6-02 — Stash and untracked files are outside the evidence set

- PASS condition: No mandatory PASS cites `refs/stash`, `stash^3`, an untracked migration, or a gitignored report as proof that schema, API enforcement, or tenant isolation is implemented.
- Evidence: Step 027 excluded stash object `c170af39` and did not read it. The Phase 1.5 report is Level 4 and not in HEAD; this checklist uses it as a record of gaps, not as proof of implementation. The six-filter gap and the migration 315 shape are **VERIFIED** from tracked files. The 376 implementation claim is **UNVERIFIED**.
- Check: For each PASS row, the evidence path is reachable from the agreed commit.
- Status: **FAIL**. The current evidence set still cannot pass the phase without decisions about untracked and unpublished material. No mandatory implementation claim has been accepted from stash.
- Approval: none to keep this exclusion. It stays in force.

#### AG-G6-03 — This definition is not yet the canonical gate

- PASS condition: The Architect approves this checklist, or a named revision, as the Phase 1.5 acceptance gate. Until then the document stays a proposal.
- Evidence: No tracked Phase 1.5 pass checklist was found (Step 027). `GOVERNANCE.md` §8 automatic promotion is none.
- Check: A written Architect approval cites the file name and the revision. Approval of the checklist still does not mark the rows above PASS.
- Status: **BLOCKED**.
- Approval: **REQUIRES APPROVAL**.

## B. Known blockers

These are open. No new evidence in Step 028 shows any of them closed.

| ID | Blocker | Evidence class | Gate rows |
|---|---|---|---|
| B-01 | Local commits `c447814d`, `98f0632c`, and `d54652d4` are not on `origin/main` and are not verified safe to publish. | RECORDED | AG-G1-02 |
| B-02 | Working tree is dirty: large modified set, large untracked set, staged files that are not in HEAD. Later step files were not re-counted. | RECORDED | AG-G1-03, AG-G6-01 |
| B-03 | Migration 376 and the forensic records cited for tenant implementation are not in HEAD history. Stash is not a substitute. | VERIFIED absence; UNVERIFIED claim | AG-G2-02, AG-G3-01 |
| B-04 | Migration 315 defines `pack_content.series_build` with no tenant attribution. No tracked `ALTER` adds one. | VERIFIED | AG-G3-02 |
| B-05 | Six `ProductionTenantActionFilter` attributes have no tracked implementation. | VERIFIED | AG-G4-01 |
| B-06 | Register line 231 has not been lifted by an Architect decision. Paragraphs that call it stale cite missing files. | VERIFIED text; UNVERIFIED stale-claim | AG-G2-03, AG-G4-03 |
| B-07 | No tracked document defines the Phase 1.5 acceptance tests. | UNKNOWN | AG-G5-02, AG-G5-03, AG-G4-02 |
| B-08 | Duplicate migration prefixes and four filenames listed in both prod and family-os manifests have no exclusive owner. | RECORDED | AG-G3-03 |
| B-09 | Content migrations after 367, including 386, are untracked while a dirty manifest lists them. | RECORDED | AG-G3-04 |
| B-10 | Conflicts C-01–C-04 and the missing reset-order path have no Architect disposition. | RECORDED; path UNKNOWN | AG-G2-01, AG-G2-04, AG-G2-05 |
| B-11 | TestPack reference and solution membership are not the clean HEAD dependency story. The only build is a dirty-tree build. | RECORDED | AG-G5-01, AG-G5-04 |

## C. Acceptance rules

**Proposed. Awaiting Architect approval. Not canonical policy.**

1. Phase 1.5 is PASS only when every mandatory row in section A is PASS on the agreed commit.
2. Any mandatory row that is FAIL, BLOCKED, or UNKNOWN blocks acceptance. AG-G1-01 PASS does not outweigh another row.
3. A successful build does not replace schema evidence (section 3), authorization evidence (AG-G2-03, AG-G4-03), or tenant-isolation evidence (AG-G4-02).
4. Publishing this checklist does not authorize implementation, deploy, migration, push, or an API change.
5. A row marked REQUIRES APPROVAL stays BLOCKED until the Architect records the decision. An agent, a later paragraph in the Register, or a missing forensic file cannot close it.
6. Stash, untracked files, and gitignored reports cannot be used as proof that a schema or API change is already implemented.
7. Order stays Phase 1, then Phase 1.5, then the Phase 2 Gate. Phase 2 does not start while Phase 1.5 is not PASS.
8. ADR-03 Option 1 stays DECIDED. This proposal does not reopen it and does not mark it implemented.

## D. Answers

### 1. Which mandatory conditions are FAIL?

AG-G1-03, AG-G2-02, AG-G3-01, AG-G3-03, AG-G3-04, AG-G4-01, AG-G5-01, AG-G5-04, AG-G6-01, AG-G6-02.

### 2. Which conditions are BLOCKED by approval?

AG-G1-02, AG-G1-04, AG-G2-01, AG-G2-03, AG-G2-04, AG-G3-02, AG-G3-05, AG-G4-03, AG-G6-03.

Several FAIL rows also need a separate approval before anyone changes files to address them: AG-G1-03, AG-G3-03, AG-G3-04, AG-G4-01, AG-G5-04. The FAIL is the current evidence. The approval is permission to change something later. This brief gives neither a PASS nor that permission.

### 3. Which evidence is still UNKNOWN?

- AG-G2-05: text and path of `RESET EXECUTION ORDER V1`.
- AG-G4-02: no named tenant-isolation check and no result.
- AG-G5-02: no tracked Phase 1.5 test list.
- AG-G5-03: tests were not run.
- The result of a clean-tree build of the agreed commit.
- Whether the NU1903 warning is itself a mandatory failure.
- The separate texts of Step 023, Step 024, Step 025, and Step 026. Those filenames were not located.
- Whether line 231 should be lifted. That is an open decision, not a fact this brief can convert from UNKNOWN into DECIDED. The sentence itself is VERIFIED as present.

### 4. Which minimum decisions must the Architect close before an implementation step?

1. Approve this checklist or a named revision (AG-G6-03). Without that, there is no accepted definition of Phase 1.5 PASS.
2. Disposition of Register line 231 (AG-G2-03, AG-G4-03).
3. Whether a new `series_build` tenant-attribution migration is in scope, as a new tracked file, not a reconstruction of 376 (AG-G3-02, AG-G3-01).
4. Where enforcement would live if an API change is later authorized, or whether the six attributes are removed (AG-G4-01). ADR-03 does not choose filter, service, or repository.
5. The tracked test list, including the tenant-isolation checks (AG-G5-02, AG-G4-02).

These five match the open items Step 027 already said only the Architect can close, plus approval of this definition. They do not, by themselves, clear the physical-consolidation rows. Before Phase 1.5 can be considered for PASS, the Architect also needs to close: publish or other source (AG-G1-02), the required file set (AG-G1-04), C-01–C-04 (AG-G2-04), reset-order in or out of scope (AG-G2-05), manifest ownership (AG-G3-03), the post-367 migration set without editing 386 (AG-G3-04), and the TestPack / solution decision (AG-G5-04).

### 5. When is a move to Phase 2 allowed?

Only after all of the following are true:

1. The Architect has approved this gate, or a named revision, in writing.
2. Every mandatory row is PASS against that approved text, on the agreed commit.
3. No mandatory row remains FAIL, BLOCKED, or UNKNOWN.
4. The written acceptance cites that commit. It does not cite stash or untracked files as implementation proof.
5. A separate approval still exists before any deploy, migration apply, or production database access. Passing the gate does not grant those.

Until then, stay in Phase 1.5. Phase 2 is not permitted. This step does not start it.

## Stop

Phase 1.5 remains **NOT PASS**. No next step was executed.
