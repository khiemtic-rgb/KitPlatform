# STEP 029 — Architect Disposition Register

Status: **DECISION PREPARATION ONLY.** Cursor proposes options. Cursor does not close them.  
Phase 1.5: **NOT PASS.**  
Baseline HEAD: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate. Phase 2 is not opened.

Acceptance criteria for this workflow: `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md`, approved by the Architect as the proposed acceptance criteria. That approval does not mark any gate row PASS. It does not promote Step 028 under `GOVERNANCE.md` §8. It does not authorize implementation, push, schema, or an API change.

This file is the only Step 029 artifact. No product file, migration, manifest, ADR, Decision Register, Master Architecture, or governance file was edited. No commit, push, merge, reset, clean, deploy, or production database access was performed. No test was run. No stash object was read.

Commercial priority stays the KIT product. Closing an architecture row is a means to unblock that work. Completing architecture documents is not the goal of this step.

## Labels

| Label | Meaning in this register |
|---|---|
| DECIDED | A tracked authority document states a closed decision. This register does not reopen it. |
| VERIFIED | Step 027 recorded the fact from git history or a tracked file. This step did not re-measure it. |
| RECORDED | The 2026-10-11 Phase 1.5 report, or Step 028 citing that report, states the fact. The report is Level 4 and is not in HEAD. This step did not re-measure it. |
| UNVERIFIED | A tracked sentence claims a fact, and the cited evidence is not in repository history. |
| REQUIRES APPROVAL | Only the Architect can close this. A Cursor proposal is not that closure. |
| UNKNOWN | No source among the three evidence documents answers the question. |

Level 4 and Level 5 documents do not become architecture authority by being cited here.

## Evidence boundary

| Source | Role here | Limit |
|---|---|---|
| `STEP-027-AUTHORITY-GAP-CLOSURE-DECISION.md` | Authority-gap facts: line 231, migration 315, migration 376 absence, six-filter gap, ADR-03 Option 1. Inspection context recorded as `E:/KitPlatform-wt-step024`. | Not re-run. Stash `c170af39` was not read there and is not read here. |
| `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md` | Proposed gate rows AG-G1 through AG-G6 and blockers B-01 through B-11. | Rows stay at the status Step 028 recorded. This register does not mark any row PASS. |
| `docs/architecture/KIT-PLATFORM-PHASE-1.5-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md` | 2026-10-11, verification ID `KPA-PHASE-1.5`, verdict NOT PASS. Git, file, manifest, build, and test observations. | Level 4. Not in HEAD. `docs/architecture/*` leaves it untracked. Used as a record of gaps, not as proof of implementation. |

This step did not re-run `git`, `dotnet build`, or `dotnet test`. Where a per-path check was recorded for only one file, this register does not generalize that check into a new measurement.

Tracked blobs cited by Step 027, at the baseline HEAD unless noted:

| Path | Blob | Commit on that path |
|---|---|---|
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | `e199ed02a4b119686a7a3f8f934e49dbba1502d4` | `d54652d4` |
| `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` | `9065fc9a264f592a19759a6d0d8ef52d95321756` | `d54652d4` |
| `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` | `128a7996fb2007bd2a595af3f15391b25087450f` | `d54652d4` |
| `deploy/ubuntu/migration-files.content.txt` | `10053f6ae9043c955f0fa208a9d499e78482df65` | `ceb521ff` |
| `migrations/315_pack_content_series_build.sql` | `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d` | tracked definition of `pack_content.series_build` |

## 1. Decision summary

Every row below is open unless the status column says DECIDED. A proposal column is Cursor’s option list. It is not an Architect disposition.

| ID | Decision the Architect still closes | Status now | What is already closed | Cursor proposal (not a decision) | If left open |
|---|---|---|---|---|---|
| D-01 | Which commit is the Phase 1.5 source of truth | REQUIRES APPROVAL | Identity of local HEAD is RECORDED. Publish of the three local commits is not approved. | Accept `d54652d4` in writing as the source, or name another commit. Publish stays a separate later approval. | AG-G1-02 stays BLOCKED. No later row can be checked “on the agreed commit”. |
| D-02 | Which Level 1–3 files the gate requires, on that source | REQUIRES APPROVAL | Rank in `GOVERNANCE.md` §3 is the tracked rank Step 028 cites. Files exist on local HEAD (RECORDED). The set is absent from `origin/main` as stated in the Phase 1.5 report §6 (RECORDED). | Use the six authority paths already named in that report. Handle C-01 and C-02 by disposition text only. | AG-G1-04 and AG-G2-01 stay BLOCKED. |
| D-03 | Disposition of C-01, C-02, C-03, C-04 | REQUIRES APPROVAL | Conflicts are RECORDED. None is resolved. | Four written dispositions. No file edit. No new Level 1. | AG-G2-04 stays BLOCKED. Two SSOT claims remain in force as text. |
| D-04 | Whether Register line 231 still binds | REQUIRES APPROVAL | ADR-03 Option 1 is DECIDED: tenant-only. Line 231 text is VERIFIED. Stale-wording premise is UNVERIFIED. | Keep line 231 in force until a written lift that does not cite missing forensics. Lifting is not a grant to edit the API. | No TenantId, WorkspaceId, RLS, or API change is authorized. |
| D-05 | Whether `series_build` tenant attribution is in Phase 1.5 scope | REQUIRES APPROVAL | Migration 315 shape is VERIFIED: no tenant column. Migration 376 implementation claim is UNVERIFIED. | Choose in-scope (new migration, separate write approval) or out-of-scope (no migration). | AG-G3-02 stays BLOCKED. No column is written. |
| D-06 | Where the six `ProductionTenantActionFilter` attributes are enforced, or whether they are removed | REQUIRES APPROVAL | Gap is VERIFIED: six attributes, no tracked implementing type. Location is not chosen in ADR-03. | Architect names filter, service, repository, or written removal. | AG-G4-01 stays FAIL. Removal without that writing is not a PASS. |
| D-07 | Exclusive manifest ownership, and the fate of content migrations after 367 | REQUIRES APPROVAL | Inventory and overlaps are RECORDED. Migration 386 is protected from edits that exist only to pass a checklist. | Ownership table for the recorded collisions. Commit-or-exclude decision for post-367 files as they already exist. | AG-G3-03 and AG-G3-04 stay FAIL. |
| D-08 | Minimum Phase 1.5 tests, TestPack reference, and `KitPlatform.slnx` membership | REQUIRES APPROVAL | Test execution is UNKNOWN. Dirty-tree build is RECORDED and does not meet AG-G5-01. | A minimum check list, not run here. Isolate tests that need no production database from tests that need their own approval. | AG-G5-02, AG-G5-03, AG-G4-02 stay UNKNOWN. AG-G5-04 stays FAIL. |
| D-09 | `RESET EXECUTION ORDER V1` in scope or out of scope | REQUIRES APPROVAL | Path and text are UNKNOWN. | Architect supplies a verifiable path, or records that the document is out of Phase 1.5 scope. | AG-G2-05 stays UNKNOWN. |

