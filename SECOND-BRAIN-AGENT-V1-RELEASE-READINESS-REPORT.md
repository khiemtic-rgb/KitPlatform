# Second Brain Agent V1 — release readiness

Conclusion: `READY_FOR_DEPLOY_APPROVAL`

The candidate can be installed with the language model left disabled and the research publication root left empty. That install has not been performed. The running service on VPS B was not restarted, migrated, or given a new environment.

This report does not mark the agent `FUNCTIONALLY_ACCEPTED`. Two business flows remain outside this acceptance: a live model call that returns a usable plan, and ingestion of a real `research-publication/v1` package.

## 1. Commit

Release tip: `2c1ea353c58fcb1cf74d996bade25df28f34f8af`  
Parent: `06e240c5ad4f059d32cddf0b57d11e5def073687`  
Baseline parent of that commit: `5f29e1bdf184364ad646e6cd4e622cdb0d0b3278`  
Branch: `second-brain/paper-proposal-v1`  
Remote: not pushed.

`git get-tar-commit-id` on `source.tar` returns `2c1ea353c58fcb1cf74d996bade25df28f34f8af`. The archive has no `.git` directory.

## 2. Release files

These 14 files are the whole difference from `5f29e1bd` to `2c1ea353`:

- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Application/Trading/PaperLedgerAnchor.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Infrastructure/Trading/PaperDecisionRecorder.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Application/Intent/BusinessIntentModel.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Application/Intent/BusinessIntentSession.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Infrastructure/Intent/CatalogGroundedIntentInterpreter.cs`
- `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Infrastructure/PersonalIntelligencePackDependencyInjection.cs`
- `src/Hosts/SecondBrainAgent/KitPlatform.SecondBrainAgent.Host/Program.cs`
- `tests/KitPlatform.PersonalIntelligence.Tests/PaperLedgerAnchorTests.cs`
- `tests/KitPlatform.PersonalIntelligence.Tests/PaperLedgerSqlTests.cs`
- `tests/KitPlatform.PersonalIntelligence.Tests/IntentModelLimitTests.cs`
- `tests/KitPlatform.PersonalIntelligence.Tests/LocalOllamaIntentTests.cs`
- `deploy/second-brain-agent/agent.env.example.md`
- `deploy/second-brain-agent/install-candidate.sh`
- `deploy/second-brain-agent/publication-sample/README.md`

Pharmacy, Family OS, Content, and the other dirty working-tree files stayed unstaged.

## 3. What changed

Paper ledger selection is an explicit map:

| Tenant | Exchange | External account |
|---|---|---|
| `KIT_MKT` | `PAPER_LEDGER` | `KIT_MKT-PAPER-LEDGER` |
| `SECOND_BRAIN` | `PAPER` | `paper-fixture` |

The recorder loads the accounts of the caller user and tenant, then `PaperLedgerSelection.TrySelect` keeps a row only when tenant, exchange, and account all match that map and the person and account ids are present. Zero ledger rows, two ledger rows, a cross-tenant row, or an exchange/account that belongs to the other map abort the save. Unknown tenants have no anchor. There is no permission bypass in the recorder; paper evaluate still requires `personal_intelligence.trading.run`.

Intent still reaches a task only after a model plan is accepted and `PaperDecisionRecorder` saves a decision. A model timeout, HTTP error, malformed body, or exhausted call budget returns before any handler and before any decision or task. The same operation key is inserted with `ON CONFLICT DO NOTHING` and then re-read, so a repeated payload returns the same decision and task.

The model stays off unless `PersonalIntelligence:BusinessIntent:Enabled` is true and base URL, API key, and model are all present. Startup calls `EnsureStartupSafe()` after the JWT check. A disabled or absent model does not stop the process. An enabled model with a missing field, or with limits outside the ranges below, stops startup. Each call sends `max_tokens`, uses one attempt, and cancels at the configured timeout. The process keeps a counter up to `MaxCallsPerProcess`.

| Limit | Default | Allowed |
|---|---|---|
| `MaxOutputTokens` | 512 | 1–1024 |
| `MaxCallsPerProcess` | 32 | 1–64 |
| `RequestTimeoutSeconds` | 20 | 5–45 |

The HTTP client timeout follows that same window, with a 5 second connect timeout. The key is sent only as a bearer header. It is not written into the prompt body.

`WorkerEnabled` is still forced false after configuration binding. The host still refuses port 5432, `novixa_prod`, `trading_research`, `kit_pi_validation`, and `103.200.23.229`.

## 4. Build and tests

Built from the extracted archive, not from the working tree:

`dotnet publish` Release of `KitPlatform.SecondBrainAgent.Host` into `E:\sb-agent-v1-2c1ea353\publish\host`.

Result: succeeded. Pre-existing `NU1903` on Newtonsoft.Json 10.0.3 and one existing `CS4014` in `CustomerPushRepository.cs`. No compile errors.

From that same archive, Release tests:

| Suite | Result |
|---|---|
| Paper ledger, intent limits, intent gate, intent execution, local-model check | 46 passed, 1 skipped, 0 failed |
| `KitPlatform.SecondBrainAgent.Tests` | 25 passed, 0 failed |

The skipped test is `LocalOllamaIntentTests`, which runs only when `SB_LOCAL_OLLAMA=1`.  
`BusinessIntentHeldOutTests` was excluded. That test calls `Assert.Fail` when no language model is configured. Leaving it red is the intended signal. It was not edited, and no model credential was added to make it pass.

The host suite covered database-guard refusal, health, database health, anonymous `401` on the trading summary, login, paper replay, thesis conflict `409`, live refusal `400`, and research publication `MISSING`.

## 5. Paper ledger regression

`PaperLedgerAnchorTests` covers the KIT_MKT map, the SECOND_BRAIN map, mismatched exchange or account, a foreign tenant row, two matching ledgers, an unknown tenant, and a BYBIT-only account. `research.read`, `exchange.read`, and `trading.approve` cannot evaluate a paper order. `personal_intelligence.trading.run` can.

`PaperLedgerSqlTests` ran against local `127.0.0.1:5433` / `second_brain_agent_v1` only. It refuses port 5432 and every other database name. SECOND_BRAIN used the existing `sb-operator` / `paper-fixture` account. KIT_MKT used a ledger inserted for the test. Both saved a decision. The SECOND_BRAIN replay returned the same decision id and task id. Task status was `waiting_approval`. The stored operation result for each tenant contained that tenant's account id. A `research.read` caller received `permission_denied` and wrote nothing. An unavailable model wrote nothing. A second ledger identity on SECOND_BRAIN (`PAPER_LEDGER` / `KIT_MKT-PAPER-LEDGER`) was rejected with the mismatch text and the extra rows were deleted. The extra-account count after the test was 0.

## 6. Task lifecycle

Proven on the local isolated database:

`scripted paper plan → ledger selection → decision → pi_task waiting_approval → replay returns the same ids → audit event DecisionMade`

Also proven by the host suite, without a model:

`login → POST /api/personal-intelligence/trading/loops → same decision on replay → POST .../paper returns PAPER → live=true returns 400 before a fixture run`

A model failure does not create that task. The local Ollama check below returned `unreadable` and the decision count stayed unchanged.

There is still no separate API that creates a task without a decision. That is the existing contract: the task follows a saved paper decision.

## 7. LLM configuration and credentials

No API key was added to source, the sample env file, logs, or this report. The four keys stay unset on VPS B. Default `Enabled=false`, so the service starts and paper loops, health, and login keep working.

Local free provider found: Ollama `0.40.13` on `127.0.0.1:11434`, model `qwen2.5:7b`. Two calls were made to that loopback port only, with a non-secret bearer placeholder. Each call hit the 45 second cap. Outcome both times: `unreadable`, next step "Không gọi được mô hình trong thời gian chờ. Không có handler nào chạy." One HTTP call per attempt. No decision was added after those calls.

Live intent end-to-end therefore stays unaccepted. The timeout path is accepted. A faster local model, or an approved key for another OpenAI-compatible endpoint, is a later configuration change. It is not part of this install. The install script refuses to run while `PersonalIntelligence__BusinessIntent__Enabled=true`.

## 8. Research publication

The reader returns `MISSING` / `Publication is missing.` when the root is empty or missing. The host test observed that code. An empty root does not block health, login, or the paper ledger.

`deploy/second-brain-agent/publication-sample/README.md` lists the real `research-publication/v1` files and hashes. It contains no sample JSON. It must not be set as `PersonalIntelligence__ResearchPublication__Root`. No research export was invented. Publication ingestion stays a data precondition.

## 9. Security acceptance

From the archive tests and the ledger SQL test:

- Live trading is refused before a fixture run.
- Guard refuses `novixa_prod`, `trading_research`, `kit_pi_validation`, port `5432`, and host `103.200.23.229`.
- `WorkerEnabled=true` is refused, and the host forces the flag false after binding.
- Health reports `workerEnabled=false` and `backfillWorkerRegistered=false`.
- Login response does not echo the fixture password. Guard errors do not echo `Password=`.
- A SECOND_BRAIN caller cannot use the KIT_MKT ledger identity, and a cross-tenant row fails selection.
- The model bearer value is not copied into the outbound JSON body (`IntentModelLimitTests`).

Refresh tokens for `sb-operator` were not revoked. Storage is `second_brain_host.refresh_token` (`token_hash`, `user_id`, `expires_at`, `revoked_at`). The raw token is not stored. Login inserts a hash. Refresh revokes the presented hash and inserts a new row. Logout revokes the presented hash and does not invalidate an access JWT that was already issued. Three unexpired rows for user `22222222-2222-4222-8222-222222222222` remained at the earlier acceptance read. Their access tokens are already past the 8 hour lifetime. Refresh remains possible until `2026-11-06`.

Separate approval, not run in this task, on VPS B only:

```sql
UPDATE second_brain_host.refresh_token
SET revoked_at = NOW()
WHERE user_id = '22222222-2222-4222-8222-222222222222'
  AND revoked_at IS NULL;
