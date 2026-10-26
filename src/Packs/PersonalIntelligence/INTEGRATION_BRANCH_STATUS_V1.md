# Second Brain integration branch — status V1

Date: 2026-10-26. This file describes the branch `integrate/second-brain-20261026` only. It is not a production deployment record and it does not authorize a merge to `main`.

## Base and source

- Integration base: `origin/main` at `fed51d14`. Since the merge-base `608bc054`, `origin/main` changes only the Novixa and Famixa public sites (three articles and stats snapshots). Those paths do not overlap the research diff.
- Personal Intelligence source: commit `36642381` on `admin-build-blocker-001`, whose parent prerequisite on the remote is `3d5d7b42` (`origin/admin-build-blocker-001`).
- The branch was not created by merging `36642381`. A merge would also import two commits that are outside this scope:
  - `c57d8d7e` — Famixa, inventory, procurement, and sales TypeScript compile fixes. It is not on this branch.
  - `96657e06` — content narrative field mapping. Cherry-picked as `d481491f`, after Personal Intelligence commit `797ca615`, because `origin/main` already calls `Territory`, `NarrativeFit`, and `NarrativeJson` without declaring them, so `KitPlatform.Api` does not compile. That cherry-pick is a content compile fix, not a Second Brain feature and not the Famixa commit.
- Files were checked out from `36642381` for the Personal Intelligence pack, its tests, the Second Brain admin module, research deploy manifests, migrations `385` and `387`–`398`, and the platform wiring those projects compile against (`Program.cs`, the API project reference, `PlatformModuleCodes`, `CoreAuthority`, admin router, module registry, feature map, and the Second Brain nav label).

The uncommitted Personal Intelligence tree on `E:\KitPlatform` has no file path that is absent from `36642381`. Nine paths differ in content. The C# edits in `C:\kit-release-038` already match `36642381`. Those worktrees were not modified and were not copied over this branch. The dirty `Program.cs` on `E:\KitPlatform` registers the pack unconditionally; this branch does not.

## What the code on this branch actually does

Present and covered by tests that do not open a database:

- Trading ledger types, repositories, and the `recommend_only` recommendation function. That function does not submit an order.
- Read connectors for Bybit, Binance, and OKX, plus execution-history sync and backfill types.
- Research host, paper host, and dashboard projects. `SmaCrossStrategy` is a research formula. It is not wired to a live Futures order in `Program.cs`.
- `ResearchSafeHostWorkerIsolation` removes background writers when the research host sets that switch.

Not present, despite earlier architecture language that sometimes treats them as one system:

- A generic Agent Runtime.
- A learning loop that updates a strategy from outcomes.
- `pi_knowledge`, `pi_memory`, and `pi_agent` stores.

`AddPersonalIntelligenceResearch()` is always registered. `AddPersonalIntelligencePack` runs only when `PersonalIntelligence:ExposeExchangeRead` is true, and the call passes `startBackfillWorker: false`. The pack method's own default is `true`; the API host does not use that default. This is not evidence that a worker is running.

## Migrations

Included files: `385`, `387`, `388`, `389`, `390`, `391`, `392`, `393`, `394`, `395`, `396`, `397`, `398`. Number `386` is unused on this branch. Nothing was applied to a database.

Manifest split, unchanged from `36642381`:

- `deploy/ubuntu/migration-files.personal-intelligence.txt` lists `385` and `387`–`394`, then `396`. Its header says not to add it to `migration-files.prod.txt`.
- `deploy/ubuntu/migration-files.trading-research.txt` lists `395` and `398`. Its header says not to add it to the production manifest or the Personal Intelligence manifest.
- `397` is intentionally on neither manifest. The SQL refuses every database except `kit_pi_validation`. `Migration397SourceTests` locks that exclusion.
- `migration-files.content.txt` and `migration-files.prod.txt` on this branch do not name these files.

The numbers `394`–`398` are therefore reserved here by Personal Intelligence and Trading Research. Uncommitted content migrations with the same numbers remain only on the dirty `E:\KitPlatform` tree. They are not on this branch. A later content integration has to renumber those content files before they can share a database with this branch. Do not apply both sets under the same numbers.

## Checks on this branch

Recorded against `d481491f` in `E:\kit-integrate-fed51d14`. Nothing was applied to a database, and the running API binary under `E:\KitPlatform` was not rebuilt.

- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors. Pre-existing NU1903 warnings remain.
- `KitPlatform.PersonalIntelligence.Tests`, excluding the fixtures that open PostgreSQL: 155 passed, 0 failed.
- `NarrativeFitIntegrityTests`: 13 passed, 0 failed.
- Admin `npx tsc --noEmit` after `npm ci` in this worktree only: exit 2, 156 errors in 34 files. None of those files differ from `fed51d14`. None are under `src/modules/second-brain`, and none are `router.tsx`, `registry.tsx`, or `platform-feature-map.ts`. The errors are Famixa and kit-video content files, plus `GoodsReceiptListPage.tsx` and `AdjustmentListPage.tsx`. `src/**/*.smoke.ts` is excluded by `tsconfig.json`. Fixing that set is the Famixa, inventory, and procurement compile work kept off this branch with `c57d8d7e`.
- `node --experimental-strip-types` on the three Second Brain smoke scripts: `trading routes ok`, `research view ok`, `research workbench ok`.

## Explicitly not done

- No merge to `main`.
- No production deploy.
- No migration execution.
- No worker enabled and no new exchange order.
- PostgreSQL integration tests were not run. They require `KIT_PI_TEST_CONNECTION` and refuse shared databases, port `5432`, `novixa_prod`, and `trading_research`. That variable was not set, because those fixtures insert tenant and user rows.
- The full admin project does not typecheck. The Second Brain admin module does. Closing the other 156 errors is a separate Famixa and inventory change, not part of this branch.