## 2. D-01 — Git source of truth

**Question.** Which commit is accepted as the Phase 1.5 source of truth?

**RECORDED identity.** Phase 1.5 report §4, repository row. Toplevel `E:/KitPlatform`. Branch `main`. HEAD `d54652d4a7734b3116db62bd356a831f9942fc73`. Remote `origin` `https://github.com/khiemtic-rgb/KitPlatform.git`. Local `main` is three commits ahead of `origin/main` `73d3c4a6c509ab25229d07a475229a6c8c1cf79d`.

**RECORDED unpublished commits.** The three commits are `c447814d`, `98f0632c`, and `d54652d4`. Step 028 AG-G1-02: they are not recorded as verified safe to publish. `git cat-file -e origin/main:docs/architecture/INDEX.md` fails. A clone of `origin/main` does not contain the architecture commit. This register does not approve a push.

**UNKNOWN in these sources.** The subject and body of `c447814d`, `98f0632c`, and `d54652d4` are not quoted in Step 027, Step 028, or the Phase 1.5 report. This register does not invent them.

**RECORDED dirty tree, separate from the three commits.** Phase 1.5 report §4 Git row: 282 untracked, 166 modified, 2 staged, porcelain count 450. Staged names recorded: `KIT-PLATFORM-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md` and `KIT-PLATFORM-NOVIXA-PRESCRIPTION-ASSIST-DATA-EVIDENCE-V1.md`. The full path recorded for the first staged file is `docs/architecture/KIT-PLATFORM-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md`. The directory of the second staged file is not stated beyond that basename. Step 027 and Step 028 were added after that porcelain count and were not inventoried in it.

### Evidence the Architect reviews before choosing

| Fact | Class | Source |
|---|---|---|
| Three local commits are not on `origin/main` | RECORDED | Phase 1.5 §4 and §6; Step 028 B-01 |
| Those commits are not verified safe to publish | RECORDED | Step 028 AG-G1-02 |
| Level 1–3 files are on local HEAD and absent from `origin/main` | RECORDED | Phase 1.5 §6. Explicit `cat-file` failure is recorded for `docs/architecture/INDEX.md` only. This step did not re-check the other paths on `origin/main`. |
| Working tree is not the HEAD commit | RECORDED | Phase 1.5 §4 Git row; Step 028 AG-G1-03 FAIL |
| Publish was not performed | RECORDED | Phase 1.5 §7; Step 028 stop |

### Risks

- Treating `origin/main` `73d3c4a6` as the source leaves the architecture files the gate cites off that source, on the evidence the Phase 1.5 report recorded.
- Treating local HEAD as the source while the tree is dirty lets untracked migrations and the uncommitted TestPack reference look like the commit.
- Pushing the three commits without a written acceptance publishes history the existing reports do not call verified safe.
- A later clean checkout of a different commit cannot reproduce a PASS that depended on this working tree (Step 028 AG-G6-01).

### Options (proposal only)

| Option | What the Architect would write | Consequence |
|---|---|---|
| D-01-A | `d54652d4`, including ancestors `c447814d` and `98f0632c`, is the Phase 1.5 source. Publish is not included in that sentence. | AG-G1-02 can be re-checked later. AG-G1-03 stays FAIL until the cited paths are clean on that commit. A push still needs its own approval. |
| D-01-B | A different commit is the source. The writing names the full hash. | Every later check uses that hash. If the hash is `73d3c4a6`, the file-set row starts from the recorded absence on `origin/main`. |
| D-01-C | No source is named yet. | Implementation stays unauthorized. Phase 1.5 stays NOT PASS. |

Cursor does not choose D-01-A, D-01-B, or D-01-C. This step does not push.

## 3. D-02 — Architecture authority file set

**Question.** Which files are the mandatory Level 1–3 set for Phase 1.5, and which of them are on local HEAD versus `origin/main`?

