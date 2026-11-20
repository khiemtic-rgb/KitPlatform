# STEP 030 — Architect Disposition: D-01 and D-02

Status: **ARCHITECT DECISION RECORD.** D-01 and D-02 only.  
Phase 1.5: **NOT PASS.**  
Baseline for acceptance checks: `d54652d4a7734b3116db62bd356a831f9942fc73`  
Order kept: Phase 1 → Phase 1.5 → Phase 2 Gate. Phase 2 is not opened.

This file records two Architect decisions and the file evidence those decisions need. It does not close D-03 through D-09. It does not mark any Step 028 gate row PASS. It does not promote a document, change a rank, or authorize implementation.

No product file, migration, manifest, ADR, Decision Register, Master Architecture, or governance file was edited. No commit, push, merge, reset, clean, deploy, migration, or production database access was performed. No test or build was run. No stash object was read. No broad repository audit was run.

## Labels

| Label | Meaning in this record |
|---|---|
| DECIDED | The Architect’s writing in this step closes that item. |
| VERIFIED | A path check in this step, limited to the named authority files and the two git tips below. |
| RECORDED | Step 027, Step 028, or the 2026-10-11 Phase 1.5 report states the fact. This step did not re-count it. |
| UNKNOWN | No source used here answers the question. |

Level 4 and Level 5 documents do not become architecture authority by being cited here.

## Evidence boundary

| Source | Role here | Limit |
|---|---|---|
| Architect writing in the Step 030 task | Closes D-01 and D-02 only. | Does not close later decisions. |
| `STEP-027-AUTHORITY-GAP-CLOSURE-DECISION.md` | Blobs for the Master Architecture, the Decision Register, and ADR-03. | Not re-opened as an audit. |
| `STEP-028-PHASE-1.5-ACCEPTANCE-GATE-DEFINITION.md` | Gate rows and blockers B-01 through B-11. | No row is marked PASS. |
| `STEP-029-ARCHITECT-DISPOSITION-REGISTER.md` | Open decisions D-01 through D-09, and the limit that only `docs/architecture/INDEX.md` then had a recorded `origin/main` path check. | Proposals in that register are not decisions. |
| `docs/architecture/KIT-PLATFORM-PHASE-1.5-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md` | Porcelain, staged names, and the three-commit lead. Level 4. Not in HEAD. | Not re-measured. |
| This step, path checks only | `git rev-parse` of HEAD and of the local ref `origin/main`. `git ls-tree` of `docs/` for `KIT-PLATFORM-ADR-*-DECISION-V1.md`. `git cat-file -e` and `git rev-parse <tip>:<path>` for the paths in the tables below. | No fetch. No status recount. |

Local HEAD matches the baseline: `d54652d4a7734b3116db62bd356a831f9942fc73`.

The local remote-tracking ref `origin/main` is `73d3c4a6c509ab25229d07a475229a6c8c1cf79d`. That ref is an ancestor of the baseline. No fetch was performed. Whether the remote host has moved since this ref was last updated is **UNKNOWN**. Every `origin/main` cell below is a check of this local ref, not a live remote listing.

## 1. D-01 — Phase 1.5 source baseline

**DECIDED.**

`d54652d4a7734b3116db62bd356a831f9942fc73` is the baseline for Phase 1.5 acceptance checks.

This decision does NOT:

- authorize push or publication;
- approve the three local commits as safe to publish;
- treat the dirty working tree as part of the baseline;
- authorize commit, merge, reset, clean, or other Git mutations.

The three commits reachable from this baseline and not contained in local `origin/main` are:

| Commit | Subject recorded by `git log --oneline origin/main..HEAD` |
|---|---|
| `c447814d4c3056bb5affe3670cf8b858671686c5` | `refactor(core): move pharmacy workflow out of core` |
| `98f0632cab983750edb032eb365c91b15db5eb44` | `feat(content): consolidate editorial workflow from kit-mkt` |
| `d54652d4a7734b3116db62bd356a831f9942fc73` | `docs(architecture): establish canonical architecture authority V1` |

Recording those subjects does not approve them for publication.

## 2. D-02 — Architecture authority hierarchy

**DECIDED.** The existing hierarchy is preserved.

- Level 1: `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`
- Level 2: `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`
- Level 3: ADR decision files.
- `docs/architecture/INDEX.md` is navigation, not an authority rank.

This decision does not change authority rank and does not promote additional documents.

## 3. Required authority file set

The required set is the hierarchy in D-02, expanded to the ADR decision files that exist on the baseline. `git ls-tree` of `docs/` at the baseline returns exactly two ADR decision files. ADR-01, ADR-02, and ADR-05 through ADR-08 decision files are not in that tree. This step does not add them to the required set and does not create them.

### 3.1 Authority files

| Path | Rank | On baseline `d54652d4` | Blob on baseline | On local `origin/main` `73d3c4a6` |
|---|---|---|---|---|
| `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md` | Level 1 | VERIFIED present | `128a7996fb2007bd2a595af3f15391b25087450f` | VERIFIED absent |
| `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md` | Level 2 | VERIFIED present | `9065fc9a264f592a19759a6d0d8ef52d95321756` | VERIFIED absent |
| `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md` | Level 3 | VERIFIED present | `e199ed02a4b119686a7a3f8f934e49dbba1502d4` | VERIFIED absent |
| `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md` | Level 3 | VERIFIED present | `b96f842b5d6b8059938c0d72d7413993eeb97a95` | VERIFIED absent |

