# Agent Runtime V1

Status: implemented on `integrate/second-brain-20261026` after `origin/main` `23b2b69d`. This is not a hosted worker and not a live trading path.

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

No learning loop, no policy rewrite, no hosted API for the task receipt, and no tenant column on `research_trading`. `pi_operation` remains the tenant trading-loop receipt and is not the runtime log. The runtime log in this slice is in memory; the durable copy of the trading outcome is the research snapshot.