**Rank already stated, not changed here.** Step 028 AG-G2-01, citing `GOVERNANCE.md` §3: Level 1 is `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`. Level 2 is the Decision Register. Level 3 is the ADR decision files. INDEX is navigation, not a rank above Level 1. A rank change would be BREAKING under `GOVERNANCE.md` §5. Step 028 does not pick a winner between C-01 and C-02.

### Files the Phase 1.5 report places on local HEAD

RECORDED, report §4 Files row. This table does not promote any of them.

| Path | Role in the recorded hierarchy | On local HEAD | On `origin/main` |
|---|---|---|---|
| `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` | Level 1 | RECORDED yes | RECORDED absent as part of the Level 1–3 set in report §6. Per-path `cat-file` on `origin/main` was not recorded for this path. |
| `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` | Level 2 | RECORDED yes | Same limit as the row above. |
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | Level 3 | RECORDED yes. Blob at HEAD is VERIFIED via Step 027. | Same limit. |
| `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md` | Level 3, named by the Phase 1.5 report §2 | RECORDED yes | Same limit. ADR-04’s decision text is not summarized in the three sources. Content: UNKNOWN in this register. |
| `docs/architecture/INDEX.md` | Navigation. Not an authority rank. | RECORDED yes | RECORDED no. `git cat-file -e origin/main:docs/architecture/INDEX.md` fails. |
| `docs/architecture/GOVERNANCE.md` | Authority-level rules cited by the gate. Not a rank above Level 1. | RECORDED yes. Step 028: absent from `origin/main` per the Phase 1.5 report. | RECORDED absent with the Level 1–3 set. Per-path `cat-file` on `origin/main` was not recorded for this path. |

Also recorded on local HEAD, and not part of the authority rank: `KitPlatform.slnx`, `.gitignore`.

### Files recorded outside HEAD

RECORDED, Phase 1.5 §4 Files row. Present on disk and ignored (`docs/architecture/*` or `docs/*`): completion gate, control-plane review, documentation state, consolidation matrix, knowledge-routing model, pack/module registry, decision-sync report, post-pack audit. Exact filenames of that ignored set are not listed in the three sources, except where a later sentence names one. Exact paths: UNKNOWN in this register beyond those descriptions.

Staged, not in HEAD: `docs/architecture/KIT-PLATFORM-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md`.

The Phase 1.5 report itself is a new untracked Level 4 file under `docs/architecture/`.

Step 027 recorded no commit reachable from HEAD for:

- `docs/forensics/KIT-PLATFORM-OPEN-AUTHORITY-DECISION-RECORD-01.md`
- `docs/forensics/PRODUCTION-OS-V1-CLOSEOUT-REVIEW-01.md`
- `docs/forensics/KIT-PLATFORM-ARCHITECTURE-DECISION-SYNC-01-REPORT.md`

### Proposal for the required set (not a promotion)

Cursor proposes that the mandatory set, if the Architect accepts it, is the four Level 1–3 decision files plus the two navigation files the gate already cites:

1. `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`
2. `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`
3. `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md`
4. `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md`
5. `docs/architecture/GOVERNANCE.md`
6. `docs/architecture/INDEX.md` as navigation only

Gitignored reports, the staged 2026-10-08 baseline, and this register stay out of that set until the Architect names them. Naming them still requires a separate promotion under `GOVERNANCE.md` §8. This step does not add a gitignore exception, does not commit, and does not edit INDEX.

### Proposal for C-01 and C-02 (disposition text only)

These proposals do not change rank.

- **C-01.** The Architect writes that the task list (INDEX, then GOVERNANCE, ahead of the Master Architecture) is a reading order. Level 1 stays the Master Architecture. No edit to INDEX, GOVERNANCE, or the Master Architecture.
- **C-02.** The Architect writes that Level 1 stays `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`. The sentence in `docs/00-control/KIT-WORK-INDEX.md` §6.1 that names `docs/novixa/03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md` as Primary SSOT does not install a second Level 1. No edit to the work index, the blueprint, or the Master Architecture. If the Architect instead wants the blueprint to become Level 1, that writing is a BREAKING change under `GOVERNANCE.md` §5 and is a different decision from this proposal.

**Consequence if D-02 is not closed.** AG-G1-04 stays BLOCKED. AG-G2-01 stays BLOCKED. Implementation cannot treat a gitignored copy as the required file.

## 4. D-03 — Evidence conflicts C-01–C-04

None of these four is DECIDED. Step 028 AG-G2-04: each needs an Architect disposition that names the conflict ID. The 2026-10-08 physical-consolidation conclusion is not accepted as Phase 1.5 PASS unless a new check replaces it. That sentence is a gate condition. It is not itself the disposition.

### C-01

| Field | Content |
|---|---|
| Conflict | The task lists INDEX, then GOVERNANCE, ahead of the Master Architecture. INDEX §2 states that INDEX is not an authority level. GOVERNANCE §3 assigns Level 1 to the Master Architecture. |
| Evidence | RECORDED. Phase 1.5 report §3. Step 028 did not re-open the files to re-audit them. |
| DECIDED | The tracked rank Step 028 cites: Level 1 is the Master Architecture. INDEX is not a rank above Level 1. This register does not add a new decision. |
| REQUIRES APPROVAL | Confirm the task list is a reading order. A rank change would be BREAKING under GOVERNANCE §5. |
| Proposal | Disposition text only, as in D-02. No file edit. |
| If undecided | Readers can treat a reading list as a new rank. AG-G2-01 and AG-G2-04 stay BLOCKED. |
| Risk | A silent rank change would move commercial implementation onto a document the governance text does not place at Level 1. |

