# Second Brain V1 — implementation readiness and remediation plan

Date: 2026-10-30. Source commit `2c1ea353c58fcb1cf74d996bade25df28f34f8af` (parent `06e240c5ad4f059d32cddf0b57d11e5def073687`). No deploy, no migration, no production write, no model enablement, and no revoke of the three refresh tokens.

VPS B was not contacted in this pass. Runtime facts below are the 11:22 +07 acceptance of that same commit and PID 64986.

## 1. Current source state

The isolated host is `src/Hosts/SecondBrainAgent`. It does not call `AddInfrastructure` or `AuthService`. Its login, refresh, and logout are `AgentAuth` in the host project. Platform `JwtTokenService`, `JwtSettings`, and `AuthService` belong to Kit Platform / NOVIXA and stay unchanged.

| Area | State |
|---|---|
| Agent access JWT | `ALREADY IMPLEMENTED`. Hardcoded 8 hours in `AgentAuth.IssueAsync`. Issuer and audience `SecondBrainAgent`. |
| Agent refresh token | `ALREADY IMPLEMENTED`. SHA-256 hash in `second_brain_host.refresh_token`. Expiry is access expiry plus 7 days. Rotation revokes the presented hash, then inserts a new row. |
| Agent logout | `ALREADY IMPLEMENTED` for the presented refresh hash only. |
| Access JWT after logout | `NEEDS FIX`. Signature and `exp` are the only checks. Acceptance at 11:22 showed `GET /api/auth/me` still HTTP 200 after logout 204. |
| Platform JWT | `ALREADY IMPLEMENTED` and out of scope. `JwtSettings` defaults are 60 minutes and 30 days. `AuthService.LogoutAsync` also revokes only the refresh hash. Do not edit those types. |
| Research reader, five GET routes, UI client | `ALREADY IMPLEMENTED` and covered by `ResearchPublicationTests` plus the research view smoke. |
| Research files on VPS | `NOT READY`. `PersonalIntelligence__ResearchPublication__Root` is empty. Live read returns `MISSING`. |
| LLM limits and default off | `ALREADY IMPLEMENTED`. `Enabled=false` starts the host. An enabled model with a missing field or an out-of-range limit refuses startup. |
| Intent to `pi_task` on a real model | `NOT READY`. No credential. Local `qwen2.5:7b` did not answer inside 45 seconds. |
| Exchange backfill worker on the agent host | `ALREADY IMPLEMENTED` as off. The host calls `AddPersonalIntelligenceServices`, not `AddPersonalIntelligencePack`. Startup throws if a hosted service name contains `ExchangeBackfill` or if `WorkerEnabled` is true. |

## 2. Session security findings

Checked directly:

- `JwtSettings` / `JwtTokenService.CreateAccessToken`: platform access TTL is `AccessTokenExpireMinutes` (default 60). Refresh TTL is `RefreshTokenExpireDays` (default 30). Used by `AuthService`, not by the agent host.
- `AuthService.LoginAsync`, `RefreshAsync`, `LogoutAsync`: refresh hash lookup, revoke that hash, issue a new pair. Logout does not record an access-token id.
- `AgentAuth`: access TTL 8 hours. Refresh expiry `expires.AddDays(7)`. Logout runs `UPDATE second_brain_host.refresh_token SET revoked_at = NOW() WHERE token_hash = @Hash`.
- JWT bearer on the agent host validates issuer, audience, lifetime, and signing key. There is no `jti`, no session id, and no revocation lookup.
- `WriteToken` always adds claim `role=ADMIN`, then the operator permission list. Every `sb-operator` token therefore passes every Personal Intelligence policy that allows `ADMIN`, including `ExchangeConnect`, even though `exchange.connect` is not in the permission array.

After logout, the still-valid access JWT can call every endpoint that only requires a bearer token, including:

- `GET /api/auth/me`
- `GET /api/personal-intelligence/trading/summary`
- `GET /api/personal-intelligence/tasks`
- `GET /api/personal-intelligence/research/*`
- `GET /api/personal-intelligence/intent/status`
- `POST /api/personal-intelligence/intent`
- `POST /api/personal-intelligence/trading/loops`
- `POST .../decisions/{id}/paper` and `.../reject`
- exchange read and exchange connect routes in `TradingExchangeController`

A short access TTL reduces the window. It does not make logout immediate. A 15-minute token would still return 200 for those routes until `exp`. Immediate revocation needs a server-side session check.

The three open refresh rows from the acceptance report, user `22222222-2222-4222-8222-222222222222`, are unchanged by this task:

| Expires | State | Derived issue |
|---|---|---|
| 2026-11-06 04:22:21 +07 | open | 2026-10-29 20:22:21 +07 |
| 2026-11-06 04:23:57 +07 | open | 2026-10-29 20:23:57 +07 |
| 2026-11-06 05:14:13 +07 | open | 2026-10-29 21:14:13 +07 |

