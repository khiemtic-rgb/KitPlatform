# STEP 037 — Implementation commit and migration ownership

Status: **READ-ONLY DECISION BRIEF.** No commit is selected. No prefix is selected. No manifest line is selected. D-05 implementation permission is not granted. Phase 1.5 is not PASS.

This file is an untracked report. It is not an implementation commit, and it is not a Decision Register entry.

## Labels

| Label | Meaning in this file |
|---|---|
| VERIFIED | A Git result from this step, against the named commit or path. |
| RECORDED | A fact carried from Step 028, Step 030, Step 033, Step 034, or Step 035. Not re-opened where this step says so. |
| INFERENCE | A conclusion from the verified facts. Not a compiler log and not an Architect decision. |
| UNKNOWN | Not measured in this step. |
| BLOCKED | A choice this evidence does not allow. |

Evidence is in Parts A and B. Recommendations are in the two recommendation sections only.

## Queries this step ran

No fetch. No stage, unstage, commit, push, merge, reset, clean, or stash. No database session. No migration run. Migration `376` was not read. Untracked migration files were not opened. The dirty content manifest was not read. The Decision Register, ADRs, Master Architecture, migrations, manifests, and application code were not edited.

| Query | What it was used for |
|---|---|
| `git rev-parse` of `HEAD`, `origin/main`, and `HEAD:migrations`, `HEAD:deploy/ubuntu`, `HEAD:deploy/ubuntu/migration-files.content.txt`, repeated for `73d3c4a6`, `c447814d`, and `98f0632c` | Identity and tree equality. |
| `git rev-list --left-right --count origin/main...HEAD` and `git merge-base HEAD origin/main` | Local relationship to the recorded remote-tracking ref. |
| `git branch -r --contains` for each of the three commits | Whether any local remote-tracking ref contains them. |
| `git log` and `git diff-tree --name-status` / `--dirstat` for the three commits | Purpose and file categories. |
| `git status --short` and `git diff` limited to paths named below | Whether those committed paths differ in the working tree. |
| `git ls-tree` names under `migrations/` with prefixes `367` and above, plus a name search for `376_` | Tracked prefix inventory on the shared migrations tree. |
| `git grep` of `367_`, `368_`, `369_`, `371_`, `383_`, and `384_` under the four tracked manifests | Manifest membership. |
| `git show` of the content-manifest header and of the first comment lines of migrations 383 and 384 | Tracked objects only. |
| `git grep` of the deleted engine type names at `c447814d` | Whether that commit still names types it deletes. |

## Part A — Candidate implementation commit

### A.1 Current HEAD and the local `origin/main` ref

**VERIFIED.**

| Item | Value |
|---|---|
| Branch | `main` |
| HEAD | `d54652d4a7734b3116db62bd356a831f9942fc73` |
| Local `origin/main` | `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` |
| `git merge-base HEAD origin/main` | `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` |
| `git rev-list --left-right --count origin/main...HEAD` | `0` `3` |
| Date on local `origin/main` | 2026-09-29 16:14:34 +0700 |

Local `main` is three commits ahead of the local remote-tracking ref `origin/main`, and zero commits behind that ref. The three commits are a straight line:

`73d3c4a6` → `c447814d` → `98f0632c` → `d54652d4`

`git branch -r --contains` printed no remote-tracking ref for `c447814d`, `98f0632c`, or `d54652d4`.

**UNKNOWN.** Whether the host at `https://github.com/khiemtic-rgb/KitPlatform.git` has moved since this local `origin/main` was last updated. This step did not fetch. Step 030 recorded the same limit.

**RECORDED.** Step 030 D-01 decided that `d54652d4a7734b3116db62bd356a831f9942fc73` is the baseline for Phase 1.5 acceptance checks. That decision does not publish the commits and does not name an implementation commit. This step does not reopen D-01.

### A.2 The three local commits

Subjects and parents are **VERIFIED** from `git log`. File categories are **VERIFIED** from `git diff-tree --name-status`.

