# Trading Research V1 — production pilot

Trạng thái: **DEPLOYED_AND_VERIFIED**

Host: `103.200.23.229` (`novixa`). Cửa sổ: 2026-10-23T06:42:39Z đến 2026-10-23T07:01:10Z. Không restart `kit-platform-api`. Không sửa `/etc/kit-platform/api.env`. Không dùng `novixa_prod`. Không gửi lệnh sàn.

## Candidate

| Artifact | SHA-256 |
|---|---|
| `KitPlatform.Packs.PersonalIntelligence.ResearchHost.dll` | `43d8c2142278d5b1488b3ca29651f507d8d85db765483101a29272555fd1c00e` |
| `migrations/395_research_trading_v1.sql` | `65f3cac2c2ced4a0dd1a38427613e68a0b0c0044c7da7ee7e5ada393c257c5e8` |

Hash trên VPS khớp hai giá trị này trước khi smoke test. Runtime trên VPS: `Microsoft.NETCore.App 10.0.9`. Publish là `linux-x64`, framework-dependent, `net10.0`.

## Dịch vụ hiện hữu

| Kiểm tra | Trước | Sau timer 14:00 +07 |
|---|---|---|
| `kit-platform-api` PID | `3947857`, active từ 2026-10-23 12:38:11 +07 | `3947857`, active |
| `http://127.0.0.1:5000/api/health` | 200 | 200 |
| `https://admin.novixa.vn/` | 200 | 200 |
| `https://app.novixa.vn/` | 200 | 200 |
| nginx | active | active |
| Listen `:5000` | `127.0.0.1:5000` | `127.0.0.1:5000` |

Research host không còn process sau mỗi lần oneshot và không mở socket.

## Database và backup

Database mới `trading_research`. Identity trước migration: `trading_research|5432`.

`novixa_prod` có ACL mặc định nên PUBLIC vẫn được CONNECT. `REVOKE CONNECT` trên role mới không chặn được đường đó. Trước rule `host all`, `pg_hba` có ba dòng reject chỉ cho user `trading_research` vào `novixa_prod`. Postgres được reload, không restart. Kết nối thử từ role research tới `novixa_prod` bị `pg_hba` từ chối. Kết nối tới `trading_research` thành công. File `pg_hba` trước khi sửa nằm ở `/var/backups/kit-trading-research/pg_hba.conf.before`.

Role `trading_research` được `GRANT USAGE` trên schema `research_trading`, `SELECT, INSERT` trên các bảng và `USAGE, SELECT` trên sequence. Grant này chỉ nằm trong database research.

Backup, cả hai đã qua `pg_restore -l`:

| File | SHA-256 | Nội dung |
|---|---|---|
| `trading_research-20261023T065028Z-empty.dump` | `f945011c9e6169dfd53352fedcc05328a0152e5f92e1a283e1a08ca4539af836` | Database trống, trước migration của lần cài thành công |
| `trading_research-20261023T065029Z-schema.dump` | `156ff2052e2ee0142808ecb8240d5f30f63508eb8c490ec5507211375477784a` | Schema sau migration và grant, trước smoke data |

Hai dump sớm hơn (`064843Z`, `064844Z`) thuộc lần cài đã rollback vì thiếu grant. Không dùng chúng để restore lần đang chạy.

## Process

- User `kit-research`, shell `nologin`. Không đọc được `/etc/kit-platform/api.env` (mode `600 root:root`).
- Binary: `/opt/kit-platform/trading-research/`, `root:root`.
- Env: `/etc/kit-platform/trading-research.env`, `640 root:kit-research`.
- Connection file: `/var/lib/kit-platform/trading-research/connection.txt`, `600 kit-research`. Database name là `trading_research`.
- Unit: `kit-trading-research.service`, oneshot, `MemoryMax=256M`, `CPUQuota=20%`, `TasksMax=32`, `NoNewPrivileges`, `PrivateTmp`.
- Timer: `kit-trading-research.timer`, enabled và active. Lần timer đầu: 2026-10-23 14:00:32 +07, exit 0. Lần kế tiếp đã lên lịch: 2026-10-23 15:00:01 +07.

Pilot: `BTCUSDT`, `sma-cross-v1`, 1 giờ, quantity `0.001`, notional tối đa `1000`, slippage 10 bps, fee rate `0.001`. Không có credential sàn.

## Smoke test

Egress từ VPS: `GET /api/v3/klines?symbol=BTCUSDT&interval=1h&limit=2` trả HTTP 200, weight 2, methods `GET, HEAD, OPTIONS`.

Lần chạy tay trước khi bật timer:

- Lần 1: exit 0. 47 nến đóng. 1 tín hiệu `ready`, risk allowed. 1 paper BUY.
- Lần 2: exit 0, `REPLAYED`, 0 nến mới, 47 trùng, 0 fill mới.
- Sau khi bật timer, một lần start tay: fill vẫn 1, nến vẫn 47.

Fill paper duy nhất:

| Trường | Giá trị |
|---|---|
| Side / quantity | BUY / 0.001 |
| Tape | 81889.32 |
| Execution | 81971.20932 (= tape × 1.001) |
| Fee | 0.08197121 |
| Slippage quote | 0.08188932 |
| Net cash | −82.05318053 |
| PnL | null, vì vị thế chưa đóng |

Journal và health không chứa `Password=`, `/api/v3/order` hay testnet.

## Lần timer đầu

14:00:32 +07, exit 0, một GET klines, weight tích lũy 4. Nến đóng mới: 1. Nến trùng: 46. Tổng nến: 48. Fill vẫn 1. Operation vẫn 1. Không có fill mới. Process research sau đó không còn chạy.

## Lần cài đã rollback

2026-10-23T06:48:42Z cài lần đầu, smoke chết vì `permission denied for schema research_trading`. Script rollback đã xóa user, unit, database và role, hoàn nguyên `pg_hba`, rồi xác nhận API vẫn PID `3947857` và health 200. Lần cài 06:50:27Z thêm grant trong `trading_research` và là lần đang chạy.