### C-02

| Field | Content |
|---|---|
| Conflict | `docs/00-control/KIT-WORK-INDEX.md` §6.1 says it is not architecture authority, and also names `docs/novixa/03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md` as Primary SSOT. Level 1 in GOVERNANCE §3 remains the Master Architecture. |
| Evidence | RECORDED. Phase 1.5 report §3 and §6. |
| DECIDED | No disposition chooses between the two “primary” sentences. The GOVERNANCE rank is the rank Step 028 refuses to change silently. |
| REQUIRES APPROVAL | Which document is the primary architecture source of truth, without installing a new Level 1 inside implementation. |
| Proposal | Keep the Master Architecture as Level 1 by written disposition. Do not edit either document in this phase. A BREAKING replacement of Level 1 is a separate Architect decision, not this proposal. |
| If undecided | Two primary-source sentences remain. Implementation has no authority to pick one. |
| Risk | Product work could follow the blueprint while architecture review follows the Master Architecture, and the two reviews would not be checking the same source. |

### C-03

| Field | Content |
|---|---|
| Conflict | Documentation State V1 says every architecture document is gitignored and that Index creation is blocked. Local HEAD contains INDEX, GOVERNANCE, the Master Architecture, the Decision Register, ADR-03, and ADR-04. |
| Evidence | RECORDED. Phase 1.5 report §3 and §5: Documentation State V1’s gitignore description is stale relative to that report’s file check. The exact path of “Documentation State V1” is not given in the three sources. Path: UNKNOWN in this register. |
| DECIDED | Nothing in the three sources withdraws or supersedes Documentation State V1. |
| REQUIRES APPROVAL | Treat that document as stale evidence, or supersede it explicitly. |
| Proposal | A written note that the gitignore-and-index-blocked claims in Documentation State V1 are stale against local HEAD as recorded on 2026-10-11. Do not delete or rewrite the document in this step. Do not treat local HEAD as published: D-01 is still open, and `origin/main` still lacks INDEX on the recorded check. |
| If undecided | A later step can block index work that local HEAD already contains, or can treat HEAD files as published when `origin/main` does not contain them. |
| Risk | Stale evidence used as a current gate, or fresh local files used as if they were on the remote. |

### C-04

| Field | Content |
|---|---|
| Conflict | Physical Consolidation Verification V1 §6.3 and §7.3–§7.4 says architecture assets are tracked and that migration IDs are unique. The 2026-10-11 git check and migration inventory contradict both claims. |
| Evidence | RECORDED. Phase 1.5 report §3, §4, §5. The 2026-10-08 baseline path recorded as staged and not in HEAD: `docs/architecture/KIT-PLATFORM-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md`. Completion-gate `architect_acceptance` is recorded empty. Duplicate-prefix count 23 is RECORDED; the full list of 23 prefixes is not enumerated except the `294` example. Full list: UNKNOWN in this register. |
| DECIDED | No Architect disposition accepts or replaces that 2026-10-08 conclusion. The Phase 1.5 report’s own verdict is NOT PASS. |
| REQUIRES APPROVAL | A written C-04 disposition. The approved gate says the 2026-10-08 conclusion is not Phase 1.5 PASS unless a new check replaces it. |
| Proposal | Write that the 2026-10-08 tracking claim and the unique-migration-ID claim are not Phase 1.5 PASS. The replacement check is the Step 028 gate, still not PASS. Do not edit the 2026-10-08 report to make its sentences match the later inventory. |
| If undecided | The older “conformant consolidation” conclusion can be reused as a PASS. Step 028 AG-G2-04 forbids that reuse. |
| Risk | A commercial milestone could be reported on an inventory the 2026-10-11 report already contradicted. |

## 5. D-04 — Tenant/API authorization

Two sentences stay separate. Option 1 is closed. Line 231 is not closed.

### DECIDED — ADR-03 Option 1

Step 027 and Step 028: ADR-03 Option 1 remains tenant-only Production scope. Workspace is not mandatory. GATE A (ADR-03 lines 118–127) still states the requirement and still says not to design the mechanism in that record. The Phase 1.5 report §2: DECIDED is not IMPLEMENTED. Master Architecture §18 still records Production OS tenant isolation as absent. This register does not design `ProductionTenantActionFilter`.

### Register line 231, stated alone

**VERIFIED** text. Decision Register line 231, blob `9065fc9a264f592a19759a6d0d8ef52d95321756`:

> Production is **not** tenant-safe today. GATE A is not executed. No TenantId/WorkspaceId/RLS/API change is authorized by this record.

**UNVERIFIED premise of the later “stale” paragraphs.** Register lines 604, 630, 677, and 693, and ADR-03 line 156, say the “GATE A not executed” wording is stale because Migration 376 and fail-closed writes exist. Those sentences cite files that are not in history reachable from HEAD. The paragraphs do not, by themselves, authorize an API change. Step 027: this brief does not lift line 231.

**REQUIRES APPROVAL.** Whether line 231 still binds.

### Options (proposal only)

None of these options grants permission to edit the API. A lift of line 231, if the Architect writes one, still leaves D-05 and D-06 for separate writing.

