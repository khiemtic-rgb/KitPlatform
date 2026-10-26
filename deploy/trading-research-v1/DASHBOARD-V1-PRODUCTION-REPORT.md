# Trading Research Dashboard V1 — production

Status: `DEPLOYED_AND_VERIFIED`

Date: 2026-10-23T08:34:06Z  
Host: `novixa` (`103.200.23.229`)  
Access from this Windows machine: `http://127.0.0.1:25088` through the existing SSH tunnel to `127.0.0.1:5088` on the VPS.

`DASHBOARD-V1-REPORT.md` was not in the repository. Deployment follows the dashboard source: loopback bind, a separate connection file, and `SELECT` against `research_trading` only.

## What was created

- Linux user `kit-research-dashboard`, shell `/usr/sbin/nologin`, home `/var/lib/kit-platform/trading-research-dashboard` mode `750`.
- PostgreSQL role `trading_research_read`, `NOSUPERUSER NOCREATEDB NOCREATEROLE`. Password exists only in the connection file below.
- Connection file `/var/lib/kit-platform/trading-research-dashboard/connection.txt`, mode `600`, owner `kit-research-dashboard:kit-research-dashboard`. Database name is `trading_research`.
- Application directory `/opt/kit-platform/trading-research-dashboard/`, owner `root:root`.
- systemd unit `/etc/systemd/system/kit-trading-research-dashboard.service`, enabled and active.
- `pg_hba.conf` gained three leading reject lines marked `kit-trading-research-dashboard-deny-novixa` for user `trading_research_read` and database `novixa_prod`. Postgres was reloaded, not restarted. Backup: `/var/backups/kit-trading-research-dashboard/pg_hba.conf.before`.

The published DLL SHA-256 is `c2b7109fdd25e188415403b20bb0f10adb202b4d58aae34808f6f62c38fc7cb4`.

The unit binds `ASPNETCORE_URLS=http://127.0.0.1:5088` and sets `Dashboard__ConnectionFile` to the connection file. It does not use `/etc/kit-platform/api.env` or the research host environment file.

## Database identity and privileges

Identity through the dashboard role: `trading_research|5432|127.0.0.1`.

`trading_research` was not reused. That role still has `INSERT` on 7 tables in `research_trading`.

`trading_research_read` table privileges, and only these:

| Table | Privilege |
|---|---|
| `candle` | `SELECT` |
| `signal` | `SELECT` |
| `risk_decision` | `SELECT` |
| `paper_fill` | `SELECT` |
| `health` | `SELECT` |
| `session_result` | `SELECT` |

Schema `USAGE` is true. Schema `CREATE` is false. An `INSERT` into `research_trading.health` returned permission denied. The health row count stayed `5` before and after that probe.

A connection to `novixa_prod` as `trading_research_read` was rejected by `pg_hba.conf`. The dashboard user cannot read `/etc/kit-platform/api.env` or the research host connection file.

## Acceptance

| Check | Evidence |
|---|---|
| Service listens on loopback | `ss` shows `127.0.0.1:5088` only, `dotnet` pid `3953296`. Active, enabled, `NRestarts=0`. |
| HTTP on the VPS | `GET /` and `GET /api/dashboard` both `200`. |
| Real data | Response `source=research_trading`. Counts match SQL: 49 candles, 1 signal, 1 fill, 5 health rows, 2 replays. Fees `0.08197121`, slippage `0.08188932`, cash from fills `-82.05318053`. |
| Screens | Latest run `COMPLETED` at `2026-10-23T08:00:10Z`, with 1 new candle, 46 duplicates, 0 new fills. Market row `BTCUSDT` `1h`, closed. Signal `sma-cross-v1` `BUY` `ready`, risk allowed. Fill `BUY` quantity `0.001`, tape `81889.32`, execution `81971.20932`. |
| PnL | Latest session `PnlStatus=not_recomputed_on_replay`, amount null. API `realizedPnl.known=false` and amount null. |
| Closed candles | SQL count of `close_time > now()` is `0`. All 49 returned rows are closed. |
| Fixture not selected | `source` is `research_trading`. The fixture file remains on disk and is used only when no connection file is configured. |
| Secrets | Password hits in the unit file, process environment, and journal: `0`. |
| API unchanged | PID `3947857`, active since `Fri 2026-10-23 12:38:11 +07`, `/api/health` `200`, before and after. |
| Research timer unchanged as a unit | `kit-trading-research.timer` stayed enabled and active since `Fri 2026-10-23 13:50:41 +07`. Unit SHA-256 stayed `f88f9335f565958e6a55fa7bdc8ba4857873848572829042414aa3ff45b5e6a0`. Service unit SHA-256 stayed `e4bd1d5fecf93c16934e37681d675251346f3f97e885b5fcf8abf90045fe0c98`. The oneshot service stayed inactive. No research run was started. |
| No exchange order | Dashboard `ExecStart` is only the dashboard DLL. No exchange credential was added. Postgres remains on `127.0.0.1:5432` and `::1:5432`. |

`daemon-reload` for the new unit left the timer definition unchanged. The displayed next elapse moved from `16:00:10 +07` to `16:00:01 +07`, still the same hourly schedule and inside the existing 30-second random delay. The timer was not rewritten or restarted.

From this machine, through the tunnel that was already open, `http://127.0.0.1:25088/api/dashboard` returned `200` with `source=research_trading`, 49 candles, 1 fill, and a null realized PnL.

Two earlier attempts stopped before the service was enabled. The first rolled back on an address-format check. The second found the read role still present because `DROP ROLE` had not cleared its grants. The successful run dropped that leftover role with `DROP OWNED`, then created it again. API PID and health stayed the same across those attempts.

## Rollback of the dashboard only

Do not stop `kit-platform-api`, `kit-trading-research.service`, or `kit-trading-research.timer`.

```bash
systemctl disable --now kit-trading-research-dashboard.service
rm -f /etc/systemd/system/kit-trading-research-dashboard.service
systemctl daemon-reload
rm -rf /opt/kit-platform/trading-research-dashboard
rm -rf /var/lib/kit-platform/trading-research-dashboard
hba=$(sudo -u postgres psql -d postgres -tA -c 'SHOW hba_file;')
cp -a /var/backups/kit-trading-research-dashboard/pg_hba.conf.before "$hba"
sudo -u postgres psql -d postgres -c 'SELECT pg_reload_conf();'
sudo -u postgres psql -d trading_research -c 'DROP OWNED BY trading_research_read;'
sudo -u postgres psql -d postgres -c 'DROP ROLE trading_research_read;'
userdel --remove kit-research-dashboard
```

Restoring that `pg_hba` backup removes the dashboard reject lines and keeps the existing research-host reject lines.
