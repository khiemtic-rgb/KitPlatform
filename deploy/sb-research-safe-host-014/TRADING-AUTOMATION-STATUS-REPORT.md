# Trading automation status

Status: `DEPLOYED_NOT_RUNNING`

Host: `103.200.23.229`  
Checked: `2026-10-23T05:45:32Z` and follow-up `2026-10-23T05:46:52Z`  
Service inspected: `kit-platform-api`  
Database inspected: `novixa_prod`  
This pass was read-only. No service was restarted, stopped, or disabled. No exchange API was called.

HTTP 404 from the trading summary route is not the basis of this conclusion.

## What is running

`kit-platform-api` is `active` and `enabled`. One `dotnet` process:

| Field | Value |
|---|---|
| PID | 3947857 |
| Parent | 1 |
| User | `www-data` |
| Started | Fri Oct 23 12:38:10 2026 +07 |
| Working directory | `/var/www/kit-platform/api` |
| Command | `/usr/bin/dotnet /var/www/kit-platform/api/KitPlatform.Api.dll` |
| Listen | `127.0.0.1:5000` |

The process environment has no variable whose name matches exchange, trading, Bybit, Binance, OKX, CCXT, credential, or background-writer suppression. The same names are absent from `/etc/kit-platform/api.env`. Values were not printed.

Other `www-data` processes are nginx workers. The only other Python process is root PID 3676377, started Fri Oct 16 22:41:57 2026, command `/usr/bin/python3 /opt/kit-platform/sms-otp-stub.py 9091`, listening on `127.0.0.1:9091`. That is not a trading bot. `unattended-upgrade-shutdown` is the Ubuntu upgrade helper. No Node.js, PM2, supervisor, Docker, or Podman process was present.

Listeners are DNS on `127.0.0.53` and `127.0.0.54`, the stub on `9091`, the API on `5000`, nginx on `80` and `443`, Postgres on `5432`, and SSH on `22`.

## Schedulers

No crontab for `root`, `www-data`, or `postgres`. No systemd service or timer whose name matches trading, CCXT, exchange, Bybit, Binance, OKX, Freqtrade, Hummingbot, or second-brain. `certbot.timer` is certificate renewal; the name contains the letters "bot" and it is not a trading job. The other timers are apt, dpkg backup, logrotate, motd, tmpfiles, e2scrub, fstrim, apport, snap, and Ubuntu advantage. Docker, Podman, supervisor, and PM2 are not installed. No tmux session was listed.

`/etc/systemd/system/kit-platform-api.service` has no drop-in. User is `www-data`. Working directory is `/var/www/kit-platform/api`.

## Deployed code, not started

On the live API tree:

| File | `ExchangeBackfillWorker` | `AddPersonalIntelligencePack` | `AddPersonalIntelligenceResearch` |
|---|---|---|---|
| `KitPlatform.Api.dll` | absent | absent | present |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | absent | absent | absent |
| `KitPlatform.Packs.PersonalIntelligence.Infrastructure.dll` | present | present | present |

The running entry assembly does not contain the worker registration. The worker type is present only inside the infrastructure DLL. No second process is executing it.

`/opt/kit-platform` contains `api`, `deploy`, `greenfield-compat`, and `migrations`. No file or directory name there matched trading, CCXT, exchange, Bybit, Binance, OKX, or Freqtrade within the search depth. The running API cwd is `/var/www/kit-platform/api`, not `/opt/kit-platform/api`.

## Order evidence

`pack_personal_intelligence` is not a schema in `novixa_prod` (count 0). These relations are absent:

- `trading_order`
- `trading_execution`
- `exchange_connection`
- `trading_account`

No exchange account row and no order row can exist in that schema.

`journalctl -u kit-platform-api --since 2026-07-01` has 0 lines matching ExchangeBackfill, place/submit/create order, Bybit, Binance, OKX, CCXT, or Freqtrade. No trading-named log file was found under `/var/log`, `/opt`, `/home`, or `/root` at the search depth used.

## Scope not opened

Exchange APIs were not called. Credential values, ciphertext, and customer rows were not read. The journal search is the `kit-platform-api` unit since 2026-07-01, not every rotated file outside journald. Filename search stopped at depth 4 and skipped `uploads` and `node_modules`.

## Conclusion

Trading worker code is on disk inside the deployed infrastructure DLL and is not registered by the running API. No trading process, account credential, scheduler, exchange connection, or order record was found. `DEPLOYED_NOT_RUNNING`.
