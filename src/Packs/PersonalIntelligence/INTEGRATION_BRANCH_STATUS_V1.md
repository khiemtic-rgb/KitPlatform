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

The code checks below were run at `d481491f` in `E:\kit-integrate-fed51d14`. `e9de1e02` and the handoff note after it change only this file. Nothing was applied to a database, and the running API binary under `E:\KitPlatform` was not rebuilt.

- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors. Pre-existing NU1903 warnings remain.
- `KitPlatform.PersonalIntelligence.Tests`, excluding the fixtures that open PostgreSQL: 155 passed, 0 failed.
- `NarrativeFitIntegrityTests`: 13 passed, 0 failed.
- Admin `npx tsc --noEmit` after `npm ci` in this worktree only: exit 2, 156 errors in 34 files. None of those files differ from `fed51d14`. None are under `src/modules/second-brain`, and none are `router.tsx`, `registry.tsx`, or `platform-feature-map.ts`. The errors are Famixa and kit-video content files, plus `GoodsReceiptListPage.tsx` and `AdjustmentListPage.tsx`. `src/**/*.smoke.ts` is excluded by `tsconfig.json`. Fixing that set is the Famixa, inventory, and procurement compile work kept off this branch with `c57d8d7e`.
- `node --experimental-strip-types` on the three Second Brain smoke scripts: `trading routes ok`, `research view ok`, `research workbench ok`.

## Explicitly not done

- No merge to `main`.
- No production deploy.
- No migration on `novixa_prod`, `trading_research`, port `5432`, or any database that already existed.
- No worker enabled and no new exchange order.
- The isolated PostgreSQL run is recorded under "Isolated validation". It did not use port `5432` or an existing data directory.
- The full admin project does not typecheck. The Second Brain admin module does. Closing the other 156 errors is a separate Famixa and inventory change, not part of this branch.

## Handoff readiness

Verdict for continuing development on this branch: ready, with the limits in this section. The isolated validation below blocks a merge review until the three paper-integrity tests can run. This is not a production release and not permission to merge `main`.

### Integrated and checked without a database

These paths compile on this branch and are covered by the non-database tests and smoke scripts above:

- Research publication read stays registered on the API. `ResearchPublicationController` is the only Personal Intelligence controller left in the application model when `PersonalIntelligence:ExposeExchangeRead` is false.
- `TradingAgent.Recommend` returns `recommend_only`, copies the proposal as `ExecutionMode.Paper`, and does not submit an order. `TradingOperationalLoop` calls that function. The loop controller is removed unless the exchange-read flag is on, and the pack services behind it are not registered in that default.
- `SmaCrossStrategy` (`sma-cross-v1`, hourly, long only) is a research formula used by `ResearchStrategies` and the research host. The research host throws if started with `--live`. It is not referenced from `Program.cs`.
- Paper-session accounting, publication file reads, and exchange HTTP parsing are tested with fake handlers and in-memory stores.
- `ResearchSafeHostWorkerIsolation` removes already-registered hosted services only when `ResearchSafeHost:SuppressBackgroundWriters` is true. That key is unset in committed configuration, so the API call does nothing.
- Admin routes, registry key `secondBrain`, and feature map `personal_intelligence` match the three smoke scripts.

### PostgreSQL coverage

The classes below were run against the new cluster described in "Isolated validation". `PaperIntegrityPostgresTests` did not store a paper round trip: `SqlResearchStore` refuses `kit_pi_validation`, and those tests require that name.

- `PostgresIntegrationTests`
- `PaperIntegrityPostgresTests`
- `OperationalDeliveryTests`
- `RuntimeHttpTests`
- `MultiExchangeBackfillTests`
- `ExchangeConnectionApiTests`
- `ExchangeExecutionSyncApiTests`

### Default activation and worker

Committed `appsettings` files have no `PersonalIntelligence` section. Both reads of `PersonalIntelligence:ExposeExchangeRead` default to false.

With that default the API does not call `AddPersonalIntelligencePack`, so `ExchangeBackfillWorker` is not registered. The pack method itself defaults `startBackfillWorker` to true. The API call, when the flag is later turned on, passes false and therefore still does not register the worker. `ExchangeBackfillOptions.WorkerEnabled` defaults to true and only matters for a host that registered the worker. `Content:WorkerEnabled` in `appsettings.Development.json` is the content worker, not this one.

Desk, loop, and exchange controllers are removed by `ResearchOnlyControllerConvention` unless the flag is true, and even then only `TradingExchangeController` is added back. Trading and exchange routes still require the policies in `PersonalIntelligenceAuthorizationExtensions`: an authenticated `ADMIN`, or `personal_intelligence.trading.run`, `personal_intelligence.trading.approve`, `personal_intelligence.exchange.connect`, `personal_intelligence.exchange.read`, or `personal_intelligence.research.read`, depending on the action. Those permission rows are not seeded by the production manifest.

The research host, paper host, and research dashboard each accept a SQL connection only when the database name is `trading_agent`. They refuse every other name, including `novixa_prod`. They also refuse a path that is the production `api.env`.

### Migrations before any test run

Do not apply these files to `novixa_prod`, to the live `trading_research` database, or through `migration-files.prod.txt` or `migration-files.content.txt`. Number `386` does not exist. The only apply was the new local cluster in "Isolated validation".