| Option | Architect writing | What it would authorize | Consequence |
|---|---|---|---|
| D-04-A | Line 231 still binds. | No TenantId, WorkspaceId, RLS, or API change. | Schema work in D-05 and filter work in D-06 stay unauthorized. Phase 1.5 can still close evidence and ownership rows that do not change the API. |
| D-04-B | Line 231 is lifted by a new decision. The writing does not cite the missing forensic files or stash `c170af39` as proof. | The lift itself. Not a location for enforcement. Not a column definition. | D-06 and D-05 still need their own approvals before any code or migration. |
| D-04-C | Line 231 stays as the safety status, and a bounded change is named in the same writing. | Only the change that writing names. | Anything unnamed stays unauthorized. Cursor does not draft the bound. |

Cursor does not choose D-04-A, D-04-B, or D-04-C. Until one of them exists in the Architect’s writing, line 231 remains the binding sentence for this gate (Step 028 AG-G2-03, AG-G4-03).

**Risk of choosing nothing.** Implementation that adds tenant columns or a filter would contradict a VERIFIED sentence. Implementation that assumes the stale paragraphs already lifted that sentence would rely on an UNVERIFIED premise.

**Risk inside D-04-B and D-04-C.** A lift can be misread as permission to design the filter. GATE A still says the mechanism is not designed in ADR-03. D-06 stays a separate decision.

## 6. D-05 — `series_build` schema scope

**VERIFIED shape.** `migrations/315_pack_content_series_build.sql`, blob `4107555bf34c9111da89e2f5b6bb0c15f0f95e7d`, creates `pack_content.series_build` with `id`, `series_code`, `episode_code`, `title`, `status`, counts, `graph_json`, and timestamps. No tenant attribution column. Listed at line 24 of `deploy/ubuntu/migration-files.content.txt`. That manifest does not list a 376 file. No tracked migration runs `ALTER TABLE pack_content.series_build`.

Step 027: `migrations/333_pack_content_kit_video_engine.sql` adds `video_production.series_build_id` and says not to overwrite `series_build`. `migrations/383_pack_content_article_series.sql` says it does not touch `series_build`. `migrations/286_pack_content_marketing_park_tenant.sql` does not mention `series_build`.

**VERIFIED absence.** `migrations/376_pack_content_production_tenant_boundary.sql` has no commit reachable from HEAD. The path appears only on `refs/stash` and `stash^3` (`c170af39`), which are not ancestors of HEAD. That object was not read.

**UNVERIFIED.** ADR-03 line 156, Register line 164, and Master Architecture line 433 claim Migration 376 already added nullable `tenant_id`.

**Tenant-attribution gap.** The tracked table has no tenant column. Who owns existing rows, and whether a future column is nullable, backfilled, or left null, is not decided by migration 315. Backfill is a separate decision. This register does not decide backfill.

**REQUIRES APPROVAL.** No tracked document located by Step 027 authorizes writing the column now. Register line 231 does not authorize a TenantId change.

### Options (proposal only)

| Option | Architect writing | Later implementation | Consequence |
|---|---|---|---|
| D-05-A | In scope for Phase 1.5. | Still blocked until a second approval to write one new tracked migration and one manifest entry. The new file is not a reconstruction of 376. The column definition is the one that second writing names. Historical backfill is not included. | AG-G3-02 can move only after both writings and the files exist on the agreed commit. D-04 must allow a TenantId change before that write. |
| D-05-B | Out of scope for Phase 1.5. | No migration is added. The missing column stays recorded. | The gate row AG-G3-02 needs the Architect’s out-of-scope sentence so the row is not an open schema task. Cursor does not delete the row. |

Cursor does not choose D-05-A or D-05-B. This step does not restore or recreate migration 376.

**If undecided.** No column is added. Claims that 376 already added `tenant_id` stay UNVERIFIED. Commercial features that assume `series_build` is tenant-attributed have no tracked schema for that assumption.

**Risk of D-05-A without the second approval.** A migration written in the same breath as the scope decision skips the separate authorization Step 027 required.

**Risk of D-05-B.** Phase 1.5 could close while `series_build` remains without tenant attribution. That is acceptable only if the Architect’s out-of-scope sentence says so. It does not make production tenant-safe.

## 7. D-06 — API enforcement disposition

**VERIFIED gap.** Step 027: six `ProductionTenantActionFilter` attributes exist on `ContentController.cs`. No tracked type implements them. ADR-03 does not choose filter, service, or repository. Line 231 has not been lifted.

**UNKNOWN in these sources.** The three evidence documents do not name the six controller actions. This register does not re-open `ContentController.cs`. Individual action names: UNKNOWN here. The count six is the verified fact.

**Phase 1.5 report.** It does not re-state the six attributes. The filter gap is carried from Step 027, not from a new 2026-10-11 code audit.

### Options (proposal only)

Cursor does not choose the enforcement site and does not edit code.

| Option | What would be true afterward | Evidence still required | Risk |
|---|---|---|---|
| D-06-A Filter | A tracked type implements the six attributes, at the project and type the Architect names. | D-04 allows the API change. The type is on the agreed commit. AG-G4-02 names the tenant-refusal checks. The six action names are identified in that writing, because they are UNKNOWN here. | An API change while line 231 still binds. A filter that does not cover every attribute. |
| D-06-B Service | The Architect names the service methods that enforce tenant scope, and states what happens to the six attributes. | Same authorization evidence. Tests cover every surface the Architect names. | A service check missed by a controller path. Attributes left in place with no type still fail AG-G4-01 unless the disposition also removes them. |
| D-06-C Repository | The Architect names the repository boundary that enforces tenant scope, and states what happens to the six attributes. | Same authorization evidence. Tests show a caller cannot cross tenants through the API, not only through a repository unit call. | The API accepts a call the repository later rejects, or a path that never reaches that repository. |
| D-06-D Removal | The Architect writes that the six attributes are removed. | The writing is explicit. Removal is not a compile shortcut. If tenant isolation stays in Phase 1.5 scope, AG-G4-02 still needs a named check. | The attributes disappear and the VERIFIED gap is replaced by no check at all. Master Architecture §18 still records isolation as absent. |

