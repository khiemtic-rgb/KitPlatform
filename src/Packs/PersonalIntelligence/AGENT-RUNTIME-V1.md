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

The response is `AgentTaskView`: task key, payload hash, tenant, status, outcome, research fill count, recommendation executed, live accepted, replayed, approval recorded, error, evidence, steps, approver, approval time, execution mode, next action, and decisions. It does not include the revision.

`nextAction` depends on the caller's permission. `AwaitingApproval` is `approve` when the caller holds `personal_intelligence.trading.approve` or `ADMIN`, and `wait_for_approval` otherwise. `ReconciliationRequired` is `manual_reconciliation` for that same approver and `wait_for_reconciliation` otherwise. `Running`, `Received`, `Planned`, and `Verified` are `wait`. `Completed`, `Failed`, and `Cancelled` are `none`. `Running` does not expire. There is no lease and no worker.

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

## Task lifecycle

Ordinary `POST /api/personal-intelligence/tasks` still identifies a task by kind, objective, completion criterion, execution mode, and any approval id sent on that first request. Sending a later approval id on that route is a different payload and returns 409. It does not create a second task and does not continue the waiting one.

`POST /api/personal-intelligence/tasks/{taskKey}/approval` continues the stored task. The body carries `approvalDecisionId` only. The stored payload hash and execution mode stay as they were. The approver is the token subject. The tenant is the token tenant. The caller must hold `personal_intelligence.trading.approve` or `ADMIN`, and the module must be on. Another tenant gets 404. A caller with trading-run but not approve gets 403.

The route accepts `AwaitingApproval`, and a `Running` row that still has a blocked approval step. It moves `AwaitingApproval` to `Running`, dispatches only a step whose stored status is `Blocked` and whose contract requires approval, then continues. A `Blocked` step was stopped by the dispatcher before the handler, so that paper effect had not happened. Steps already `Succeeded` are not called. The same decision id replays the stored receipt and does not append another decision. A different decision id conflicts. Two callers take the same advisory lock. `Completed` and `Failed` replay only the same decision id. `ReconciliationRequired` cannot be approved. A stored `Live` mode is still rejected by the execute tool, and `liveAccepted` stays false. The approval body cannot change paper into live.

`POST /api/personal-intelligence/tasks/{taskKey}/reconciliation` is the V1 record for an uncertain effect. It requires the approve permission. `occurred` and `absent` require an operator note of at most 500 characters. The decision, actor, time, note, check source, and observed result are appended to the receipt. The status stays `ReconciliationRequired`. No tool runs, and choosing `absent` does not call the paper or live step again. `unknown`, a blank note, or a note that is too long returns 400 and leaves the row unchanged. The same actor, disposition, note, check source, and observed result replay. A different disposition conflicts and does not replace the first decision. Two concurrent requests take the advisory lock and leave one decision.

The note is the operator's claim. It is not the observed result. When the receipt has an approval decision, the server reads `research_trading.paper_operation` for the task key `task:{tenant}:{taskKey}:{decision}` and records `occurred`, `absent`, or `unknown`. A claim that disagrees with that row is stored as a disagreement. The task still does not move. Without an approval decision there is no task-scoped row to check, so the check source is `not_available` and the observed result is `unknown`.

V1 does not complete or continue a `ReconciliationRequired` task. Completing it would mark an uncommitted step succeeded or skip later steps. Continuing it would call a side-effecting tool. Neither rule is safe. The operator reads the task result link, the research snapshot, and the paper operation, records the claim, and leaves the task blocked.

## Task evidence

The task receipt is the task result. `result.taskOutcome` is that result. The research snapshot is the evidence written when `runtime.record` ran, and `result.snapshotOutcome` cites it. Approval does not update that snapshot. A paper execution is claimed only after `research_trading.paper_operation` contains a row whose key is `task:{tenant}:{taskKey}:{decision}`, whose payload hash is the task hash, and whose status is `paper_filled`. That key is `result.paperOperationKey`. `recommendationExecuted` is true only in that case. Research fills in the snapshot are not that row. `liveAccepted` stays false. Another tenant receives 404 and does not see the key.

The order is the research session first, then the snapshot written by `runtime.record`, then the approval time, then the task paper operation when approval actually stores one. A process that exits after a tool returns and before the task step commits still leaves the task in `ReconciliationRequired` and does not call the tool again.

Valid status moves remain: `Received` to `Planned`, `Failed`, or `Cancelled`; `Planned` to `Running`, `Failed`, or `Cancelled`; `Running` to `AwaitingApproval`, `Verified`, `Failed`, `Cancelled`, or `ReconciliationRequired`; `AwaitingApproval` to `Running`, `Failed`, or `Cancelled`; `Verified` to `Completed` or `Failed`. `Completed` does not return to `Running`.

## What the lifecycle tests showed

Measured on `127.0.0.1:5433` after the approval and reconciliation routes:

- `KitPlatform.PersonalIntelligence.ApiTests`: 19 passed, 0 failed. An unapproved paper task stays `AwaitingApproval` with next action `approve`. A second POST that adds an approval id conflicts and leaves that hash in place. The approval route then reaches `Completed` and `paper_executed` and keeps the research step summary. It does not add a research session. The task paper operation added by that route is covered under Task evidence. Replay of the same decision does not change the revision. A different decision conflicts. A caller without approve permission gets 403 and sees `wait_for_approval`. Another tenant gets 404. `Completed` and `Failed` reject a new decision. A stored live task stays `Live`, ends `Failed`, and keeps `liveAccepted` false. Two concurrent approvals leave one decision. Reconciliation without evidence or with `unknown` does not change the revision. A recorded `absent` decision stays on the row, a different disposition conflicts, and the ordinary POST does not retry it.
- `KitPlatform.PersonalIntelligence.Tests`: 230 passed, 0 failed. The runtime test calls the blocked paper step once and does not call the committed read again. The reconciliation test leaves the uncertain step in place.
- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors.

The research snapshot written before approval is not rewritten, because `runtime.record` had already committed. The task receipt is the approval result. This is not a production-readiness claim. It is not exactly-once, and an uncertain paper or live effect still needs a person.

## What the evidence tests showed

Measured on `127.0.0.1:5433` after the task result link:

- `KitPlatform.PersonalIntelligence.ApiTests`: 21 passed, 0 failed. After approval, the task outcome is `paper_executed`, the snapshot outcome on the session row is still `approval_required`, and one task-scoped paper operation matches the task hash. Replay does not add another operation. Another tenant gets 404 and the body does not contain the operation key. Two reconciliation requests leave one decision and do not add a paper operation. An operator note of `absent` stays the claim when the stored row is `occurred`. The status stays `ReconciliationRequired`.
- `KitPlatform.PersonalIntelligence.Tests`: 231 passed, 0 failed. The memory runtime keeps the snapshot at `approval_required`, stores one task paper operation, and a replay does not store a second one. A rejected live proposal stores no task paper operation.
- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors.

No migration was added. No worker was added. This is not a production-readiness claim.

## Product capability

The task API now checks the business goal before it will call a task `Completed`. `AgentRequest.VerifyGoal` is set by that API. The runtime reaches `Verified` when every step succeeded, then moves to `Completed` only if the last succeeded step output `goalMet=true`. Otherwise it moves to `Failed` and keeps the `goalReason`. `AwaitingApproval` stays waiting. `goal.met` on the receipt is true only when the status is `Completed` and that output is true. Running every step is not itself success.

The shared outputs are `goalMet`, `goalReason`, `scope`, `fact`, `inference`, `assumption`, `missing`, and `limit`. The kernel does not know trading rules. `TradingResearchRuntime.PlanFor` builds the trading plan from the objective and the completion text. Each step carries its purpose:

```text
research.session       collect the permitted workflow-poc session
trading.recommend      infer a paper recommendation; this is not an execution
trading.assess         read; compare the completion text with that session
trading.paper_execute  one task-scoped paper operation, and only after approval
runtime.record         store the research snapshot as history
```

`trading.assess` fails closed, and does not call paper execution, when the objective or completion text asks for profit, a percent, a win rate, or `lợi nhuận`, or when the permitted session has no fills. The receipt lists that text under `goal.missing` and still cites the fills that were actually read. It does not invent a profit figure. A recommendation remains an inference. Research fills remain observations. `paper_executed` still requires the task-scoped `paper_operation` row. `LiveAccepted` stays false.

The response adds `objective`, `doneWhen`, each step `purpose`, and `goal`: scope, met, reason, facts, inferences, assumptions, missing, and limits. A recovery that already committed `research.session` may run `trading.assess` again because it is a read, then blocks paper execution until approval. An uncommitted paper gap is still `ReconciliationRequired` and is not retried.

## What the capability tests showed

Measured on `127.0.0.1:5433` after the goal check:

- `KitPlatform.PersonalIntelligence.ApiTests`: 22 passed, 0 failed. An unapproved paper task stays `AwaitingApproval` with `goal.met` false and a research step whose purpose names the objective. Approval then reaches `Completed`, `goal.met` true, `paper_executed`, and one task paper operation for the task hash. The snapshot outcome stays `approval_required`. Replay does not add another operation. A completion text of `Profit is at least 10 percent.` ends `Failed`, not `Completed`, with that text under `goal.missing`, a `workflow-poc` fact, no succeeded execute step, and no paper operation key. Replay does not add a paper operation. The earlier process-exit tests, exit 86, exit 87, and the held lock, are inside this same 22.
- `KitPlatform.PersonalIntelligence.Tests`: 234 passed, 0 failed. A succeeded step that reports `goalMet=false` stays `Failed` when verification is on. `goalMet=true` can complete. An approved trading request whose completion text cannot be proved calls execute zero times and stores no task paper operation. The memory approval path still keeps the snapshot at `approval_required`, stores one task paper operation, and reports `goal.met` true only after that operation.
- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors.

No migration was added. No worker was added. This is not exactly-once and not a production-readiness claim. The isolated databases do not make the path production-ready.