Their access JWTs are already past 8 hours. The refresh rows can still mint a new access JWT until 2026-11-06. The table has no id and no last-used column, so use after issue cannot be proven. The fourth row, expiry 2026-11-06 18:00:42 +07, is already revoked.

`NEEDS APPROVAL`. Do not run it in this task. Database `second_brain_agent_v1` on port 5433 only:

```sql
UPDATE second_brain_host.refresh_token
SET revoked_at = NOW()
WHERE user_id = '22222222-2222-4222-8222-222222222222'
  AND revoked_at IS NULL;
```

Effect: those three rows cannot refresh. The running process and any access JWT a client already holds stay valid until `exp`. No hash is required.

## 3. Proposed JWT and session policy

One design, agent host only.

Bind each new access JWT to the refresh row that issued it.

1. Add nullable `session_id UUID` on `second_brain_host.refresh_token`. New login and refresh set a new id on the inserted row. Existing rows stay null.
2. Put that id in access-token claim `sid`.
3. In the agent host's `OnTokenValidated`, when `sid` is present, allow the request only if that row has `revoked_at IS NULL` and `expires_at > now()`. A logout that already revokes the refresh row then fails the access token on the next request.
4. Tokens without `sid` keep today's lifetime check until they expire. That includes any access JWT already issued and any JWT minted by the three old refresh tokens before this column exists. Revoking those three rows is still the separate statement above.
5. Leave platform `JwtSettings`, `JwtTokenService`, and `AuthService` untouched. Leave the agent access TTL at 8 hours. The session check is what makes logout immediate. A shorter TTL is not a substitute.
6. Do not increment a single operator-wide version on logout. That would end every session, including the three refresh tokens, as a side effect of one logout.

Trade-off: one indexed read of `second_brain_host.refresh_token` on each authenticated agent request. The host is one operator on loopback, so the cost is small. The schema change is limited to `second_brain_host` and needs approval before it is applied on VPS B. Until that migration runs, the code must treat a missing column as "sid check unavailable" and refuse to start, rather than silently skipping the check.

`role=ADMIN` on every agent token is a separate decision. Removing it would make `exchange.connect` match the permission list, which does not include that permission. Keep the claim until an architect accepts the narrower operator. `NEEDS APPROVAL`.

## 4. Research architecture findings

`ALREADY IMPLEMENTED` and left as they are:

- `publish_research_v1.py` in `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1`. It copies four finished files, strips `api_key` fields from status and `frozen_lab_result` from the report, writes a new directory by staging and rename, and does not read journals, cache, logs, or the snapshot.
- `FileResearchPublicationReader` requires `schemaVersion = research-publication/v1`, `publicationId`, `publisher`, `publishedAt`, and four SHA-256 fields: `labResultSha256`, `paperStatusSha256`, `paperReportSha256`, `paperHealthSha256`. A hash mismatch is `INCONSISTENT`. An empty root is `MISSING` before the manifest is opened. Health `OK` becomes `FRESH`. Health `STALE` stays `STALE`.
- `ResearchPublicationController` exposes `publication`, `experiments`, `experiments/{id}`, `paper/status`, and `paper/report`. No path is accepted from the query.
- Policy `PersonalIntelligenceResearchRead`.
- Admin client `research.api.ts`, `research-view.ts`, and `ResearchPanel` mounted from `TradingPages.tsx`.
- Tests in `ResearchPublicationTests.cs`.

`ResearchRead` also allows `ADMIN` and `personal_intelligence.trading.run`. A research-only permission is therefore not required for the current operator. `research.read` does not grant `exchange.connect`. `POST /intent` allows `research.read`, and the paper handler still requires `trading.run`. That split is already tested.

The reader does not use exchange tables. Research can be missing while paper trading and health continue.

## 5. Research and trading dependency map

| Registration | What it adds | Agent host | Platform `KitPlatform.Api` |
|---|---|---|---|
| `AddResearchPublication` | Does not exist yet. Reader and research capability handler are inside the shared method. | Registered today via the shared method. | Same. |
| `AddPersonalIntelligenceServices` | Paper ledger, task store, desk, intent, research reader, and also Bybit, Binance, Okx, credential ring, connection service, execution sync, and `IExchangeBackfill`. | Called. | Called indirectly. |
| `AddPersonalIntelligencePack` | The services above plus `AddHostedService<ExchangeBackfillWorker>`. | Not called. | Called from `Program.cs`. |
| `ExchangeBackfillWorker.ExecuteAsync` | Returns immediately when `WorkerEnabled` is false. | Not registered. Startup also rejects the type name. | Registered. Pump runs only when the flag is true. |
| `AddApplicationPart(TradingDeskController assembly)` | All Personal Intelligence controllers, including research, trading, and exchange. | Registered. | Registered. |

