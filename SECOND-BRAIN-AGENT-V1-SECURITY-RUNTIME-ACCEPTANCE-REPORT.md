# Second Brain Agent V1 — security and runtime acceptance

Checked: 2026-10-30 11:22:39 +07 on VPS B `157.10.53.182`.  
Service `second-brain-agent`, commit `2c1ea353c58fcb1cf74d996bade25df28f34f8af`.  
No restart, no migration, no schema change, no LLM call, no worker change, no exchange call, no VPS A contact, and no revoke of the three existing refresh tokens.

Conclusion: `PARTIALLY ACCEPTED`

Runtime boundaries, refresh rotation, and the paper-ledger code path hold. The service is not fully accepted while three refresh tokens can still mint an access token, logout leaves an access JWT valid, production has no `pi_task`, and the model plus research publication remain off.

## 1. Runtime integrity

| Check | Result |
|---|---|
| PID | 64986, unchanged since the 11:12 deploy |
| Status | `active`, `NRestarts=0` |
| Working directory | `/opt/second-brain-agent-v1-2c1ea353/host` |
| Host DLL SHA-256 | `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` |
| Listen | `127.0.0.1:5095` owned by PID 64986 |
| Port 5096 | closed |
| PostgreSQL 5433 | `127.0.0.1:5433`, cluster PID 43039 |
| PostgreSQL 5432 | listening for `postgresql@16-main` PID 34958; the agent process had no socket to it |
| VPS A `103.200.23.229` | no socket |
| Exchange connections | 0 before and after the checks |
| Executions | 2 rows, both `PAPER`; no `OBSERVED` |
| Model | `modelConfigured=false` |
| Publication | `publicationConfigured=false`, availability `MISSING` |
| Worker / backfill | still false from the running process health at deploy; this process was not restarted |

`GET /api/personal-intelligence/intent/status` returned HTTP 200 with the model off. Authenticated summary, task list, and publication read also returned HTTP 200. No `Set-Cookie` header was present on login.

## 2. Authentication and session matrix

Exercised through the API as `sb-operator` / `SECOND_BRAIN`. Tokens and the password are omitted. The API inserted two refresh rows for this exercise and both were revoked before the check ended. Open refresh count stayed 3. Decision, task, operation, and exchange-connection counts stayed 3, 0, 3, and 0.

| Case | HTTP | Result |
|---|---:|---|
| Login with empty username and password | 400 | Rejected |
| Login with a wrong password | 401 | Body does not contain the password |
| Login of `sb-operator` as tenant `KIT_MKT` | 401 | Rejected |
| Login with the existing operator | 200 | Access and refresh returned in JSON, not a cookie |
| `GET /api/auth/me` | 200 | Tenant `SECOND_BRAIN`, role `ADMIN` |
| Refresh with the issued token | 200 | A different refresh token was issued |
| Refresh again with the first token | 401 | The presented token was already revoked |
| Refresh with a random string | 401 | Rejected |
| Refresh with an empty string | 401 | Rejected |
| Logout with the rotated token | 204 | Accepted |
| Refresh after logout | 401 | Rejected |
| `GET /api/auth/me` with the access JWT after logout | 200 | The access JWT is still accepted |

The operator permissions observed on `me` are `personal_intelligence.trading.run`, `personal_intelligence.trading.approve`, `personal_intelligence.research.read`, and `personal_intelligence.exchange.read`.

Journal lines from the last five minutes do not contain the password, the refresh token, or the access token. The server does not set a cookie. The client is expected to hold the JSON tokens itself.

`second_brain_host.refresh_token` stores `token_hash`, `user_id`, `expires_at`, and `revoked_at`. There is no token id, no `created_at`, and no last-used column. The raw token is not stored. Hash is SHA-256. Access lifetime is 8 hours. Refresh expiry is that access expiry plus 7 days. Login inserts a row. Refresh revokes the presented hash and inserts a new row. Logout revokes only the presented hash.

A live call with an already-expired raw refresh token was not possible: the raw value is not stored, and no expired open row exists. Invalid and blank tokens were rejected. The lookup itself requires `revoked_at IS NULL AND expires_at > now()`.

## 3. Authorization and tenant isolation

| Case | HTTP |
|---|---:|
| Trading summary without a token | 401 |
| Task list without a token | 401 |
| Trading summary with the operator token | 200 |
| Task list with the operator token | 200 |
| Intent POST naming a different tenant id | 403 `Tenant context cannot switch tenant.` |
| Login into `KIT_MKT` with the SECOND_BRAIN operator | 401 |

The production database has one paper ledger: `SECOND_BRAIN` / `PAPER` / `paper-fixture`. There is no `KIT_MKT` tenant on this database, so a second live tenant could not be logged in. Cross-ledger rejection for `KIT_MKT` versus `SECOND_BRAIN` was run on the isolated database, not by writing a second tenant here.

There is no second, low-privilege user on this host. A caller limited to `research.read` was not exercised against the live service. That denial is covered by `PaperLedgerAnchorTests` on the isolated suite.

The agent process was not observed on port 5432, `novixa_prod`, or `103.200.23.229`.

## 4. PAPER workflow

Production, read plus one refused live call:

- Ledger row: `SECOND_BRAIN|PAPER|paper-fixture`, active.
- Decisions: 2 `accepted`, 1 `proposed`. Tasks: 0.
- Audit events already present: `DecisionMade`, `ActionTaken`, `ContextObserved`, `EvidenceRecorded`, `ExperienceDerived`, `OutcomeObserved`, 3 each.
- `POST /api/personal-intelligence/trading/loops` with `live=true` returned HTTP 400 and `Live execution is not available.`
- Operation, decision, task, and exchange-connection counts were unchanged by that call.