**If undecided.** AG-G4-01 stays FAIL. The six attributes remain without an implementing type. No agent removes them to make a build cleaner.

**Dependency.** D-06-A, D-06-B, and D-06-C wait on D-04. D-06-D is also an API-surface change and waits on a written decision even if the Architect’s reading of line 231 is that adding a filter is forbidden. Step 028: removal without that decision is not a PASS.

## 8. D-07 — Migration ownership and post-367 files

**RECORDED inventory.** Phase 1.5 report §4 Migrations row. Not re-counted in Step 028 or here.

| Fact | Record |
|---|---|
| Directory | Single `migrations/` directory. Streams were not applied. |
| Counts | 407 `.sql` files. Prefixes 0–386. 379 unique numbers. 23 duplicated prefixes. Gaps 202–209. Unnumbered `192a_pack_family_os_vertical_owner.sql`. |
| Duplicate example in HEAD | `294_pack_content_gemini_36.sql`, `294_pack_local_os_v1.sql`, `294_pharmacy_consultation_mvp1.sql`. The other 22 duplicated prefixes are not listed. |
| Manifest overlap | `228_kit_accounts_memberships.sql`, `229_kit_accounts_prefer_pharmacy.sql`, `230_pack_family_parent_emotion_push.sql`, `231_pack_family_memory.sql` appear in both `deploy/ubuntu/migration-files.prod.txt` and `deploy/ubuntu/migration-files.family-os.txt`. |
| Content manifest at HEAD | `migration-files.content.txt` ends at `367_pack_content_character_reference_pack_contract.sql` (58 entries). Blob cited by Step 027: `10053f6ae9043c955f0fa208a9d499e78482df65` at `ceb521ff`. |
| Dirty content manifest | Lists through `386_pack_content_famixa_canon_administer.sql` and skips 381 and 385. |
| Untracked, unmodified by that task | `migrations/368`–`382` content/test files (individual names not listed), `385_pack_personal_intelligence_trading_ledger_v1.sql`, `386_pack_content_famixa_canon_administer.sql`. |
| Untracked manifests | `migration-files.personal-intelligence.txt`, `migration-files.test-pack.txt`. |
| Other dirty SQL | `migrations/281_pack_content_wave0.sql` had an unstaged one-line change. The line text is not quoted. |
| Resolution | Every manifest filename resolved to a file on disk. |

### Decisions required

1. One manifest owner for each recorded colliding filename. The gate names the four overlaps 228–231. The 23 duplicate prefixes are a related ownership fact. Whether every duplicate prefix needs an owner in Phase 1.5, or only the four manifest overlaps, is itself part of the Architect’s table. Cursor does not merge manifests.
2. Whether content migrations after 367 enter the agreed commit as they already exist, or are excluded by decision. Individual names inside 368–382 are UNKNOWN in this register until the Architect names the set or points at a listing.
3. Fate of untracked manifests `migration-files.personal-intelligence.txt` and `migration-files.test-pack.txt`: include or exclude. Not decided here.
4. Fate of the unstaged one-line change in `migrations/281_pack_content_wave0.sql`: keep or drop, in a later approved change. Not decided here.

### Migration 386

`migrations/386_pack_content_famixa_canon_administer.sql` is not edited to satisfy a checklist. Phase 1.5 §7: it was read only as a path and git status. Its contents were not edited. This step does not commit it and does not stage it.

**If undecided.** AG-G3-03 and AG-G3-04 stay FAIL. A dirty manifest that lists 386 is not a commit of 386. Duplicate prefix 294 and the four shared filenames stay without an exclusive owner.

**Risk.** Applying two manifests that both list 228–231 can run the same file under two ownership stories. Committing 386 with a content edit made only to pass Phase 1.5 would change a protected migration. Leaving 368–386 untracked means a clean clone of HEAD stops the content manifest at 367.

## 9. D-08 — Test and dependency gate

**UNKNOWN execution.** Step 027 ran no tests and found no tracked Phase 1.5 test list. No tracked file under `docs/` at that HEAD contained the text “Phase 1.5”. The Phase 1.5 report §4 Test row: tests were not executed. Compilation of test projects during build is not test execution. This step does not run tests.

**RECORDED build, not acceptance evidence.** Command `dotnet build e:\KitPlatform\KitPlatform.slnx`, 2026-10-11, exit 0, 0 errors, 63 warnings, 00:00:41.72. That compile included the uncommitted TestPack reference. It does not prove `origin/main` or a clean HEAD. AG-G5-01 status in Step 028: FAIL. The result of a future clean build is UNKNOWN.

**UNKNOWN.** Whether NU1903 for `Newtonsoft.Json` 10.0.3 is itself a mandatory failure. Step 028 does not add it as a separate mandatory row. Frontend lockfile version drift was not compared.

### Proposed minimum acceptance checks (not approved, not run)

