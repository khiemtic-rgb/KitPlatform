# Agent Runtime V1

Status: Agent Runtime V1 is on `origin/main` at `2b870c4a`. The PostgreSQL task log and the authenticated task API are on `integrate/second-brain-20261026`. This is not a hosted worker and not a live trading path. `LiveAccepted` stays false.

## Components

```text
AgentRequest
  -> AgentRuntime
       -> AgentTaskTransitions
       -> ToolDispatcher
            -> ToolRegistry
                 -> research.session        paper, no approval
                 -> trading.recommend       read, no approval
                 -> trading.paper_execute   paper, approval required
                 -> runtime.record          read, writes the existing snapshot
       -> MemoryAgentRunLog
```

KIT Platform still owns tenant, user, and permission. The runtime does not grant a permission and does not accept a different tenant in the request. Personal Intelligence owns the task, the plan, and the receipt. Trading is the tool host in `TradingResearchRuntime`. The research ledger stays on database `trading_agent` and still has no tenant id.

## What V1 does

A task moves only through `AgentTaskTransitions`. `Received` cannot become `Completed`. A completed task is reached only from `Verified`, and only when every step succeeded. A blocked approval leaves the task at `AwaitingApproval`.

The dispatcher checks the tool contract before the handler runs. A missing permission, a tenant mismatch, or a missing approval does not call the handler. `ToolEffect.Live` is rejected even when an approval object is present. The trading execute handler also rejects a proposal whose requested mode is live, so approval cannot turn that call into an order.

The same task key and payload hash returns the first receipt and does not call the tools again. A different payload for that key throws. A thrown tool becomes `Failed`, and later steps do not run. There is no worker, timer, or retry loop. The local budget is eight steps.

The trading slice stores the recommendation in the existing `ResearchSnapshot` receipt and stores `AgentTaskTrace` beside it. That is the same `session_result` row, not a new table. Research fills and `recommendationExecuted` stay separate fields. No migration was added.

## Local run

Start the isolated cluster on `127.0.0.1:5433` only. Set `KIT_PI_TEST_CONNECTION` to database `kit_pi_validation` on that port. Do not use port `5432`.

```text
dotnet test tests/KitPlatform.PersonalIntelligence.Tests --filter FullyQualifiedName~AgentRuntime
dotnet test tests/KitPlatform.PersonalIntelligence.Tests
dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj
```

The workflow test uses the fixed `workflow-poc` candles from `2031-06-01T00:00:00Z`. Its task key is `runtime-workflow-poc`. The expected task status is `AwaitingApproval`, the recommendation outcome is `approval_required`, and the research fill count stays 2.

## Not in V1

No learning loop, no policy rewrite, and no tenant column on `research_trading`. `pi_operation` remains the tenant trading-loop receipt and is not the runtime log. The research snapshot stores the trading outcome. `AgentRuntime` does not resume a `Running` row.

## Durable task state

`MemoryAgentRunLog` still ends when the process drops it. `ResearchSnapshot.TaskTrace` remains a trading outcome on `trading_agent` and is not the task log. `pi_operation` remains the trading-loop receipt and is still deserialized as `TradingLoopResult`.

`PostgresAgentRunLog` stores the task in `pack_personal_intelligence.pi_agent_task`. Migration `399_pack_personal_intelligence_agent_task_v1.sql` creates that table. The tenant column references `public.tenants(id)`, the same tenant row the platform resolves from the `tenant_id` claim. The unique key is `(tenant_id, task_key)`. `revision` is the optimistic concurrency token: an update matches the revision it read, and a stale save changes zero rows. The receipt JSON stores the plan, step results, approval marker, outcome, error, and evidence. Every read and write includes `tenant_id`. There is no row level security, matching migration 389.

Migration `400_pack_personal_intelligence_agent_task_reconciliation_v1.sql` adds `ReconciliationRequired` to the status check. It refuses `novixa_prod`, `trading_research`, `trading_agent`, `kitplatform`, `postgres`, and `kit_pi_validation`. Neither `399` nor `400` is listed in `migration-files.prod.txt`, `migration-files.personal-intelligence.txt`, `migration-files.trading-research.txt`, or the content, family, kit-sales, and local-os manifests. `399` was applied only to the new database `kit_pi_agent_task` on `127.0.0.1:5433`. `400` is applied by the task-store fixture on that same database when the check does not yet name `ReconciliationRequired`. `kit_pi_validation` and `trading_agent` do not receive these migrations. `research_trading` was not changed.

