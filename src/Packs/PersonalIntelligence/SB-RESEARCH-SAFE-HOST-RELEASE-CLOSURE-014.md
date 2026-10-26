# SB-RESEARCH-SAFE-HOST-RELEASE-CLOSURE-014

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`
HEAD: `85db8bdfd269b3eef12fb1729d95d5b59596c79e`

## Decision

`BLOCKED`

Section 9 is the final gate. Artifact `sb-research-safe-host-014` and DLL SHA-256 `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9` were rechecked and still match. The preflight script in section 9.4 is ready for root to run on the production host. It has not been run there. Production host, database, publication path, and backup facts are not yet observed, so this is not a deploy approval. Restart remains a separate approval. No copy and no restart were performed.

## 1. What changed

Background writers stay registered when `ResearchSafeHost:SuppressBackgroundWriters` is unset or `false`. That is the production default. No appsettings file sets the key.

A Research Safe Host process sets the key to `true`. `ResearchSafeHostWorkerIsolation.Apply` then removes every `IHostedService` already registered, immediately before `WebApplication.Build`. The call is in `src/KitPlatform.Api/Program.cs`. The type is in `PersonalIntelligenceResearchHost.cs`.

Writers removed only when the flag is true:

| Worker | Why it was in scope |
|---|---|
| `LocalOsWatchWorker` | No off switch. After 15 seconds it can call `RunAsync` |
| `KitSalesJourneyWorker` | Runs in this host. After 10 minutes it reads sales settings and can send when a page token exists |
| `ContentWorkWorker` | `WorkerEnabled` defaults to true |
| `NotifyQueueWorker` | `Enabled` defaults to true |
| `IntegrationOutboxWorker` | Enabled in `appsettings.json` |
| `PlatformEventWorker` | Enabled in `appsettings.Development.json` |
| `FamilyOsParentReminderWorker` | Enabled in `appsettings.Development.json` |
| `KapBenchmarkAggregateWorker` | `BenchmarkAggregateEnabled` defaults to true |
| `MedicationReminderPushWorker` | Follows `CustomerAppPush` |

`AddPersonalIntelligencePack` is still not called, so `ExchangeBackfillWorker` is not registered even when the flag is false. The infrastructure assembly still contains that type.

Trading and exchange controllers stay unmapped through `ResearchOnlyControllerConvention`. Research authorization is unchanged: anonymous is 401, `research.read`, `trading.run`, and `ADMIN` can read, approval and exchange permissions are 403, and a disabled module is 403 including for `ADMIN`.

## 2. Tests and build

SDK `10.0.301`. Release.

```text
dotnet test tests/KitPlatform.PersonalIntelligence.Tests/KitPlatform.PersonalIntelligence.Tests.csproj -c Release --filter "FullyQualifiedName~ResearchPublicationTests|FullyQualifiedName~ResearchSafeHostWorkerIsolationTests"
```

Passed 19, failed 0, skipped 0. That is the 16 existing Research publication facts plus three isolation facts:

- An unset flag leaves a hosted service registered.
- The value `false` leaves it registered.
- The value `true` removes every hosted service and leaves other services in place.

Postgres integration tests were not run. Their fixture writes `kit_pi_validation`. The API process was not started. Port 5290 was not called.

NU1903 for Newtonsoft.Json 10.0.3 and the existing CS8602 warnings in `Program.cs` are unchanged by this work.

## 3. Release candidate

Deploy input is this directory only:

`artifacts/sb-research-safe-host-014/api/`

| File | SHA-256 |
|---|---|
| `KitPlatform.Api.dll` | `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9` |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | `50253A9471143B5025BB4DECB468F134740D9E90801D85D6D8A964B3B44E4FFA` |

The previous API hash `8DB1EBC5B4B1109FA0A458BB1FA9291D5C6892FFF342608E0F0D9D03CCABF3A8` is `artifacts/sb-research-safe-host-003`. It does not contain this isolation change. Do not deploy it for this closure.

UTF-8 scan of the new binaries:

| Marker | API DLL | Personal Intelligence API DLL | Personal Intelligence Infrastructure DLL |
|---|---|---|---|
| `ResearchSafeHostWorkerIsolation` | yes | yes | no |
| `ResearchOnlyControllerConvention` | yes | yes | no |
| `AddPersonalIntelligenceResearch` | yes | no | yes |
| `ExchangeBackfillWorker` | no | no | yes |

The published `appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json` do not contain `SuppressBackgroundWriters`. Those files still carry the repository's local connection settings. The copy step excludes them. Do not print them.

No new admin build was made. No client file changed. Do not rsync an admin tree as part of this candidate. The live admin tree stays as it is.

The three source files are uncommitted on top of `6596cfb9`. This session did not commit or push. The byte pin is the hash above, not HEAD.

## 4. Runbook

Do not use `scripts/deploy-update-vps.ps1`, `scripts/deploy-production.ps1`, or `scripts/second-brain-trading-ui-start.ps1`. Do not run a migration. Do not edit permissions or tenant settings during the copy. Do not load a second copy of `/etc/kit-platform/api.env` into a shell that prints it.

This binary is the shared `kit-platform-api` process. Pharmacy and the other packs stay in the process. Trading controllers from the Personal Intelligence assembly are not mapped.

### 4.1 Stop before copy

Stop if any of these is true:

- The live plan is not this file's API hash.
- The three source files are still uncommitted and the owner has not accepted the artifact hash as the pin.
- `narrative_json` on the production Content database has not been confirmed with a read-only catalog query. Do not run migration 384 to find out.
- The publication directory, if one will be set, is inside an nginx `root`.
- The backup directory is unnamed or is inside `/var/www/kit-platform`.
- The plan sets `ResearchSafeHost__SuppressBackgroundWriters` on the production unit. Production must leave that key unset so reminder, content, outbox, and Local OS workers keep their current behavior.
- The plan adds exchange credential variables.

### 4.2 Backup

Copy these to a timestamped directory outside the web root:

- `/var/www/kit-platform/api`, including `uploads/`
- `/etc/kit-platform/api.env`, with `cp -a`

Record SHA-256 of the live `KitPlatform.Api.dll` and the byte size of `api.env`. Do not record file contents. The backup DLL hash must match the live DLL. If it does not, stop.

Do not copy the admin tree for this candidate. If a publication directory already exists, copy it to the same backup area and do not create one when it is missing.

### 4.3 Copy

```bash
rsync -a --exclude 'appsettings*.json' --exclude 'uploads/' --exclude 'api.env' \
  artifacts/sb-research-safe-host-014/api/ /var/www/kit-platform/api/
```

Do not use `--delete`. Do not copy customer, staff, survey, prescriber, partner, or family trees. Do not change nginx. Do not change `api.env` in this step.

### 4.4 Verify, then stop

Before any restart:

- Live `KitPlatform.Api.dll` hash is `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9`.
- Server `appsettings*.json` and `uploads/` were not replaced.
- `api.env` byte size still matches the backup.
- `ResearchSafeHost__SuppressBackgroundWriters` is absent from the production environment.
- `systemctl is-active kit-platform-api` is unchanged.

If a check fails, restore the API tree and `api.env` from the backup and do not restart.

### 4.5 Restart

Only under a later approval that names this hash and the window:

```bash
systemctl restart kit-platform-api
```

The service remains `kit-platform-api`, user `www-data`, working directory `/var/www/kit-platform/api`, environment file `/etc/kit-platform/api.env`.

### 4.6 Smoke after restart

- `GET /api/health` is 200.
- Anonymous `GET /api/personal-intelligence/research/publication` is 401.
- `GET /api/personal-intelligence/trading/summary` is 404.
- `https://admin.novixa.vn/` returns HTML.
- The journal shows no `ValidateProductionConfiguration` startup exception. Do not copy environment values out of the log.
- Production workers are still allowed to run. Do not treat worker log lines as a failed deploy.

Stop and roll back if health is not 200, the anonymous Research route is not 401, or the trading summary is not 404.

An authorized Research call is not a pass condition yet. The production permission, module list, and publication path are still open. `MISSING` is the expected body only after those exist and the root is empty. `FRESH` requires source health `OK`.

### 4.7 Rollback

1. `systemctl stop kit-platform-api`.
2. Replace `/var/www/kit-platform/api` and `/etc/kit-platform/api.env` with the timestamped backup.
3. `systemctl start kit-platform-api`.
4. Confirm the live DLL hash equals the pre-change record and `GET /api/health` is 200.

Do not drop schemas or run migrations backward. If the backup hashes were not recorded, stop.

### 4.8 Local Research process