Step 028 refused to invent a test list because none exists in tracked sources. Step 029 is asked for a proposal. The list below is that proposal. It becomes the Phase 1.5 list only if the Architect accepts it in writing. Concrete test method names are not invented. The six controller actions are UNKNOWN here, so the tenant check is stated as a class of check.

| Proposed check | What it shows | Isolation | Approval |
|---|---|---|---|
| P-1 Clean build | `dotnet build` of the Architect-named solution file, on a clean checkout of the D-01 commit, exits 0. | Can run without a production database, deploy, or paid provider. | The build command is the one the Architect names. The dirty-tree result above is not P-1. |
| P-2 Tenant refusal | For each surface the Architect names, a caller of another tenant is refused. A compile of test projects is not this result. | Can run in isolation only if the Architect’s command does not open a database. | Separate approval of the command before any run. Production database, deploy, and paid providers stay out. |
| P-3 Reproducibility | A second reader, using only the agreed commit and the named commands, reaches the same PASS or FAIL without this working tree, stash, or gitignored copies. | Read of the agreed commit. | None beyond D-01 and the named commands. Copying the working tree into the checkout is not a pass. |

P-2 cannot be executed until D-04 and D-06 say which surfaces exist. If D-06-D removes the attributes and the Architect also puts tenant-isolation behavior out of scope, the Architect’s writing says so. Cursor does not drop P-2.

### TestPack, `KitPlatform.slnx`, and project membership

RECORDED, Phase 1.5 §4 Dependencies and §6.

| Fact | Record |
|---|---|
| Solution | `KitPlatform.slnx` is clean and lists 17 `.csproj` entries. |
| Disk | 30 `.csproj` files under `src/` and `tests/`. The report §6 says the solution omits 13 projects. The 13 names are not listed. Names: UNKNOWN in this register. |
| TestPack | HEAD `KitPlatform.Api.csproj` does not reference TestPack. The working tree adds a one-line `ProjectReference` to `src/Packs/TestPack/KitPlatform.Packs.TestPack.Infrastructure/KitPlatform.Packs.TestPack.Infrastructure.csproj`. |
| Package source files | No `Directory.Packages.props`, `nuget.config`, or `global.json`. `package-lock.json` is tracked at the repo root. There is no root `package.json`. |

### Options (proposal only)

| Option | Architect writing | Consequence |
|---|---|---|
| D-08-A | `KitPlatform.slnx` plus HEAD project references are the dependency source of truth. The TestPack reference is reverted. The 13 omitted projects are explicitly deferred. | The uncommitted reference cannot be used as build evidence. AG-G5-04 still needs that writing and a clean diff on the agreed commit. |
| D-08-B | The TestPack reference is committed as its own approved change. Solution membership of the other projects is a separate sentence, either decided or deferred. | One-line reference change is not mixed into an unrelated commit. The dirty-tree build remains insufficient for AG-G5-01. |
| D-08-C | The Architect names additional projects that must join `KitPlatform.slnx`. | That membership change is a later approved edit. It is not done in this step. |

**If undecided.** AG-G5-04 stays FAIL. AG-G5-02 and AG-G5-03 stay UNKNOWN. A green dirty build cannot unblock schema or API rows (Step 028 acceptance rule 3).

## 10. D-09 — Reset execution order

**UNKNOWN.** Phase 1.5 report §3: `RESET EXECUTION ORDER V1` was not found under `docs/`, `docs/architecture/`, `docs/forensics/`, or `docs/00-control/`. It was not altered. Its text is UNKNOWN until the Architect supplies the path. Step 028 AG-G2-05 status: UNKNOWN.

This register does not rewrite the document and does not guess its contents.

### Options (proposal only)

| Option | Architect writing | Consequence |
|---|---|---|
| D-09-A | A path that exists on the D-01 commit, so the gate can cite that text. | AG-G2-05 can be checked by reading that path. Inventing missing paragraphs is not the check. |
| D-09-B | The document is out of scope for Phase 1.5. | The gate no longer waits on a file the three sources could not find. Other blockers stay open. |

**If undecided.** AG-G2-05 stays UNKNOWN. An UNKNOWN mandatory row blocks Phase 1.5 PASS (Step 028 acceptance rule 2).

## 11. Dependency order

A later row can be discussed before an earlier row is closed. Implementation of a later row waits on the earlier row where the table says so.

| Order | Decision | Waits on | Can proceed in parallel with |
|---|---|---|---|
| 1 | D-01 source commit | Nothing in this register | D-09 option writing, because a missing-document decision does not need the commit hash |
| 2 | D-02 file set | D-01, because “on the agreed source” is meaningless before the source exists | Drafting C-01 and C-02 text |
| 3 | D-03 C-01 and C-02 | Nothing, for the writing itself. Applying a new rank waits on a BREAKING decision this proposal does not make. | D-01 |
| 4 | D-03 C-03 and C-04 | Nothing, for the writing itself | D-01 |
| 5 | D-04 line 231 | Nothing. Option 1 is already DECIDED and is not in this queue. | D-01, D-03, D-09 |
| 6 | D-05 scope | D-04 before any migration is written. The scope sentence itself can be written while line 231 still binds; the write cannot. | D-07, except both must not be used as an excuse to recreate 376 |
| 7 | D-06 location or removal | D-04. Also D-01 before the type is required to exist on a commit. | D-07 |
| 8 | D-07 ownership and post-367 | D-01 before a commit of existing files. Independent of the filter location. | D-04, D-09 |
| 9 | D-08 test list and TestPack | D-01 for a clean build. P-2 waits on D-04 and D-06 for the surface list. | D-03, D-07, D-09 |
| 10 | D-09 path or out-of-scope | Nothing | All other decisions |