```

Expected effect: those refresh rows can no longer mint an access token. No hash needs to be printed. Do not run this against any other database.

## 10. Artifact

Directory: `E:\sb-agent-v1-2c1ea353`  
Checksum file: `E:\sb-agent-v1-2c1ea353\SHA256SUMS.txt`  
Re-read after writing: 0 mismatches.

| Path | Bytes | SHA-256 |
|---|---:|---|
| `source.tar` | 341698560 | `804e8bd42431db2c513cec2e8310b8caf3b3d37882f34d44cf53b133d5cec3da` |
| `publish/host/KitPlatform.SecondBrainAgent.Host.dll` | 83456 | `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` |
| `publish/ui/index.html` | 409 | `bcf870925d0c65115c3d0160ce6aa58b1813a6f209ed69e7af99d8eab05ee8fb` |
| `rollback/host/KitPlatform.SecondBrainAgent.Host.dll` | 83456 | `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263` |
| `package/install-candidate.sh` | 3772 | `c59a2091805a0486533a34eca0dc3183f6055e63d865dc1af58d0a7a965522ec` |

UI source is unchanged since `5f29e1bd`. `index.html` matches the file already served from `/opt/second-brain-agent/ui`. The install script copies only the host directory and does not change `SecondBrain__WebRoot`.

## 11. Deployment package

`E:\sb-agent-v1-2c1ea353\package`

- `install-candidate.sh` — refuses to run unless `APPROVE=yes` and `TARGET_HOST=vps-b`. Checks the new DLL hash, the running DLL hash, the absence of port 5432 / `novixa_prod` / `103.200.23.229` in the env file, `WorkerEnabled` not true, and the model not enabled. Installs under `/opt/second-brain-agent-v1-2c1ea353`, swaps the systemd drop-in, restarts only `second-brain-agent`, and restores the previous drop-in if `/api/health` or `/api/health/db` is not ok.
- `agent.env.example.md` — names and safe defaults. No secrets.
- `publication-sample/README.md` — not a publication package.

The script was not executed.

## 12. Rollback package

`E:\sb-agent-v1-2c1ea353\rollback\host`  
DLL SHA-256 `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263`  
This is the binary running at `/opt/second-brain-agent-v1-5f29e1bd/host/KitPlatform.SecondBrainAgent.Host.dll`.  
The older on-disk backup `/var/backups/second-brain-agent-891e5726` remains the previous generation and is not the rollback target for this candidate.

## 13. Environment

Leave `/etc/second-brain-agent/agent.env` as it is for this install. Required names already present there:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://127.0.0.1:5095`
- `ConnectionStrings__Default` — `127.0.0.1`, port `5433`, database `second_brain_agent_v1`
- `SecondBrain__JwtSecret` — at least 32 characters
- `SecondBrain__BootstrapAdminPassword` — used only when `public.users` is empty
- `SecondBrain__WebRoot=/opt/second-brain-agent/ui`
- `SecondBrain__SystemIdentifier=7702079316921501630`
- `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`
- `PersonalIntelligence__ResearchPublication__Root=` empty

