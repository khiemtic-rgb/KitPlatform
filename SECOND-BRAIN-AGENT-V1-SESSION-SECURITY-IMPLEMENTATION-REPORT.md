# SECOND BRAIN AGENT V1 — SESSION SECURITY IMPLEMENTATION REPORT

Conclusion: **ACCEPTED** for source, isolated tests, and release preparation.

Production was not changed. The migration file was not applied on VPS B. No commit was created because the working tree contains unrelated work.

## Source changes

Baseline commit `2c1ea353c58fcb1cf74d996bade25df28f34f8af`. The release tree is a `git archive` of that commit plus only the files below. `JwtTokenService.cs` and `JwtSettings.cs` were not edited. `AuthService.cs` already had unrelated working-tree edits and was not edited for this work.

| File | Change |
|---|---|
| `src/Hosts/SecondBrainAgent/KitPlatform.SecondBrainAgent.Host/AgentAuth.cs` | Login creates `auth_session`. Refresh keeps the same `session_id`. Logout revokes the session and every refresh row that belongs to it. Access JWT carries `sid` and no longer carries `role=ADMIN`. |
| `src/Hosts/SecondBrainAgent/KitPlatform.SecondBrainAgent.Host/Program.cs` | JWT `OnTokenValidated` requires an active session for the same user and tenant. Startup refuses to run when `auth_session` or `refresh_token.session_id` is missing. |
| `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Api/PersonalIntelligenceAuthorizationExtensions.cs` | Shared policies keep the platform `ADMIN` role check and add the capability aliases below. Research read no longer accepts `trading.run`. Intent creation no longer accepts research or exchange read. |
| `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Api/PaperTaskController.cs` | Task list uses `TaskRead` instead of the intent-create policy. |
| `src/Packs/PersonalIntelligence/KitPlatform.Packs.PersonalIntelligence.Api/TradingDeskController.cs` | PAPER approve and reject use only the approve policy. Desk reads and imports still require the run policy. |
| `deploy/second-brain-agent/001_auth_session.sql` | New migration file. Not added to any auto-apply manifest. |
| `tests/KitPlatform.SecondBrainAgent.Tests/SessionSecurityTests.cs` | Session, race, and permission tests. |
| `tests/KitPlatform.SecondBrainAgent.Tests/IsolatedHostTests.cs` | Applies the migration only to loopback `second_brain_agent_v1` on port 5433 before the host starts. |
| `tests/KitPlatform.PersonalIntelligence.Tests/ResearchPublicationTests.cs` | `trading.run` is no longer expected to read research. |
| `tests/KitPlatform.PersonalIntelligence.Tests/BusinessIntentTests.cs` | Research read cannot create an intent. Intent creation uses `intent.create`. |

## Architecture before / after

Before: an agent login inserted a refresh hash with no session. Refresh revoked that hash and inserted another hash. Logout revoked only the hash that was presented. The access JWT lived for eight hours with no `sid` and with a hardcoded `role=ADMIN` claim, so logout did not invalidate it. Shared policies treated that role as platform admin.

After: agent login inserts one `second_brain_host.auth_session` row and one refresh row that points at it. The access JWT contains `sid` and the permission claims stored for that operator. It does not contain `role=ADMIN`. Every authenticated agent request looks up that session and rejects the token when the session is missing, revoked, expired, or belongs to another user or tenant. A token without `sid` is rejected. Platform `JwtTokenService` and `AuthService` are unchanged, so NOVIXA and the other Kit Platform modules keep their own issuer, audience, and refresh behavior.

The shared Personal Intelligence policies still accept a real platform `ADMIN` role. That short-circuit is for platform administrators. An agent token no longer receives the role, so it cannot use it.

## Session lifecycle

1. Login opens a transaction, inserts `auth_session` (`session_id`, `user_id`, `tenant_id`, `expires_at` = access expiry + 7 days), and inserts the first refresh hash with that `session_id`.
2. The access JWT is HMAC-SHA256, issuer and audience `SecondBrainAgent`, claims `sub`, `tenant_id`, `tenant_code`, `sid`, and one `permission` claim per stored permission. Access lifetime stays 8 hours.
3. Refresh locks the session row with `SELECT … FOR UPDATE OF s`. It continues only when the presented hash is unrevoked, the session is unrevoked and unexpired, and the operator row still belongs to that user and tenant. It revokes only the presented hash, inserts a new hash with the same `session_id`, and extends `auth_session.expires_at`. It does not create a session.
4. A refresh row whose `session_id` is null does not match the join, returns 401, and does not create a session. That is the compatibility path for rows that already exist.
5. Logout locks the session through the presented hash, even when that hash was already rotated. It sets `auth_session.revoked_at` and revokes every refresh hash for that `session_id`. Another session of the same user is a different row and stays active.
6. A presented hash that has no session is revoked by itself. That covers old rows and does not invent a session.
7. `OnTokenValidated` rejects the request when `sid`, `sub`, or `tenant_id` is missing or does not match an active session row. The check is user and tenant, not only the session id.

