# SB-RESEARCH-SAFE-HOST-LOCAL-VALIDATION-010

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`

The validation cluster is running. Identity is `kit_pi_validation` on `127.0.0.1:5433`. No write SQL was sent. The Windows service was not changed. Port 5432 was not opened.

## 1. Commands and results

| Step | Command | Result |
|---|---|---|
| Pid check | Process id `28104` | Not running |
| Cluster status | `pg_ctl status -D E:\kit-pi-validation-pg` | `no server running`, exit 3 |
| Pid file | `E:\kit-pi-validation-pg\postmaster.pid` | Present. Pid `28104`, directory `E:/kit-pi-validation-pg`, port `5433`, address `127.0.0.1` |
| Start | `pg_ctl -D E:\kit-pi-validation-pg start -w -t 30 -o "-c listen_addresses=127.0.0.1 -c port=5433"` | Exit 0. `server started` |
| Listener | TCP listen on port 5433 | `127.0.0.1:5433` only, postmaster pid `22128` |
| Identity | `psql -w -h 127.0.0.1 -p 5433 -U pi_validation -d kit_pi_validation -c "SELECT current_database(), inet_server_port();"` | `kit_pi_validation` and `5433`, exit 0 |

`pg_ctl.exe` and `psql.exe` were `C:\Program Files\PostgreSQL\18\bin`. Local `pg_hba.conf` on this cluster trusts `127.0.0.1/32`, so the identity query used no password. No credential was read or written into this report.

The stale pid file was not deleted. Startup warned `another server might be running; trying to start server anyway`, then PostgreSQL 18.4 started because pid `28104` was dead. After start, `postmaster.pid` records pid `22128`, the same data directory, port `5433`, and `127.0.0.1`.

## 2. Errors

The first connection used the Windows user and failed before any query:

```text
FATAL: role "Admin" does not exist
```

The retry used the role already named by `scripts/second-brain-trading-ui-start.ps1`, `pi_validation`. That connection succeeded.

PostgreSQL logged that the cluster had not shut down cleanly. Last known up time was `2026-10-22 12:01:51 +07`. It ran its own redo and then reported `database system is ready to accept connections`. No config edit, restore, or repair command was run.

## 3. What was left untouched

| Item | State |
|---|---|
| Windows service `postgresql-x64-18` | Still running. Not started or stopped |
| Port 5432 | Not connected |
| SQL writes, migrations, seeds, roles, tenants | Not run |
| PostgreSQL configuration | Not edited |
| Application, deploy, commit, push | Not done |

No query other than `current_database()` and `inet_server_port()` was sent. The read-only preflight statements in report 008 were not run.

## 4. Final status

Identity gate passed: `current_database() = kit_pi_validation` and `inet_server_port() = 5433`.

The cluster is accepting connections on `127.0.0.1:5433` only. Further preflight reads still need a separate approval. The prepared write SQL in report 008 remains unexecuted.
