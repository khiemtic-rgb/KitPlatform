# SECOND BRAIN AGENT V1 — SESSION SECURITY DEPLOYMENT PREFLIGHT

Verdict: **CONDITIONAL GO**.

The package checksums match, the migration is compatible with the existing refresh table, and the host fails closed when the session table or `session_id` column is missing. This preflight does not approve execution. VPS B was not contacted. No migration, restart, token revoke, or file change was performed.

It is **NO-GO** to run `deploy/second-brain-agent/install-candidate.sh` for this release. That script still names drop-in `v1-5f29e1bd.conf`, expects `ARTIFACT/publish/host`, does not apply `001_auth_session.sql`, and restarts the service. The active drop-in is `v1-2c1ea353.conf`.

## Verified artifact checksums

Checked on this machine against `E:\sb-agent-v1-session-security\package\SHA256SUMS.txt`. All four lines match. The SQL in the package and `deploy/second-brain-agent/001_auth_session.sql` are the same file, 1086 bytes.

| File | Bytes | SHA-256 | Result |
|---|---|---|---|
| `package/host/KitPlatform.SecondBrainAgent.Host.dll` | 99840 | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` | match |
| `package/host/KitPlatform.Packs.PersonalIntelligence.Api.dll` | 67584 | `44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0` | match |
| `package/001_auth_session.sql` | 1086 | `7aa9fcc61788720350f7ee36fda190a1249354a4c5d1fd5763014c1cb454b268` | match |
| `package/ROLLBACK.md` | 1565 | `26799b8618d581548f201b44c4a366fdf6f1807bd3cc844c10e04deff7b18dbe` | match |

Running VPS B host, not in this package and not modified: `/opt/second-brain-agent-v1-2c1ea353/host/KitPlatform.SecondBrainAgent.Host.dll`, SHA-256 `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285`. `package/host/appsettings.json` has no secret. No new environment key is required.

## Migration compatibility

`001_auth_session.sql` does four things and nothing else:

1. `CREATE TABLE IF NOT EXISTS second_brain_host.auth_session` with `session_id UUID PRIMARY KEY`, `user_id` and `tenant_id` `NOT NULL` foreign keys to `public.users(id)` and `public.tenants(id)`, `created_at` and `expires_at` `NOT NULL`, `revoked_at` nullable.
2. `ALTER TABLE second_brain_host.refresh_token ADD COLUMN IF NOT EXISTS session_id UUID` with a foreign key to `auth_session(session_id)`.
3. Partial index `ix_refresh_token_session` on `refresh_token(session_id) WHERE session_id IS NOT NULL`.
4. Index `ix_auth_session_user` on `auth_session(user_id, tenant_id)`.

Compatibility with `000_identity_minimum.sql`:

| Item | Current `refresh_token` | After `001` |
|---|---|---|
| Primary key | `token_hash` | unchanged |
| `user_id`, `expires_at` | `NOT NULL` | unchanged |
| `revoked_at` | nullable | unchanged |
| `session_id` | absent | nullable UUID, not unique |
| Existing row contents | hash, user, expiry, revoke time | same values; `session_id` is null |

`session_id` is not unique because refresh rotation stores many hashes for one session. Null is allowed, so the foreign key does not reject existing rows. There is no `DEFAULT`, no `UPDATE`, and no backfill. PostgreSQL 16 adds a nullable column with no default as a catalog change. It takes a short `ACCESS EXCLUSIVE` lock on `second_brain_host.refresh_token` and does not rewrite the hash values.

Re-running the file is idempotent: `IF NOT EXISTS` on the table, the column, and both indexes. A second successful run does not create sessions and does not fill `session_id`. One caveat: `ADD COLUMN IF NOT EXISTS` does not repair a column that already exists without the foreign key. The script adds the column and the foreign key in the same statement, so a committed run has both. The host startup check does not look at the foreign key or the indexes. The verification queries below are required before the restart.

The file is not in `migration-files.prod.txt` or any other auto-apply manifest. Apply it only to database `second_brain_agent_v1` on `127.0.0.1:5433`.

## Existing-session compatibility

Existing rows do not receive a `session_id`. The column stays null. The migration does not revoke them and does not create `auth_session` rows for them.

| Step | Existing refresh rows | Access JWTs issued by the current host |
|---|---|---|
| Migration, old process still running | Stay open. `session_id` is null. Old inserts omit the new column and still succeed. | Still accepted by the current process. |
| New host starts | Still stored. Refresh joins `auth_session` and returns 401. No session is created for them. | Rejected. They have no `sid`. Startup does not accept a missing `sid`. |
| Logout of an old raw refresh token on the new host | That one hash is revoked because `session_id` is null. No other row is revoked. | Already rejected for lack of `sid`. |

The binary swap is what cuts off existing agent sessions. The migration alone does not. Any access JWT still inside its 8-hour life stops working when the new process is listening. A new login after the swap creates the first real session.

The last read of production, on 2026-10-30, showed three open rows and one already revoked row for user `22222222-2222-4222-8222-222222222222` (`sb-operator`). Their access JWTs had already expired before 11:22 +07. Those three can still refresh until 2026-11-06 on the current binary. After the new binary they cannot. Recount them in preflight. If more open rows exist now, they are legacy too and follow the same null-`session_id` rule. Do not assume the count is still three.

## Startup refusal

Before the new host listens, it requires both of the following on the configured database:

- `to_regclass('second_brain_host.auth_session')` is not null
- `information_schema.columns` contains exactly one `second_brain_host.refresh_token.session_id`

If either check fails it throws `second_brain_host.auth_session is required before the agent accepts sessions` and the process exits. It does not serve traffic with the old session behavior. A start without the migration takes port 5095 down until the drop-in is restored or the migration is present. Apply and verify the migration first, then restart.

The same startup path still refuses port 5432, database `novixa_prod`, host `103.200.23.229`, a mismatched `system_identifier`, `WorkerEnabled=true`, and a hosted service whose name contains ExchangeBackfill, Medication, Pharmacy, Content, or Famixa. `PostConfigure` forces the exchange backfill worker off. Do not change `agent.env`.

## Three old refresh tokens

They remain identifiable without the hash and without the raw token. The table still has no id and no `created_at`. After the migration they are the open rows for that user whose `session_id` is null.

Last observed open expiries, all +07, all for `sb-operator`:

| Expires | State at last read |
|---|---|
| 2026-11-06 04:22:21 | open |
| 2026-11-06 04:23:57 | open |
| 2026-11-06 05:14:13 | open |
| 2026-11-06 18:00:42 | already revoked |

Safe revoke, not part of the migration and not executed by this preflight. Run it only with a separate approval, only on `second_brain_agent_v1` port 5433, and only with `session_id IS NULL`. That predicate does not revoke a session created by the new host.

```sql
UPDATE second_brain_host.refresh_token
SET revoked_at = NOW()
WHERE user_id = '22222222-2222-4222-8222-222222222222'
  AND revoked_at IS NULL
  AND session_id IS NULL;
