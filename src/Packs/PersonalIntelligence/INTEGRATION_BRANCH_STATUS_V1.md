# Second Brain integration branch — status V1

Date: 2026-10-26. This file describes the branch `integrate/second-brain-20261026`. It is not a production deployment record. Pull request 7 merged Agent Runtime V1 onto `origin/main` at `2b870c4a`. The local ref `main` remains `8c59ceb4`.

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

Verdict for continuing development on this branch: the Trading Agent paper path is verified on the isolated cluster. This is not a production release and not permission to merge `main`.

### Integrated and checked without a database

These paths compile on this branch and are covered by the non-database tests and smoke scripts above:

- Research publication read stays registered on the API. `ResearchPublicationController` is the only Personal Intelligence controller left in the application model when `PersonalIntelligence:ExposeExchangeRead` is false.
- `TradingAgent.Recommend` returns `recommend_only`, copies the proposal as `ExecutionMode.Paper`, and does not submit an order. `TradingOperationalLoop` calls that function. The loop controller is removed unless the exchange-read flag is on, and the pack services behind it are not registered in that default.
- `SmaCrossStrategy` (`sma-cross-v1`, hourly, long only) is a research formula used by `ResearchStrategies` and the research host. The research host throws if started with `--live`. It is not referenced from `Program.cs`.
- Paper-session accounting, publication file reads, and exchange HTTP parsing are tested with fake handlers and in-memory stores.
- `ResearchSafeHostWorkerIsolation` removes already-registered hosted services only when `ResearchSafeHost:SuppressBackgroundWriters` is true. That key is unset in committed configuration, so the API call does nothing.
- Admin routes, registry key `secondBrain`, and feature map `personal_intelligence` match the three smoke scripts.

### PostgreSQL coverage

The classes below were run against the new cluster described in "Isolated validation". `PaperIntegrityPostgresTests` opens `trading_agent` on that same server. `SqlResearchStore` still refuses every other database name.

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

`388` drops the foreign keys that use `uq_pi_outcome_id_tenant` and `uq_trading_order_id_tenant` before it replaces those unique constraints. `389` creates `pi_operation` with `IF NOT EXISTS`. On a new database `kit_pi_repeat`, both files applied, then applied again, without error. They were not run a second time on `kit_pi_validation`. `395` and `398` already applied again without error. `396` and `397` still refuse every database except `kit_pi_validation`. `394` refuses `novixa_prod` and `trading_research`, and the paper host requires it on `trading_agent` before storing `paper_host_receipt`. It stays off the production and trading-research manifests.

Content manifest numbers `292`, `294`, `295`, `296`, and `297` also appear in the production manifest, with different filenames. Personal Intelligence numbers are in neither of those manifests. Content `394`–`398` remain only on the dirty `E:\KitPlatform` tree and were not applied.

`KitPlatform.PersonalIntelligence.Tests` with `KIT_PI_TEST_CONNECTION` pointed at `kit_pi_validation` on this server: **193 passed, 0 failed, 0 skipped**. The paper tests switch that connection to database `trading_agent` and then write, replay, and read `research_trading` rows. `platform_events` row count stays the safety check. The fixture gives tenant B the existing module `inventory` when its module list is empty.

`dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` after that run: 0 errors. The API process on port `5290` was not restarted.

To repeat: start only the cluster in `E:\kit-pi-validate-20261026` on `127.0.0.1:5433`, set `KIT_PI_TEST_CONNECTION` to `kit_pi_validation` on that port, and run `dotnet test tests/KitPlatform.PersonalIntelligence.Tests`. Do not point that variable at port `5432`.

This validates the Trading Agent paper path on an isolated database. It does not make the production migration manifest or the Content manifest ready, and it does not add an Agent Runtime or a learning loop.

## Merge review

Verdict at `b3bcafcd`: **READY FOR MERGE REVIEW**. The fast-forward that followed is recorded under Finalization. This file is not a production release.

Compared with `origin/main` `fed51d14`, the branch adds Personal Intelligence, the Second Brain admin routes, the research publication tool, and the content narrative fields required for the API to compile (`d481491f`). `Program.cs` always registers research publication and registers the trading pack only when `PersonalIntelligence:ExposeExchangeRead` is true. That default is false, and the backfill worker is passed `false`. `ResearchSafeHost:SuppressBackgroundWriters` stays unset, so the isolation helper does not remove workers.

Direct merge blocker: none found in the branch diff. The API builds. The Personal Intelligence suite passes on the isolated cluster, including the workflow test below.

Accept before merge:

- Personal Intelligence migrations `385` and `387`–`398` are not in `migration-files.prod.txt`. `396` aborts unless the database is `kit_pi_validation`. `397` is on no manifest and has the same abort. Do not append this manifest to a production apply.
- `394` is allowed on `trading_agent` by its guard. The paper host requires it there. It still refuses `novixa_prod` and `trading_research`.
- `docs/novixa-production.env.example` documents `PersonalIntelligence__ResearchPublication__Root`. An empty directory makes the publication API return `MISSING`. It does not open exchange reads.
- Deploy scripts under `deploy/sb-research-safe-host-014` and `deploy/trading-research-v1` are historical notes. Merging them does not authorize running them.