Personal Intelligence manifest order, after the platform tables it references (`public.tenants`, `public.users`, `platform_module_registry`, and `kit_uuid_v7`):

1. `385` ledger, including `person`.
2. `387` `pi_events`. `388` makes that table append-only. `389` adds `pi_operation`.
3. `390` inserts module code `personal_intelligence`.
4. `391` exchange connection, then `392` execution sync, then `393` backfill jobs. Each references `person` and the trading account from `385`.
5. `394` paper-host receipt. It has no tenant foreign key and aborts on `novixa_prod` or `trading_research`.
6. `396` exchange-connection audit. It is on this manifest and aborts unless `current_database()` is exactly `kit_pi_validation`. A runner that applies the whole manifest therefore cannot finish on any other name. Do not add another database name without a recorded owner decision.

Trading Research manifest, separate database, after the platform baseline and not on the Personal Intelligence manifest:

1. `395` creates schema `research_trading`. It aborts on `novixa_prod`.
2. `398` adds nullable paper-integrity columns and one empty table. It requires `395` and aborts on `novixa_prod` or `trading_research`.

`397` is on neither manifest. Its SQL aborts unless the database is `kit_pi_validation`, and it references tables from `385` and `391`.

The safe database check was executed on a new local cluster. See "Isolated validation". Do not repeat it against an existing database.

### Branch and the VPS

Last read-only service snapshot was 2026-10-26 14:03 +07. It was not repeated for this note. At that time `kit-platform-api` had been active since 09:46 +07, which is before `797ca615`, `d481491f`, and `e9de1e02`. `kit-trading-research.timer` was enabled and had last triggered at 14:00 +07. The API DLL hash on the server was not read.

This branch is not merged to `main` and was not deployed. The running API and the research timer are the previously deployed processes. Their behavior does not verify this commit, and this commit does not describe what those processes are executing.

### Still outside this branch

- Famixa, kit-video, procurement, and inventory TypeScript errors from the admin typecheck. They match `fed51d14` and stay with the excluded compile commit `c57d8d7e`.
- Content migrations numbered `394`–`398` on the dirty `E:\KitPlatform` tree. They must be renumbered before they share a database with this branch.
- No Agent Runtime, no learning loop, and no `pi_knowledge`, `pi_memory`, or `pi_agent` store. `recommend_only`, the ledger, the SMA formula, and the read connectors are not those systems.

## Isolated validation

Date: 2026-10-26. Cluster data directory `E:\kit-pi-validate-20261026`, created with PostgreSQL 18 `initdb`. It listens only on `127.0.0.1:5433`. Port `5432` and its existing process were not used. The cluster contains `kit_pi_validation` and `trading_agent`, and does not contain `novixa_prod` or `trading_research`. Local trust authentication is limited to localhost. No production credential was used.

`migration-files.prod.txt` was applied to `kit_pi_validation` except these files, which fail on an empty database and were not edited. They are outside Personal Intelligence:

- `060_family_members_status_align.sql` references `family_members.is_active`, which is not on the fresh table.
- `090_kap_pharmacy_intelligence_seed.sql` and `102_kap_pharmacy_v1_1_telemed.sql` insert assessment rows whose parents are absent.
- `155_learning_os_schema.sql` and `158_learning_credentials_gates_l3_l5.sql` require a role named `kitplatform`.
- `163`, `165`, `168`, `178`, `179`, and `180` learning seeds reference quiz modules that were not created after `155` failed.
- `230_pack_family_parent_emotion_push.sql` and `231_pack_family_memory.sql` require schema `pack_family`.

The Personal Intelligence manifest then applied cleanly on `kit_pi_validation`, including `396`. `397` also applied there and is still on neither manifest. `395` and `398` applied on both `kit_pi_validation` and `trading_agent`. `396` and `397` refused `trading_agent`. `394` does not refuse `trading_agent`; it applied there because its guard names only `novixa_prod` and `trading_research`.

A second apply of the Personal Intelligence manifest failed for `388` and `389`. `388` tries to replace an index that a foreign key already uses. `389` creates `pi_operation` without `IF NOT EXISTS`. `395` and `398` applied again without error. These files are still first-run migrations, not a repeatable runner.

Content manifest numbers `292`, `294`, `295`, `296`, and `297` also appear in the production manifest, with different filenames. Personal Intelligence numbers are in neither of those manifests. Content `394`–`398` remain only on the dirty `E:\KitPlatform` tree and were not applied.

`KitPlatform.PersonalIntelligence.Tests` with the fixture pointed at this `kit_pi_validation`: **190 passed, 3 failed, 0 skipped, 193 total**. The three failures are `PaperIntegrityPostgresTests`. Each stops in `SqlResearchStore` because the tests require database `kit_pi_validation` and the store accepts only `trading_agent`. No paper row was written by those tests. The store was not changed. `platform_events` had zero rows after the journal test; the assertion now counts rows, because migration `067` creates that table on a platform database. The fixture gives tenant B the existing module `inventory` when its module list is empty, so the entitlement call is not rejected for an empty list.

`dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` after that run: 0 errors. The API process on port `5290` was not restarted. During exchange tests the backfill worker logged that it is disabled.

Merge review stays blocked on the paper-test contract. The smallest next decision is which database name those three tests should use. Do not widen the host allowlist and do not point the tests at an existing database as part of that decision.