```

Do not use a revoke that omits `session_id IS NULL`. After the first new login, that statement would revoke the new session as well.

If this revoke is not approved, leave the rows. The new host still rejects them. They stay visible by `expires_at` for a later decision. If the revoke is approved, run it after the migration check and before the binary swap, before any acceptance login.

## What can affect sessions or other services

| Action | Effect |
|---|---|
| Migration on 5433 | Short lock on `second_brain_host.refresh_token`. Current agent stays up. No session id is assigned. |
| Restart of `second-brain-agent` only | Port 5095 is briefly down. The last swap of this service refused connections for about one second, then health recovered. |
| New process accepts traffic | Every current agent access JWT and every legacy refresh row stops working. This is the intended cutover. |
| `kit-platform-api`, nginx, `postgresql@16-main` (5432), `postgresql@16-sbagent` | Not restarted and not migrated. Their PIDs must be the same after the window. |
| Platform JWT issuer `KitPlatform` | Unchanged. This host's issuer remains `SecondBrainAgent`. |
| Paper, exchange, research publication, model | No schema change and no new production business row. Acceptance must not post a paper decision, connect an exchange, or enable the model. |

## Exact command sequence

Host: VPS B `157.10.53.182` only. Do not open `103.200.23.229`. Do not use port 5432. Do not print `agent.env` values, token hashes, or JWTs.

Run the blocks in order. Stop at the first failed check. The service restart is block 6 and must not run before block 4 passes.

### 1. Local preflight, before upload

```powershell
Get-FileHash -Algorithm SHA256 @(
  'E:\sb-agent-v1-session-security\package\host\KitPlatform.SecondBrainAgent.Host.dll',
  'E:\sb-agent-v1-session-security\package\host\KitPlatform.Packs.PersonalIntelligence.Api.dll',
  'E:\sb-agent-v1-session-security\package\001_auth_session.sql'
)
```

Require the three hashes in the table above. Upload the `package` directory to `/var/tmp/sb-agent-session-security` on VPS B. On the server:

```bash
cd /var/tmp/sb-agent-session-security
sha256sum -c SHA256SUMS.txt
test "$(sha256sum host/KitPlatform.SecondBrainAgent.Host.dll | awk '{print $1}')" = "ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc"
```

### 2. Runtime preflight

```bash
systemctl is-active second-brain-agent
systemctl show second-brain-agent -p WorkingDirectory -p MainPID -p NRestarts --no-page
sha256sum /opt/second-brain-agent-v1-2c1ea353/host/KitPlatform.SecondBrainAgent.Host.dll
test -f /etc/systemd/system/second-brain-agent.service.d/v1-2c1ea353.conf
curl -fsS http://127.0.0.1:5095/api/health
curl -fsS http://127.0.0.1:5095/api/health/db
grep -E '^(ASPNETCORE_URLS|PersonalIntelligence__ExchangeBackfill__WorkerEnabled|PersonalIntelligence__ResearchPublication__Root|PersonalIntelligence__BusinessIntent__Enabled)=' /etc/second-brain-agent/agent.env
if grep -Eq '5432|novixa_prod|103\.200\.23\.229' /etc/second-brain-agent/agent.env; then echo FORBIDDEN_ENV; exit 2; fi
```

Abort unless the running DLL is `e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285`, the working directory is `/opt/second-brain-agent-v1-2c1ea353/host`, health is ok, the URL is `http://127.0.0.1:5095`, and `WorkerEnabled=false`. `BusinessIntent__Enabled=true` must be absent. Record the peer PIDs of `kit-platform-api`, nginx, `postgresql@16-sbagent`, and `postgresql@16-main`, and do not restart those units.