#### `c447814d4c3056bb5affe3670cf8b858671686c5`

| Item | Value |
|---|---|
| Parent | `73d3c4a6c509ab25229d07a475229a6c8c1cf79d` |
| Date | 2026-10-04 17:21:51 +0700 |
| Subject | `refactor(core): move pharmacy workflow out of core` |
| Body | `Co-authored-by: Cursor <cursoragent@cursor.com>` only |
| Size | 18 paths |

**Purpose.** Moves the workflow engine into the Pharmacy pack and deletes core engine files in the same commit.

**File categories.**

| Category | Paths |
|---|---|
| Pharmacy API | `src/KitPlatform.Api/Controllers/Pharmacy/SalesController.cs` (modified) |
| Application project and deleted core contracts | `src/KitPlatform.Application/KitPlatform.Application.csproj` (modified). Deleted: `IInventoryEngine.cs`, `IPermissionEngine.cs`, `IPricingEngine.cs`, `IWorkflowEngine.cs`, `WorkflowDtos.cs` under `src/KitPlatform.Application/Core/Engines/` |
| Infrastructure composition and deleted engines | `src/KitPlatform.Infrastructure/DependencyInjection.cs` (modified). Deleted: `PermissionEngine.cs`, `PricingEngine.cs`, `WorkflowEngine.cs` under `src/KitPlatform.Infrastructure/Core/Engines/` |
| Pharmacy pack | Added `Workflow/IWorkflowEngine.cs`, `Workflow/WorkflowDtos.cs`, and `Workflow/WorkflowEngine.cs`. Modified `PharmacyPackDependencyInjection.cs`, `Procurement/PurchaseOrderService.cs`, `Sales/SalesRepository.cs`, `Sales/SalesService.cs` |

The added interface is **VERIFIED** at `namespace KitPlatform.Packs.Pharmacy.Workflow`. The added engine class is **VERIFIED** at `namespace KitPlatform.Packs.Pharmacy.Infrastructure`.

No path under `migrations/` or `deploy/ubuntu/` is in this commit.

**Callers left on the deleted types.** **VERIFIED** at this commit, these tracked files still name types whose definition files the commit deletes:

| Path at `c447814d` | Name still present |
|---|---|
| `src/KitPlatform.Api/Controllers/System/WorkflowController.cs` | `IWorkflowEngine` |
| `src/KitPlatform.Infrastructure/CustomerApp/CustomerDraftOrderService.cs` | `IPermissionEngine` |
| `src/KitPlatform.Infrastructure/Dashboard/DashboardService.cs` | `IPermissionEngine` |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/BatchResolver.cs` | `IInventoryEngine` |
| `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Procurement/ProcurementRepository.cs` | `IPricingEngine` |
| `tests/KitPlatform.Application.Tests/CoreEngineTests.cs` | `IPricingEngine` and `new PricingEngine()` |

`git grep` for `interface IPermissionEngine`, `interface IPricingEngine`, `interface IInventoryEngine`, `class PricingEngine`, and `class PermissionEngine` at this commit found no definition. The only `class PricingEngine` hit is the test class `PricingEngineTests`.

Those six blobs are **VERIFIED** identical at `c447814d` and at HEAD. `98f0632c` and `d54652d4` do not change them.

**Working tree.** **VERIFIED** `git status --short` shows those six caller paths modified. A `git diff -U1 HEAD` filter on the deleted type names shows the working-tree side removing `IPermissionEngine`, `IPricingEngine`, `IInventoryEngine`, and the `PricingEngine` construction, and changing `WorkflowController`'s using to `KitPlatform.Packs.Pharmacy.Workflow`.

**VERIFIED** three paths this commit itself modified are also modified in the working tree: `SalesController.cs`, `PharmacyPackDependencyInjection.cs`, and `SalesRepository.cs`. Together with `src/KitPlatform.Api/KitPlatform.Api.csproj`, `git diff --stat HEAD` reports 140 insertions and 1 deletion. The bodies of those three pharmacy hunks were not used as evidence. What those three hunks do is **UNKNOWN**.

**INFERENCE.** The committed snapshot still points callers at types it deleted. The working-tree edits on the six caller paths are the edits that drop those names. A clean tree of `c447814d` does not contain those edits. No `dotnet build` was run. The compiler exit code is **UNKNOWN**.

This commit does not contain a D-05 migration.

#### `98f0632cab983750edb032eb365c91b15db5eb44`

| Item | Value |
|---|---|
| Parent | `c447814d4c3056bb5affe3670cf8b858671686c5` |
| Date | 2026-10-04 20:35:34 +0700 |
| Subject | `feat(content): consolidate editorial workflow from kit-mkt` |
| Body | `Co-authored-by: Cursor <cursoragent@cursor.com>` only |
| Size | 21 paths |

**Purpose.** Consolidates the editorial content workflow: article series, brand view, topic workspace, calendar, ideas, ops, lineage, and the matching API and repository surface.

**File categories.**

| Category | Paths |
|---|---|
| Admin routing and content UI | `client/admin/src/app/router.tsx`; content pages and `content-lineage.tsx`, `content-nav.tsx` under `client/admin/src/modules/content/`; `client/admin/src/shared/api/content.api.ts`; `client/admin/src/styles/index.css` |
| Operations notes | Three added files under `docs/novixa/03-operations/`: `content-workspace-v2g-audit-20260927.md`, `episode-workspace-v2f-gap-audit-20260927.md`, `episode-workspace-v2f-smoke-20260927.md` |
| Content pack and API | `src/KitPlatform.Api/Controllers/Content/ContentController.cs`; `ContentContracts.cs`; `ContentArticleSeriesRepository.cs`; `ContentOpsService.cs` |

No path under `migrations/` or `deploy/ubuntu/` is in this commit.

**Working tree.** **VERIFIED** `git status --short` on these 21 paths is empty. Their bytes at HEAD match the working tree.

**VERIFIED** `git diff --name-only HEAD` under `client/admin/src/modules/content/`, `src/Packs/Content/`, and `src/KitPlatform.Api/Controllers/Content/` lists 98 paths. The overlap with this commit's 21 paths is 0. Those 98 paths are later working-tree edits. They are excluded from this commit. This step did not read them and does not treat them as D-05 implementation.

**INFERENCE.** This commit's own file set does not depend on those 98 modified paths. They are outside its diff. Whether a clean checkout builds is **UNKNOWN**. No build was run.

This commit does not contain a D-05 migration. The authority files named by Step 030 D-02 are not in this commit. They appear in the child commit below.

#### `d54652d4a7734b3116db62bd356a831f9942fc73`

| Item | Value |
|---|---|
| Parent | `98f0632cab983750edb032eb365c91b15db5eb44` |
| Date | 2026-10-05 20:12:15 +0700 |
| Subject | `docs(architecture): establish canonical architecture authority V1` |
| Body | `Completes KPA-ARCH-DOC-FOUNDATION-006` and `Co-authored-by: Cursor <cursoragent@cursor.com>` |
| Size | 7 paths |

**Purpose.** Tracks the architecture authority set by adding ignore exceptions and the six documents.

**File categories.**

| Category | Paths |
|---|---|
| Ignore exceptions | `.gitignore`. Adds exceptions for `docs/architecture/INDEX.md`, `docs/architecture/GOVERNANCE.md`, and the four `docs/KIT-PLATFORM-*-V1.md` files named below. Also changes `src/KitPlatform.Api/App_Data/kit-video-kf/` and `src/KitPlatform.Api/App_Data/kit-video-i2v/` into one `src/KitPlatform.Api/App_Data/` rule. |
| Authority and navigation documents | Added: `docs/KIT-PLATFORM-MASTER-ARCHITECTURE-V1.md`, `docs/KIT-PLATFORM-ARCHITECTURE-DECISION-REGISTER-V1.md`, `docs/KIT-PLATFORM-ADR-03-DECISION-V1.md`, `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md`, `docs/architecture/GOVERNANCE.md`, `docs/architecture/INDEX.md` |

No path under `migrations/` or `deploy/ubuntu/` is in this commit.

**Working tree.** **VERIFIED** `git status --short` on these seven paths is empty.

**RECORDED.** Step 030: this commit is the Phase 1.5 acceptance baseline. Step 035: a D-05 migration file may be created only after a Level 2 Decision Register entry on the agreed implementation commit states the implementation grant, the filename, and the manifest position. That entry is not in this commit. Step 032, Step 033, Step 034, and Step 035 are untracked reports and are not that entry.

This commit does not contain a D-05 migration. Its document text is the acceptance baseline, not an implementation grant stored on an implementation commit.

### A.3 Separated facts

| Fact | Identity | Label |
|---|---|---|
| Phase 1.5 acceptance baseline | `d54652d4a7734b3116db62bd356a831f9942fc73`, by Step 030 D-01 | RECORDED decision. HEAD equality is VERIFIED. |
| Candidate implementation commit | None of the three commits contains the Step 035 Register grant or a D-05 migration. | NOT SELECTED |
| Remote publication | The three commits are absent from every local remote-tracking ref. Local `main` is three commits ahead of local `origin/main` `73d3c4a6`. | VERIFIED for the local ref. Host tip UNKNOWN. Publication not approved. |
| Dirty working tree | Excluded from all three commits. | VERIFIED for the paths checked below. A full porcelain recount was not done. |

**Dirty paths checked in this step, excluded from every commit.**

| Path or set | Working tree | In which commit's diff |
|---|---|---|
| `deploy/ubuntu/migration-files.content.txt` | Modified | None. The tracked blob is unchanged across the three commits. |
| `src/KitPlatform.Api/KitPlatform.Api.csproj` | One added line: `ProjectReference` to `..\Packs\TestPack\KitPlatform.Packs.TestPack.Infrastructure\KitPlatform.Packs.TestPack.Infrastructure.csproj` | None |
| Six pharmacy caller paths in A.2 | Modified; diff removes references to types `c447814d` deleted | None of the three commits contains that diff. The blobs match `c447814d` through HEAD. |
| `SalesController.cs`, `PharmacyPackDependencyInjection.cs`, `SalesRepository.cs` | Modified | First committed in `c447814d`. The later uncommitted hunks are excluded. Their role is UNKNOWN. |
| 98 paths under the content UI, Content pack, and Content controller | Modified | None of them is among the 21 paths in `98f0632c`. |

**RECORDED**, not re-counted: Step 030 B-02 cites the Phase 1.5 porcelain figure of 282 untracked, 166 modified, 2 staged. Step 028 B-09 and B-11 cite the uncommitted TestPack reference, the dirty content manifest, and untracked migrations after 367. The current full porcelain count is **UNKNOWN**.

**RECORDED.** Step 028 AG-G5-01: the 2026-10-11 `dotnet build` of `KitPlatform.slnx` included the uncommitted TestPack reference. That build is not evidence for a clean tree of any of these three commits. A clean build result remains **UNKNOWN**.

### A.4 Recommendation — smallest next decision on the commit

**Recommendation, not a selection.**

Do not choose among `c447814d`, `98f0632c`, and `d54652d4` as the implementation commit in the next writing. They share one migrations tree and one content-manifest blob, so a choice among them does not produce a prefix or a manifest position. None of them is a D-05 implementation. D-01 already names `d54652d4` as the acceptance baseline only.

The smallest defensible next Architect decision is the manifest decision in B.5. After that decision exists, a later writing can name an implementation base. Naming a base before B.5 does not make an insertion position safe.

**Missing evidence, left UNKNOWN.**

| Gap | Why it is still open |
|---|---|
| Host tip of `origin/main` | No fetch. |
| Clean `dotnet build` of each commit | Not run. The dangling type names at `c447814d` are verified; the compiler exit code is not. |
| Role of the uncommitted hunks in `SalesController.cs`, `PharmacyPackDependencyInjection.cs`, and `SalesRepository.cs` | Bodies not used. |
| Full current porcelain count | Not recounted. |
| Whether the dirty content-manifest bytes still match the Step 033 hash `6bc5f545035c62d4a9ce9f45e1fe9ab2380ad839` | Status is modified. The file was not hashed and was not read. |

## Part B — Migration namespace and ownership

### B.1 One tracked tree on every candidate

**VERIFIED.** These object ids are the same at `73d3c4a6`, `c447814d`, `98f0632c`, and `d54652d4`:

| Tree or blob | Id |
|---|---|
| `migrations` | `3d40d47d9f2d7ab76b7c25cb606296a7ca6d984c` |
| `deploy/ubuntu` | `853e23a035bd4992575a37e96a8de5ac1721bd73` |
| `deploy/ubuntu/migration-files.content.txt` | `10053f6ae9043c955f0fa208a9d499e78482df65` |

The inventory below is one inventory. It applies to each of the three candidate commits because the trees are equal. It also applies to local `origin/main`. Publishing the three commits would not change this inventory.

**Tracked names at prefix 367 and above.** **VERIFIED** with `git ls-tree` on `d54652d4`, which is the same migrations tree as the other two candidates.

| Prefix | Tracked path | Manifest hit on the shared `deploy/ubuntu` tree |
|---|---|---|
| 367 | `migrations/367_pack_content_character_reference_pack_contract.sql` | `migration-files.content.txt` line 62 |
| 368 | `migrations/368_pack_local_os_job_public_45d.sql` | `migration-files.local-os.txt` line 32 |
| 369 | `migrations/369_pack_local_os_room_keep_public.sql` | `migration-files.local-os.txt` line 33 |
| 371 | `migrations/371_inventory_stock_review.sql` | `migration-files.prod.txt` line 225 |
| 383 | `migrations/383_pack_content_article_series.sql` | No hit in the four manifests named below |
| 384 | `migrations/384_pack_content_brand_narrative_dna.sql` | No hit in the four manifests named below |

`git ls-tree` names from prefix 367 upward do not include `370_`, `372_` through `382_`, `376_`, `385_`, or `386_`. A name search for `376_` under `migrations/` returned no path.

A missing tracked name is not a reserved prefix and is not an available prefix. Step 028's untracked range, quoted in B.2, covers numbers that have no tracked file.

`368`, `369`, and `371` are occupied by tracked files on other manifests. They are not free content slots.

### B.2 Untracked prefix conflicts, from Step 028 only

**RECORDED.** Step 028 AG-G3-04, from the Phase 1.5 report §4. This step did not open those files and did not list the working tree.

- Untracked on-disk files in prefixes `368`–`382`, described there as content/test files.
- `migrations/385_pack_personal_intelligence_trading_ledger_v1.sql`
- `migrations/386_pack_content_famixa_canon_administer.sql`
- Untracked manifests `migration-files.personal-intelligence.txt` and `migration-files.test-pack.txt`
- The dirty working copy of the content manifest lists through 386 and skips 381 and 385.
- `migrations/281_pack_content_wave0.sql` had an unstaged one-line change.

**UNKNOWN** from this step: the current filenames inside that untracked range, and whether extra untracked files share the prefixes `368`, `369`, or `371` that already have tracked files. The Step 028 range overlaps those tracked prefixes. This step does not resolve that overlap by opening files.

Those recorded untracked names are not implementation evidence and are not a prefix reservation.

### B.3 Tracked content-manifest state

**VERIFIED** on the shared blob `10053f6ae9043c955f0fa208a9d499e78482df65`, which is the manifest at every candidate commit.

Header, lines 1–2:

```
# KIT Content Park — PILOT / INDEPENDENT
# Do NOT merge into migration-files.prod.txt (Pharmacy) until GA is explicitly approved.
```

`git grep -c "\.sql"` reports 58 matching lines. The last line is line 62:

`367_pack_content_character_reference_pack_contract.sql`

`git grep` of `383_` and `384_` in these four tracked manifests found no line:

- `deploy/ubuntu/migration-files.content.txt`
- `deploy/ubuntu/migration-files.prod.txt`
- `deploy/ubuntu/migration-files.family-os.txt`
- `deploy/ubuntu/migration-files.local-os.txt`

The same grep found `367_` only at content line 62, `368_` and `369_` only on the local-os manifest, and `371_` only on the prod manifest.

**VERIFIED** the working-tree path `deploy/ubuntu/migration-files.content.txt` is modified. Its contents were not read. The Step 033 working-tree hash was not re-measured.

### B.4 The 383/384 gap

**VERIFIED** from the tracked files, first comment lines only.

`migrations/383_pack_content_article_series.sql`:

```
-- KitPlatform 370: Article Content Series Engine V1
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Does NOT touch series_pilot / series_build / video_story_graph / Famixa video.
```

`migrations/384_pack_content_brand_narrative_dna.sql`:

```
-- KitPlatform 371: Brand Narrative DNA V1
-- Manifest: deploy/ubuntu/migration-files.content.txt
```

The filename prefix and the comment number differ in both files. The comment `KitPlatform 370` is not a tracked `370_` file. The comment `KitPlatform 371` is not `migrations/371_inventory_stock_review.sql`. That 371 file is a different tracked migration, listed on `migration-files.prod.txt` line 225. Both headers name `deploy/ubuntu/migration-files.content.txt`. That manifest's 58 entries do not name either file.

**Why this blocks a safe D-05 insertion position. BLOCKED.**

The tracked content list ends at line 62 with migration 367. Migrations 383 and 384 are already in the same commits' migration tree, are named as content migrations, and claim this manifest in their headers. They have no line on it.

- A new line after 367 would sit in the manifest while two tracked content-named files remain unlisted, with no order against them.
- A new line after 384 would require 383 and 384 to be on the manifest first. That listing is the open gate row below, not a D-05 line.
- A line aimed at the comment numbers 370 or 371 would aim at a prefix with no tracked `370_` file, or at the different tracked file `371_inventory_stock_review.sql`.

The dirty manifest is a modified working-tree file. Step 028 recorded that it lists through 386 and skips 381 and 385. This step did not read it. That working-tree list is not a tracked position.

**RECORDED.** Step 028 AG-G3-04 status is FAIL. Step 034 and Step 035 leave 383 and 384 outside D-05. This step does not re-score the gate and does not edit either file.

### B.5 Recommendation — minimum separate Architect decision

**Recommendation, not a repair and not a classification of other migrations.**

One written disposition, separate from D-05, covering only these two tracked files:

- `migrations/383_pack_content_article_series.sql`
- `migrations/384_pack_content_brand_narrative_dna.sql`

For each file, the disposition says one of these two things: the file is included on `deploy/ubuntu/migration-files.content.txt`, with an order relative to line 62, or the file is excluded from that manifest. The disposition also says that the comments `KitPlatform 370` and `KitPlatform 371` are not manifest lines and are not prefix assignments.

That writing is the minimum decision that can make a later content-manifest position definable. It is the Step 028 AG-G3-04 question, narrowed to the two tracked files this step verified as absent from the manifest.

The disposition does not edit either SQL file, does not edit `migrations/371_inventory_stock_review.sql`, does not choose a D-05 filename, and does not decide the duplicate-prefix collisions in Step 028 AG-G3-03.

Until that disposition exists, the manifest position for a future D-05 line stays **BLOCKED** on every candidate commit, because every candidate has the same manifest blob.

## Closing state

- candidate implementation commit: `NOT SELECTED`
- migration prefix: `NOT SELECTED`
- manifest position: `NOT SELECTED`
- D-05 implementation permission: `NOT GRANTED`
- Phase 1.5: `NOT PASS`