The three blobs also cited by Step 027 match this check. ADR-04’s blob is from this step’s path check. ADR-04’s decision text is not summarized here.

No required authority file is on local `origin/main`. Remote presence is not claimed for any of them.

### 3.2 Navigation file, not an authority rank

| Path | Role | On baseline `d54652d4` | Blob on baseline | On local `origin/main` `73d3c4a6` |
|---|---|---|---|---|
| `docs/architecture/INDEX.md` | Navigation. Not an authority rank. | VERIFIED present | `7954bd8d6e44947b4e3cdb9e0bfaeca4d87acb5d` | VERIFIED absent |

This matches the earlier recorded failure of `git cat-file -e origin/main:docs/architecture/INDEX.md`. The file stays out of the authority rank.

### 3.3 Checked and not promoted

`docs/architecture/GOVERNANCE.md` is on the baseline, blob `823a5df765835638a6db2bd0af9e4211dc8b9345`, and is absent from local `origin/main`. Prior steps cite it as the text that states the hierarchy. D-02 does not promote it to an authority rank.

These ADR decision paths are absent from both the baseline and local `origin/main`:

- `docs/KIT-PLATFORM-ADR-01-DECISION-V1.md`
- `docs/KIT-PLATFORM-ADR-02-DECISION-V1.md`
- `docs/KIT-PLATFORM-ADR-05-DECISION-V1.md`
- `docs/KIT-PLATFORM-ADR-06-DECISION-V1.md`
- `docs/KIT-PLATFORM-ADR-07-DECISION-V1.md`
- `docs/KIT-PLATFORM-ADR-08-DECISION-V1.md`

Absence of those six paths is a file fact. It is not a decision to write them.

Gitignored reports, the staged 2026-10-08 baseline, Step 027, Step 028, Step 029, and this file are not in the required set.

## 4. Remaining publish blockers

D-01 names the check baseline. It leaves publication blocked.

| ID | Blocker | Evidence |
|---|---|---|
| B-01 | `c447814d4c3056bb5affe3670cf8b858671686c5`, `98f0632cab983750edb032eb365c91b15db5eb44`, and `d54652d4a7734b3116db62bd356a831f9942fc73` are not in local `origin/main`. D-01 does not approve them as safe to publish and does not authorize a push. | VERIFIED commit range on the local ref. Decision text in §1. |
| Publish of the required set | Each required authority file, and `docs/architecture/INDEX.md`, is on the baseline and absent from local `origin/main`. | VERIFIED per path in §3. |
| AG-G1-02 | The check baseline is now named. The row is not PASS. The commits are still local-only, and publication is still not approved. | Step 028 status was BLOCKED. This step does not mark it PASS. |
| Live remote tip | This record does not claim that GitHub’s `main` equals `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` at the time of writing. | No fetch. **UNKNOWN** if the host has moved. |

## 5. Remaining working-tree blockers

D-01 excludes the dirty working tree from the baseline. The tree was not recounted.

| ID | Blocker | Evidence |
|---|---|---|
| B-02 | The working tree is not the baseline commit. Phase 1.5 report §4 Git row: 282 untracked, 166 modified, 2 staged, porcelain count 450. Staged names recorded there: `KIT-PLATFORM-PHYSICAL-CONSOLIDATION-VERIFICATION-V1.md` and `KIT-PLATFORM-NOVIXA-PRESCRIPTION-ASSIST-DATA-EVIDENCE-V1.md`. | RECORDED. Not re-measured. |
| Later step files | Step 027, Step 028, and Step 029 were added after that porcelain count. This file is a further untracked step artifact. | RECORDED for 027–029. This file is the Step 030 artifact. Current porcelain count: **UNKNOWN**. |
| AG-G1-03 | A clean tree for the paths the gate cites is still required. D-01 does not authorize a commit, reset, or clean that would clear the row. | Step 028 status FAIL. Unchanged. |
| AG-G6-01 | A second reader still cannot reproduce a phase verdict from the baseline alone while the recorded evidence depends on the dirty tree, gitignored copies, and untracked step files. | Step 028 status FAIL. Unchanged. |
| Other dirty paths already recorded | Uncommitted TestPack reference, dirty content manifest through migration 386, and untracked migrations after 367. | RECORDED in Step 028 B-09 and B-11. Not re-checked. |

## 6. Decisions this step does not close

D-03 through D-09 stay open. In particular, C-01 through C-04 are not disposed here. Preserving the hierarchy does not write those four dispositions.

Still open, carried from Step 028 and Step 029, and not converted into PASS:

- B-03 through B-11.
- Register line 231.
- `series_build` tenant attribution.
- `ProductionTenantActionFilter`.
- Manifest ownership and content migrations after 367.
- Test list, clean-tree build, and TestPack membership.
- `RESET EXECUTION ORDER V1` path.

AG-G1-01 stays the identity row Step 028 already marked PASS. That single row does not pass the phase.

## 7. Phase 1.5 verdict

Phase 1.5 remains **NOT PASS**.

D-01 fixes the commit used for acceptance checks. D-02 fixes the authority rank and the required file set on that commit. The required files are not on local `origin/main`. The working tree is not part of the baseline. The remaining blockers in §4, §5, and §6 are open. Phase 2 is not opened.

## Stop

This step created only `STEP-030-ARCHITECT-DISPOSITION-D01-D02.md`.

No product file, migration, manifest, solution, ADR, Decision Register, Master Architecture, or governance file was edited. No test, build, database session, deploy, commit, push, merge, reset, or clean was run.

D-03 was not started. No implementation follows this report.