Not started here. When a later approval starts it, use the new DLL, not 003, and set:

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5088` |
| `ConnectionStrings__Default` | `Host=127.0.0.1;Port=5433;Database=kit_pi_validation;Username=pi_validation` |
| `ResearchSafeHost__SuppressBackgroundWriters` | `true` |
| `PersonalIntelligence__ResearchPublication__Root` | Empty |
| `Jwt__Secret` | A new 32-character value that exists only in that process |
| `Auth__DefaultTenantCode` | A tenant code confirmed absent, so a missing `tenantCode` cannot select `NVX-CS08` or `DEMO_PHARMACY` |

Do not load `/etc/kit-platform/api.env`. Do not set exchange variables. Do not use ports 5290, 7224, 5173, 5174, 5432, or 5433 for the API. Recheck that 5088 is free first. Startup opens a database connection even before a request, so this process is not read-only.

Creating `pi-research-readonly` and logging in, including a `PI_B` login, remain under the separate approval in report 013. Login writes `last_login_at` and a refresh-token hash.

## 5. Security bounds verified in this session

| Bound | Evidence |
|---|---|
| Production workers stay when the flag is unset | Tests for unset and `false` |
| A Research process can remove every hosted service | Test for `true`, and `Apply` runs before `Build` |
| Exchange worker is not registered by this host | Existing Research host tests, and the API DLL has no `ExchangeBackfillWorker` string |
| Trading routes are not mapped | Existing tests expect 404 for summary, exchange catalog, and trading loop |
| Research policy | Existing tests: anonymous 401, reader and `trading.run` and `ADMIN` 200 `MISSING`, exchange and approval 403, module off 403 |
| Flag is not baked into configuration | Absent from source and published appsettings |
| Old artifact is not this candidate | Hashes differ |

## 6. Reports 007–013

The local database plan is unchanged. `kit_pi_validation` on `127.0.0.1:5433` is the only validation database. `PI_A` has the module enabled. `PI_B` does not. `PI_RESEARCH_READONLY` exists on `PI_A` with only `personal_intelligence.research.read`. No login user was created. Production tenants, `novixa_prod`, `NT_XUANHOA`, and `DEMO_PHARMACY` were not opened.

## 7. Still not verified

These are not passed.

| Item | Handling |
|---|---|
| Commit of the three source files | Done in section 8. `85db8bdf`. Not pushed |
| Production `narrative_json` | Read-only catalog query on the production Content database, in its own approval. Do not apply migration 384 |
| Production publication path | Owner names a directory outside every nginx `root`. Empty root returns `MISSING`. The API user gets read and execute only |
| Production module and permission | Do not call `UpdateTenantEntitlementAsync` or `UpdateRolePermissionsAsync`. Additive SQL only after the production database identity is proven |
| Login proof | Report 013. Not run. `PI_B` login is a write |
| Local host on port 5088 | Not started. Requires the flag `true` and the account approval |
| Restart of `kit-platform-api` | Separate approval naming this hash |

No deploy, restart, migration, permission change, or push was made. The isolation commit is recorded in section 8.

## 8. Final deploy preflight

Date: 2026-10-22. No production connection was opened.

### 8.1 Commit and artifact

| Item | Result |
|---|---|
| Commit | `85db8bdfd269b3eef12fb1729d95d5b59596c79e` |
| Parent | `6596cfb996b3b4698917cb6f72b809931a42cc84` |
| Branch | `admin-build-blocker-001` |
| Files | `Program.cs`, `PersonalIntelligenceResearchHost.cs`, `ResearchSafeHostWorkerIsolationTests.cs` |
| Diff | 100 insertions. The flag is off unless `ResearchSafeHost:SuppressBackgroundWriters` is true |
| Push | Not done |
| `KitPlatform.Api.dll` SHA-256 after the commit | `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9` |

The commit does not rebuild the DLL. The hash was read before and after the commit and both values match. No other source file was included.

### 8.2 Proven from the repository

| Check | Evidence | Live result |
|---|---|---|
| Shared process | `deploy/ubuntu/kit-platform-api.service`, `docs/novixa/05-operations/deployment-model-v1.md` | Documented. Not observed on the server |
| Documented database name | Same deployment model, section 1 | `novixa_prod`. Not confirmed with `current_database()` |
| API and admin paths | `deploy/ubuntu/nginx-kit-platform.conf` | API files at `/var/www/kit-platform/api`. Admin root `/var/www/kit-platform/admin`. Also customer-app, staff-app, assessment-web, prescriber-portal, partner-portal, family-app |
| Secrets file | Deployment model | `/etc/kit-platform/api.env`. Not read |
| Column the shared API selects | `ContentRepository.ListBrandsAsync` and `GetBrandAsync` | `pack_content.brand.narrative_json`. Presence on `novixa_prod` is **UNVERIFIED** |
| Publication path | No path in shared appsettings | **UNVERIFIED**. It must not sit inside any nginx `root` above |
| Backup directory | Not named by the owner | **UNVERIFIED**. It must be outside `/var/www/kit-platform` |
| Worker flag on production | Key absent from appsettings | Must stay absent from `api.env` |

Catalog query to run only after the identity gate passes. It returns column metadata, not brand text:

```sql
SELECT current_database(), inet_server_port();
SELECT table_schema, table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'pack_content'
  AND table_name = 'brand'
  AND column_name = 'narrative_json';
```

Stop if `current_database()` is not `novixa_prod` or the port is not the production listener the operator just identified. Do not run this against `kit_pi_validation`, `NT_XUANHOA` as a guessed database, or port 5432 on this workstation.

### 8.3 Approval required before that read

Read-only access to the production host that runs `kit-platform-api`, with permission to:

1. Show the service unit and confirm `WorkingDirectory=/var/www/kit-platform/api` and `EnvironmentFile=/etc/kit-platform/api.env`, without printing the environment file.
2. Open `novixa_prod` and run only the two statements above.
3. List nginx `root` directives and compare them with the publication directory the owner names.
4. Name a new backup directory outside `/var/www/kit-platform`.

That approval was not present. The connection was not attempted.

### 8.4 Backup, rollback, and smoke

Unchanged from section 4, with this commit as the source pin. Copy only `artifacts/sb-research-safe-host-014/api/`. Exclude `appsettings*.json`, `uploads/`, and `api.env`. Do not copy an admin tree. Do not set `ResearchSafeHost__SuppressBackgroundWriters` on the shared service.

Rollback replaces `/var/www/kit-platform/api` and `/etc/kit-platform/api.env` from the timestamped backup, then starts `kit-platform-api`. Smoke after an approved restart remains health 200, anonymous publication 401, and trading summary 404. Those were not run.

### 8.5 Final blockers

| Blocker | Evidence still needed |
|---|---|
| Production database identity | `current_database()` and `inet_server_port()` on the service's database |
| `narrative_json` | One row from the catalog query, or zero rows if the column is absent |
| Publication directory | A path chosen by the owner that is outside every nginx `root` |
| Backup directory | A path chosen by the owner outside `/var/www/kit-platform` |
| Restart window | Named by the owner. Not implied by this report |

Status: `BLOCKED`. The candidate is not deployed.

## 9. Production preflight gate

Date: 2026-10-22. No file was copied. No service was restarted. No database session was opened. No secret was printed.

### 9.1 Release check

| Check | Result |
|---|---|
| Branch | `admin-build-blocker-001` |
| HEAD | `85db8bdfd269b3eef12fb1729d95d5b59596c79e` |
| Commit files | The same three: `Program.cs`, `PersonalIntelligenceResearchHost.cs`, `ResearchSafeHostWorkerIsolationTests.cs` |
| Those three files after the commit | Clean |
| API DLL SHA-256 | `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9` |
| Admin build | Not required. No client file is in this commit. Do not copy an admin tree |
| Source or artifact edited in this gate | No |

### 9.2 Production access

Checked again at 2026-10-22 22:03 UTC+7. The workstation SSH directory has no config and no private key. The attempt used BatchMode, so it could not prompt for a password:

```text
ssh -o BatchMode=yes -o ConnectTimeout=10 -o StrictHostKeyChecking=yes root@103.200.23.229
root@103.200.23.229: Permission denied (publickey,password).
```

No password was tried and no SSH configuration was changed. Systemd, nginx, disk, and PostgreSQL on `103.200.23.229` were not observed. The service name `kit-platform-api` was not confirmed on the host. Documented paths remain documents, not server evidence. A later look at the host found `/tmp/sb-research-preflight.sh` with one blank line; its `EXIT_CODE=0` did not execute a check and is not evidence.

### 9.3 Blockers

| Issue | Evidence | Impact | Minimum action | Approver |
|---|---|---|---|---|
| No SSH to the API host | 22:03 UTC+7: `Permission denied (publickey,password)` for `root@103.200.23.229`. This user has no SSH config and no private key. Rechecked 22:07 UTC+7; the hash and commit are unchanged | Cannot prove the service, database, nginx roots, publication path, or a backup location | A person who already has root on that host runs the script in section 9.4 and pastes only its stdout. Do not paste `api.env` | Owner of the VPS |
| Database identity | Not queried. The name in configuration is not proof | A catalog query against the wrong database is forbidden | Section 9.4 connects with the API target, runs `current_database()` first, and runs the catalog query only when that result is `novixa_prod` | Same operator, read-only |
| `narrative_json` | Not queried | Shared brand reads select this column. Absence is a startup or brand-read failure after restart | Only after the identity row matches: `SELECT table_schema, table_name, column_name, data_type FROM information_schema.columns WHERE table_schema = 'pack_content' AND table_name = 'brand' AND column_name = 'narrative_json';` | Same operator |
| Publication directory | Not read | An empty root is valid `MISSING`, but a path inside a web root is not safe | Compare one value, `PersonalIntelligence__ResearchPublication__Root`, with every live nginx `root`. Do not print the rest of `api.env`. If the key is absent, record that and do not invent a path | Owner names the directory if the key is absent |
| Backup directory | Not chosen on the server | Copy without a measured destination can fill the disk or land inside the web root | `df` the chosen parent and `stat` it. Parent must be outside `/var/www/kit-platform`. Do not create the directory until the deploy approval | Owner |
| Restart window | Not named | Restarting the shared API affects pharmacy traffic | Owner names the off-hours window and this DLL hash | Owner |

### 9.4 One-shot read-only package

A one-line `/tmp/sb-research-preflight.sh` that exits 0 is not a pass. Earlier handoff hashes, including `7cbd257d5cf02be8a9d1b0542271fd7e765cf6d9550262fcfac92cb83f77f799`, `b6553ca09a45f77ecf4cadffa841a5b7a33efee2a6a758042236b09a6adb4a15`, `c49145f503e00a512059f29a922f167a45f05ca9ea9940da2ae0f40e0044c93b`, and `1412a1cf1ffe1dfae5700e9b4d3ec3d6a9cae8f6e7c3f7b8ea8d56324911861b`, plus `ce222ced3c1be285cb83dc0b31ec6434753ba6718a39a03d5d89b17fd7715019` and `b82575950bd2e8ee8afc3a92285c64b98855c2ed05b4b1bcc047ea2a844f6fa9`, described other bodies. They are withdrawn. Do not use them.

This is the only preflight script. It does not migrate, copy, deploy, or restart. Log lines go to stdout and, through one held file descriptor, to a stage file created with `O_CREAT|O_EXCL|O_NOFOLLOW` and mode 0600 inside a mode-0700 directory owned by the effective user under `/tmp`. Later log lines do not reopen that path. `/tmp` is opened with `O_DIRECTORY|O_NOFOLLOW`, and the descriptor path must be `/tmp`. Group or other write on `/tmp` is accepted only together with the sticky bit. Publication is one `rename` of `stage` onto `sb-research-preflight-result.txt` inside that opened directory. On Linux, `rename` replaces the final directory entry and does not follow a symbolic link. This workstation cannot create a real symlink, so that behavior was not executed. A look at a name before `rename` is not a TOCTOU barrier, and the same-user window is not closed. If the destination is already a symlink, a directory, or anything other than a regular file, `rename` is not called. Failure does not publish through `ln`, `cp`, `mv`, or shell redirection. `TERM`, `INT`, `HUP`, and `PIPE` remove the private directory. `SIGKILL` cannot be caught and is not described as trap cleanup. A later run removes a leftover private directory only when this user owns it, its mode is 0700, it is not a symlink, and it contains only regular files named `stage`, `acl`, or `psql-err`. Any other leftover is left in place. `getfacl` output is read from the command's stdout, and `psql` stderr is discarded instead of being stored in a file. The creating process checks mode 0600. A second process on this Git bash host still observed mode 644, so durable mode 0600 was not proven. The script does not print `ExecStart`, systemd `Environment` values, `api.env`, a connection string, a password, a token, customer rows, or tool error text. It does not take a backup, write the database, edit configuration, copy an artifact, change host permissions, or restart a service. Exit 0 means every gate passed and the publisher returned `PUBLISHED`. Exit 2 means blocked. Exit 3 means the log channel failed. Stdout can show `PREFLIGHT_DONE complete` before publication is confirmed. The result file is evidence only when the process exits 0.

Catalog queries run only after the connection string names a plain database and one proven local endpoint. An empty host does not select a default socket. `127.0.0.1` and `::1` stay on TCP. `localhost` is accepted only when exactly one of those listeners exists, and it stays on TCP. A Unix socket is used only when the connection string host is that absolute socket directory and `ss` shows one postgres listener for it. A failed TCP connection is not moved to a socket. An identity line is logged only when it matches `name|port|address|port` or `name|port|socket|socket`. Drop-in paths come from the `# /path` headers of `systemctl cat` and from `find -print0` on the three standard drop-in directories. Those paths are not split on spaces. If `systemctl cat` cannot be read, drop-ins are not proven and the run is `BLOCKED`. The publication directory must not be group-writable or writable by others, and `www-data` must be able to read and traverse it. If that directory has an ACL, `getfacl` must exit 0 and return `user::`, `group::`, and `other::`. The base entries `user::` and `group::` are not an extended ACL. A named user, a named group, `mask::`, or a `default:` entry is. A `+` listing without one of those markers, or an extended entry without `mask::`, is `BLOCKED`. A write bit on any entry other than the owning `user::` or `default:user::` is `BLOCKED`. A missing, failed, or partial ACL read is `BLOCKED`. Every parsed nginx `root` or `alias` must be an absolute plain path, and that count must equal the directive count. Otherwise the publication and backup paths are not treated as outside the web roots. A root that cannot be resolved is `BLOCKED`. The backup byte budget is `du -sb` of the API tree, which already includes uploads and appsettings, plus `/etc/kit-platform/api.env`, every other systemd environment file once, and `du -sb` of the publication directory when that directory exists. A missing publication directory is not copied by section 9.5 and is not counted. A symlink at the API tree, uploads, an appsettings file, an environment file, or the publication path makes the size unproven. `BACKUP_SPACE OK` is logged only for that counted total plus 20 percent and at least 64 MiB, and only after the backup directory resolves outside `/var/www/kit-platform` and outside every resolved nginx root. Anything unproven is `BLOCKED` and the process exits 2. The only success token is `PREFLIGHT_DONE complete` together with exit 0.