## Shared task contract

Both task kinds use the same runtime, the same `goalMet` outputs, and `GoalContract`. A completion condition is verifiable only when it equals a criterion id or statement that the domain registered. Any other text, including `Make it good.` or a profit sentence, is not interpreted and is not marked met. The kernel still does not contain trading or evidence rules.

`trading-paper` registers `no-live-order`: `The receipt names the outcome and does not claim a live order.` Its plan is research, recommend, assess, paper execute, and record. An undeclared condition produces only the assess step, so the research ledger and paper operation are not written. `workflow-poc` remains a fixture. `LiveAccepted` stays false. A research fill is still not a paper operation.

`evidence-review` registers two read-only criteria: `observation-present` and `fact-present`. It reads `pack_personal_intelligence.pi_evidence` for the caller tenant only, from database `kit_pi_validation` on a port other than `5432`. It counts `OBSERVATION` or `FACT` rows. Tenant A has observation rows and no fact rows, so the observation criterion can complete and the fact criterion stays unmet even though both steps succeed. An unknown sentence does not query the table. The review does not accept live mode or an approval decision, and it does not write a paper operation. The caller still needs `personal_intelligence.trading.run` because the isolated permission catalog has no separate evidence permission. The service grants `personal_intelligence.research.read` from that existing permission.

## What the shared-contract tests showed

Measured on `127.0.0.1:5433`:

- `KitPlatform.PersonalIntelligence.ApiTests`: 24 passed, 0 failed. The trading approval path still reaches `Completed` with `goal.met` true and one task paper operation. An undeclared profit sentence does not run research or execute. Evidence review of the stored observation rows completes and names `pi_evidence`. A fact criterion stays `Failed` with `FACT evidence rows` missing. `Make it good.` does not collect. Replay does not change the revision. Tenant B reads 0 observation rows and does not receive tenant A's `source_ref`. The earlier process-exit tests remain inside this run.
- `KitPlatform.PersonalIntelligence.Tests`: 237 passed, 0 failed. A declared trading sentence is not an evidence criterion. An evidence count of 2 can complete, a second call does not read again, and a count of 0 does not.
- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors.

No migration was added. No worker was added. This is not free-form business language, not exactly-once, and not a production-readiness claim.

## Goal understanding

A `goal` task accepts structured completion conditions. The `doneWhen` sentence is stored and is not parsed. `Make it good.` with no condition is `not-understood`. A condition is `ready-to-plan` only when its capability, subject, relation, and target match the registered catalog. The only registered capability is `evidence.count`, which reads `OBSERVATION` or `FACT` rows from `pack_personal_intelligence.pi_evidence` for the caller tenant. `atLeast` compares that count with a positive integer. `greaterThan` compares two stored kinds. The server builds the plan. A client-supplied step list is not executed, and an unknown capability such as `evidence.payload` is `missing-capability` rather than a new tool.

The same receipt then distinguishes a plan that was executed from a goal that was proved. A read whose count misses the condition finishes `executed-unproven` with `goal.met` false, even when every step succeeded. `proven` requires every condition to hold for a count that was read. `missing-data` covers a subject or target the catalog does not read, and that path does not query the table.

`personal_intelligence.evidence.read` is a separate permission from `personal_intelligence.trading.run`. The tool dispatcher and the service both require it before `pi_evidence` is opened. A trading-only caller is refused and no task row is written. An evidence-only caller can run the goal and cannot start a trading-paper task. The isolated validation fixture inserts the permission and the `PI_EVIDENCE` role. No production migration was added.

The registered `trading-paper` and `evidence-review` paths still use their exact sentences. `workflow-poc` remains a fixture. `LiveAccepted` stays false.

## What the understanding tests showed

These checks are deterministic. No model-generated plan was scored.

Measured on `127.0.0.1:5433`:

- `KitPlatform.PersonalIntelligence.ApiTests`: 28 passed, 0 failed. The stored tenant A table had more `OBSERVATION` rows than `FACT` rows, and the `FACT` count was 0. A structured comparison that is not a registered sentence completed with `understanding` `proven`, cited both counts and `pi_evidence`, and did not write a paper operation. Asking for both kinds to be at least 1 stayed `executed-unproven` because the fact count was 0. `Make it good.`, `evidence.payload`, and subject `OPINION` did not collect. A trading-only caller received 403 and no task row. An evidence-only caller completed the comparison and was refused for trading paper. Tenant B's comparison did not contain tenant A's `source_ref`. The trading approval path and the earlier process-exit tests remain inside this run.
- `KitPlatform.PersonalIntelligence.Tests`: 243 passed, 0 failed. A client plan that names `trading.paper_execute` is ignored. A trading permission does not call the evidence ledger. Replay does not read again.
- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj`: 0 errors.

No migration was added to a deployment manifest. No worker was added. This is not free-form business language, not exactly-once, and not a production-readiness claim. The catalog can compose counts for two stored kinds. It cannot interpret an arbitrary business sentence or read a source that has no registered tool.
