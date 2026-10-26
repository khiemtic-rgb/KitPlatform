# SB-00, SB-01, SB-02 and the execution receipt

Status: specification. Read from `origin/main` `ade5ab28`. This document does not add a table, a migration, or an Agent Runtime. Nothing named here is implemented unless the evidence section says the current code already does it.

## Evidence used

- `src/KitPlatform.Api/Program.cs` registers research publication always. The trading pack is registered only when `PersonalIntelligence:ExposeExchangeRead` is true. The API call passes `startBackfillWorker: false`.
- `migrations/387_pack_personal_intelligence_loop_v1.sql` creates the canonical loop tables and the trading adapter tables. `388` adds the `EvidenceRecorded` event type and the tenant unique keys.
- `migrations/389_pack_personal_intelligence_operation_v1.sql` creates `pi_operation`, one receipt per tenant and operation key.
- `migrations/395_research_trading_v1.sql` and `398_research_trading_paper_integrity_v1.sql` create the research store. It has no `tenant_id` and no foreign key to `public.tenants`.
- `TradingOperationalLoop` writes `pi_operation` and the canonical rows. `TradingAgent.Recommend` forces `ExecutionMode.Paper` and `recommend_only`.
- `TradingAgentWorkflowTests` proves a research session on database `trading_agent`: fixed candles, two paper fills, replay adds none, and an in-memory recommendation that does not place an order. That recommendation is not written to a table.

## SB-00 — Boundary contract

Four layers, and a call may not skip one to grant itself a right the previous layer withheld.

| Layer | Owns | Must not own |
|---|---|---|
| KIT Platform kernel | Tenant, user, role, permission, module flag, `public.tenants` | Trading math, exchange calls, research candles |
| Personal Intelligence | Canonical loop in `pack_personal_intelligence`: context, evidence, decision, action, outcome, reflection, experience, policy, and `pi_events` | A second task store, a learning loop, live orders |
| Domain adapter | Trading ledger, research session, paper fills, risk limits for a proposal | Granting a platform permission or switching tenant |
| Execution tool | Exchange read connector, paper host, research store writer | Running because a document or a UI route exists |

Identity and authorization:

- A platform request carries the current tenant from the host. `TradingOperationalLoop` rejects a body that asks for a different tenant.
- Policies are `personal_intelligence.trading.run`, `.trading.approve`, `.exchange.connect`, `.exchange.read`, and `.research.read`. Exchange read also accepts exchange connect. Admin satisfies the role check. A recommendation does not add a permission.
- The default API does not construct `TradingExchangeController`. Setting the expose flag is a host decision, not an agent decision.
- Module gate `RequirePersonalIntelligenceModule` returns 403 when the tenant module list does not contain `personal_intelligence`. A cross-tenant account lookup that passes the module gate returns 404 from the tenant-scoped query. Neither response may include the other tenant's account or credential.
- Approval is a separate value, `ExecutionApproval`, with a non-empty approver label. `TradingAgent.Recommend` does not create one. `DemoPaperExecution` refuses paper execution when it is absent and refuses live execution even when it is present.
- `pi_agent.autonomy` allows only `recommend_only`. No code inserts that row. The constant `TradingAgent.Autonomy` is the enforced behavior.

External effect rule:

- Research and paper code may write the isolated `trading_agent` database and the Personal Intelligence tables of the current tenant.
- They may not place an exchange order, start `ExchangeBackfillWorker`, or set a timer.
- `research_trading` is isolated by database name, not by tenant. `SqlResearchStore` accepts only `trading_agent`. That store must not be pointed at a platform tenant database to "add" tenancy.

Replay and audit:

- `pi_events` is append-only and is not `platform_events`. The workflow test asserts the research path inserts no `platform_events` row.
- `pi_operation` returns the stored JSON when the same tenant and operation key repeat with the same payload hash. A different hash is a conflict.
- `research_trading.session_result` uses the session content hash as the key. A second run returns the stored snapshot and adds no fill.
- RLS is deferred. Isolation is the `tenant_id` predicate in Personal Intelligence SQL, plus the database allow-list for research. A missing predicate is a defect, not a reason to add RLS in the next slice.

## SB-01 — Canonical domain model

The canonical entities already have tables. The next slice uses these names and does not create `pi_task`, `pi_plan`, `pi_tool_call`, or a parallel receipt table.

