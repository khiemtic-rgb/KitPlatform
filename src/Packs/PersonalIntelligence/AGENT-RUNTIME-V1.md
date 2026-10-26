# Agent Runtime V1

Status: Agent Runtime V1 is on `origin/main` at `2b870c4a` through pull request 7. That commit does not include durable task storage. The file-reload prototype below is on the integration branch after that merge. This is not a hosted worker and not a live trading path.

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

What survives today:

- `MemoryAgentRunLog` keeps the full receipt, including the payload hash, status, steps, and error, until the process drops it.
- `ResearchSnapshot.TaskTrace` is written into `research_trading.session_result` on database `trading_agent`. It records the task key, a status string, the recommendation outcome, the research fill count, and whether the recommendation executed. It does not record the payload hash, the plan, step errors, or a tenant.
- `pi_operation` stores one trading-loop result per tenant and operation key. A retry deserializes that JSON as `TradingLoopResult`. It is not a task table.

Those stores do not give the runtime a task to resume. A new `MemoryAgentRunLog` runs the plan again. The research session itself stays replay-safe, so the unapproved slice does not add a fill, but that is the research ledger, not the task log.

`pi_operation` cannot be reused. Its result reader expects `TradingLoopResult`, its key space belongs to the trading loop, and a stored row is the loop receipt rather than a task that can remain `AwaitingApproval`. `research_trading` cannot be the task store either: its key is the session hash, it has no tenant, and adding a tenant id there is a separate decision.

The next store, not created and not migrated in this change:

```text
pack_personal_intelligence.pi_agent_task
  task_id        UUID PRIMARY KEY
  tenant_id      UUID NOT NULL REFERENCES public.tenants(id)
  task_key       TEXT NOT NULL
  payload_hash   TEXT NOT NULL
  status         TEXT NOT NULL
  receipt        JSONB NOT NULL
  created_at     TIMESTAMPTZ NOT NULL
  updated_at     TIMESTAMPTZ NOT NULL
  UNIQUE (tenant_id, task_key)
```

The receipt JSON holds the plan, step results, tool errors, evidence refs, and `LiveAccepted`. A different payload hash for the same tenant and key is a conflict. A different tenant does not read the row. No row level security is proposed here; that matches migration 389, where RLS stays deferred. The table belongs on the Personal Intelligence database that already has `public.tenants` and `pi_operation`. It does not belong on `trading_agent`, and it does not belong in `migration-files.prod.txt` until a later decision. No migration file was added.

## File-reload prototype

`AgentRuntimeRestartTests` writes the receipt to a temporary JSON file and reads it back with a new log instance. Seven tests passed: same payload returns the stored receipt, a different payload throws, `AwaitingApproval` stays waiting and does not repeat the earlier step, a completed task does not run again, a failed task still names step `two` and the error `boom`, another tenant does not receive the stored receipt, and a live tool is still rejected. This is not a database restart and not a new operating-system process. A SQL restart test has not passed because the table above does not exist.

The same change makes `AgentRuntime` refuse a stored task key when the caller tenant differs. That check is not in `origin/main` `2b870c4a`.

## Product entry still missing

`Program.cs` does not register `AgentRuntime`. There is no endpoint that accepts a task. The next product step, after the table is approved, is a tenant-scoped log behind the existing Personal Intelligence permissions. It still must not start a worker, must not accept live execution, and must not add a tenant receipt API before the ownership model is decided.