Measured from the script bytes in the block below, written as a LF file with no BOM: 1169 lines, 41404 bytes, SHA-256 `1741bf8493ce7370f4394acf30a96e058e84b0e4b3aa5e9be224401817863bc2`. `bash -n` exited 0. The embedded Perl block passed `perl -c` in the local harness.

Save the block below as `/tmp/sb-research-preflight.sh` with LF line endings and mode `0700`. Before running it, confirm all four:

```text
wc -l /tmp/sb-research-preflight.sh
wc -c /tmp/sb-research-preflight.sh
sha256sum /tmp/sb-research-preflight.sh
bash -n /tmp/sb-research-preflight.sh
```

Required results: `1169`, `41404`, `1741bf8493ce7370f4394acf30a96e058e84b0e4b3aa5e9be224401817863bc2`, and `bash -n` exit 0. If any differ, do not run the file. Then:

```text
bash /tmp/sb-research-preflight.sh
echo EXIT_CODE=$?
```

Paste `/tmp/sb-research-preflight-result.txt` only. Do not paste `api.env`.

```bash
#!/bin/bash
# Read-only production preflight for Research Safe Host.
# Result file: /tmp/sb-research-preflight-result.txt
# Logs go to one file descriptor on a stage file inside a mode-0700 directory
# owned by this user. Later log lines do not reopen that path. Publication is
# one rename from that directory onto the result name in /tmp opened with
# O_DIRECTORY|O_NOFOLLOW. On Linux that rename replaces the final entry and
# does not follow a symlink. A name check before the rename is not a barrier.
# The same-user window before the rename is not closed. TERM, INT, HUP, and
# PIPE discard the private directory. SIGKILL cannot be caught. A later run
# removes a leftover private directory only when this user owns it, its mode
# is 0700, it is not a symlink, and it contains only regular files named
# stage, acl, or psql-err. Any other leftover is left untouched.
# Exit 0 only when every gate passed and the rename was confirmed.
# Exit 2 when a gate is blocked. Exit 3 when the log channel itself fails.
# Does not print ExecStart, Environment values, connection strings, passwords, or tokens.
set -u
set -o pipefail
trap '' PIPE
OUT=/tmp/sb-research-preflight-result.txt
OUT_NAME=sb-research-preflight-result.txt
published=0
PRIV=""
LOG_IN=""
LOG_OUT=""
if ! command -v perl >/dev/null 2>&1; then
  printf '%s\n' "BLOCKED cannot prove a no-follow result file"
  exit 2
fi
coproc PF {
  perl /proc/self/fd/3 "$OUT_NAME" 3<<'PERL'
use strict;
use warnings;
use Fcntl qw(O_RDONLY O_DIRECTORY O_NOFOLLOW O_CREAT O_EXCL O_WRONLY S_ISREG S_ISLNK S_ISDIR);
$| = 1;
binmode STDIN;
binmode STDOUT;
my $CLOEXEC = eval { Fcntl::O_CLOEXEC() } || 0;
my ($dest) = @ARGV;
if (!defined $dest || $dest ne "sb-research-preflight-result.txt") {
  print "BLOCKED result name is not the expected name\n";
  exit 4;
}
umask 0077;
my ($dh, $ph, $stage, $priv);
my ($stage_dev, $stage_ino, $priv_dev, $priv_ino);
my $discarded = 0;
sub discard {
  return if $discarded;
  $discarded = 1;
  if (defined $ph) {
    chdir($ph);
    foreach my $extra (qw(stage acl psql-err)) {
      my @n = lstat($extra);
      next if !@n || S_ISDIR($n[2]);
      if ($extra eq "stage") {
        next if !defined $stage_dev || S_ISLNK($n[2]) || !S_ISREG($n[2]);
        next if $n[0] != $stage_dev || $n[1] != $stage_ino;
      }
      unlink($extra);
    }
  }
  if (defined $dh && defined $priv) {
    chdir($dh);
    my @p = lstat($priv);
    if (@p && !S_ISLNK($p[2]) && S_ISDIR($p[2]) && defined $priv_dev && $p[0] == $priv_dev && $p[1] == $priv_ino) {
      rmdir($priv);
    }
  }
}
$SIG{TERM} = sub { discard(); exit 4; };
$SIG{INT} = sub { discard(); exit 4; };
$SIG{HUP} = sub { discard(); exit 4; };
$SIG{PIPE} = sub { discard(); exit 4; };
END { discard(); }
sysopen($dh, "/tmp", O_RDONLY | O_DIRECTORY | O_NOFOLLOW | $CLOEXEC) or do {
  print "BLOCKED cannot open /tmp without following a symlink\n";
  exit 4;
};
my $fdpath = readlink("/proc/self/fd/" . fileno($dh));
if (!defined $fdpath || $fdpath ne "/tmp") {
  print "BLOCKED /tmp directory identity is not proven\n";
  exit 4;
}
my @dir = stat($dh);
if (!@dir || !S_ISDIR($dir[2])) {
  print "BLOCKED /tmp is not a directory\n";
  exit 4;
}
my $mode = $dir[2] & 07777;
my $owner = $dir[4];
my $euid = $>;
my $sticky = ($mode & 01000) ? 1 : 0;
my $gwrite = ($mode & 020) ? 1 : 0;
my $owrite = ($mode & 02) ? 1 : 0;
if (($gwrite || $owrite) && !$sticky) {
  print "BLOCKED /tmp is writable by others without the sticky bit\n";
  exit 4;
}
if ($owner != $euid && $owner != 0) {
  print "BLOCKED /tmp owner is not proven\n";
  exit 4;
}
chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
my @cwd = stat(".");
if (!@cwd || $cwd[0] != $dir[0] || $cwd[1] != $dir[1]) {
  print "BLOCKED /tmp directory identity changed\n";
  exit 4;
}
my $stale_cleaned = 0;
my $stale_left = 0;
opendir(my $scan, ".") or do { print "BLOCKED cannot read /tmp\n"; exit 4; };
my @stale_names = ();
while (my $name = readdir($scan)) {
  next if $name !~ /\Asb-research-preflight\.[A-Za-z0-9]{8}\z/;
  push @stale_names, $name;
}
closedir($scan);
foreach my $stale_name (@stale_names) {
  my @st = lstat($stale_name);
  if (!@st || S_ISLNK($st[2]) || !S_ISDIR($st[2]) || (($st[2] & 07777) != 0700) || $st[4] != $euid) {
    $stale_left++;
    next;
  }
  my $old;
  if (!sysopen($old, $stale_name, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | $CLOEXEC)) {
    $stale_left++;
    next;
  }
  my @ost = stat($old);
  if (!@ost || $ost[0] != $st[0] || $ost[1] != $st[1]) {
    close($old);
    $stale_left++;
    next;
  }
  if (!chdir($old)) {
    close($old);
    $stale_left++;
    next;
  }
  opendir(my $inner, ".") or do { close($old); $stale_left++; chdir($dh); next; };
  my $bad = 0;
  my @kids = ();
  while (my $child = readdir($inner)) {
    next if $child eq "." || $child eq "..";
    if ($child !~ /\A(?:stage|acl|psql-err)\z/) {
      $bad = 1;
      last;
    }
    push @kids, $child;
  }
  closedir($inner);
  if (!$bad) {
    foreach my $child (@kids) {
      my @cs = lstat($child);
      if (!@cs || S_ISLNK($cs[2]) || S_ISDIR($cs[2]) || $cs[4] != $euid || !unlink($child)) {
        $bad = 1;
        last;
      }
    }
  }
  chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
  close($old);
  if ($bad) {
    $stale_left++;
    next;
  }
  my @again = lstat($stale_name);
  if (@again && !S_ISLNK($again[2]) && S_ISDIR($again[2]) && $again[0] == $st[0] && $again[1] == $st[1] && rmdir($stale_name)) {
    $stale_cleaned++;
  } else {
    $stale_left++;
  }
}
my $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
my $made = 0;
for (my $try = 0; $try < 20 && !$made; $try++) {
  sysopen(my $rnd, "/dev/urandom", O_RDONLY | $CLOEXEC) or last;
  my $raw = "";
  my $got = sysread($rnd, $raw, 8);
  close $rnd;
  next if !defined $got || $got != 8;
  my $suffix = "";
  for my $i (0 .. 7) {
    $suffix .= substr($alphabet, ord(substr($raw, $i, 1)) % 62, 1);
  }
  $priv = "sb-research-preflight.$suffix";
  next if lstat($priv);
  next if !mkdir($priv, 0700);
  if (!sysopen($ph, $priv, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | $CLOEXEC)) {
    rmdir($priv);
    next;
  }
  my @pst = stat($ph);
  my @pn = lstat($priv);
  if (!@pst || !@pn || S_ISLNK($pn[2]) || !S_ISDIR($pst[2]) || ($pst[2] & 07777) != 0700 || $pst[4] != $euid || $pst[0] != $pn[0] || $pst[1] != $pn[1]) {
    rmdir($priv);
    next;
  }
  ($priv_dev, $priv_ino) = ($pst[0], $pst[1]);
  $made = 1;
}
if (!$made) {
  print "BLOCKED cannot create a private result directory\n";
  exit 4;
}
chdir($ph) or do { print "BLOCKED cannot use the private result directory\n"; exit 4; };
if (!sysopen($stage, "stage", O_CREAT | O_EXCL | O_WRONLY | O_NOFOLLOW | $CLOEXEC, 0600)) {
  print "BLOCKED cannot create the stage file\n";
  exit 4;
}
eval {
  local $SIG{__WARN__} = sub {};
  require POSIX;
  POSIX::fchmod(fileno($stage), 0600);
};
if (!chmod(0600, "stage")) {
  print "BLOCKED cannot set stage file mode\n";
  exit 4;
}
my @sst = stat($stage);
my @sn = lstat("stage");
if (!@sst || !@sn || (($sn[2] & 07777) != 0600) || (($sst[2] & 07777) != 0600)) {
  print "BLOCKED stage file mode is not 0600\n";
  exit 4;
}
if (!@sst || !@sn || S_ISLNK($sn[2]) || !S_ISREG($sst[2]) || $sst[0] != $sn[0] || $sst[1] != $sn[1]) {
  print "BLOCKED stage file identity is not proven\n";
  exit 4;
}
($stage_dev, $stage_ino) = ($sst[0], $sst[1]);
chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
print "READY $priv\n";
print "STALE cleaned=$stale_cleaned left=$stale_left\n";
while (defined(my $line = <STDIN>)) {
  chomp $line;
  if ($line eq "C DISCARD") {
    discard();
    print "DISCARDED\n";
    exit 0;
  }
  if ($line eq "C PUBLISH") {
    my @fdst = stat($stage);
    chdir($ph) or do { print "BLOCKED cannot use the private result directory\n"; exit 4; };
    my @now = lstat("stage");
    # This lstat can be stale before rename. It is not a TOCTOU barrier.
    if (!@fdst || !@now || S_ISLNK($now[2]) || !S_ISREG($now[2]) || $fdst[0] != $now[0] || $fdst[1] != $now[1] || $fdst[0] != $stage_dev || $fdst[1] != $stage_ino) {
      print "BLOCKED stage file identity changed\n";
      exit 4;
    }
    my $synced = eval { require IO::Handle; $stage->sync(); };
    if ($@ || !defined $synced) {
      print "BLOCKED result file sync failed\n";
      exit 4;
    }
    chdir($dh) or do { print "BLOCKED cannot use the /tmp directory\n"; exit 4; };
    my @here = stat(".");
    if (!@here || $here[0] != $dir[0] || $here[1] != $dir[1]) {
      print "BLOCKED /tmp directory identity changed\n";
      exit 4;
    }
    my @pnow = lstat($priv);
    if (!@pnow || S_ISLNK($pnow[2]) || !S_ISDIR($pnow[2]) || $pnow[0] != $priv_dev || $pnow[1] != $priv_ino) {
      print "BLOCKED private directory name changed\n";
      exit 4;
    }
    # A symlink or directory at the result name is refused before rename.
    # This look can be stale. It is not a TOCTOU barrier.
    my @destnow = lstat($dest);
    if (@destnow && (S_ISLNK($destnow[2]) || S_ISDIR($destnow[2]) || !S_ISREG($destnow[2]))) {
      print "BLOCKED cannot publish result file\n";
      exit 4;
    }
    if (!rename("$priv/stage", $dest)) {
      print "BLOCKED cannot publish result file\n";
      exit 4;
    }
    chmod(0600, $dest);
    my @after = lstat($dest);
    if (!@after || S_ISLNK($after[2]) || !S_ISREG($after[2]) || $after[0] != $stage_dev || $after[1] != $stage_ino || (($after[2] & 07777) != 0600)) {
      if (@after && !S_ISLNK($after[2]) && S_ISREG($after[2]) && $after[0] == $stage_dev && $after[1] == $stage_ino) {
        unlink($dest);
      }
      print "BLOCKED published result is not the stage file\n";
      exit 4;
    }
    $discarded = 0;
    discard();
    $discarded = 1;
    print "PUBLISHED\n";
    exit 0;
  }
  if ($line =~ /\AL (.*)\z/) {
    my $buf = $1 . "\n";
    my $off = 0;
    while ($off < length($buf)) {
      my $n = syswrite($stage, $buf, length($buf) - $off, $off);
      if (!defined $n) {
        print "BLOCKED result log write failed\n";
        exit 4;
      }
      $off += $n;
    }
    next;
  }
  print "BLOCKED result log protocol failed\n";
  exit 4;
}
exit 4;
PERL
}
exec {LOG_IN}<&"${PF[0]}"
exec {LOG_OUT}>&"${PF[1]}"
ready=""
stale_line=""
IFS= read -r ready <&"$LOG_IN" || ready=""
case "$ready" in
  READY\ sb-research-preflight.[A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9][A-Za-z0-9])
    PRIV="/tmp/${ready#READY }"
    ;;
  BLOCKED*)
    printf '%s\n' "$ready"
    exit 2
    ;;
  *)
    printf '%s\n' "BLOCKED cannot prove a no-follow result file"
    exit 2
    ;;
esac
IFS= read -r stale_line <&"$LOG_IN" || stale_line=""
case "$stale_line" in
  STALE\ cleaned=[0-9]*\ left=[0-9]*) ;;
  *)
    printf '%s\n' "BLOCKED result log protocol failed"
    exit 2
    ;;
esac
channel_stop() {
  local rc=$?
  if [ "${published:-0}" -ne 1 ] && [ -n "${LOG_OUT:-}" ]; then
    printf 'C DISCARD\n' >&"$LOG_OUT" 2>/dev/null || true
    local dstatus=""
    IFS= read -r -t 2 dstatus <&"$LOG_IN" 2>/dev/null || true
  fi
  exit "$rc"
}
trap channel_stop EXIT
trap 'exit 2' INT TERM
log() {
  printf '%s\n' "$*" || true
  if ! printf 'L %s\n' "$*" >&"$LOG_OUT"; then
    printf '%s\n' "BLOCKED result log write failed"
    exit 3
  fi
}
publish_result() {
  if ! printf 'C PUBLISH\n' >&"$LOG_OUT"; then
    printf '%s\n' "BLOCKED result publication is not proven"
    exit 2
  fi
  local status=""
  IFS= read -r status <&"$LOG_IN" || status=""
  if [ "$status" = "PUBLISHED" ]; then
    published=1
    return 0
  fi
  printf '%s\n' "${status:-BLOCKED result publication is not proven}"
  exit 2
}
finish_blocked() { log "PREFLIGHT_DONE blocked"; publish_result; exit 2; }
note_block() { log "BLOCKED $1"; blocked=1; }
blocked=0
skip_db=0
nginx_ok=0
log "STALE_PRIVATE ${stale_line#STALE }"
log "PREFLIGHT_RESULT ${OUT}"
script_hash=$(sha256sum "$0" | awk '{print $1}') || script_hash=""
script_hash=${script_hash#\\}
if [ -z "${script_hash:-}" ]; then
  log "BLOCKED script hash could not be read"
  finish_blocked
fi
log "SCRIPT_LINES $(wc -l <"$0" | tr -d '[:space:]')"
log "SCRIPT_SHA256 ${script_hash}"
lines=$(wc -l <"$0" | tr -d '[:space:]')
if [ "${lines:-0}" -lt 80 ]; then
  log "BLOCKED script body is too short"
  finish_blocked
fi
if [ "$(id -u)" -ne 0 ]; then
  log "BLOCKED NEED_ROOT"
  finish_blocked
fi
if ! command -v ip >/dev/null 2>&1; then
  log "BLOCKED ip command is missing"
  finish_blocked
fi
if ! ip -4 -o addr show | grep -qE '(^|[^0-9])103\.200\.23\.229([^0-9]|$)'; then
  log "BLOCKED host IPv4 list does not include 103.200.23.229"
  while IFS= read -r line; do
    log "$line"
  done < <(ip -4 -o addr show | awk '{print "ADDR", $2, $4}')
  finish_blocked
fi
log "HOST_IPV4 103.200.23.229"
if ! command -v systemctl >/dev/null 2>&1; then
  log "BLOCKED systemctl is missing"
  finish_blocked
fi
active=$(systemctl is-active kit-platform-api 2>/dev/null || true)
log "SERVICE_ACTIVE ${active:-missing}"
if [ "$active" != "active" ]; then
  log "BLOCKED kit-platform-api is not active"
  finish_blocked
fi
wd=$(systemctl show kit-platform-api -p WorkingDirectory --value 2>/dev/null || true)
user=$(systemctl show kit-platform-api -p User --value 2>/dev/null || true)
group=$(systemctl show kit-platform-api -p Group --value 2>/dev/null || true)
frag=$(systemctl show kit-platform-api -p FragmentPath --value 2>/dev/null || true)
envfiles=$(systemctl show kit-platform-api -p EnvironmentFiles --value 2>/dev/null || true)
log "WORKING_DIRECTORY ${wd:-missing}"
log "SERVICE_USER ${user:-missing}"
log "SERVICE_GROUP ${group:-missing}"
log "FRAGMENT ${frag:-missing}"
if [ "$wd" != "/var/www/kit-platform/api" ]; then
  log "BLOCKED working directory is not /var/www/kit-platform/api"
  finish_blocked
fi
if [ -z "${user:-}" ]; then
  note_block "service user is unset"
elif [ "$user" != "www-data" ]; then
  note_block "service user is not www-data"
elif ! id "$user" >/dev/null 2>&1; then
  note_block "service user account cannot be resolved"
else
  log "SERVICE_USER_OK www-data"
fi
if [ "${group:-}" != "www-data" ]; then
  note_block "service group is not www-data"
fi
seen_units=$'\n'
scan_unit() {
  local unitpath=$1
  case "$seen_units" in
    *$'\n'"$unitpath"$'\n'*) return 1 ;;
  esac
  seen_units="${seen_units}${unitpath}"$'\n'
  if [ ! -r "$unitpath" ]; then
    note_block "a systemd unit file could not be read"
    return 0
  fi
  if grep -q 'ResearchSafeHost__SuppressBackgroundWriters' "$unitpath"; then
    note_block "suppress flag is present in a systemd unit file"
  fi
  if grep -q 'PersonalIntelligence__ExchangeCredentialKey' "$unitpath"; then
    note_block "exchange credential setting is present in a systemd unit file"
  fi
  if grep -q 'ExecStart' "$unitpath"; then
    if grep -q 'KitPlatform.Api.dll' "$unitpath"; then
      log "UNIT_EXECSTART_DLL referenced"
    else
      note_block "a unit file sets ExecStart without the API DLL path"
    fi
  fi
  return 0
}
if [ -z "${frag:-}" ] || [ ! -r "$frag" ]; then
  note_block "systemd unit fragment could not be read"
else
  scan_unit "$frag"
fi
dropin_count=0
unit_cat=$(systemctl cat kit-platform-api 2>/dev/null) || unit_cat=""
unit_cat_rc=$?
if [ "$unit_cat_rc" -ne 0 ] || [ -z "${unit_cat:-}" ]; then
  note_block "systemd unit text could not be read; drop-ins were not proven"
else
  while IFS= read -r cat_line; do
    case "$cat_line" in
      "# /"*)
        unitpath=${cat_line#"# "}
        case "$unitpath" in
          /*) ;;
          *) note_block "a systemd fragment path is not absolute"; continue ;;
        esac
        if scan_unit "$unitpath"; then
          if [ "$unitpath" != "${frag:-}" ]; then
            dropin_count=$((dropin_count + 1))
          fi
        fi
        ;;
    esac
  done <<< "$unit_cat"
fi
unset unit_cat unit_cat_rc cat_line unitpath
if ! command -v find >/dev/null 2>&1; then
  note_block "find is missing; drop-in directories were not proven"
else
for dir in /etc/systemd/system/kit-platform-api.service.d /run/systemd/system/kit-platform-api.service.d /usr/lib/systemd/system/kit-platform-api.service.d; do
  if [ -d "$dir" ] && [ ! -r "$dir" ]; then
    note_block "a systemd drop-in directory could not be read"
  elif [ -d "$dir" ]; then
    while IFS= read -r -d '' unitpath; do
      if [ -d "$unitpath" ] && [ ! -L "$unitpath" ]; then
        continue
      fi
      if scan_unit "$unitpath"; then
        dropin_count=$((dropin_count + 1))
      fi
    done < <(find "$dir" -mindepth 1 -maxdepth 1 ! -type d -print0 2>/dev/null)
  fi
done
fi
log "SYSTEMD_DROPIN_SEEN ${dropin_count}"
env_paths=()
seen_env=" "
while IFS= read -r envpath; do
  [ -n "$envpath" ] || continue
  case "$seen_env" in
    *" $envpath "*) continue ;;
  esac
  seen_env="${seen_env}${envpath} "
  env_paths+=("$envpath")
done < <(printf '%s\n' "$envfiles" | grep -oE '/[[:graph:]]+' || true)
unset seen_env
log "ENV_FILE_COUNT ${#env_paths[@]}"
if [ "${#env_paths[@]}" -eq 0 ]; then
  note_block "no systemd environment file path could be read"
fi
has_api_env=0
pub=""
cs=""
if [ "${#env_paths[@]}" -gt 0 ]; then
  for envpath in "${env_paths[@]}"; do
    log "ENV_FILE_PATH ${envpath}"
    if [ "$envpath" = "/etc/kit-platform/api.env" ]; then
      has_api_env=1
    fi
    if [ -L "$envpath" ]; then
      note_block "a systemd environment file is a symlink"
      continue
    fi
    if [ ! -r "$envpath" ]; then
      note_block "a systemd environment file is not readable"
      continue
    fi
    log "ENV_FILE_BYTES ${envpath} $(stat -c '%s' "$envpath")"
    if grep -q 'ResearchSafeHost__SuppressBackgroundWriters' "$envpath"; then
      note_block "suppress flag is present in a systemd environment file"
    fi
    if grep -q 'PersonalIntelligence__ExchangeCredentialKey' "$envpath"; then
      note_block "exchange credential setting is present in a systemd environment file"
    fi
    next_pub=$(awk -F= 'index($0,"PersonalIntelligence__ResearchPublication__Root=")==1 {v=substr($0,index($0,"=")+1)} END{print v}' "$envpath")
    next_pub=${next_pub%$'\r'}
    next_pub=${next_pub%\"}
    next_pub=${next_pub#\"}
    if [ -n "$next_pub" ]; then
      pub=$next_pub
    fi
    next_cs=$(awk -F= 'index($0,"ConnectionStrings__Default=")==1 {v=substr($0,index($0,"=")+1)} END{print v}' "$envpath")
    next_cs=${next_cs%$'\r'}
    next_cs=${next_cs%\"}
    next_cs=${next_cs#\"}
    if [ -n "$next_cs" ]; then
      cs=$next_cs
    fi
    unset next_pub next_cs
  done
fi
if [ "$has_api_env" -ne 1 ]; then
  note_block "systemd EnvironmentFiles does not include /etc/kit-platform/api.env"
fi
sys_env=$(systemctl show kit-platform-api -p Environment --value 2>/dev/null)
sys_env_rc=$?
if [ "$sys_env_rc" -ne 0 ]; then
  note_block "systemd Environment property could not be read"
else
  if printf '%s' "$sys_env" | grep -q 'ResearchSafeHost__SuppressBackgroundWriters'; then
    note_block "suppress flag is present in systemd Environment"
  else
    log "SYSTEMD_ENVIRONMENT_SUPPRESS_FLAG ABSENT"
  fi
  if printf '%s' "$sys_env" | grep -q 'PersonalIntelligence__ExchangeCredentialKey'; then
    note_block "exchange credential setting is present in systemd Environment"
  else
    log "SYSTEMD_ENVIRONMENT_EXCHANGE_CREDENTIAL ABSENT"
  fi
  if printf '%s' "$sys_env" | grep -q 'PersonalIntelligence__ResearchPublication__Root='; then
    token=$(printf '%s' "$sys_env" | tr ' ' '\n' | awk -F= 'index($0,"PersonalIntelligence__ResearchPublication__Root=")==1 {v=substr($0,index($0,"=")+1)} END{print v}')
    if [[ "$token" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
      pub=$token
      log "PUBLICATION_SOURCE systemd-environment"
    else
      pub=""
      note_block "publication path in systemd Environment is not a plain absolute path"
    fi
    unset token
  fi
  if printf '%s' "$sys_env" | grep -q 'ConnectionStrings__Default='; then
    skip_db=1
    note_block "connection string is set in systemd Environment; it was not parsed and the catalog was not run"
  fi
fi
unset sys_env
if ! command -v nginx >/dev/null 2>&1; then
  note_block "nginx command is missing"
else
  nginx_dump=$(nginx -T 2>/dev/null)
  nginx_rc=$?
  if [ "$nginx_rc" -ne 0 ] || [ -z "${nginx_dump:-}" ]; then
    note_block "effective nginx configuration could not be read"
  else
    nginx_kw=$(printf '%s\n' "$nginx_dump" | grep -cE '^[[:space:]]*(root|alias)[[:space:]]+' || true)
    nginx_roots=$(printf '%s\n' "$nginx_dump" | sed -n -E 's/^[[:space:]]*(root|alias)[[:space:]]+//p' | sed -E 's/[[:space:]]*;.*$//; s:/*$::')
    log "NGINX_EFFECTIVE_BEGIN"
    nginx_count=0
    nginx_bad=0
    while IFS= read -r rootdir; do
      [ -n "$rootdir" ] || continue
      nginx_count=$((nginx_count + 1))
      case "$rootdir" in
        *'$'*|*' '*) nginx_bad=1; note_block "an effective nginx root is not a plain path" ;;
        /*) log "NGINX_WEB_ROOT ${rootdir}" ;;
        *) nginx_bad=1; note_block "an effective nginx root is not absolute" ;;
      esac
    done <<< "$nginx_roots"
    log "NGINX_WEB_ROOT_COUNT ${nginx_count}"
    if [ "${nginx_kw:-0}" -ne "$nginx_count" ] || [ "$nginx_bad" -ne 0 ]; then
      nginx_ok=0
      note_block "effective nginx root directives were not fully parsed"
    else
      nginx_ok=1
    fi
    log "NGINX_EFFECTIVE_END"
  fi
  unset nginx_dump
fi
if [ -z "${pub:-}" ]; then
  log "PUBLICATION_PATH ABSENT"
  note_block "publication path is not set; owner must name a directory outside every nginx web root"
else
  case "$pub" in
    *[Pp]assword*|*ConnectionString*|*://*|*Token*|*Secret*)
      log "PUBLICATION_PATH REDACTED"
      note_block "publication value is not a plain directory path"
      pub=""
      ;;
    /*) log "PUBLICATION_PATH ${pub}" ;;
    *)
      log "PUBLICATION_PATH REDACTED"
      note_block "publication path is not absolute"
      pub=""
      ;;
  esac
fi
if [ -n "${pub:-}" ]; then
  pub_real=$(readlink -f "$pub" 2>/dev/null || true)
  if [ -z "${pub_real:-}" ]; then
    note_block "publication path could not be resolved"
  else
    log "PUBLICATION_REALPATH ${pub_real}"
    case "$pub_real" in
      /var/www/kit-platform|/var/www/kit-platform/*)
        note_block "publication path is inside /var/www/kit-platform"
        ;;
    esac
    if [ "$nginx_ok" -ne 1 ]; then
      note_block "publication path was not compared with the effective nginx configuration"
    else
      unsafe=0
      while IFS= read -r rootdir; do
        [ -n "$rootdir" ] || continue
        root_real=$(readlink -f "$rootdir" 2>/dev/null || true)
        if [ -z "${root_real:-}" ]; then
          unsafe=1
          note_block "an effective nginx root could not be resolved"
          continue
        fi
        case "$pub_real" in
          "$root_real"|"$root_real"/*)
            log "PUBLICATION_INSIDE_WEB_ROOT ${root_real}"
            unsafe=1
            ;;
        esac
      done <<< "${nginx_roots:-}"
      if [ "$unsafe" -ne 0 ]; then
        note_block "publication path is inside an effective nginx web root"
      else
        log "PUBLICATION_OUTSIDE_WEB_ROOTS yes"
      fi
    fi
  fi
  if [ -d "$pub" ]; then
    log "PUBLICATION_EXISTS yes"
    log "PUBLICATION_OWNER $(stat -c '%U' "$pub")"
    log "PUBLICATION_MODE $(stat -c '%A' "$pub")"
    mode=$(stat -c '%a' "$pub" 2>/dev/null || true)
    perm=${mode: -3}
    if [ -z "${mode:-}" ] || [ "${#perm}" -ne 3 ]; then
      note_block "publication mode could not be read"
    else
      group_digit=${perm:1:1}
      other_digit=${perm:2:1}
      case "$group_digit" in
        2|3|6|7) note_block "publication directory is writable by group" ;;
        *) log "PUBLICATION_GROUP_WRITE no" ;;
      esac
      case "$other_digit" in
        2|3|6|7) note_block "publication directory is writable by others" ;;
        *) log "PUBLICATION_OTHER_WRITE no" ;;
      esac
    fi
    ls_bits=$(ls -ld "$pub" 2>/dev/null | awk '{print $1}')
    if [ -z "${ls_bits:-}" ]; then
      note_block "publication directory listing could not be read"
    else
    case "$ls_bits" in
      *+)
        if ! command -v getfacl >/dev/null 2>&1; then
          note_block "publication path has an ACL and getfacl is missing"
        else
          acl_text=$(getfacl -c -p "$pub" 2>/dev/null) || acl_text=""
          acl_rc=$?
          acl_user=0
          acl_group=0
          acl_other=0
          acl_mask=0
          acl_extended=0
          acl_write=0
          if [ "$acl_rc" -ne 0 ] || [ -z "${acl_text:-}" ]; then
            note_block "publication ACL could not be read"
          else
            while IFS= read -r acl_line || [ -n "${acl_line:-}" ]; do
              [ -n "$acl_line" ] || continue
              if [[ "$acl_line" == user::* ]]; then
                acl_user=1
              elif [[ "$acl_line" == group::* ]]; then
                acl_group=1
              elif [[ "$acl_line" == other::* ]]; then
                acl_other=1
              elif [[ "$acl_line" == mask::* ]]; then
                acl_mask=1
                acl_extended=1
              elif [[ "$acl_line" =~ ^user:[^:]+: ]]; then
                acl_extended=1
              elif [[ "$acl_line" =~ ^group:[^:]+: ]]; then
                acl_extended=1
              elif [[ "$acl_line" == default:* ]]; then
                acl_extended=1
              fi
              acl_perm=${acl_line##*:}
              case "$acl_perm" in
                *w*)
                  case "$acl_line" in
                    user::*|default:user::*|mask::*) ;;
                    *) acl_write=1 ;;
                  esac
                  ;;
              esac
            done <<< "$acl_text"
            if [ "$acl_user" -ne 1 ] || [ "$acl_group" -ne 1 ] || [ "$acl_other" -ne 1 ]; then
              note_block "publication ACL is incomplete"
            elif [ "$acl_extended" -ne 1 ] || [ "$acl_mask" -ne 1 ]; then
              note_block "publication ACL is incomplete"
            elif [ "$acl_write" -ne 0 ]; then
              note_block "publication directory ACL grants write beyond the owner"
            else
              log "PUBLICATION_ACL_GROUP_OTHER_WRITE no"
            fi
          fi
          unset acl_text acl_line acl_perm
        fi
        ;;
      *)
        log "PUBLICATION_ACL_GROUP_OTHER_WRITE no"
        ;;
    esac
    fi
    if [ "${user:-}" = "www-data" ] && id www-data >/dev/null 2>&1; then
      if sudo -u www-data test -r "$pub" 2>/dev/null && sudo -u www-data test -x "$pub" 2>/dev/null; then
        log "PUBLICATION_SERVICE_ACCESS read-execute"
      else
        note_block "service user www-data cannot read and traverse the publication path"
      fi
    else
      note_block "publication access was not tested because the service user is not www-data"
    fi
  else
    log "PUBLICATION_EXISTS no"
    note_block "publication path is not an existing directory"
  fi
fi
field() {
  printf '%s' "$1" | tr ';' '\n' | awk -F= -v k="$2" 'tolower($1)==tolower(k) {print substr($0, index($0,"=")+1); exit}'
}
if [ "$skip_db" -eq 0 ]; then
  if [ -z "${cs:-}" ]; then
    note_block "API connection string could not be read from systemd environment files"
  else
    db=$(field "$cs" database)
    dbhost=$(field "$cs" host)
    dbport=$(field "$cs" port)
  fi
fi
unset cs
endpoint_host=""
if [ "$skip_db" -eq 0 ] && [ -n "${db:-}" ]; then
  if [[ ! "$db" =~ ^[A-Za-z0-9_]+$ ]]; then
    unset db dbhost dbport
    note_block "API database name is not a plain identifier"
  elif [[ ! "${dbport:-}" =~ ^[0-9]+$ ]]; then
    unset db dbhost dbport
    note_block "database port is not set; catalog was not run"
  else
    db_transport=""
    case "${dbhost:-}" in
      127.0.0.1|::1) endpoint_host=$dbhost; db_transport=tcp ;;
      localhost) endpoint_host=localhost; db_transport=tcp ;;
      "")
        unset db dbhost dbport
        note_block "database host is not set; default socket was not used and catalog was not run"
        ;;
      /*)
        if [[ "$dbhost" =~ ^/[A-Za-z0-9._/-]+$ ]]; then
          endpoint_host=$dbhost
          db_transport=socket
        else
          unset db dbhost dbport
          note_block "database socket directory is not a plain absolute path; catalog was not run"
        fi
        ;;
      *)
        unset db dbhost dbport
        note_block "API database host is not a proven local endpoint; catalog was not run"
        ;;
    esac
  fi
fi
if [ "$skip_db" -eq 0 ] && [ -n "${db:-}" ]; then
  if ! command -v psql >/dev/null 2>&1 || ! command -v ss >/dev/null 2>&1; then
    unset db dbhost dbport endpoint_host
    note_block "psql or ss is missing; database endpoint was not proven and catalog was not run"
    log "NARRATIVE_CATALOG skipped"
  else
    if [ "$endpoint_host" = "localhost" ]; then
      listen_v4=$(ss -H -ltn sport = :"$dbport" 2>/dev/null | awk -v p="$dbport" '$4 == ("127.0.0.1:" p) {found=1} END{if (found) print "127.0.0.1"}')
      listen_v6=$(ss -H -ltn sport = :"$dbport" 2>/dev/null | awk -v p="$dbport" '$4 == ("[::1]:" p) {found=1} END{if (found) print "::1"}')
      if [ -n "$listen_v4" ] && [ -z "$listen_v6" ]; then
        endpoint_host=127.0.0.1
      elif [ -z "$listen_v4" ] && [ -n "$listen_v6" ]; then
        endpoint_host=::1
      else
        unset db dbhost dbport endpoint_host
        note_block "localhost does not map to one local database listener; catalog was not run"
        log "NARRATIVE_CATALOG skipped"
      fi
    fi
  fi
fi
if [ "$skip_db" -eq 0 ] && [ -n "${db:-}" ]; then
  listener_rc=0
  if [ "${db_transport:-}" = "socket" ]; then
    listener_pid=$(ss -H -xlp 2>/dev/null | awk -v dir="$endpoint_host" -v port="$dbport" '{
      if (index($0, "postgres") == 0) next
      suffix = ".s.PGSQL." port
      pos = index($0, suffix)
      if (pos == 0) next
      start = pos
      while (start > 1 && substr($0, start - 1, 1) != " ") start--
      path = substr($0, start, pos + length(suffix) - start)
      n = split(path, parts, "/")
      file = parts[n]
      dirpath = substr(path, 1, length(path) - length(file) - 1)
      if (dirpath != dir) next
      pidpos = index($0, "pid=")
      if (pidpos == 0) next
      pid = substr($0, pidpos + 4)
      sub(/[^0-9].*/, "", pid)
      if (pid ~ /^[0-9]+$/) print pid
    }')
    listener_rc=$?
  else
    listener_pid=$(ss -H -ltnp sport = :"$dbport" 2>/dev/null | awk -v host="$endpoint_host" -v port="$dbport" 'index($0, "postgres") == 0 {next} {addr=$4; if (addr == (host ":" port) || addr == ("[" host "]:" port)) {if (match($0, /pid=[0-9]+/)) print substr($0, RSTART + 4, RLENGTH - 4)}}')
    listener_rc=$?
  fi
  listener_count=$(printf '%s\n' "${listener_pid:-}" | awk 'NF{c++} END{print c+0}')
  if [ "$listener_rc" -ne 0 ]; then
    unset db dbhost dbport endpoint_host listener_pid db_transport
    note_block "database listener listing could not be read; catalog was not run"
    log "NARRATIVE_CATALOG skipped"
  elif [ "$listener_count" -ne 1 ]; then
    unset db dbhost dbport endpoint_host listener_pid
    note_block "configured database endpoint has no single postgres listener; catalog was not run"
    log "NARRATIVE_CATALOG skipped"
  else
    log "DB_ENDPOINT ${endpoint_host}:${dbport}"
    log "DB_TRANSPORT_WANTED ${db_transport}"
    log "DB_LISTENER_PID ${listener_pid}"
    psql_host=$endpoint_host
    identity=""
    psql_rc=1
    identity=$(sudo -u postgres env -u PGPASSWORD PGPASSFILE=/dev/null psql -h "$endpoint_host" -p "$dbport" -d "$db" -w -v ON_ERROR_STOP=1 -Atc "SELECT current_database() || '|' || current_setting('port') || '|' || COALESCE(host(inet_server_addr()), 'socket') || '|' || COALESCE(inet_server_port()::text, 'socket');" 2>/dev/null)
    psql_rc=$?
    unset db dbhost
    identity_safe=0
    if [ "$psql_rc" -eq 0 ] && [[ "${identity:-}" =~ ^[A-Za-z0-9_]+\|[0-9]+\|(127\.0\.0\.1|::1|socket)\|([0-9]+|socket)$ ]]; then
      identity_safe=1
    fi
    if [ "$identity_safe" -ne 1 ] || [ -z "${psql_host:-}" ]; then
      unset dbport endpoint_host listener_pid psql_host identity db_transport
      note_block "database identity could not be proven on the configured endpoint; catalog was not run"
      log "NARRATIVE_CATALOG skipped"
    else
      log "DB_IDENTITY ${identity}"
      if [ "$db_transport" = "tcp" ]; then
        log "DB_TRANSPORT tcp"
      else
        log "DB_TRANSPORT socket"
      fi
      id_name=${identity%%|*}
      id_rest=${identity#*|}
      id_port=${id_rest%%|*}
      id_tail=${id_rest#*|}
      id_addr=${id_tail%%|*}
      id_seen_port=${id_tail#*|}
      endpoint_ok=0
      if [ "$id_name" = "novixa_prod" ] && [ "$id_port" = "$dbport" ] && [ "$psql_host" = "$endpoint_host" ]; then
        if [ "$db_transport" = "tcp" ] && [ "$id_addr" = "$endpoint_host" ] && [ "$id_seen_port" = "$dbport" ]; then
          endpoint_ok=1
        elif [ "$db_transport" = "socket" ] && [ "$id_addr" = "socket" ] && [ "$id_seen_port" = "socket" ]; then
          endpoint_ok=1
        fi
      fi
      if [ "$endpoint_ok" -ne 1 ]; then
        unset dbport endpoint_host listener_pid psql_host db_transport
        note_block "identity result does not match the configured database endpoint; catalog was not run"
        log "NARRATIVE_CATALOG skipped"
      else
        column=$(sudo -u postgres env -u PGPASSWORD PGPASSFILE=/dev/null psql -h "$psql_host" -p "$dbport" -d novixa_prod -w -v ON_ERROR_STOP=1 -Atc "SELECT data_type FROM information_schema.columns WHERE table_schema = 'pack_content' AND table_name = 'brand' AND column_name = 'narrative_json';" 2>/dev/null)
        col_rc=$?
        unset dbport endpoint_host listener_pid psql_host db_transport
        if [ "$col_rc" -ne 0 ]; then
          note_block "narrative_json catalog query failed"
          log "NARRATIVE_CATALOG skipped"
        elif [ -z "${column:-}" ]; then
          log "NARRATIVE_DATA_TYPE ABSENT"
          note_block "pack_content.brand.narrative_json is absent"
        else
          case "$column" in
            jsonb|json)
              log "NARRATIVE_DATA_TYPE ${column}"
              log "NARRATIVE_TYPE_OK yes"
              ;;
            [A-Za-z]*)
              if [[ "$column" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]]; then
                log "NARRATIVE_DATA_TYPE ${column}"
                note_block "narrative_json type is not json or jsonb"
              else
                note_block "narrative_json catalog result was not a plain type name"
              fi
              ;;
            *)
              note_block "narrative_json catalog result was not a plain type name"
              ;;
          esac
        fi
      fi
      unset id_name id_rest id_port id_tail id_addr id_seen_port
    fi
  fi
else
  unset db dbhost dbport endpoint_host
  log "NARRATIVE_CATALOG skipped"
fi
if [ -L /var/www/kit-platform/api/KitPlatform.Api.dll ]; then
  note_block "live API DLL path is a symlink"
elif [ -f /var/www/kit-platform/api/KitPlatform.Api.dll ]; then
  dll_hash=$(sha256sum /var/www/kit-platform/api/KitPlatform.Api.dll | awk '{print $1}') || dll_hash=""
  if [ -z "${dll_hash:-}" ]; then
    note_block "live API DLL hash could not be read"
  else
    log "LIVE_DLL_SHA256 ${dll_hash}"
  fi
else
  note_block "live API DLL is missing"
fi
log "CANDIDATE_DLL_SHA256 F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9"
if [ -L /var/www/kit-platform/api/uploads ]; then
  note_block "uploads path is a symlink"
  backup_known=0
elif [ -d /var/www/kit-platform/api/uploads ]; then
  log "UPLOADS_BYTES $(du -sb /var/www/kit-platform/api/uploads | awk '{print $1}')"
else
  log "UPLOADS_ABSENT"
fi
settings_seen=0
for cfg in /var/www/kit-platform/api/appsettings.json /var/www/kit-platform/api/appsettings.Production.json /var/www/kit-platform/api/appsettings.Development.json; do
  if [ -L "$cfg" ]; then
    note_block "an appsettings file is a symlink"
    backup_known=0
  elif [ -f "$cfg" ]; then
    settings_seen=1
    log "APPSETTINGS_BYTES $(basename "$cfg") $(stat -c '%s' "$cfg")"
  fi
done
if [ "$settings_seen" -eq 0 ]; then
  log "APPSETTINGS_PRESENT no"
fi
backup_need=0
if [ "${backup_known:-1}" -ne 0 ]; then
  backup_known=1
fi
if [ -L /var/www/kit-platform/api ]; then
  api_bytes=0
  backup_known=0
  note_block "API tree path is a symlink"
elif [ -d /var/www/kit-platform/api ]; then
  api_bytes=$(du -sb /var/www/kit-platform/api 2>/dev/null | awk '{print $1}')
  if [[ "${api_bytes:-}" =~ ^[0-9]+$ ]] && [ "$api_bytes" -gt 0 ]; then
    log "API_TREE_BYTES ${api_bytes}"
    backup_need=$api_bytes
  else
    api_bytes=0
    backup_known=0
    note_block "API tree size could not be read"
  fi
else
  api_bytes=0
  backup_known=0
  note_block "API tree is missing"
fi
if [ -L /etc/kit-platform/api.env ]; then
  backup_known=0
  note_block "api.env path is a symlink"
  env_size=""
else
  env_size=$(stat -c '%s' /etc/kit-platform/api.env 2>/dev/null || true)
fi
if [[ "${env_size:-}" =~ ^[0-9]+$ ]]; then
  backup_need=$((backup_need + env_size))
  log "BACKUP_ENV_BYTES /etc/kit-platform/api.env ${env_size}"
else
  backup_known=0
  note_block "api.env size could not be read"
fi
if [ "${#env_paths[@]}" -gt 0 ]; then
  for envpath in "${env_paths[@]}"; do
    [ "$envpath" = "/etc/kit-platform/api.env" ] && continue
    if [ -L "$envpath" ]; then
      backup_known=0
      note_block "a systemd environment file is a symlink"
      continue
    fi
    env_size=$(stat -c '%s' "$envpath" 2>/dev/null || true)
    if [[ "${env_size:-}" =~ ^[0-9]+$ ]]; then
      backup_need=$((backup_need + env_size))
      log "BACKUP_ENV_BYTES ${envpath} ${env_size}"
    else
      backup_known=0
      note_block "a systemd environment file size could not be read"
    fi
  done
fi
if [ -z "${pub:-}" ] || [ ! -e "${pub:-}" ]; then
  log "BACKUP_PUBLICATION_BYTES not-in-copy"
elif [ -L "$pub" ]; then
  backup_known=0
  note_block "publication path is a symlink; its backup size was not proven"
elif [ -d "$pub" ]; then
  pub_bytes=$(du -sb "$pub" 2>/dev/null | awk '{print $1}')
  if [[ "${pub_bytes:-}" =~ ^[0-9]+$ ]]; then
    backup_need=$((backup_need + pub_bytes))
    log "BACKUP_PUBLICATION_BYTES ${pub_bytes}"
  else
    backup_known=0
    note_block "publication tree size could not be read"
  fi
  unset pub_bytes
else
  log "BACKUP_PUBLICATION_BYTES not-in-copy"
fi
log "BACKUP_COUNTED_RULE api-tree plus api.env plus other environment files plus the publication tree when that directory exists"
if [ "$backup_known" -eq 1 ]; then
  backup_margin=$((backup_need / 5))
  if [ "$backup_margin" -lt 67108864 ]; then
    backup_margin=67108864
  fi
  backup_required=$((backup_need + backup_margin))
  log "BACKUP_CONTENT_BYTES ${backup_need}"
  log "BACKUP_MARGIN_BYTES ${backup_margin}"
  log "BACKUP_REQUIRED_BYTES ${backup_required}"
else
  backup_required=0
  note_block "backup size could not be proven"
fi
if [ ! -d /var/backups ]; then
  note_block "backup parent /var/backups is absent"
else
  parent=$(readlink -f /var/backups 2>/dev/null || true)
  if [ -z "${parent:-}" ]; then
    note_block "backup parent could not be resolved"
  else
  case "$parent" in
    /var/www/kit-platform|/var/www/kit-platform/*)
      note_block "backup parent is inside the web tree"
      ;;
    *)
      backup_unsafe=0
      backup_compared=0
      if [ "$nginx_ok" -eq 1 ]; then
        backup_compared=1
        while IFS= read -r rootdir; do
          [ -n "$rootdir" ] || continue
          root_real=$(readlink -f "$rootdir" 2>/dev/null || true)
          if [ -z "${root_real:-}" ]; then
            backup_compared=0
            note_block "an effective nginx root could not be resolved"
            continue
          fi
          case "$parent" in
            "$root_real"|"$root_real"/*) backup_unsafe=1 ;;
          esac
        done <<< "${nginx_roots:-}"
      else
        note_block "backup parent was not compared with the effective nginx configuration"
      fi
      if [ "$backup_unsafe" -ne 0 ]; then
        note_block "backup parent is inside an effective nginx web root"
      fi
      avail=$(df -B1 --output=avail "$parent" 2>/dev/null | tail -n 1 | tr -d '[:space:]')
      log "BACKUP_PARENT ${parent}"
      if [[ ! "${avail:-}" =~ ^[0-9]+$ ]]; then
        note_block "backup free space could not be read"
      else
        log "BACKUP_PARENT_AVAIL_BYTES ${avail}"
        log "BACKUP_PROPOSAL ${parent}/kit-research-$(date -u +%Y%m%dT%H%M%SZ)"
        if [ "$backup_known" -ne 1 ] || [ "${backup_required:-0}" -le 0 ]; then
          note_block "backup free space was not compared because the required size is unknown"
        elif [ "$nginx_ok" -ne 1 ] || [ "$backup_compared" -ne 1 ] || [ "$backup_unsafe" -ne 0 ]; then
          note_block "backup free space was not accepted because the backup location was not proven outside every nginx web root"
        elif [ "$avail" -lt "$backup_required" ]; then
          note_block "backup parent does not have enough free space"
        else
          log "BACKUP_SPACE OK"
        fi
      fi
      ;;
  esac
  fi
fi
log "BACKUP_CREATED no"
log "DISK_BEGIN"
disk_ok=0
while IFS= read -r line; do
  [ -n "$line" ] || continue
  disk_ok=1
  log "$line"
done < <(df -B1 --output=target,avail,pcent / /var /var/www /var/backups 2>/dev/null || df -B1 --output=target,avail,pcent / /var /var/www 2>/dev/null || true)
if [ "$disk_ok" -ne 1 ]; then
  note_block "disk listing could not be read"
fi
log "DISK_END"
if [ "$blocked" -ne 0 ]; then
  finish_blocked
fi
log "PREFLIGHT_DONE complete"
publish_result
exit 0
```

