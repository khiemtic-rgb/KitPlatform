# Agent Runtime V1

Status: Agent Runtime V1 is on `origin/main` at `2b870c4a`. The PostgreSQL task log is on the integration branch after that commit. It is not on `main`. This is not a hosted worker and not a live trading path.

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

No learning loop, no policy rewrite, no hosted API for the task receipt, and no tenant column on `research_trading`. `pi_operation` remains the tenant trading-loop receipt and is not the runtime log. The runtime log in the merged slice is in memory. The research snapshot stores the trading outcome, not a task that can be resumed by `AgentRuntime`.

## Durable task state

`MemoryAgentRunLog` still ends when the process drops it. `ResearchSnapshot.TaskTrace` remains a trading outcome on `trading_agent` and is not the task log. `pi_operation` remains the trading-loop receipt and is still deserialized as `TradingLoopResult`.

`PostgresAgentRunLog` stores the task in `pack_personal_intelligence.pi_agent_task`. Migration `399_pack_personal_intelligence_agent_task_v1.sql` creates that table. The tenant column references `public.tenants(id)`, the same tenant row the platform resolves from the `tenant_id` claim. The unique key is `(tenant_id, task_key)`. `revision` is the optimistic concurrency token: an update matches the revision it read, and a stale save changes zero rows. The receipt JSON stores the plan, step results, approval marker, outcome, error, and evidence. Every read and write includes `tenant_id`. There is no row level security, matching migration 389.

The migration is not listed in `migration-files.prod.txt`, `migration-files.personal-intelligence.txt`, or `migration-files.trading-research.txt`. It was applied only to a new database, `kit_pi_agent_task`, on `127.0.0.1:5433`. `kit_pi_validation` and `trading_agent` do not have the table. `research_trading` was not changed.

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

The saved row is recovered. A tool that has returned, but whose update has not committed, is not in that row. A later log returns the last committed receipt and does not retry the missing step, so the task cannot be marked `Completed` from that gap. This is not exactly-once execution. A `Running` receipt is not resumed. The log must be disposed, or the process must exit, before another caller can take the advisory lock.

## Product entry still missing

`Program.cs` does not register `AgentRuntime`. There is no endpoint that accepts a task. The next product step is to point `PostgresAgentRunLog` at an approved Personal Intelligence database behind the existing permissions. It still must not start a worker, must not accept live execution, and must not add a tenant receipt API before the ownership model is decided.