| Concept | Existing row | Identity and tenant | Lifecycle already enforced | Epistemic rule |
|---|---|---|---|---|
| Session / task | `research_trading.session_result` for research; `pi_operation` for the tenant loop | Research key is the content hash and has no tenant. Loop key is `(tenant_id, operation_key)` | Research replay. Loop idempotency | The stored result is a record of a run, not a new fact about the market |
| Context | `pi_context` | `context_id` plus tenant and person | Written by the operational loop | Observation |
| Evidence | `pi_evidence.source_ref` | Tenant and person; optional context | Written by the loop from an observed trade id | Observation. The payload column is currently written as `{}` |
| Plan / step | No table | — | Not implemented | Do not infer a plan from `pi_action.intended` |
| Decision | `pi_decision` | `decision_id` plus tenant | `proposed`, `accepted`, `rejected`, `superseded`. The loop inserts `proposed` | The loop stores the caller thesis as `HYPOTHESIS`. An agent claim cannot be stored as `FACT` |
| Tool invocation | No table | — | Not implemented | An exchange connector call is not yet a canonical invocation row |
| Authorization | Platform permission plus `ExecutionApproval` | Approval is an argument, not a row | Absent approval does not become approval | An approval label is not a credential |
| Execution result | `pi_action`, and `trading_order` / `trading_execution` when paper persistence says write | Tenant | Action status `intended`, `paper_filled`, `refused`, `observed`. Order status `paper` or `rejected` | A paper fill is a simulated result |
| Outcome | `pi_outcome` | Tenant, linked to decision | Written in the same loop transaction | Observation of what the paper step did, including an explicit non-execution sentence |
| Reflection, experience, policy | Matching `pi_*` tables | Tenant | Experience `candidate`, `retained`, `rejected`. Policy `proposed`, `approved`, `retired` | The fixture loop may write a candidate experience and a proposed policy only from caller text. That is not retention and not approval |

`pi_knowledge`, `pi_memory`, `pi_agent`, and `pi_evaluation` have tables and no insert in application code. They stay unused. `pi_evaluation.basis` allows `FIXTURE` or `LIVE_OBSERVED`; no writer may insert `LIVE_OBSERVED` in the next slice.

Relationship the next slice must preserve:

- A research session does not create a `pi_decision`.
- A `pi_decision` is not a `trading_order`.
- A `trading_order` written by the loop uses `external_order_id` derived from the decision id and status `paper`. It is not an exchange order.
- Source data, inference, decision, and verified result stay in different rows. A receipt may cite them. It may not copy an inference into an observation column.

## SB-02 — Trading ledger V1

Three stores already exist. The ledger is their link, not a fourth store.

1. Observed ledger, tenant scoped: `person`, `exchange`, `trading_account`, `trade`, `fee`, `position`, `portfolio_snapshot`. A trade row is an observation. Import is idempotent on `(trading_account_id, external_trade_id)`.
2. Platform paper ledger, tenant scoped: `trading_order`, `trading_execution`, `pi_operation`, `pi_action`, `pi_outcome`. Written by `TradingOperationalLoop` from fixture CSV or selected stored trades. Input is not a research candle.
3. Research ledger, database scoped: `research_trading.candle`, `session_result`, `signal`, `risk_decision`, `paper_fill`, `paper_operation`, `run_history`. Written by `SqlResearchStore` on `trading_agent` only. `paper_host_receipt` is the separate paper-host idempotency row, with status `paper_filled`, `risk_refused`, or `session_notional_refused`.

Provenance:

- Research candle identity is `(source, symbol, interval_code, open_time)` plus `payload_hash`.
- Research session identity is the hash of accepted candles, research config, and strategy.
- Signal identity is `signal_key`. Fill and risk identity is `operation_key`. `398` adds nullable `strategy_id`, `strategy_version`, `timeframe`, `candle_open_time`, and `signal_key` links.
- A replayed session keeps the first snapshot. `run_history.status` may be `completed`, `failed`, `replayed`, or `unknown`.
- The research risk row is the paper session's allow/refuse for a tape row. It is not `TradingAgent`'s `RiskVerdict`. Both may appear in one receipt and must keep their own names.

Idempotency rules that stay:

- Same research hash: no second fill.
- Same tenant operation key and same payload hash: return the stored `TradingLoopResult`.
- Same key and different hash: `TradingOperationConflictException`.
- Same paper-host operation key: no second receipt row.

The current gap: the research ledger does not store `TradingRecommendation`, and the platform loop does not store a research session key. The receipt below closes that gap in the research snapshot JSON. It does not merge the two databases.

## Execution receipt

The receipt is a read-only projection. For the research slice it is serialized inside `research_trading.session_result.result`, which already stores `ResearchSnapshot`. The next implementation adds a `RecommendationReceipt` object on that snapshot. Old snapshots without the object remain valid and read as "recommendation not recorded". No migration.

A receipt reports only a step that the run actually performed.