Refresh and logout take the same row lock, so one commits before the other proceeds. After both finish, the original access token is rejected. If refresh issued a new access token before logout committed, that new token is also rejected because logout revokes the session after the lock is released to it.

## Permission matrix

These are the policies that exist after the change. `ADMIN` in this table means the platform role claim, not a claim the agent now issues.

| Capability | Policy | Accepted permissions | Platform ADMIN |
|---|---|---|---|
| Authenticated profile `/api/auth/me` | authenticated session | any active agent session | not required |
| Agent status `agent.read` | no endpoint uses it yet | stored only for a future empty-database bootstrap | no |
| Research read | `ResearchRead` | `personal_intelligence.research.read` | yes |
| Create intent | `Intent` | `personal_intelligence.intent.create`, `personal_intelligence.trading.run`, `personal_intelligence.paper.execute` | yes |
| Read tasks | `TaskRead` | `task.read`, `intent.create`, `trading.run`, `paper.execute`, `trading.approve`, `paper.approve` | yes |
| Approve PAPER | `Approve` on the paper and reject actions only | `personal_intelligence.trading.approve`, `personal_intelligence.paper.approve` | yes |
| Execute PAPER / run trading | `Run` | `personal_intelligence.trading.run`, `personal_intelligence.paper.execute` | yes |
| Exchange connect | `ExchangeConnect` | `personal_intelligence.exchange.connect` | yes |
| Exchange read | `ExchangeRead` | `personal_intelligence.exchange.read`, `personal_intelligence.exchange.connect` | yes |

`trading.run` does not grant research read, paper approval, or exchange access. `research.read` does not grant intent creation, paper approval, trading, or exchange. `trading.approve` does not grant trading run. Exchange read does not grant research or trading.

The existing operator row is not updated. A database that already has users keeps its stored roles and permissions. On the current operator those stored values are `trading.run`, `trading.approve`, `research.read`, and `exchange.read`, plus the database role `ADMIN`. After a future deploy the JWT will carry the four permissions and will not carry `role=ADMIN`, so paper approve, paper execute, research read, and exchange read keep working. Exchange connect will return 403 because `exchange.connect` is not stored and the agent token no longer has the admin role. This work does not grant that permission.

A brand-new database whose `public.users` table is empty bootstraps role `OPERATOR` and the permissions `agent.read`, `research.read`, `intent.create`, `task.read`, and `exchange.read`. It does not bootstrap `ADMIN`, `trading.run`, or `trading.approve`. Local and production databases already have users, so this path does not run there.

## Migration

File: `deploy/second-brain-agent/001_auth_session.sql`.

- Creates `second_brain_host.auth_session` (`session_id` primary key, `user_id` and `tenant_id` foreign keys, `created_at`, `expires_at`, nullable `revoked_at`).
- Adds nullable `second_brain_host.refresh_token.session_id` with a foreign key to `auth_session`. The column is not unique, because rotation stores many hashes for one session.
- Adds partial index `ix_refresh_token_session` and index `ix_auth_session_user`.
- Does not backfill old rows. Existing hashes stay, with `session_id` null.
- Not listed in `migration-files.prod.txt`, `migration-files.family-os.txt`, `migration-files.local-os.txt`, or the personal-intelligence manifest.

The file was executed only on the local loopback test database `second_brain_agent_v1` at `127.0.0.1:5433`, so the isolated tests could start. It was not executed on VPS B, port 5432, `novixa_prod`, or any other database. The local cluster was stopped afterward.

The migration that needs approval before any deploy is `deploy/second-brain-agent/001_auth_session.sql`, against `second_brain_agent_v1` on VPS B port 5433 only.

## Test results

Isolated cluster `127.0.0.1:5433`, database `second_brain_agent_v1`. The same tests were run from the clean overlay tree.

`KitPlatform.SecondBrainAgent.Tests`: 28 passed, 0 failed.