Activating the research root on the agent host does not register the worker and does not open an exchange connection. The remaining coupling is in-process: the same DI method constructs exchange adapters, and the same MVC part exposes exchange routes. Those routes stay unauthorized for a caller who lacks `ADMIN`, `exchange.read`, or `exchange.connect`. The current operator token has `ADMIN`, so the routes are reachable by that user. They are not reachable by an anonymous caller.

Do not create a second host process or a research database. Split the DI methods so a research registration cannot call `AddHostedService`. Leave `KitPlatform.Api` calling `AddPersonalIntelligencePack` so `kit-platform-api` behavior does not change in the same change.

## 6. Migration and entitlement matrix

Research Read has no table. It needs the module gate and the permission string. It does not need 391–393.

| Migration | Role | Research Read | Apply for a research-only step |
|---|---|---|---|
| 385 | Trading ledger schema `pack_personal_intelligence` | Not required to read files | No |
| 387 | `pi_events` and loop tables | Not required to read files | No |
| 388 | Loop integrity | Trading | No |
| 389 | `pi_operation` | Trading / paper idempotency | No |
| 390 | Insert `personal_intelligence` into `platform_module_registry` | The platform catalog. The agent host reads `tenants.settings.enabled_modules`, which the bootstrap already sets to `personal_intelligence`. | No, on the current VPS database. `NEEDS APPROVAL` only if a platform catalog row is required elsewhere. |
| 391 | `exchange_connection` | Exchange | No |
| 392 | Execution sync / `trade.source_type` | Exchange | No |
| 393 | Market split and backfill tables | Exchange | No |
| `deploy/second-brain-agent/000_identity_minimum.sql` | `second_brain_host.operator` and `refresh_token` | Already present on VPS B | Do not reapply. The session column is a new follow-up script, not this file rewritten in place. |

Permission `personal_intelligence.research.read` is already on the bootstrapped `sb-operator`. No production grant change. A future non-admin operator who should read research and not trade needs that permission and must not receive `role=ADMIN`. That is the approval item in section 3.

Research does not depend on an exchange connection, a backfill job, or a trading row. It does depend on the host process, the module flag for the caller tenant, and a publication directory. Paper trading separately depends on 385 and 387–389, which are already on `second_brain_agent_v1`.

## 7. Research publication artifact status

`NEEDS INTEGRATION` on VPS. The local export is verified.

Sources, unmodified, under `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\output`:

- `LAB_RESULT.json`
- `PAPER_OPS_STATUS.json`
- `PAPER_RESEARCH_REPORT.json`
- `PAPER_OPS_HEALTH.json`

Journals, `state/paper-ops`, and the snapshot were not read and not passed to the publisher.

New directory, created by `publish_research_v1.py`:

`E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\output\publication-candidate-20261030`

| Field | Value |
|---|---|
| schema | `research-publication/v1` |
| publicationId | `pub-e65b6912e02a168d` |
| publisher | `research-lab-v1` |
| publishedAt | `2026-10-30T04:30:00+00:00` |
| health | `OK` |
| `labResultSha256` | `8f19e6e010999637fe2b1b793106ea2c22b5def60e06fbb8b06dcb6267d5714b` match |
| `paperStatusSha256` | `0cdecbb0e7b2a5f8430cd32d713aaf5be447096272a273cb72e62b0f32bc84b5` match |
| `paperReportSha256` | `84b67af769b13ff13eae823f07446de682334f03fff2b7643a6069f2b83f549f` match |
| `paperHealthSha256` | `d4e557a3510047b127cd9ff1b714a63dd9030b115e0a25399fa27d26b4fab6ab` match |

`LAB_RESULT.json` bytes in the publication match the source file. The existing `output/publication` directory was not replaced. This candidate is not the VPS root. Copying it and setting `PersonalIntelligence__ResearchPublication__Root` is `NEEDS APPROVAL`. Until that happens the live API stays `MISSING`, which is the safe state.

Nothing else is missing for a file publication. A newer lab run is a separate data decision, not a blocker for this candidate.

## 8. LLM pipeline readiness

`ALREADY IMPLEMENTED` on commit `2c1ea353`:

- `GET /api/personal-intelligence/intent/status` reports `modelConfigured`.
- `POST /api/personal-intelligence/intent` returns 403 when `tenantId` is not the caller tenant, before the model.
- `CatalogGroundedIntentInterpreter` is OpenAI-compatible `chat/completions`, one attempt, `max_tokens`, temperature 0, bearer key in the header only.
- Defaults: enabled false, 512 output tokens (max 1024), 32 calls per process (max 64), 20 second timeout (range 5–45). No retry.
- Unconfigured, timeout, HTTP error, bad JSON, and call budget return before a handler. They do not write a decision or a task.
- `PaperDecisionRecorder` accepts `SECOND_BRAIN` / `PAPER` / `paper-fixture` and `KIT_MKT` / `PAPER_LEDGER` / `KIT_MKT-PAPER-LEDGER`, and rejects a mismatch. A saved paper decision then inserts `pi_task` at `waiting_approval`. Replay uses the operation key.
- Loop audit events are `DecisionMade` and the rest of the existing event set. Production currently has those events for earlier paper loops and zero `pi_task`, because no model plan has been accepted there.

`NOT READY` to enable:

- No `PersonalIntelligence__BusinessIntent__*` keys on VPS B.
- No free completion inside the 45 second cap was observed.
- Do not put an API key in git, the sample env, or a report.

Provider shape when an architect later approves a credential: OpenAI-compatible base URL, model name, and a bearer value only in the service env file. Cost guard is the existing call cap and output cap, not a billing API. Test plan: keep `BusinessIntentHeldOutTests` excluded; keep `IntentModelLimitTests` and `PaperLedgerSqlTests` on the isolated database; run `LocalOllamaIntentTests` only with `SB_LOCAL_OLLAMA=1` against `127.0.0.1:11434`. Do not point that test at a paid host.

## 9. Implementation order

Do these as separate changes. Do not mix them into a NOVIXA auth edit or a Personal Intelligence migration manifest apply.

1. Session check, agent host only. Add `session_id` in a new SQL file under `deploy/second-brain-agent/`. Issue `sid`. Validate it in `AgentAuth` bearer options. Tests: logout then `GET /api/auth/me` is 401 for a token that has `sid`; a second refresh row still works; a token without `sid` still works until `exp`; platform `AuthService` tests stay untouched. Do not apply the SQL on VPS until approval.
2. DI split inside `PersonalIntelligencePackDependencyInjection`. Add `AddResearchPublication` and keep the worker exclusively in `AddPersonalIntelligencePack`. Point the agent host at research plus paper trading plus the connectors the current controllers require. Do not remove `AddPersonalIntelligencePack()` from `KitPlatform.Api`. Test: the agent host test still sees no `ExchangeBackfill` hosted service, and `WorkerEnabled=true` still fails startup.
3. Publication install, after approval. Copy `publication-candidate-20261030` to a directory owned by `sbagent`. Set only `PersonalIntelligence__ResearchPublication__Root`. Restart is a later deploy, not part of the source change. Smoke is `GET /api/personal-intelligence/research/publication` with the operator, expecting `FRESH` and id `pub-e65b6912e02a168d`.
4. LLM enablement, after a credential approval and after the session check is deployed if immediate logout is required first. Set the four env values. Leave the worker false. Confirm one status read shows `modelConfigured=true`, then one intent that either writes a `waiting_approval` task or returns `unreadable` with decision count unchanged.

## 10. Acceptance criteria

| Phase | Pass |
|---|---|
| Session | New login, logout, then the same access JWT receives 401 on `GET /api/auth/me` and on `POST /loops`. A different refresh token for the same user still refreshes. Open count of the three historical rows is unchanged unless the separate revoke was approved. Platform login TTL and logout behavior are unchanged. |
| DI split | Agent host boots with research `MISSING` when the root is empty. Health stays 200. Hosted services do not include `ExchangeBackfillWorker`. `kit-platform-api` still uses `AddPersonalIntelligencePack`. Exchange connection count on VPS stays 0. |
| Publication | Root points at the verified candidate. Publication GET is `FRESH`. Hash mismatch on a copied file returns `INCONSISTENT` and no invented numbers. Trading summary counts do not change because of the read. |
| LLM | Flag remains false in every build and in the sample env. After an approved enablement, a failed or slow model adds zero `pi_decision` and zero `pi_task`. A valid paper plan adds one decision and one `waiting_approval` task, and a repeat returns the same ids. |

## 11. Decisions for the architect

1. Approve the `session_id` column and the agent-only `sid` check. Do not approve a platform JWT change.
2. Approve or defer the revoke SQL for the three open refresh tokens. This plan does not run it.
3. Keep or remove hardcoded `role=ADMIN` on agent tokens. Removing it changes which exchange routes `sb-operator` can call.
4. Approve copying `publication-candidate-20261030` onto VPS B and setting the publication root. Until then the root stays empty.
5. Approve a model base URL, model name, and secret location before `PersonalIntelligence__BusinessIntent__Enabled=true`.
6. Leave the exchange backfill worker off. Do not apply migrations 391–393 again for Research Read.

`PARTIALLY READY`

Session revocation, the VPS publication root, and a live model are the three items that still need an approval before they are turned on. The reader, the publisher, the paper ledger map, and the model-off startup path are already in source and should not be rewritten.