Model keys, set only under a later approval, in the service env file:

- `PersonalIntelligence__BusinessIntent__Enabled`
- `PersonalIntelligence__BusinessIntent__BaseUrl`
- `PersonalIntelligence__BusinessIntent__ApiKey`
- `PersonalIntelligence__BusinessIntent__Model`

Optional when the model is enabled: `MaxOutputTokens`, `MaxCallsPerProcess`, `RequestTimeoutSeconds`.

## 14. Install and runtime checks

Not done in this task. After approval, on VPS B `157.10.53.182` only:

1. Copy `E:\sb-agent-v1-2c1ea353` to the server. Recompute SHA-256 for the host DLL and require `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285`.
2. Confirm the running DLL is `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263` and the drop-in is `/etc/systemd/system/second-brain-agent.service.d/v1-5f29e1bd.conf`.
3. Run `package/install-candidate.sh` with `APPROVE=yes`, `TARGET_HOST=vps-b`, `RELEASE_ID=2c1ea353`, `ARTIFACT` set to the unpacked directory, `EXPECTED_HOST_SHA256` set to the new hash, and `ROLLBACK_HOST_SHA256` set to the running hash.
4. The script restarts only `second-brain-agent`. It rolls back the drop-in if health fails.
5. After a successful health response, confirm `GET /api/health` has `status=ok`, `workerEnabled=false`, `backfillWorkerRegistered=false`, and `GET /api/health/db` reports database `second_brain_agent_v1` on port `5433`.
6. Confirm `kit-platform-api`, nginx, and the PostgreSQL clusters were not restarted.
7. Do not migrate, do not edit the env file, do not enable the worker, do not connect an exchange, and do not revoke tokens unless the separate SQL above is approved.

## 15. Limits that remain

- Live model acceptance is open. Local `qwen2.5:7b` did not answer inside 45 seconds, and no paid credential was used.
- Research publication content is absent. Empty root stays `MISSING`.
- Three refresh tokens stay valid until 2026-11-06 unless the separate revoke is approved.
- `BusinessIntentHeldOutTests` stays excluded until a model is intentionally configured for that harness.
- The candidate is not deployed. VPS B continues on commit `5f29e1bd` and DLL `336ca77d…`.

`READY_FOR_DEPLOY_APPROVAL`
