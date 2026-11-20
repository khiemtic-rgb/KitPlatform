# RESEARCH PUBLICATION V1 — DEPLOYMENT REPORT

Kết luận: **SUCCESS**

Publication `pub-e65b6912e02a168d` đã được cài trên VPS B và Reader của `second-brain-agent` trả `FRESH`. Chỉ một khóa cấu hình đổi, chỉ service này restart. DLL, database schema, `kit-platform-api` và cụm PostgreSQL cổng 5432 không đổi.

Thực hiện lúc 2026-10-30 15:03–15:05 +07 trên `157.10.53.182`. Không kết nối `103.200.23.229` và không mở cổng 5432.

## Từng bước

1. Package `E:\KitPlatform\deploy\second-brain-agent\research-publication-v1` khớp `SHA256SUMS.txt`, 0 lệch. Manifest là `pub-e65b6912e02a168d`, `research-publication/v1`, `research-lab-v1`.
2. Trước khi ghi, service active, PID 68847, `NRestarts=0`. Root trong `agent.env` là chuỗi rỗng. Worker `false`. Model không có khóa bật. Connection string vẫn `127.0.0.1:5433`, database `second_brain_agent_v1`, user `sb_agent`. DLL `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`.
3. `SELECT COUNT(*) FROM public.users` trên cổng 5433 trả 1.
4. Root được chọn là `/opt/second-brain-research-publication-v1`. Thư mục chưa tồn tại. Reader đọc `manifest.json` và bốn file cố định bên trong thư mục này.
5. Sao lưu `/etc/second-brain-agent/agent.env` tới `/var/backups/agent.env.before-research-publication-v1`, mode `600`.
6. Đúng một khóa đổi thành `PersonalIntelligence__ResearchPublication__Root=/opt/second-brain-research-publication-v1`. Đối chiếu từng dòng trước và sau: chỉ khóa đó khác.
7. Năm file JSON được cài, owner `sbagent`, trước restart. Checksum trên đĩa đích khớp manifest. Không có file thừa.
8. `systemctl restart second-brain-agent` lúc 15:04:30 +07. PID mới 76816, `NRestarts=0`. Hai lần đầu cổng 5095 chưa nghe, lần tiếp theo health HTTP 200.
9. Năm GET research đều HTTP 200 sau đăng nhập `sb-operator`. Phiên dùng để nghiệm thu đã logout HTTP 204. `active_sessions=0`.
10. Ba refresh token legacy, `session_id` null, vẫn mở.
11. `kit-platform-api` vẫn PID 37553 và active. `postgresql@16-main` vẫn PID 34958. `postgresql@16-sbagent` vẫn PID 43039. nginx vẫn active.

## File đã cài

| File | SHA-256 |
|---|---|
| `manifest.json` | `cf0661e9a64f2a40ded3202b9a1dca4621839a5be44b45b0aed57f969095c578` |
| `lab/LAB_RESULT.json` | `8f19e6e010999637fe2b1b793106ea2c22b5def60e06fbb8b06dcb6267d5714b` |
| `paper/PAPER_OPS_STATUS.json` | `0cdecbb0e7b2a5f8430cd32d713aaf5be447096272a273cb72e62b0f32bc84b5` |
| `paper/PAPER_RESEARCH_REPORT.json` | `84b67af769b13ff13eae823f07446de682334f03fff2b7643a6069f2b83f549f` |
| `paper/PAPER_OPS_HEALTH.json` | `d4e557a3510047b127cd9ff1b714a63dd9030b115e0a25399fa27d26b4fab6ab` |

## Reader

| Route | HTTP | Kết quả |
|---|---|---|
| `GET /api/personal-intelligence/research/publication` | 200 | `FRESH`, id `pub-e65b6912e02a168d`, schema `research-publication/v1`, publisher `research-lab-v1`, 4 object đúng checksum, không có `readError` |
| `GET /api/personal-intelligence/research/experiments` | 200 | `FRESH`, 3 experiment, `liveOrders=false`, `exchangeOrdersSent=0` |
| `GET /api/personal-intelligence/research/experiments/EXP-TREND-SMA-10-30-V1` | 200 | `FRESH`, đúng id, không có lệnh sàn |
| `GET /api/personal-intelligence/research/paper/status` | 200 | `FRESH`, health `OK`, `liveOrders=false`, `exchangeOrdersSent=0` |
| `GET /api/personal-intelligence/research/paper/report` | 200 | `FRESH`, 3 strategy, `labResult=SEE_LAB_RESULT_ENDPOINT`, không có lệnh sàn |

Ba experiment: `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, `EXP-REVERSION-RANGE-20-V1`.

## Service trước và sau

| Mục | Trước | Sau |
|---|---|---|
| Agent PID | 68847 từ 12:33:46 +07 | 76816 từ 15:04:30 +07 |
| DLL | `ff37afc7…deabc` | cùng checksum |
| `/api/health` | HTTP 200 ở lần kiểm tra trước | HTTP 200, `workerEnabled=false` |
| Hosted services | DataProtection, GenericWebHost | cùng hai service, không có ExchangeBackfill |
| `/api/health/db` | `second_brain_agent_v1` | HTTP 200, cùng database |
| Publication root | rỗng | `/opt/second-brain-research-publication-v1` |
| `users` | 1 | 1 |
| `exchange_connection` / `trade` / `pi_task` / `pi_events` | 0 / 3 / 0 / 18 | 0 / 3 / 0 / 18 |
| Legacy refresh mở | 3 | 3 |

Process đang chạy có `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false` và `ASPNETCORE_URLS=http://127.0.0.1:5095`.

## Ranh giới

- Không đổi connection string, DLL, hay source.
- Không migration, không SQL ghi, không đổi quyền.
- Không restart `kit-platform-api`.
- Không mở cổng 5432, không mở `novixa_prod`, không vào VPS A.
- Không bật LLM, Exchange, ExchangeBackfillWorker, hay trading.
- Đăng nhập nghiệm thu tạo một session rồi logout. Không còn session active. Ba token legacy không bị revoke.

## Rollback

Chưa cần chạy. Khi cần, chỉ trong phạm vi cấu hình và file publication:

```bash
cp -a /var/backups/agent.env.before-research-publication-v1 /etc/second-brain-agent/agent.env
systemctl restart second-brain-agent
```

Sau khi health HTTP 200 và root trong process là chuỗi rỗng, GET publication phải trả `MISSING`. Có thể xóa `/opt/second-brain-research-publication-v1` sau bước đó. Không đụng DLL, database, `kit-platform-api`, hay cụm cổng 5432.