Session coverage inside that run:

- Login creates a session and the access JWT `sid` matches it.
- Refresh keeps the same `sid`. The refresh token from before rotation is rejected.
- Logout revokes that session. The old access JWT and the refresh token are rejected afterward.
- A second session of the same user still calls `/api/auth/me` and can refresh.
- A token whose `sub` or `tenant_id` does not match the session is rejected.
- A token with no `sid` is rejected.
- Five concurrent refresh/logout pairs leave the target session rejected and the other session usable.
- `research.read` can read research and receives 403 for trading summary, tasks, exchange catalog, intent creation, paper approval, and trading loops.
- `task.read` can list tasks and receives 403 for intent creation and paper approval.
- A request with no session receives 401.

`KitPlatform.PersonalIntelligence.Tests`, excluding `BusinessIntentHeldOutTests`, `LocalOllamaIntentTests`, and `RuntimeHttpTests`: 145 passed, 0 failed. That includes paper ledger mapping for `KIT_MKT` and `SECOND_BRAIN`, idempotent paper replay, and live-order refusal. No model key was set and Local Ollama was not called.

## Regression results

- Paper ledger selection and idempotent replay passed.
- Live execution is still refused before an order is placed.
- Platform `JwtTokenService.cs` has no diff against `2c1ea353`.
- `KitPlatform.Application.Tests` was not rebuilt. A local `KitPlatform.Api` process, PID 28112, holds the API output files. That process was not stopped.
- `RuntimeHttpTests` target the prebuilt API at `E:\KitPlatform\.dev\sb-runtime-http`. An earlier run returned HTTP 404 from that binary before authorization. That binary was not rebuilt and is not part of this package.

## Security limitations

- Access tokens still last 8 hours. Revocation is the session lookup, not a shorter lifetime.
- `/api/auth/me` is available to any active session. `agent.read` is not a gate on it.
- The profile JSON still returns the role array stored in `second_brain_host.operator`. That array is not copied onto the agent JWT and is not an authorization decision.
- Old refresh rows with a null `session_id` cannot mint a new access token after this binary is deployed. They are not deleted by this work.
- The three open production refresh tokens were not revoked.
- Exchange connect is not granted to the existing operator.
- A platform user whose only Personal Intelligence permission is `trading.run` can no longer read research. A user whose only permission is `research.read` can no longer create an intent. Both boundaries are intentional.
- The host still registers exchange connectors in process. The worker stays off. This change does not split that registration.

## Release artifact and checksum

Directory: `E:\sb-agent-v1-session-security`.

The host was published in Release from that clean tree, not from the dirty working tree. `package/host/appsettings.json` contains no secret. No new configuration key is required.

| File | Bytes | SHA-256 |
|---|---|---|
| `package/host/KitPlatform.SecondBrainAgent.Host.dll` | 99840 | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| `package/host/KitPlatform.Packs.PersonalIntelligence.Api.dll` | 67584 | `44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0` |
| `package/001_auth_session.sql` | 1086 | `7aa9fcc61788720350f7ee36fda190a1249354a4c5d1fd5763014c1cb454b268` |
| `package/ROLLBACK.md` | 1565 | `26799b8618d581548f201b44c4a366fdf6f1807bd3cc844c10e04deff7b18dbe` |

`package/SHA256SUMS.txt` repeats these hashes. The base archive commit id is `2c1ea353c58fcb1cf74d996bade25df28f34f8af`.

## Rollback

Do not install this package. VPS B remains on DLL `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285` under `/opt/second-brain-agent-v1-2c1ea353/host`.

The new host will not start until the session table exists. If a later deploy fails health, restore the `2c1ea353` drop-in and that DLL, and start only `second-brain-agent`. Leave `auth_session` in place. The full procedure is `E:\sb-agent-v1-session-security\package\ROLLBACK.md`.

## Production actions that still need approval

1. Apply `deploy/second-brain-agent/001_auth_session.sql` to `second_brain_agent_v1` on VPS B port 5433.
2. Deploy host DLL `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` and the matching Personal Intelligence API DLL. Do not start the new binary before the migration.
3. Decide whether to revoke the three open refresh tokens explicitly. After the new binary is running they cannot refresh or authorize, because their `session_id` is null. This work did not revoke them.
4. No configuration change, no new secret, no LLM enablement, no exchange backfill, and no permission grant for the existing operator.

No commit, push, merge, deploy, service restart, or production database write was performed.