### 3. Database identity and backup

```bash
sudo -u postgres psql -h 127.0.0.1 -p 5433 -d second_brain_agent_v1 -v ON_ERROR_STOP=1 -c "SELECT current_database(), inet_server_port(), system_identifier::text FROM pg_control_system();"
```

Abort unless the result is `second_brain_agent_v1`, `5433`, and `7702079316921501630`.

```bash
install -d -m 700 /var/backups
sudo -u postgres pg_dump -h 127.0.0.1 -p 5433 -d second_brain_agent_v1 -n second_brain_host -Fc -f /var/backups/second-brain-host-before-001-auth-session.dump
chown root:root /var/backups/second-brain-host-before-001-auth-session.dump
chmod 600 /var/backups/second-brain-host-before-001-auth-session.dump
cp -a /etc/systemd/system/second-brain-agent.service.d/v1-2c1ea353.conf /var/backups/second-brain-agent-dropin-2c1ea353.conf
```

Leave the dump on VPS B. Do not copy it off the host. It contains refresh hashes, not raw tokens, and those hashes must not be printed.

Record the legacy rows before DDL. Abort if `auth_session` already exists or if `session_id` already exists. This preflight assumes a first application.

```bash
sudo -u postgres psql -h 127.0.0.1 -p 5433 -d second_brain_agent_v1 -v ON_ERROR_STOP=1 <<'SQL'
SELECT to_regclass('second_brain_host.auth_session') AS auth_session;
SELECT COUNT(*) AS session_id_column
FROM information_schema.columns
WHERE table_schema = 'second_brain_host'
  AND table_name = 'refresh_token'
  AND column_name = 'session_id';
SELECT COUNT(*) AS refresh_rows,
       COUNT(*) FILTER (WHERE revoked_at IS NULL) AS open_rows
FROM second_brain_host.refresh_token;
SELECT expires_at, (revoked_at IS NULL) AS open
FROM second_brain_host.refresh_token
WHERE user_id = '22222222-2222-4222-8222-222222222222'
ORDER BY expires_at;
SQL
```

Expect `auth_session` null and `session_id_column` 0. Save the open count and the expiry list. The three open expiries above must still be present, or stop and reconcile before DDL.

### 4. Migration

```bash
sudo -u postgres psql -h 127.0.0.1 -p 5433 -d second_brain_agent_v1 -v ON_ERROR_STOP=1 -f /var/tmp/sb-agent-session-security/001_auth_session.sql
```