Commercial reading of this order: D-04, D-01, and D-03 C-02 are the decisions that tell implementation which source and which authorization sentence to obey. D-05-A and D-06-A add schema and API work. They are not the shortest path unless the Architect explicitly wants them in Phase 1.5. D-05-B and D-04-A leave that work out. Cursor still does not select those options.

## 12. Decisions the Architect closes before implementation

Implementation is not authorized when this list is only printed. Each item needs the Architect’s own writing. Approval of Step 028 as the criteria for this workflow is already recorded above. That approval is not item 1 being PASS, and it is not permission to start the list.

1. D-01. Name the source commit. The three local commits stay unpublished until a separate publish approval exists.
2. D-02. Name the required file set. Leave rank unchanged unless a BREAKING decision says otherwise.
3. D-03. Write four dispositions, one for each of C-01, C-02, C-03, and C-04.
4. D-04. State whether Register line 231 still binds. Do not treat missing forensic files as the proof.
5. D-05. State whether a new `series_build` tenant-attribution migration is in scope. If it is, a further approval is required before the file is written. Do not reconstruct 376. Do not decide backfill in the scope sentence.
6. D-06. Name filter, service, repository, or written removal of the six attributes. ADR-03 does not choose.
7. D-07. Assign exclusive ownership for the recorded manifest collisions. Decide how files after 367 enter git, without editing migration 386.
8. D-08. Accept or replace the proposed checks P-1, P-2, and P-3. Decide TestPack and solution membership. Tests stay unrun until that writing exists.
9. D-09. Supply a verifiable path for `RESET EXECUTION ORDER V1`, or declare it out of scope.

Step 028 §D.4 matches items 4, 5, 6, and 8, plus approval of the checklist. The task statement for this step records that checklist approval for this workflow only. Items 1, 2, 3, 7, and 9 are the physical-consolidation decisions Step 028 said are still required before Phase 1.5 can be considered for PASS.

## 13. Phase 1.5 blockers still open

No blocker below was closed by this register. Status values are the Step 028 evidence classes. Gate-row status is unchanged: no row is marked PASS.

| ID | Blocker | Class | Gate rows | Disposition |
|---|---|---|---|---|
| B-01 | `c447814d`, `98f0632c`, and `d54652d4` are not on `origin/main` and are not verified safe to publish. | RECORDED | AG-G1-02 | D-01 |
| B-02 | Working tree is dirty. Later step files were not in the porcelain count of 450. | RECORDED | AG-G1-03, AG-G6-01 | D-01, then a later approved commit this step does not grant |
| B-03 | Migration 376 and the cited forensic records are not in HEAD history. Stash is not a substitute. | VERIFIED absence; UNVERIFIED claim | AG-G2-02, AG-G3-01 | D-04, D-05. Do not restore 376 to clear this row. |
| B-04 | Migration 315 defines `pack_content.series_build` with no tenant attribution. | VERIFIED | AG-G3-02 | D-05 |
| B-05 | Six `ProductionTenantActionFilter` attributes have no tracked implementation. Action names are UNKNOWN in the three sources. | VERIFIED count; UNKNOWN names | AG-G4-01 | D-06 |
| B-06 | Register line 231 has not been lifted. Paragraphs that call it stale cite missing files. | VERIFIED text; UNVERIFIED stale-claim | AG-G2-03, AG-G4-03 | D-04 |
| B-07 | No tracked document defines the Phase 1.5 acceptance tests. | UNKNOWN | AG-G5-02, AG-G5-03, AG-G4-02 | D-08 |
| B-08 | 23 duplicate migration prefixes, and four filenames in both prod and family-os manifests, have no exclusive owner. | RECORDED | AG-G3-03 | D-07 |
| B-09 | Content migrations after 367, including 386, are untracked while a dirty manifest lists them. | RECORDED | AG-G3-04 | D-07. Migration 386 stays unedited. |
| B-10 | C-01–C-04 have no disposition. `RESET EXECUTION ORDER V1` has no located path. | RECORDED; path UNKNOWN | AG-G2-01, AG-G2-04, AG-G2-05 | D-03, D-09 |
| B-11 | TestPack reference and solution membership are not the clean HEAD dependency story. The only build is a dirty-tree build. | RECORDED | AG-G5-01, AG-G5-04 | D-08 |

Also still open, from Step 028 §D.3, and not converted into facts here:

- Result of a clean-tree build of the agreed commit: UNKNOWN.
- Whether NU1903 blocks acceptance: UNKNOWN.
- Separate texts of Step 023, Step 024, Step 025, and Step 026: UNKNOWN. Those filenames were not located.
- AG-G3-05: no schema change is authorized by a checklist row. Status remains BLOCKED.
- AG-G6-03: Step 028 is the proposed acceptance criteria for this workflow. That does not mark AG-G6-03 PASS and does not mark any other row PASS.

## 14. Stop

This step created only `STEP-029-ARCHITECT-DISPOSITION-REGISTER.md`.

No product file, migration, manifest, solution, ADR, Decision Register, Master Architecture, or governance file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

Phase 1.5 remains **NOT PASS**. Phase 2 is not opened. No implementation step follows this report.
