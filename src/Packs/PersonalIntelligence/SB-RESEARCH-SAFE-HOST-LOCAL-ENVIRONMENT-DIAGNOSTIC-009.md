# SB-RESEARCH-SAFE-HOST-LOCAL-ENVIRONMENT-DIAGNOSTIC-009

Date: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Branch: `admin-build-blocker-001`
HEAD: `6596cfb996b3b4698917cb6f72b809931a42cc84`
Tracked diff: empty

Nothing was started, stopped, connected, or written. Port 5290 was not called. No database query was sent.

## 1. Why port 5433 has no listener

The validation cluster is installed and stopped.

| Evidence | Result |
|---|---|
| Data directory `E:\kit-pi-validation-pg` | Present. `PG_VERSION` is `18` |
| `pg_ctl status -D E:\kit-pi-validation-pg` | `pg_ctl: no server running`, exit 3 |
| `postmaster.opts` | Last start was `postgres.exe -D E:/kit-pi-validation-pg -c listen_addresses=127.0.0.1 -c port=5433` |
| `postmaster.pid` | Still records pid `28104`, data directory `E:/kit-pi-validation-pg`, port `5433`, address `127.0.0.1` |
| Process `28104` | Not running |
| `postgresql.conf` uncommented `port` or `listen_addresses` | None. Port 5433 was a command-line override, not a saved config edit |
| TCP `127.0.0.1:5433` | No listener, from the previous mission and from the stopped status above |

The port is closed because that postmaster is not running. The pid file is left over from the last run.

## 2. Where PostgreSQL is now

| Piece | State |
|---|---|
| Install | `C:\Program Files\PostgreSQL\18`, including `psql.exe` and `pg_ctl.exe` |
| Windows service `postgresql-x64-18` | Running, start mode Automatic |
| Service data directory | `C:\Program Files\PostgreSQL\18\data` |
| Listening ports owned by `postgres` | `0.0.0.0:5432` and `:::5432` only. Process id `6216` |
| Validation data directory | `E:\kit-pi-validation-pg`, not the service data directory, not running |
| Docker | `docker` is not installed, so there is no container to inspect |
| `KIT_PI_TEST_CONNECTION` | Not required for this check. The integration test still refuses any database name other than `kit_pi_validation` and refuses port `5432` |

The service on 5432 was not opened. Its database list is **UNVERIFIED**. It is a different data directory from the validation cluster, and it listens on all interfaces. It is not the Research test target.

The files inside `E:\kit-pi-validation-pg` were not queried. That the directory is the validation cluster is shown by its path and last start options. That the catalog still contains a database named `kit_pi_validation` is **UNVERIFIED** until that postmaster is started and `current_database()` is read.

## 3. Least-risk next step

Start only the existing validation data directory, with the same loopback and port already recorded in `postmaster.opts`. Do not start, stop, or retarget `postgresql-x64-18`. Do not run `scripts/second-brain-trading-ui-start.ps1`: that script builds `E:\KitPlatform`, writes accounts, and can write an exchange key.

After approval, the start is the same command line already recorded in `postmaster.opts`:

```text
pg_ctl -D E:\kit-pi-validation-pg start -o "-c listen_addresses=127.0.0.1 -c port=5433"
```

Use the `pg_ctl.exe` from `C:\Program Files\PostgreSQL\18\bin`. The `-o` value is passed to `postgres.exe`. `pg_ctl -c` is not that option. If `pg_ctl` refuses because `postmaster.pid` still names dead pid `28104`, the only file change is removal of that stale pid file, and only after the process check is repeated. Do not delete the data directory.

Then, before any write, run only:

```sql
SELECT current_database(), inet_server_port();
```

Continue the read-only preflight from report 008 only when the result is `kit_pi_validation` and `5433`.

Impact of the approved start: a PostgreSQL 18 postmaster listens on `127.0.0.1:5433` and serves whatever is already stored in `E:\kit-pi-validation-pg`. The service on port 5432 is left as it is. No migration, seed, or permission change is part of this start.

## 4. Approval required

1. Start `E:\kit-pi-validation-pg` on `127.0.0.1:5433` with `pg_ctl start -o` as written above.
2. If `pg_ctl` stops on the stale pid file, remove only `E:\kit-pi-validation-pg\postmaster.pid` after confirming pid `28104` is still not running.
3. Do not change `postgresql-x64-18`, its data directory, or port 5432.
4. Do not connect to port 5432 to look for `kit_pi_validation`.
5. Do not run the write SQL in report 008 until the identity query matches.

## 5. Production

No service was started or stopped. No database was created, queried, or modified. No credential was read. No deploy, restart, commit, or push was made.