```bash
sudo -u postgres psql -h 127.0.0.1 -p 5433 -d second_brain_agent_v1 -v ON_ERROR_STOP=1 <<'SQL'
SELECT current_database(), inet_server_port();
SELECT to_regclass('second_brain_host.auth_session') AS auth_session;
SELECT COUNT(*) AS sessions FROM second_brain_host.auth_session;
SELECT is_nullable
FROM information_schema.columns
WHERE table_schema = 'second_brain_host'
  AND table_name = 'refresh_token'
  AND column_name = 'session_id';
SELECT c.conname
FROM pg_constraint c
JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
WHERE c.conrelid = 'second_brain_host.refresh_token'::regclass
  AND c.contype = 'f'
  AND a.attname = 'session_id';
SELECT indexname
FROM pg_indexes
WHERE schemaname = 'second_brain_host'
  AND indexname IN ('ix_refresh_token_session', 'ix_auth_session_user')
ORDER BY indexname;
SELECT COUNT(*) AS refresh_rows,
       COUNT(*) FILTER (WHERE revoked_at IS NULL) AS open_rows,
       COUNT(*) FILTER (WHERE session_id IS NOT NULL) AS with_session
FROM second_brain_host.refresh_token;
SELECT expires_at, (revoked_at IS NULL) AS open, (session_id IS NULL) AS legacy
FROM second_brain_host.refresh_token
WHERE user_id = '22222222-2222-4222-8222-222222222222'
ORDER BY expires_at;
SQL
```

Pass requires all of the following: database and port unchanged, `auth_session` exists, `sessions = 0`, `session_id` nullable (`YES`), one foreign key on that column, both indexes present, `with_session = 0`, and the refresh row count and open count equal the pre-DDL numbers. The service is still the old process. Do not restart until this passes.

### 5. Optional legacy revoke

Skip this block unless the revoke is explicitly approved. It runs after the migration check and before the binary swap.

```bash
sudo -u postgres psql -h 127.0.0.1 -p 5433 -d second_brain_agent_v1 -v ON_ERROR_STOP=1 <<'SQL'
UPDATE second_brain_host.refresh_token
SET revoked_at = NOW()
WHERE user_id = '22222222-2222-4222-8222-222222222222'
  AND revoked_at IS NULL
  AND session_id IS NULL;
SQL
```

Confirm the remaining open count is the pre-DDL open count minus the legacy rows just revoked, and that no row with a non-null `session_id` exists.

### 6. Install and restart only second-brain-agent

```bash
install -d -o sbagent -g sbagent /opt/second-brain-agent-v1-session-security
cp -a /var/tmp/sb-agent-session-security/host /opt/second-brain-agent-v1-session-security/host
chown -R sbagent:sbagent /opt/second-brain-agent-v1-session-security
test "$(sha256sum /opt/second-brain-agent-v1-session-security/host/KitPlatform.SecondBrainAgent.Host.dll | awk '{print $1}')" = "ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc"
test "$(sha256sum /opt/second-brain-agent-v1-session-security/host/KitPlatform.Packs.PersonalIntelligence.Api.dll | awk '{print $1}')" = "44bd3174c50e6018f7e05f743782f9e467b68a71819540d2a33dce822b8563e0"
cat > /etc/systemd/system/second-brain-agent.service.d/v1-session-security.conf <<'EOF'
[Service]
WorkingDirectory=/opt/second-brain-agent-v1-session-security/host
ExecStart=
ExecStart=/usr/bin/dotnet /opt/second-brain-agent-v1-session-security/host/KitPlatform.SecondBrainAgent.Host.dll
EOF
rm -f /etc/systemd/system/second-brain-agent.service.d/v1-2c1ea353.conf
systemctl daemon-reload
systemctl restart second-brain-agent
```

Do not restart any other unit. Do not edit `/etc/second-brain-agent/agent.env`. Do not change `/opt/second-brain-agent/ui` or the `2c1ea353` directory. If health does not pass, run the rollback block immediately.

```bash
ok=0
for _ in 1 2 3 4 5 6 7 8 9 10; do
  if curl -fsS http://127.0.0.1:5095/api/health | grep -q '"status":"ok"' \
     && curl -fsS http://127.0.0.1:5095/api/health/db | grep -q '"status":"ok"'; then
    ok=1
    break
  fi
  sleep 1
done
test "$ok" = 1
systemctl is-active second-brain-agent
sha256sum /opt/second-brain-agent-v1-session-security/host/KitPlatform.SecondBrainAgent.Host.dll
```

## Backup and rollback

Taken in block 3, and still required if the new process fails:

- `/var/backups/second-brain-host-before-001-auth-session.dump`
- `/var/backups/second-brain-agent-dropin-2c1ea353.conf`
- Untouched binary `/opt/second-brain-agent-v1-2c1ea353/host/KitPlatform.SecondBrainAgent.Host.dll` (`e3b67b85f542538226e19e8cad6845738819c328f6b6b69361f7cad4e585c285`)

Rollback the process only:

```bash
rm -f /etc/systemd/system/second-brain-agent.service.d/v1-session-security.conf
cp -a /var/backups/second-brain-agent-dropin-2c1ea353.conf /etc/systemd/system/second-brain-agent.service.d/v1-2c1ea353.conf
systemctl daemon-reload
systemctl restart second-brain-agent
curl -fsS http://127.0.0.1:5095/api/health
curl -fsS http://127.0.0.1:5095/api/health/db
sha256sum /opt/second-brain-agent-v1-2c1ea353/host/KitPlatform.SecondBrainAgent.Host.dll
```

Leave `auth_session` and `refresh_token.session_id` in place. The `2c1ea353` binary does not read them, and its insert still succeeds because the column is nullable. Do not drop the table, do not restore the dump over the live table, and do not revoke tokens as part of rollback.

Schema removal is a separate approval, and only after the old binary is healthy again:

```sql
ALTER TABLE second_brain_host.refresh_token DROP COLUMN IF EXISTS session_id;
DROP TABLE IF EXISTS second_brain_host.auth_session;
```

## Expected downtime

`second-brain-agent` on `127.0.0.1:5095` only. Budget the restart at under 10 seconds. The previous swap of this service refused connections for about one second and then returned health ok. The migration lock is earlier, while the old process is still serving, and is a short catalog lock on one small table.

No downtime is planned for `kit-platform-api`, nginx, either PostgreSQL cluster, or VPS A. Existing agent callers lose their current access JWT when the new process starts, even if 5095 is only down for a second. They need a new login.

## Runtime acceptance matrix

Run this after health is ok. Print status codes, claim names, and whether `sid` equals `session_id`. Do not print access tokens, refresh tokens, hashes, or passwords. Do not post a paper decision, a live loop, an exchange connection, or an intent that can write a task. The model and the publication root stay off, so publication remains `MISSING` and that response is success.

| Check | Expected |
|---|---|
| `GET /api/health` and `GET /api/health/db` | 200, `"status":"ok"` |
| Journal during startup | No ExchangeBackfill hosted service. Worker remains disabled. |
| Env file | Same keys as before the swap. `WorkerEnabled=false`. No `BusinessIntent__Enabled=true`. No `5432`, `novixa_prod`, or `103.200.23.229`. |
| Running DLL | `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` |
| Login `sb-operator` / `SECOND_BRAIN` | 200. JWT contains `sid` and does not contain `role`. `sid` equals the new `auth_session.session_id`. |
| Second login | Different `sid`. First session still answers `GET /api/auth/me`. |
| Refresh of the first session | 200, same `sid`. The pre-rotation refresh token returns 401. |
| Logout of the first session | 204. That access JWT returns 401. That refresh token returns 401. The second session still returns 200. |
| JWT signed with the agent secret but no `sid` | 401 on `/api/auth/me` |
| JWT whose `sub` or `tenant_id` does not match the session | 401 |
| `GET /api/personal-intelligence/research/publication` with the operator token | 200, availability `MISSING` |
| `GET /api/personal-intelligence/trading/exchanges/catalog` | 200 |
| Exchange connect POST | 403. `exchange_connection` count stays 0. |
| `POST /api/personal-intelligence/trading/loops` with `live=true` | 400. No new execution row. |
| Legacy open rows, if revoke was not approved | Same three expiries, `session_id` null, `revoked_at` null. They cannot refresh. |
| Legacy open rows, if revoke was approved | Those expiries have `revoked_at` set. The acceptance session is a different row. |
| `auth_session` rows created by acceptance | Logout leaves the tested session revoked. No legacy hash is attached to a session. |
| Peer PIDs and port 5432 | Unchanged. Agent sockets stay on `127.0.0.1:5433`. |

One acceptance login creates one session and one refresh row. Logout revokes that session. That is the only production write the acceptance needs. Do not leave the acceptance session open.

## Approvals required before execution

1. Approve this command sequence, including the restart of `second-brain-agent` and the cutover that rejects every current agent access JWT.
2. Approve `001_auth_session.sql` on `second_brain_agent_v1`, port 5433, VPS B only.
3. Approve installing host DLL `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc` under `/opt/second-brain-agent-v1-session-security` and switching the drop-in.
4. Decide the three legacy refresh rows: leave them null and unusable, or run the `session_id IS NULL` revoke before the swap.
5. Approve one acceptance login and logout on `sb-operator`. No other production write.

No approval is requested for LLM, research publication root, exchange backfill, exchange connect, paper execution, VPS A, nginx, `kit-platform-api`, or either PostgreSQL restart.