A new log claims the row before any tool runs and holds a session advisory lock until the receipt is terminal. The other caller with the same tenant and key waits, then reads the stored receipt and does not run tools. A different payload hash throws before tools run. After each step the log writes the current receipt. A new log that finds a row returns it and does not continue the plan.

## What the PostgreSQL tests showed

`PostgresAgentTaskTests` uses `kit_pi_agent_task`. These are not the file-reload tests:

- A new `PostgresAgentRunLog` reads the stored receipt, including the plan and evidence, and does not call the tool.
- `AwaitingApproval` stays waiting on a new log. The earlier read stays at one call and the approval tool stays at zero.
- A completed task does not run again.
- A failed task still has the failed step and the error `boom`.
- The same tenant, key, and hash does not call the tool a second time.
- A different hash throws and does not call the tool.
- Another tenant's log cannot read the row. Writing that receipt through the other tenant's log throws. The owner's summary stays unchanged.
- Two overlapping callers create one row and call the tool once.
- A revision bump during the tool makes the save fail. The stored status is not `Completed`, and a new log does not call the tool.
- An exception after the first committed step leaves `Running` with one step. A new log does not mark it `Completed` and does not call the tool.
- A separate process, `KitPlatform.PersonalIntelligence.AgentTaskProbe`, exits after that same committed step. The test process then reads `Running`, not `Completed`, and does not call the tool.
- A live tool is rejected. `LiveAccepted` stays false in the stored receipt.

The file-reload tests still pass. They are not evidence for PostgreSQL.

## Crash window

The saved row is recovered. A tool that has returned, but whose update has not committed, is not in that row. The next caller can see the last committed steps and the missing step. It does not treat that gap as success. This is not exactly-once execution. The log must be disposed, or the process must exit, before another caller can take the advisory lock.

## Unknown tool effect

`ToolResponse.Uncertain` stops the plan. The task becomes `ReconciliationRequired`, later steps do not run, and a replay returns that receipt without calling the tool. This is not `Completed` and it is not a retry. `AwaitingApproval` also stays a waiting status. Neither status is treated as success.

## Product task API

`POST /api/personal-intelligence/tasks` and `GET /api/personal-intelligence/tasks/{taskKey}` use the platform JWT, `tenant_id` claim, and `DbPermissionsClaimsTransformation`. The body cannot choose the tenant or the payload hash. `RequestedTenantId` must match the token tenant. POST requires `personal_intelligence.trading.run` or `ADMIN`, plus the `personal_intelligence` module. GET requires `personal_intelligence.research.read`, the trading-run permission, or `ADMIN`, plus the module. The service grants the research tool when the caller already holds trading-run or `ADMIN`, matching the research-read policy. An approval decision is recorded only when the body sends `approvalDecisionId` and the caller holds `personal_intelligence.trading.approve` or `ADMIN`. `ApprovedBy` is the token subject.

The only accepted kind is `trading-paper`. The server owns the plan and the fixed `workflow-poc` candles. `executionMode` `paper` or empty stays paper. `live` is passed to the existing execute tool, which rejects it, and the receipt keeps `liveAccepted` false. Any other mode is 400.

`AgentRuntime:TaskConnection` is the PostgreSQL task log. `AgentRuntime:ResearchConnection` must be database `trading_agent`. Both refuse port `5432` before a task or research connection opens. If either setting is missing, or the port is refused, the API returns 503. A payload conflict is 409. A tenant or permission refusal is 403. An unknown task is 404. Anonymous calls are 401. The trading-loop route stays unmapped while `PersonalIntelligence:ExposeExchangeRead` is false.

The response is `AgentTaskView`: task key, payload hash, tenant, status, outcome, research fill count, recommendation executed, live accepted, replayed, approval recorded, error, evidence, and steps. It does not include the revision.

## What the product tests showed

`KitPlatform.PersonalIntelligence.ApiTests`, 14 passed and 0 failed, against `kit_pi_validation`, `kit_pi_agent_task`, and `trading_agent` on `127.0.0.1:5433`:

- Anonymous POST is 401. A token whose user has no stored permission is 403. Tenant B with the module off is 403.
- An unapproved trading task is stored as `AwaitingApproval`, outcome `approval_required`, research fill count 2, `recommendationExecuted` false, and `liveAccepted` false. The execute step is `Blocked`. The same POST replays. GET returns the same hash and tenant.
- A different objective for the same key is 409.
- An approved paper task reaches `Completed` and `paper_executed`. An approved live mode is `Failed`, the execute step is `Rejected`, and `liveAccepted` stays false.
- Tenant B, with the module enabled only for the request and then restored, gets 404 and the body does not contain tenant A's objective.
- Two concurrent POSTs leave one row. Both receipts show two research fills, and one is a replay.
- A second host reads the stored `AwaitingApproval` row and does not complete it. That second host is another process in the same test run, not a separate operating-system crash. The process-exit evidence remains `AgentTaskProbe`.
- A task connection that names port `5432` returns 503 and is refused before the task store opens.
- `POST /api/personal-intelligence/trading/loops` stays 404.

`KitPlatform.PersonalIntelligence.Tests`: 228 passed, 0 failed. That includes the PostgreSQL task tests, the process probe, the uncertain-effect tests, the read-step recovery tests, and the manifest check that `400` is absent.

Three API tests start a separate `KitPlatform.Api` process on `127.0.0.1` ports `5391`–`5398`. They are not another host inside the test process. `ResearchSafeHost:SuppressBackgroundWriters` is true, and the captured log does not contain `Integration outbox worker started`. `AgentRuntime:AllowCrashHook` is false unless that test sets it. The hook is ignored without the flag, so a production process does not exit on those environment names.

- Exit 86 after `research.session` returns and before the step commit leaves `Running` with no succeeded research step. The next process records `ReconciliationRequired`, does not call the tool, and does not add a research session or paper operation. `LiveAccepted` stays false.
- Exit 87 after the research step commits leaves that step in place. A second process GET shows `Running` and does not continue it. The following POST runs the read recommendation only, blocks paper execution for lack of approval, and ends `AwaitingApproval` with research fill count 2. A repeat POST is a replay. The research session count stays the same.
- A process that holds the advisory lock after the research commit makes the next process return 503, `The task is still running for another caller.` It does not add a session. After the holder exits, a new process continues the read step to `AwaitingApproval`.

`GET` shows the last committed receipt and does not recover it. `POST` of the same payload is the recovery path. There is no timer or worker. A `Running` task stays observable until that POST. The lock wait is 15 seconds.

## How to run the product tests

Set `KIT_PI_TEST_CONNECTION` to `Host=127.0.0.1;Port=5433;Database=kit_pi_validation;Username=postgres`. The API fixture rewrites that string to `kit_pi_agent_task` and `trading_agent` and refuses any other port or database. Do not point it at port `5432`.

```text
dotnet test tests/KitPlatform.PersonalIntelligence.ApiTests
dotnet test tests/KitPlatform.PersonalIntelligence.Tests
dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj
```

`AddAgentTasks` registers the scoped task service only. It does not add a worker. Production leaves `AgentRuntime:TaskConnection` unset, so the route cannot open a task store until that isolated database is configured.

## Recovery policy

The next POST of the same tenant, key, and payload handles a `Running` row. It keeps every committed step. A missing step whose contract is `ToolEffect.Read` may run again, because a repeated read does not create a new side effect. After that read commits, later steps in the same locked call are first attempts. A missing step whose contract is `Paper` or `Live`, or whose tool is not registered, becomes `ReconciliationRequired` and is not called. `trading.recommend` is the read step. It rereads the committed research session and does not write one. `research.session`, `trading.paper_execute`, and `runtime.record` are paper steps and are not repeated from an uncommitted gap.

`AwaitingApproval`, `Failed`, `Cancelled`, `Completed`, and `ReconciliationRequired` are returned unchanged. `Completed` does not run again. A `Verified` row with every step already committed is moved to `Completed` without calling tools. Two callers take one advisory lock. The waiter gets 503 instead of starting a second effect.

## Recovery limits

A committed step survives process exit. An uncommitted side effect is not exactly-once and is not retried. A read that was not committed can run once more. `Running` is not completed by GET and is not swept by a worker. Manual reconciliation is required before an uncommitted paper or live step may be attempted again. `LiveAccepted` stays false. The crash hook is a test switch and is off unless `AgentRuntime:AllowCrashHook` is true.