### 9.5 Deploy and rollback plan

This section is historical. The executable runbook is `deploy/sb-research-safe-host-014/RUNBOOK.md`, recorded in closure 015. Neither document authorizes execution.

1. Backup `/var/www/kit-platform/api` including `uploads/`, and `/etc/kit-platform/api.env` with `cp -a`, to a new timestamped directory outside `/var/www/kit-platform`. If the publication root from section 9.4 exists, copy that tree too. Section 9.4 adds `du -sb` of that directory to `BACKUP_REQUIRED_BYTES` when the directory exists. A missing publication directory is not copied and is not counted. `BACKUP_SPACE OK` covers only that counted total: the API tree, `api.env`, the other systemd environment files, the publication tree when present, plus 20 percent and at least 64 MiB. Record the live DLL SHA-256, the byte size of `api.env`, and the backup path. Do not record file contents.
2. Confirm the backup DLL hash equals the live DLL hash.
3. Copy only `artifacts/sb-research-safe-host-014/api/` with `rsync -a`, excluding `appsettings*.json`, `uploads/`, and `api.env`. Do not use `--delete`. Do not copy admin. Do not change nginx. Do not run a migration. Do not set `ResearchSafeHost__SuppressBackgroundWriters`.
4. Before restart, the live DLL hash must be `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9`, `appsettings*.json` and `uploads/` must be unchanged, and the `api.env` size must match the backup unless the owner made a separate environment edit.
5. Restart only in the named window: `systemctl restart kit-platform-api`.
6. Smoke: `GET /api/health` is 200, anonymous `GET /api/personal-intelligence/research/publication` is 401, and `GET /api/personal-intelligence/trading/summary` is 404. Any other result is a failure.
7. Rollback: stop the service, restore the API tree and `api.env` from that backup, start the service, and confirm the DLL hash equals the pre-change record. Do not drop schemas.