No new paper decision, task, or approval was written on the production database. Idempotent replay and the approval transition were not repeated there.

Isolated suite from archive `2c1ea353`, local `127.0.0.1:5433` only, then the local cluster was stopped:

| Suite | Result |
|---|---|
| Paper ledger, intent limits, intent gate, intent execution | 46 passed |
| Host guard, health, login, paper replay, live refusal, publication `MISSING` | 25 passed |

That suite recorded both `KIT_MKT` / `PAPER_LEDGER` / `KIT_MKT-PAPER-LEDGER` and `SECOND_BRAIN` / `PAPER` / `paper-fixture`, rejected a mismatched or cross-tenant ledger, returned the same decision and task on replay, and left `waiting_approval` until a separate approval. A `research.read` caller was denied. An unavailable model wrote no decision.

Production therefore shows the SECOND_BRAIN ledger and historical paper decisions. It does not show a `pi_task`, because task insert follows a model plan and the model is off.

## 5. Three existing refresh tokens

User `22222222-2222-4222-8222-222222222222` (`sb-operator`). No hash is listed. The table has no id and no creation time. Issue time below is `expires_at` minus 7 days and 8 hours. Access from that issue expired 8 hours later, all before 11:22 +07 today.

| Expires | State | Derived issue | Derived access expiry |
|---|---|---|---|
| 2026-11-06 04:22:21 +07 | open | 2026-10-29 20:22:21 +07 | 2026-10-30 04:22:21 +07 |
| 2026-11-06 04:23:57 +07 | open | 2026-10-29 20:23:57 +07 | 2026-10-30 04:23:57 +07 |
| 2026-11-06 05:14:13 +07 | open | 2026-10-29 21:14:13 +07 | 2026-10-30 05:14:13 +07 |
| 2026-11-06 18:00:42 +07 | revoked | earlier acceptance logout | already revoked |

They are not the access session of PID 64986. Their access JWTs are already expired. Each open row can still be exchanged for a new access JWT until 2026-11-06 if the raw refresh token is presented. Nothing in the schema records whether they were used after issue. They do not change the current process, and they were not revoked.

Safe revoke, application-compatible, not executed:

```sql
UPDATE second_brain_host.refresh_token
SET revoked_at = NOW()
WHERE user_id = '22222222-2222-4222-8222-222222222222'
  AND revoked_at IS NULL;
```

Effect: those three rows can no longer refresh. No hash needs to be printed. The running process and the current access JWT, if any client still holds one, are unchanged. Run only on `second_brain_agent_v1` via the 5433 cluster.

## 6. Remaining risk

- Logout does not revoke the access JWT. After a 204 logout, `GET /api/auth/me` with that JWT still returned 200. The JWT stays valid until its own expiry, up to 8 hours.
- Three refresh tokens remain open until 2026-11-06.
- Refresh rows have no id and no last-used time, so a later audit cannot show which of the three was presented.
- Production has paper decisions and zero tasks. Intent cannot persist a task while the model is off.
- The operator is `ADMIN` with trading, research, and exchange read permissions. A narrower live user was not available.
- `KIT_MKT` exists in code and in the isolated test database. It is not a tenant on this VPS database.

## 7. Not tested, and why

| Item | Reason |
|---|---|
| Presenting an expired raw refresh token | Raw tokens are not stored, and no open row is expired |
| New paper decision, task, approval, or replay on production | Would write business rows; done on the isolated suite instead |
| Low-privilege user on the live service | The host has one operator |
| Live `KIT_MKT` ledger on VPS B | That tenant is not in `second_brain_agent_v1` |
| Paid or local model completion | Model is off; a completion was not requested |
| Research package ingestion | Root is empty; no real export was supplied |
| Worker or backfill enabled path | Left disabled |
| Exchange connect, test, or order | Not called; connection count stayed 0 |

## 8. Conclusion

`PARTIALLY ACCEPTED`

The running binary, bind address, database port, worker flag, model-off startup, refresh rotation, logout of the refresh token, tenant switch refusal, and live-order refusal match the release. Full business acceptance is still open for tasks driven by a model, research publication, and the refresh-token cleanup.

## 9. Before enabling a model or an orchestrator

1. Approve and run the revoke statement above, or accept the three open refresh tokens until 2026-11-06.
2. Decide whether access JWTs must die at logout before a model can change state. Today they do not.
3. Set `PersonalIntelligence__BusinessIntent__Enabled`, `BaseUrl`, `ApiKey`, and `Model` only in the service env file. Startup refuses an enabled model that is missing any of those. Keep the output, call, and timeout limits inside 1024 tokens, 64 calls per process, and 5–45 seconds.
4. Confirm one real completion inside that timeout before treating intent-to-task as accepted. Local `qwen2.5:7b` did not finish inside 45 seconds during release.
5. Leave `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`. Do not register a backfill worker or connect an exchange as part of turning the model on.
6. Supply a real `research-publication/v1` directory before treating publication as ready. An empty root stays `MISSING` and does not block the process.

## Decisions that need approval

1. Revoke the three open `sb-operator` refresh tokens with the statement in section 5.
2. Whether the access-JWT-after-logout behavior must be changed before the model is enabled.
3. Model credential and the decision to set `PersonalIntelligence__BusinessIntent__Enabled=true`.
4. A real research publication root, when a package exists.
5. Any future enablement of the exchange backfill worker. It stays off.