Outside this diff, and not a reason to block this review: the 156 admin TypeScript errors, the 13 production-manifest files that fail on an empty database, and Content files numbered `394`–`398` that exist only on the dirty `E:\KitPlatform` tree.

Owner checklist before anyone merges:

1. Review the content narrative commit separately from the Personal Intelligence pack.
2. Keep `PersonalIntelligence:ExposeExchangeRead` unset or false on the KIT Platform API.
3. Do not apply `migration-files.personal-intelligence.txt` to `novixa_prod`.
4. Keep research writes on a database named `trading_agent`, never on port `5432` of an existing cluster.
5. Do not enable `ExchangeBackfillWorker`, a research timer, or `--live`.

## Workflow proof

`TradingAgentWorkflowTests.Fixed_candles_are_stored_replayed_and_recommended_without_an_order` is the acceptance test. Input is thirty fixed one-hour BTCUSDT bars from `2031-06-01T00:00:00Z`, source `workflow-poc`, classified by `ResearchCandles`. `ResearchSession.Run` uses `sma-cross-v1` and writes the paper session through `SqlResearchStore` on `trading_agent`. The first execution stores two paper fills, one BUY and one SELL. The same input inside the test, and a second process run, replay the stored session and add no fills. `TradingAgent.Recommend` receives those fills as observations and a proposal marked live; the returned proposal is paper, the claim kind is inference, and the rationale says it does not create a decision or an order. `DemoPaperExecution` refuses the recommendation without a separate approval and refuses a live proposal even when an approval object is supplied. No exchange client is called.

Repeat with the same cluster and `KIT_PI_TEST_CONNECTION` as the isolated validation section, then:

`dotnet test tests/KitPlatform.PersonalIntelligence.Tests --filter FullyQualifiedName~TradingAgentWorkflowTests`

This reuses research intake, the SMA strategy, the paper session, the research store, the recommendation function, and the paper-execution refusal. It does not add a task queue, a learning loop, or an agent that assigns its own work.

## Agent runtime boundary

Already present and used by the workflow: a structured candle intake, a deterministic session hash, a database allow-list, paper fill rows, a replay that writes nothing new, `recommend_only`, a risk verdict, and a separate approval check that still cannot send a live order. Authorization policies exist for research read, trading run, and trading approve, and the default API does not expose the exchange controllers.

Not built, and not required to reproduce this workflow: a generic task record, a capability grant separate from the configuration flag, a retrieval index, an orchestrator that chooses tools, a durable approval queue, or a retry worker. Those belong in a later specification. The first useful slice, if the owner wants one, is a read-only receipt that joins the stored session key, the recommendation rationale, and the refusal reason. It should not gain permission to place an order.

## Finalization

Date: 2026-10-26. Workspace `E:\kit-integrate-fed51d14`. No production deploy. No migration was run on port `5432` or on an existing database. Live trading, workers, timers, and Novixa Video were not changed. Source worktrees stayed at `8c59ceb4`, `85db8bdf`, `cbe72f3b`, and `36642381`.

Before the fast-forward, `origin/main` was `fed51d14` and `integrate/second-brain-20261026` was `b3bcafcd`. `fed51d14` is an ancestor of that commit, seven commits ahead, none behind. GitHub branch protection for `main` was not set, and the repository ruleset list was empty. The local ref named `main` points at `8c59ceb4`, which is neither an ancestor nor a descendant of `origin/main`. That ref was not moved and was not reset.

`origin/main` was updated by a fast-forward push of `b3bcafcd`. `git ls-remote` showed both `refs/heads/main` and `refs/heads/integrate/second-brain-20261026` at `b3bcafcd`. There is no merge commit.

Checks on `b3bcafcd` before that push: API build 0 errors and 48 warnings; `KitPlatform.PersonalIntelligence.Tests` 194 passed, 0 failed; `NarrativeFitIntegrityTests` 13 passed, 0 failed. The paper workflow, paper-integrity, and migration source tests `394`, `396`, and `397` were inside that suite and also passed as a focused run of 7. `397` is still on no manifest. `396` still aborts unless the database is `kit_pi_validation`. `388` still drops the dependent foreign keys before replacing the unique constraints. `389` still uses `IF NOT EXISTS`. None of `385`–`398` are in `migration-files.prod.txt` or the content manifest.