A 401 and a 404 do not prove an authorized Research read. Permission, module, and publication path are still open. `MISSING` is the expected body only after those exist and the root is empty.

### 9.6 Decision

Superseded for deployment. Do not extract or run the script in section 9.4. Its SHA-256 `1741bf8493ce7370f4394acf30a96e058e84b0e4b3aa5e9be224401817863bc2` is withdrawn as the deploy source. Use `deploy/sb-research-safe-host-014/` and `SB-RESEARCH-SAFE-HOST-DEPLOY-CLOSURE-015.md`. The table below is the 014 historical result.

`BLOCKED`

`BLOCKED` for deploy. The script publisher is also `BLOCKED`: the local harness passed the checks this workstation can run, and it did not prove symlink publication, durable mode 0600, disk-full behavior, or closure of the same-user rename window. Production has not been observed.

| Condition | State | Evidence | What closes it |
|---|---|---|---|
| Release candidate | PASS | `artifacts/sb-research-safe-host-014/api/KitPlatform.Api.dll` SHA-256 `F394E67433952A70E26F80C22F1D5B96822BFE00ED59C59D8D9A53DAF24A0BA9`. Commit `85db8bdf`. The three source files are unchanged. Artifact 003 was not used | None |
| Preflight script | BLOCKED | 1169 lines, 41404 bytes, SHA-256 `1741bf8493ce7370f4394acf30a96e058e84b0e4b3aa5e9be224401817863bc2`. `bash -n` exit 0. Local harness: 53 passed, 0 failed, 6 not run. Symlink publication, durable mode 0600, and disk-full were not executed. The same-user rename window is not closed | A Linux run of the six skipped checks. The same-user window remains a limit of rename-by-name |
| Host and systemd | BLOCKED | Not observed. SSH from this workstation is still `Permission denied (publickey,password)` | The result file must show `HOST_IPV4 103.200.23.229`, `SERVICE_ACTIVE active`, working directory `/var/www/kit-platform/api`, and `SERVICE_USER_OK www-data` |
| Environment files, suppress flag, exchange credential key | BLOCKED | Not observed. The script scans every systemd environment file, the `Environment` property, the fragment, and drop-ins without printing values | Result file has no `BLOCKED` line for those checks |
| Effective nginx roots | BLOCKED | Not observed. Repo nginx files are not server evidence | Result file lists `NGINX_WEB_ROOT` lines from `nginx -T` |
| Publication path and access | BLOCKED | Not observed. No path is invented | Result file shows an absolute path, `PUBLICATION_OUTSIDE_WEB_ROOTS yes`, `PUBLICATION_GROUP_WRITE no`, `PUBLICATION_OTHER_WRITE no`, and `PUBLICATION_SERVICE_ACCESS read-execute` |
| Database identity and `narrative_json` | BLOCKED | Not queried. The name in configuration is not proof | Result file shows `DB_ENDPOINT` for the configured local host and port, `DB_TRANSPORT tcp` or `DB_TRANSPORT socket`, `DB_IDENTITY` starting with `novixa_prod`, then `NARRATIVE_TYPE_OK yes` |
| Backup location and free space | BLOCKED | Not measured. The directory is not created by this script. `BACKUP_SPACE OK` counts the API tree, `api.env`, other environment files, and the publication tree only when that directory exists | Result file shows `BACKUP_PARENT`, `BACKUP_PUBLICATION_BYTES`, `BACKUP_SPACE OK`, and `BACKUP_CREATED no` |
| DLL, configuration, uploads, rollback | BLOCKED on the server | The candidate hash is proven here. Live DLL, `api.env` size, appsettings sizes, and uploads size are not | Result file records those sizes and the live DLL hash. Section 9.5 uses them after a separate approval. Restart is not part of preflight |
| Deploy | BLOCKED | No production preflight result. No restart approval | Do not copy, migrate, or restart. A later `PREFLIGHT_DONE complete` still does not authorize restart |

Commit `85db8bdf` and the candidate DLL hash still match. No code was changed. No deploy script was run.

Root on the production host saves section 9.4, confirms 1169 lines, 41404 bytes, and SHA-256 `1741bf8493ce7370f4394acf30a96e058e84b0e4b3aa5e9be224401817863bc2`, then runs `bash /tmp/sb-research-preflight.sh` and pastes only `/tmp/sb-research-preflight-result.txt`. A local harness result is not that evidence. This closure does not mark the publisher PASS. Deploy stays closed until that file says `PREFLIGHT_DONE complete`, the process exits 0, and the owner separately approves the restart in section 9.5.