| Field | Source | Rule |
|---|---|---|
| `sessionKey` | `session_result.session_key` | Required |
| `payloadHash` | `session_result.payload_hash` | Must equal the key |
| `input.source` | Candle source, for the proof `workflow-poc` | Name of the authorized source, not a connection string |
| `input.symbol`, `input.timeframe` | Accepted candles | Required |
| `input.candleCount`, `input.firstOpen`, `input.lastClose` | Accepted candles | Counts and bounds, not the raw exchange payload |
| `research.status` | `ResearchSession` status | `COMPLETED`, `REPLAYED`, `DATA_GAP`, `NO_CANDLES`, `KILLED`, `LIVE_BLOCKED` |
| `research.fillCount` | Snapshot fills | The strategy paper session |
| `recommendation.recorded` | New optional object | False on old rows |
| `recommendation.autonomy` | `TradingAgent.Autonomy` | Must be `recommend_only` when recorded |
| `recommendation.kind` | `EpistemicKind` | `Inference` for an agent claim |
| `recommendation.rationale` | `TradingRecommendation.Rationale` | Includes that it does not create a decision or an order |
| `recommendation.evidenceIds` | Fill operation keys cited by the recommendation | Ids only |
| `recommendation.confidence` | Proposal confidence | 0 to 1 |
| `risk.allowed` | `RiskVerdict.Allowed` | Independent from `research_trading.risk_decision` |
| `risk.reasons` | Verdict reasons | Empty when allowed |
| `risk.limits` | Max notional, max leverage, min confidence | The limits supplied to the run |
| `approval.state` | `absent` or `present` | `present` only when a non-empty approver was supplied. The label is stored; a secret is not |
| `outcome` | Derived | One of the codes below |
| `outcomeReason` | Paper or risk text | Required when the outcome is not `paper_executed` |
| `evidenceRefs` | Signal keys and operation keys | Refs into the same session |
| `recordedAt` | `session_result.created_at` | The first write |
| `provenance.database` | `current_database()` | Must be `trading_agent` |
| `provenance.tenantId` | None | Must stay null on this store |

Outcome codes, and the only way each may be set:

| Code | Required evidence |
|---|---|
| `replayed` | The run returned the stored session and `NewPaperFills` is 0 |
| `paper_executed` | `DemoPaperExecution` returned executed, fill mode is `PAPER`, and approval state is `present` |
| `approval_required` | Verdict allowed paper, and approval state is `absent` |
| `risk_rejected` | Verdict not allowed. Approval cannot override it |
| `live_blocked` | The proposal mode was live, or the research session returned `LIVE_BLOCKED` |
| `failed` | The run recorded an error and did not store a new fill |

`paper_executed` on the recommendation is not implied by a `research_trading.paper_fill` row. Those fills belong to `research.fillCount`. The receipt shows both numbers.

The projection must omit ciphertext, API keys, API secrets, passphrases, and any row whose tenant is not the caller. The research receipt has no tenant, so it is not served by the platform tenant API. A reader refuses every database name other than `trading_agent`.

## Vertical slice for the next implementation mission

Name: research recommendation receipt. Domain-specific piece: candles, SMA, paper fills, and the trading proposal. Reusable piece: the receipt fields `sessionKey`, `outcome`, `approval.state`, `risk`, `evidenceRefs`, and the rule that an inference is not stored as a fact. No planner, no tool router, and no live order.

Flow:

1. Input is the existing thirty BTCUSDT one-hour bars, source `workflow-poc`, from `2031-06-01T00:00:00Z`.
2. `ResearchSession.Run` writes the research ledger on `trading_agent` as it does now.
3. `TradingAgent.Recommend` reads the stored fills as observations and receives a proposal whose requested mode is live.
4. `DemoPaperExecution` runs without approval, then a second call with a live mode is refused.
5. The snapshot JSON gains `RecommendationReceipt` for that first recommendation: outcome `approval_required`, autonomy `recommend_only`, kind inference, and the live call is not a second success.
6. Replay reads the same receipt and adds no fill.

Acceptance:

- A new run and a replay expose the same `sessionKey`, fill count 2, and recommendation outcome `approval_required`.
- The receipt rationale says it does not create a decision or an order.
- `proposal.mode` in the receipt is `Paper` even though the input proposal was `Live`.
- No row is inserted into `pi_decision`, `trading_order`, `pi_knowledge`, or `platform_events`.
- The test refuses port `5432`, `novixa_prod`, and `trading_research`.
- A snapshot written before this field still deserializes, with `recommendation.recorded` false.

Test plan:

- Extend `TradingAgentWorkflowTests` to read the receipt back from `research_trading.session_result`.
- Add one deserialize test for a snapshot JSON that has no recommendation object.
- Do not change `TradingOperationalLoop` in this slice. Its tenant receipt stays the fixture/stored-trade path.

Files the next mission may change:

- `ResearchSession.cs`, to carry the optional receipt on `ResearchSnapshot`.
- `TradingAgentWorkflowTests.cs`.
- A projector in the Personal Intelligence application project, with no host and no controller.
- `INTEGRATION_BRANCH_STATUS_V1.md`, after tests pass.

Files the next mission must not change: `Program.cs`, migration files, exchange connectors, Novixa Video, and the production manifest.

## Decisions closed by this readback

- Personal Intelligence is a plane inside KIT Platform. Trading is the first adapter, not a separate product.
- The canonical loop tables are the domain model. Research tables are the trading research adapter.
- The first receipt extends `ResearchSnapshot` JSON. It does not add a migration.
- Recommendation outcome and research paper fills are different facts.
- `recommend_only`, the absent approval, and the live refusal stay in force.
- `pi_knowledge`, `pi_memory`, and `pi_agent` are unused tables, not an agent runtime.

## Decisions still owned by the architect

- Whether a later mission may add `tenant_id` to `research_trading`. This slice must not.
- Whether the platform API may ever read a research receipt. Not until a tenant key exists and the architect accepts it.
- Whether `pi_evidence.payload` should later store a hash of the source row. This slice keeps evidence refs as identifiers only.