The first suite run after the fast-forward failed one test: `ExchangeConnectionApiTests.Lifecycle_encrypts_isolates_snapshots_and_does_not_create_trading_records`, expected `Forbidden`, actual `NotFound`, with 193 passed. The account lookup is tenant-scoped and returns 404 when the other tenant may call it. The module filter returns 403 when `personal_intelligence` is off for tenant B. Parallel tests change that flag. The production lookup was not changed. The test now accepts only those two refusal statuses and rejects a body that contains the account equity or the fixture credential. A later full run passed 194, 0 failed. The API build after that run had 0 errors.

The commit that adds this section also contains that test assertion. It is fast-forwarded to `origin/main` and to `integrate/second-brain-20261026` after the 194-pass run. It does not add an Agent Runtime, a learning loop, or permission to place an order.

Still outside this merge: 156 admin TypeScript errors that were already on `fed51d14`, 13 production-manifest files that fail on an empty database, and Content migrations numbered `394`–`398` that exist only on the dirty `E:\KitPlatform` tree. Those Content files were not brought in.

## Execution receipt V1

Date: 2026-10-26. Implemented on `integrate/second-brain-20261026` from `ade5ab28`. No migration, no `Program.cs` change, and no change to `TradingOperationalLoop`.

`ResearchSnapshot` now carries an optional `ExecutionReceipt`. `ExecutionReceiptProjector` builds that object from the research intake, the paper proposal the agent returned, the risk verdict on that paper proposal, and `DemoPaperExecution` without an approval. `ResearchSession.SaveReceipt` writes it into the existing `research_trading.session_result.result` row when the session key and payload hash still match. The writer is `IResearchLog.TryUpdateResult`. It does not insert a session or a fill. A snapshot JSON that omits `Receipt` still deserializes, and `ExecutionReceiptProjector.Read` reports `Recorded` false.

The fixed `workflow-poc` run stores outcome `approval_required`, autonomy `recommend_only`, kind `Inference`, mode `Paper`, research fill count 2, and `provenance.database` `trading_agent` with `tenantId` null. Replay keeps that receipt and adds no fill. A separate live call is refused and does not change the stored outcome. `trading_agent` still has no `pi_decision`, `trading_order`, `pi_knowledge`, or `platform_events` table.

`KitPlatform.PersonalIntelligence.Tests` after this change: 195 passed, 0 failed. That includes the workflow test, the old-snapshot test, the three paper-integrity tests, and the migration source tests for `394`, `396`, and `397`. No migration was executed. The receipt is not exposed on the platform API.

## Agent Runtime V1

`origin/main` was fast-forwarded from `ade5ab28` to `23b2b69d` after the receipt tests passed again. The local ref `main` was left at `8c59ceb4`. Runtime V1 was then added on the integration branch. It is not a migration and it is not registered in `Program.cs`.

`AgentRuntime` checks task transitions, dispatches tools only after permission, tenant, effect, and approval checks, and returns the first receipt for the same task key and payload hash. `TradingResearchRuntime` is the trading tool host. The fixed `workflow-poc` task ends `AwaitingApproval`, with recommendation outcome `approval_required`, research fill count 2, and `recommendationExecuted` false. A live proposal is rejected inside the execute tool even when an approval object is present. The task trace is stored on the existing research snapshot.

After these tests were added, `KitPlatform.PersonalIntelligence.Tests` passed 204 of 204 and `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` finished with 0 errors. No worker was started and no migration was run. The local steps are in `AGENT-RUNTIME-V1.md`.

## Runtime merge and task storage

Before the merge, at `c63f57b0`, the runtime filter passed 9 of 9, the Personal Intelligence suite passed 204 of 204, and the API build finished with 0 errors and 48 warnings. `origin/main` had moved to `4399cc80` by two generated stats snapshots, `ce6ff278` and `4399cc80`. Those files do not overlap the runtime. The branch merged them as `e3abaf50`, and the suite passed 204 of 204 on that tree. Pull request 7 merged to `origin/main` `2b870c4a`. That commit has the same tree as `e3abaf50`. Local `main` was not moved.

The merged runtime log is `MemoryAgentRunLog`. It does not survive a process restart. `pi_operation` stays the trading-loop receipt and is deserialized as `TradingLoopResult`, so it is not the task store. `AgentTaskTrace` on the research snapshot does not carry the payload hash, step results, or a tenant, and `AgentRuntime` does not load it. No tenant column was added to `research_trading`. No migration was run.

A file-reload prototype on this branch, after `2b870c4a`, passed 7 tests by reading the receipt into a new log instance. Same payload replays, a different payload conflicts, approval wait and failure details remain, a completed task does not run again, another tenant is refused, and a live tool is still rejected. That file is a test double. It is not SQL, and a database restart test has not passed. The proposed `pi_agent_task` table is described in `AGENT-RUNTIME-V1.md` and was not created.

After the prototype tests, `KitPlatform.PersonalIntelligence.Tests` passed 211 of 211 and the API build again finished with 0 errors and 48 warnings. `Program.cs` still does not register the runtime or start a worker. The prototype commit is on the integration branch and is not the commit merged by pull request 7.
