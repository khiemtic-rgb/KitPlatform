# SECOND BRAIN × TRADING RESEARCH — CURRENT STATE AUDIT

As-of: 2026-11-02, lần đọc VPS B từ 11:23 đến 11:35 +07.  
Host: `157.10.53.182`, hostname `interdata.1793262774`, timezone `Asia/Ho_Chi_Minh`.  
Chế độ: chỉ đọc. Không sửa file, không restart, không migration, không POST/PUT/PATCH/DELETE, không đụng cổng PostgreSQL 5432, không đụng `novixa_prod`, không đụng VPS A.

Mỗi kết luận ghi **Observation** khi đo được trực tiếp, hoặc **Inference** khi suy từ code cộng với hiện vật đã đo. Không có số hiệu suất nào trong báo cáo này được dùng làm kết quả chiến lược.

## 1. Executive Summary

Second Brain trên VPS B đang chạy. Process `second-brain-agent` PID 78259 khởi động 2026-10-30 15:58 +07, `NRestarts=0`, health và DB health đều HTTP 200. Binary đúng gói Research Evaluation đã cài ngày 30/10. Publication `pub-e65b6912e02a168d` còn nguyên checksum. Worker, backfill và model LLM đều tắt. Không có session đăng nhập đang hiệu lực.

Pipeline nến trên VPS không còn chạy. Không có unit, timer, process hay journal của `kit-trading-research`. Database `second_brain_agent_v1` trên cổng 5433 không chứa nến. Cổng 5432 không được mở, nên không xác nhận database `trading_research` cũ còn hay mất.

Dữ liệu nghiên cứu SOLUSDT nằm trên máy trạm, không phải trong database VPS. Snapshot đóng băng 2 159 nến 1 giờ, liên tục, từ 2026-07-23 10:00 UTC đến 2026-10-21 08:00 UTC. Checksum nến khớp publication. Cache paper cục bộ nối thêm đến 2026-11-01 15:00 UTC, 271 nến, không đứt bên trong. Lần chạy thành công cuối của task Windows là 2026-11-01 16:04 UTC. Lần kích hôm nay 11:12 +07 trả `0x800710E0` và không ghi log mới.

Ba experiment trong publication đều tự ghi `EXPERIMENT_NOT_APPROVED`, `INSUFFICIENT_EVIDENCE`, `NOT_A_PERFORMANCE_CLAIM`. API đánh giá, nếu được gọi với quyền đọc, chỉ chiếu publication và gán Sharpe, CAGR, max drawdown, expectancy, equity curve là `NOT_IN_SOURCE`. Lượt này không đăng nhập vì login là POST. Không có chiến lược nào đủ bằng chứng để chọn.

Trading Agent tự nghiên cứu chưa tồn tại như một vòng đang chạy. Đã có host, publication, máy chiếu đánh giá, và một lab cục bộ. Chưa có collector VPS, chưa có backtest chạy lại trên VPS, chưa có paper liên tục trên VPS, chưa có learning loop có dữ liệu, chưa có lệnh sàn.

| Hạng mục | Trạng thái |
|---|---|
| A. Second Brain Host | `OPERATIONAL_WITH_GAPS` |
| B. Research Data Pipeline | `NOT_OPERATIONAL` trên VPS. Dataset SOLUSDT trên máy trạm còn dùng được, đuôi dữ liệu đã dừng từ 2026-11-01 16:04 UTC |
| C. Research Publication | `OPERATIONAL_WITH_GAPS` |
| D. Strategy Evaluation | `PARTIALLY_OPERATIONAL` |
| E. Trading Agent | `NOT_OPERATIONAL` |
| F. Paper Trading | `NOT_OPERATIONAL` trên VPS. Task máy trạm được lên lịch mỗi giờ nhưng lần gần nhất không ghi dữ liệu mới |
| G. Live Trading | `NOT_OPERATIONAL` |

## 2. Overall System Status

Hai mặt phẳng đang tách nhau.

Mặt phẳng VPS B, đo 11:23 +07: một process web .NET nghe `127.0.0.1:5095`, đọc publication trên đĩa, nối PostgreSQL `127.0.0.1:5433` / `second_brain_agent_v1`. Không có worker nghiên cứu trong process. Journal từ lúc start chỉ có năm dòng systemd lúc restart ngày 30/10, không có dòng lỗi ứng dụng.

Mặt phẳng máy trạm `E:\Kit-Brain\second-brain-trading-source-inventory`: lab Python, snapshot SOLUSDT, journal thí nghiệm, cache paper và scheduled task `SecondBrainPaperOpsV1`. Task đang `Ready`, trigger mỗi giờ. Log thành công cuối 2026-11-01 16:04:02 UTC. Cache nến không được ghi thêm sau đó.

`origin/main` tại `7505e016` (2026-10-28) không chứa commit evaluation `9d28227` hay commit session `2c1ea353`. Hai commit đó chỉ có trên nhánh local `park-marketing/agent-dang-bai` và `second-brain/paper-proposal-v1`. Binary đang chạy khớp hash gói ngày 30/10, không phải bản build từ HEAD hiện tại.

## 3. VPS B Runtime Inventory

Observation, 2026-11-02 11:23:12 +07, `hostname`, `date -Is`, `systemctl show second-brain-agent`.

| Mục | Giá trị |
|---|---|
| OS | Ubuntu 24.04.5 LTS |
| Uptime máy | 3 ngày 19 giờ. Boot hiện tại bắt đầu 2026-10-29 16:03:19 +07 (`journalctl --list-boots`) |
| Service | `second-brain-agent.service`, `active` / `running`, `enabled` |
| PID | 78259, user `sbagent` |
| Start | 2026-10-30 15:58:00 +07 |
| Elapsed lúc đọc | 2 ngày 19 giờ 25 phút |
| NRestarts | 0 |
| Result | success |
| RAM | khoảng 55 MB |
| Working directory | `/opt/second-brain-agent-v1-9d28227/host` |
| Exec | `/usr/bin/dotnet` cùng DLL trong thư mục đó |
| Drop-in | `/etc/systemd/system/second-brain-agent.service.d/v1-session-security.conf` |
| Listen | `127.0.0.1:5095` chỉ process này |
| PostgreSQL | `postgresql@16-sbagent` cổng 5433, `127.0.0.1`. `postgresql@16-main` cổng 5432 online, không được truy vấn |

Health, `GET http://127.0.0.1:5095/api/health` lúc 11:23 +07, HTTP 200:

`status=ok`, `service=second-brain-agent`, `database=second_brain_agent_v1`, `workerEnabled=false`, `backfillWorkerRegistered=false`, hosted services chỉ có DataProtection và GenericWebHost.

`GET /api/health/db` HTTP 200: `status=ok`, `database=second_brain_agent_v1`, `value=1`, `workerEnabled=false`.

`agent.env` mode `600`. Các giá trị secret không được in. Các khóa quan sát được:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://127.0.0.1:5095`
- Connection string trỏ `127.0.0.1:5433`, database `second_brain_agent_v1`, user `sb_agent`. Không có `Port=5432`, không có `novixa_prod`.
- `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`
- `PersonalIntelligence__ResearchPublication__Root=/opt/second-brain-research-publication-v1`
- `SecondBrain__SystemIdentifier` khớp `system_identifier` của cụm 5433: `7702079316921501630`
- Không có khóa model, LLM, OpenAI hay BusinessIntent
- JWT secret và bootstrap password đang được đặt. Giá trị không được đọc ra báo cáo

Binary đang chạy, `sha256sum` lúc 11:25 +07, khớp từng byte với báo cáo deploy ngày 30/10:

| File | SHA-256 |
|---|---|
| `KitPlatform.SecondBrainAgent.Host.dll` | `f8d87d61d4fd0abea12a08a58dbc1d43fb90f4e9f3df6c801f665ec01c375f3f` |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | `9a9188d992a95d9eff63a08fd8fb42cdcc5a9bf538a68e09b6afb151bbe4e94d` |
| `KitPlatform.Packs.PersonalIntelligence.Application.dll` | `ebff1843e3cfb3103af26f9a1df41c28159b0f93b502558aa681201cc37b6fd2` |
| `KitPlatform.Infrastructure.dll` | `ae757eab4a880900083ca20e95dcdde2a9c3f1e5627c45a285839477f636967a` |

Chuỗi UTF-16 trong DLL đang chạy: `auth_session`, `Session is required.`, `publication_projection`, `NOT_SELECTABLE`, `NOT_IN_SOURCE`, `NO_EQUITY_CURVE`. Không có `MayExposeUser`. Trên 22 DLL của thư mục host, không có literal `/api/v3/order` hay `/api/v3/klines`. `KitPlatform.Packs.PersonalIntelligence.Infrastructure.dll` có literal `api.binance.com`. Process không có khóa sàn và `exchange_connection` = 0, nên literal đó không phải một phiên giao dịch đang mở.

Các bản DLL cũ còn trên đĩa và không phải process đang chạy: `/opt/second-brain-agent/host`, `second-brain-agent-candidate`, `v1-2c1ea353`, `v1-5f29e1bd`, `v1-session-security` (hash host cũ `ff37afc7…`).

UI đang phục vụ: `/opt/second-brain-agent/ui/index.html` hash `e8c8c387…`, script `index-CAMJg88Q.js` hash `8a5f5f08…`. File JS có các câu “So sánh evaluation”, “Đây là chiếu publication”, `NOT_A_PERFORMANCE_CLAIM`. Còn một file `index-DmF68tyh.js` không được `index.html` trỏ tới.

Nginx sites-enabled có `kit-platform` và `kit-platform-w5`. `grep` `5095` và `second-brain` trong config nginx không ra dòng. Agent không được publish qua nginx.

Journal `second-brain-agent` từ 2026-10-30 15:58: năm dòng, toàn bộ là stop/start systemd. Không có dòng exception.

## 4. Second Brain Capability Audit

GET không token, 11:23 +07, tất cả trả 401, không trả 404:

- `/api/personal-intelligence/research/publication`
- `/api/personal-intelligence/research/experiments`
- `/api/personal-intelligence/research/evaluations`
- `/api/personal-intelligence/research/paper/status`
- `/api/personal-intelligence/research/paper/report`
- `/api/personal-intelligence/trading/summary`, `accounts`, `trades`, `decisions`, `operations`, `paper-executions`
- `/api/personal-intelligence/trading/exchanges`, `catalog`, `backfill`
- `/api/personal-intelligence/tasks`
- `/api/personal-intelligence/intent/status`

`GET /` trả 200. Login không được gọi vì đó là POST.

Observation trên `second_brain_host.operator`, 11:32:43 +07: role `ADMIN`. Quyền lưu kèm user: `personal_intelligence.trading.run`, `trading.approve`, `research.read`, `exchange.read`. Bảng `public.permissions` chỉ có hai dòng catalog: `exchange.connect` và `exchange.read`. `research.read` nằm trên operator, không nằm trong catalog. Module `personal_intelligence` status lưu số `1`. User duy nhất `sb-operator`, tenant `SECOND_BRAIN`, status lưu số `1`.

Inference từ code `ResearchPublicationController` và policy `ResearchRead`: các GET research cần đúng quyền đọc. Operator có `research.read` trong mảng quyền. Lượt này không chứng minh lại HTTP 200 vì không tạo session mới.

Hosted worker nghiên cứu: không có. Intent/LLM: khóa bật không tồn tại trong env. Exchange backfill: `false` và health báo worker không được đăng ký.

Ledger trong DB là dữ liệu fixture và paper cũ, không phải vòng agent đang chạy. Chi tiết ở mục 6.

## 5. Market Data Collection Inventory

### 5.1 Collector trên VPS

Observation, 11:23–11:27 +07:

- `systemctl list-unit-files` không có unit trading hay research ngoài `second-brain-agent`.
- Không có file `kit-trading-research.service` hoặc `.timer` trong `/etc/systemd/system` hay `/lib/systemd/system`.
- `systemctl list-timers --all` không có timer research. 21 timer còn lại là timer hệ điều hành.
- `journalctl -u kit-trading-research.service` trả “No entries”.
- `journalctl -F _SYSTEMD_UNIT` chỉ thấy `second-brain-agent.service` trong nhóm tên brain/research/trading.
- Không có process ResearchHost, trading-research hay binance.
- Cổng 5088 không listen. Dashboard research cũ không chạy.
- `/opt/kit-platform/trading-research` và `/var/lib/kit-platform` không tồn tại.
- `/etc/kit-platform` chỉ có `api.env` và `secrets.generated`. Nội dung không được in.
- `find` dưới `/opt` và systemd chỉ thấy file migration SQL có chữ trading, không thấy binary collector.

Journal máy chỉ giữ boot hiện tại từ 2026-10-29 16:03 +07, cộng ba boot năm 2024. Log collector ngày 23–24/10 không còn trong journal hiện tại.

Inference: sau boot 29/10, collector `kit-trading-research` không được phục hồi. Các báo cáo ngày 24/10 mô tả timer BTCUSDT là hiện trạng cũ, không phải hiện trạng hôm nay.

Cổng 5432 không được mở theo ràng buộc của lần kiểm này. Việc database `trading_research` còn các nến BTCUSDT hay không là `INSUFFICIENT_EVIDENCE`. Điều đã chắc: không có collector nào đang ghi nến trên VPS lúc kiểm tra, và cụm 5433 không có database nào ngoài `postgres` và `second_brain_agent_v1`.

### 5.2 Dataset SOLUSDT trên máy trạm

Đây không phải dịch vụ VPS. Đây là file lab.

Observation, file `E:\Kit-Brain\second-brain-trading-source-inventory\small-capital-discovery-v1\output\snapshots\SOLUSDT.json`:

| Mục | Giá trị |
|---|---|
| Symbol / timeframe | `SOLUSDT` / `1h` |
| Nguồn ghi trong file | `BINANCE_PUBLIC_REST` |
| Quality | `PASS` |
| Số nến | 2 159. `record_count` = 2 159. Duplicate collapsed = 0 |
| Open đầu | 2026-07-23 10:00 UTC |
| Open cuối | 2026-10-21 08:00 UTC |
| Thời điểm thu | 2026-10-21 09:07 UTC |
| Funding | `FETCHED`, 270 bản ghi, `funding_error` rỗng |
| Bước 1 giờ | 0 bước lệch. 0 bước âm. Unique `open_time` = 2 159 |
| OHLC | 0 nến có high/low phá open/close |
| Checksum nến | `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3`, tính lại bằng cùng quy tắc `checksum_bars` và khớp trường trong file lẫn publication |
| Hash cả file | khác checksum nến. Publication dùng checksum nến, không dùng hash file thô |

`strategy.py` hiện tại có SHA-256 `d8ed62c644ac9d4c27d21513d1e9cf8a2c6b3f3323dea553526d164ae491c714`, khớp `code_sha256` trong publication.

### 5.3 Cache paper cục bộ, mới hơn publication

File `research-lab-v1\state\paper-ops\market-cache.json`, mtime 2026-11-01 23:04:03 +07:

- 271 nến, cùng cấu trúc OHLC.
- Open đầu 2026-10-21 09:00 UTC. Open cuối 2026-11-01 15:00 UTC.
- Unique open = 271. Số bước khác 1 giờ = 0.

Nến cuối của snapshot là 08:00 UTC ngày 21/10. Cache bắt đầu 09:00 UTC cùng ngày. Hai dải này tiếp nối nhau trên trục giờ. Cache không nằm trên VPS và không nằm trong publication.

Scheduled task `SecondBrainPaperOpsV1`, đọc 11:35 +07:

- State `Ready`, trigger bật, lặp `PT1H`, mốc gốc 2026-10-21 17:04.
- Last run 2026-11-02 11:12:50 +07, result `0x800710E0` (không phải 0).
- Next run lúc đọc: 2026-11-02 12:04 +07. Lần đó chưa xảy ra tại 11:35.
- `ops.log` không được ghi thêm. Dòng cuối vẫn là 2026-11-01 16:04:02 UTC.

Log có 153 dòng từ 2026-10-22 02:15 UTC đến 2026-11-01 16:04 UTC. Khoảng cách lịch khoảng 254 giờ, 31 khoảng im hơn 90 phút, có khoảng 10 giờ. Kết quả từng dòng: `SUCCEEDED` 153, `live=False` 153, `orders=0` 153, `gaps=0` 153, `api_key_used=False` 153. Outcome: `PAPER_SIMULATED` 148, `NO_NEW_DATA` 4, `NO_NEW_SIGNAL` 1. Chữ “error” trong log là trường `error=None`, không phải 153 lần thất bại.

Inference: task giờ vẫn được lên lịch, nhưng lần 11:12 hôm nay không nối cache. Đuôi dữ liệu dừng ở nến mở 2026-11-01 15:00 UTC. Từ nến đó đến lúc kiểm tra thiếu khoảng 12 giờ đã đóng. Phần lịch sử trong cache vẫn liên tục.

## 6. Database and Data Quality

Cụm 5433, database `second_brain_agent_v1`, `SELECT` lúc 11:29–11:32 +07. `inet_server_port` của hàm control không trả số vì kết nối qua socket; `ss` xác nhận process postgres PID 43039 nghe `127.0.0.1:5433`. System identifier `7702079316921501630`.

Schema: `public`, `second_brain_host`, `pack_personal_intelligence`. Extension: `plpgsql`, `pgcrypto`. Foreign server: 0. Subscription: 0. Role `sb_agent`: login, không superuser, không createdb. Grant của role này gồm SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER trên khoảng 37–38 bảng. Quyền ghi và truncate rộng hơn nhu cầu của một API chỉ đọc publication.

Không có bảng nến. `market_snapshot` = 0.

| Bảng | Dòng | Ý nghĩa đã kiểm |
|---|---:|---|
| `public.users` | 1 | `sb-operator` |
| `public.tenants` | 1 | `SECOND_BRAIN` |
| `public.permissions` | 2 | chỉ exchange connect/read |
| `public.platform_module_registry` | 1 | `personal_intelligence` |
| `second_brain_host.operator` | 1 | ADMIN + bốn quyền ở mục 4 |
| `second_brain_host.auth_session` | 4 | cả 4 đã revoke. 0 session còn hạn và chưa revoke. Tạo từ 2026-10-30 12:34 đến 15:58 +07 |
| `second_brain_host.refresh_token` | 12 | 9 revoked, 3 còn mở. Cả 3 còn mở có `session_id` null. Hết hạn trong ngày 2026-11-06 +07. 12 hash phân biệt |
| `pack_personal_intelligence.trade` | 3 | `FIXTURE` / `IMPORT` / `BUY`. Cùng `executed_at` 2026-10-01 07:00 +07. `created_at` từ 2026-10-29 20:23 đến 2026-10-30 10:00 +07 |
| `trading_order` | 2 | `status=paper`, `source=paper`, `submitted_at` cùng cửa sổ 29–30/10. Cột external id có giá trị. Không in id |
| `trading_execution` | 2 | không đọc chi tiết giá |
| `exchange_connection` | 0 | không có kết nối sàn |
| `exchange_backfill_job`, `exchange_execution_sync`, `exchange_account_snapshot`, `exchange_asset_balance` | 0 | |
| `pi_events` | 18 | sáu loại, mỗi loại 3 dòng, occurred 2026-10-29 20:23 đến 2026-10-30 10:00 +07 |
| `pi_decision` | 3 | `accepted` 2, `proposed` 1 |
| `pi_context`, `pi_evidence`, `pi_experience`, `pi_outcome`, `pi_operation` | 3 mỗi bảng | |
| `pi_task`, `pi_agent`, `pi_evaluation`, `pi_knowledge`, `pi_memory`, `pi_policy`, `pi_reflection`, `position`, `portfolio_snapshot`, `fee`, `funding_payment` | 0 | |
| `trading_account`, `person`, `exchange` | 1 mỗi bảng | bản ghi catalog/tài khoản, không phải dòng lệnh mới |

Dấu hiệu bất thường, observation:

- Ba lệnh fixture chia sẻ một `executed_at`. Đây là dữ liệu import, không phải dòng khớp liên tục.
- Ba refresh token còn mở mà không có session. Hết hạn 2026-11-06. Contract SQL của session security nói các dòng `session_id` null không được đổi lấy access token mới. Lượt này không thử refresh.
- Không có session đang dùng. Mọi gọi API nghiệp vụ lúc này đều 401 cho đến khi có login mới.
- Không có dòng ledger nào mới hơn 2026-10-30 10:00 +07.
- `sb_agent` có TRUNCATE. Đó là rủi ro vận hành, không phải dấu hiệu dữ liệu đã bị xóa trong lần này.

## 7. Research Publication Audit

Thư mục `/opt/second-brain-research-publication-v1`, mode 755, owner root. Mtime file 2026-10-30 15:04 +07. Không có `SHA256SUMS.txt` trên server. Năm file JSON có mặt.

Checksum tính lại lúc 11:27 +07, khớp từng trường manifest và khớp bản trong repo `deploy/second-brain-agent/research-publication-v1`:

| File | SHA-256 | Khớp manifest |
|---|---|---|
| `manifest.json` | `cf0661e9a64f2a40ded3202b9a1dca4621839a5be44b45b0aed57f969095c578` | chính file manifest |
| `lab/LAB_RESULT.json` | `8f19e6e010999637fe2b1b793106ea2c22b5def60e06fbb8b06dcb6267d5714b` | có |
| `paper/PAPER_OPS_STATUS.json` | `0cdecbb0e7b2a5f8430cd32d713aaf5be447096272a273cb72e62b0f32bc84b5` | có |
| `paper/PAPER_RESEARCH_REPORT.json` | `84b67af769b13ff13eae823f07446de682334f03fff2b7643a6069f2b83f549f` | có |
| `paper/PAPER_OPS_HEALTH.json` | `d4e557a3510047b127cd9ff1b714a63dd9030b115e0a25399fa27d26b4fab6ab` | có |

Danh tính: schema `research-publication/v1`, id `pub-e65b6912e02a168d`, publisher `research-lab-v1`, `publishedAt` `2026-10-30T04:30:00+00:00`.

Nội dung lab, không lấy làm hiệu suất:

- Symbol `SOLUSDT`, parent `EXP-SMA-CROSS-V1`, snapshot đúng hash ở mục 5.2.
- `snapshot_selection=FROZEN_DISCOVERY_SNAPSHOT_NOT_SELECTED_BY_PNL`
- `selected_strategy` null
- `approval_status=EXPERIMENT_NOT_APPROVED`
- `evidence_status=INSUFFICIENT_EVIDENCE`
- `paper_new_bars=0`, `paper_status=NO_NEW_CLOSED_BAR` trên lab
- `continuous_schedule=NOT_VERIFIED_OPERATOR_RUN_ONLY`
- `live_orders=false`, `exchange_orders_sent=0`
- Đúng ba experiment. Không có experiment thứ tư. `EXP-SMA-CROSS-V1` chỉ là parent, không phải một dòng thí nghiệm.

Paper status: `runner_status=SUCCEEDED`, `cycle_outcome=PAPER_SIMULATED`, `last_run_at=2026-10-30T04:04:02.086108+00:00`, `mode=ONCE`, `continuous_schedule=NOT_OBSERVED`, `data_gaps=[]`, `last_error` null. Phí ghi `ASSUMPTION_NOT_ACCOUNT_SCHEDULE` với rate giả định `0.0005`. Slippage ghi `ASSUMPTION` với rate giả định `0.0001`. Fill model `NEXT_OPEN_AND_COMPLETED_BAR_STOP`. `effective_live_mode=LIVE_DISABLED`.

Paper report: `PAPER_EXTENSION_NOT_A_SELECTION`, 211 bar, từ 2026-10-21 09:00 UTC đến 2026-10-30 03:00 UTC, `continuity_breaks=[]`. Ba strategy đều `simulated_entries=0`, `closed_trades=0`, `open_trades=0`, và các trường net, profit factor, max drawdown, expectancy đều là `NOT_REPORTED_SAMPLE_TOO_SMALL`.

Health file: `health=OK`, `last_bar_age_ms=242250` tại thời điểm ghi, không phải tại hôm nay.

Freshness. Observation: file không đổi từ 15:04 +07 ngày 30/10. Last run trong file cũ hơn lúc kiểm tra khoảng 72 giờ 25 phút. Nến mở cuối mà publication mô tả cũ hơn khoảng 73 giờ 30 phút. Inference từ `FileResearchPublicationReader`: giá trị `health=OK` được map thành availability `FRESH`. Reader không tính lại tuổi nến. `FRESH` ở API là nhãn trong file, không phải “dữ liệu còn mới lúc 02/11”. Cache máy trạm đã có nến đến 2026-11-01 15:00 UTC và publication không chứa các nến đó. Publication và nguồn mới hơn đã lệch nhau.

Các file nến của 211 bar extension không có trong thư mục publication. `PAPER_EXTENSION.json` trên máy trạm, mtime 2026-10-21, chỉ ghi `new_bars=0`. Chuỗi 211 bar là lời kể trong report, không được tính lại từ nến trong lần này.

## 8. Research Evaluation Audit

API `GET /api/personal-intelligence/research/evaluations` tồn tại và trả 401 khi không có token, 11:23 +07. Không có body đánh giá mới trong lần này.

Inference, vì DLL Application đang chạy chứa `publication_projection`, `NOT_SELECTABLE`, `NOT_IN_SOURCE`, `NO_EQUITY_CURVE`, và hash DLL khớp gói đã đọc code `ResearchEvaluationProjector`:

- Mode là `publication_projection`.
- Projector không tính lại lợi nhuận, drawdown hay equity.
- Verdict là `NOT_SELECTABLE` khi nguồn nói không phải performance claim, chưa duyệt, hoặc evidence chưa đủ. Cả ba experiment đều thỏa điều kiện đó.
- Confidence thiết kế là `LOW` khi có dòng.
- Năm limitation cố định: `NOT_A_PERFORMANCE_CLAIM`, `NOT_LIVE_TRADING`, `NO_EQUITY_CURVE`, `NO_RECOMPUTE`, `NOT_PAPER_LEDGER`.
- Sharpe, CAGR, maximum drawdown, expectancy và equity curve trong object metrics luôn là `NOT_IN_SOURCE`.
- Sample development và out-of-sample có thể mang trade count, win/loss, win rate, net và profit factor từ file, kèm cờ `win_rate_is_performance_evidence=false` và `profit_factor_is_performance_evidence=false`.

Các khóa không có trong lab JSON: sharpe, cagr, equity curve, danh sách lệnh. `events` bị lab bỏ trước khi ghi file. `observed` có trường max drawdown fraction và fee, nhưng máy chiếu đánh giá không dùng trường đó làm metric. Báo cáo này không chép các số tiền trong lab.

Truy xuất nguồn: publication id + bốn hash file + experiment id + mode tạo evaluation id. Bốn hash đã được tính lại và khớp. Snapshot hash đã được tính lại từ 2 159 nến trên máy trạm và khớp. Code strategy khớp. Điều chưa truy được từ VPS: chuỗi nến của 211 bar extension, và một response HTTP 200 của chính ngày 02/11.

Đây là chiếu publication, không phải một backtest mới chạy trên VPS hôm nay.

## 9. Trading Strategy Inventory

Đúng ba experiment trong publication. Không có experiment khác.

Cùng snapshot, cùng symbol `SOLUSDT`, cùng timeframe `1h`, cùng `data_source=BINANCE_PUBLIC_REST`, `strategy_version=1`, `selected_by_pnl=false`, `parameters_chosen_on_out_of_sample=false`, `live_orders=false`, `exchange_orders_sent=0`, `risk_limits_approval=PROPOSED_NOT_APPROVED`.

| Experiment | Family | Tuần có bucket trong file | Development trade count | Out-of-sample trade count | Journal dòng trên máy trạm |
|---|---|---|---:|---:|---:|
| `EXP-TREND-SMA-10-30-V1` | TREND | 2026-W30 … W43, 14 tuần | 25 | 17 | 271, mtime 2026-10-21 16:27 +07 |
| `EXP-BREAKOUT-DONCHIAN-20-V1` | BREAKOUT | 2026-W30 … W42, 13 tuần | 38 | 16 | 400 |
| `EXP-REVERSION-RANGE-20-V1` | MEAN_REVERSION | 2026-W30 … W42, 13 tuần | 35 | 14 | 350 |

Số lệnh ở bảng là kích thước mẫu trong file, kèm ghi chú `OBSERVED_SAMPLE_NOT_A_STABLE_EDGE`. Không phải bằng chứng lợi nhuận.

Cách chia mẫu, đọc `lab.py` trên máy trạm, không chạy lại backtest: 70% nến đầu là development, phần còn lại là out-of-sample theo thời gian. Với 2 159 nến, mốc cắt là index 1511, open 2026-09-24 09:00 UTC. Development 1 511 nến, out-of-sample 648 nến. Robustness đổi tham số lân cận chỉ trên cửa sổ development và bị đánh dấu không dùng để chọn. Parent SMA không nằm trong ba experiment.

Paper extension trong publication: cả ba strategy 0 lệnh mô phỏng, 0 lệnh đóng, 0 lệnh mở.

`sma-cross-v1` không có trong publication. Không gọi endpoint có token để xác nhận 404 hôm nay. Inference từ danh sách file: id đó không nằm trong ba experiment.

## 10. Trading Agent Capability Matrix

| Capability | Đã triển khai | Đã kiểm thử | Đang hoạt động | Có dữ liệu chứng minh | Chỉ có cấu trúc / API | Chưa triển khai |
|---|---|---|---|---|---|---|
| Market Data Collector | Có, hai kiểu: timer VPS cũ và task Windows SOLUSDT | Timer VPS được mô tả ngày 24/10. Task Windows có 153 dòng log | Task Windows còn lịch, lần 11:12 hôm nay không ghi log. Timer VPS không còn | Snapshot 2 159 nến và cache 271 nến trên máy trạm. VPS không giữ nến | | Collector VPS hiện không được cài |
| Strategy Research | Lab Python trên máy trạm. VPS chỉ đọc publication | Ba experiment đã được lab ghi ra file ngày 21–30/10 | VPS không tạo experiment mới. Không có worker | Ba file experiment trong publication | API liệt kê experiment, cần auth | Vòng tự sinh thí nghiệm |
| Backtesting | Engine `trading_agent/backtest.py` trên máy trạm | Publication là đầu ra đã đóng băng của engine đó | Không chạy trên VPS. Không chạy lại hôm nay | Snapshot khớp hash. Journal cục bộ còn. Events không có trong publication | | Backtest service trên VPS |
| Strategy Evaluation | Projector trong DLL đang chạy | Deploy 30/10 từng nhận HTTP 200. Hôm nay chỉ xác nhận 401 và hash DLL không đổi | Route sống, không có session để gọi | File nguồn còn nguyên checksum | Metrics Sharpe/CAGR/MDD/expectancy/equity bị khóa `NOT_IN_SOURCE` | Bộ chọn chiến lược |
| Out-of-sample Validation | Chia 70/30 theo thời gian trong `lab.py` | Cờ trong publication nói tham số không được chọn trên OOS | Không được tính lại hôm nay | Có block `out_of_sample` trong file | Một symbol, một lần cắt | Walk-forward nhiều cửa sổ, nhiều symbol |
| Paper Trading | Task Windows và một cycle `ONCE` đã ghi vào publication | 153 lần log `live=False`, `orders=0` | Task lỗi lúc 11:12. VPS không chạy paper | Cache đến 2026-11-01 15:00 UTC. Extension publication kết thúc 30/10 với 0 lệnh | API `paper-executions` tồn tại, 401 | Paper broker trên VPS |
| Risk Management | Gate trong lab, limit đề xuất, fill model giả định | Publication ghi `PROPOSED_NOT_APPROVED` và `no_trade_allowed=true` khi bị yêu cầu live | Không có risk service đang chặn lệnh thật, vì không có lệnh thật | 0 exchange connection, 0 lệnh sàn trong publication | | Hạn mức đã duyệt, đo drawdown vận hành |
| Agent Autonomy | Bảng `pi_agent`, `pi_task`, intent API | Không thấy lần chạy tự lập kế hoạch | `pi_agent=0`, `pi_task=0`, model tắt | | API intent trả 401 | Planner tự nghiên cứu |
| Learning Loop | Bảng evaluation, knowledge, memory, experience | Ba dòng experience cũ cùng cụm fixture 29–30/10 | `pi_evaluation=0`, `pi_knowledge=0`, `pi_memory=0` | Không có vòng cải thiện chiến lược | Schema có sẵn | |
| Live Trading | Không có literal đặt lệnh trong DLL host đang chạy | Không thử | `exchange_connection=0`, worker tắt, publication `LIVE_DISABLED`, log paper `api_key_used=False` | 0 lệnh sàn được publication ghi | API exchange tồn tại và yêu cầu auth. Operator không có `exchange.connect` trong mảng quyền | Kết nối tài khoản và gửi lệnh |

## 11. Security and Operational Safety

Observation:

- Agent và PostgreSQL 5433 chỉ nghe loopback.
- `agent.env` mode 600. Secret không được đưa vào báo cáo.
- Mọi API nghiệp vụ không token trả 401.
- Bốn session đều đã revoke. Ba refresh còn hạn không gắn session, hết hạn 2026-11-06.
- Bootstrap password vẫn nằm trong env. Không kiểm tra nó còn đăng nhập được hay không, vì đó là POST.
- `sb_agent` không phải superuser nhưng có DELETE và TRUNCATE trên các bảng ứng dụng.
- Catalog quyền và mảng quyền operator không trùng nhau.
- Không có foreign server hay subscription từ cụm 5433.
- Nginx không trỏ tới 5095 trong phần config đã grep.
- Cổng 5432 và `novixa_prod` không bị mở.
- Không có khóa LLM. Không có khóa sàn trong env của agent. Log paper ghi `api_key_used=False`.
- Literal `api.binance.com` có trong một DLL. Không có literal đường dẫn đặt lệnh. Không có connection row.

Việc không làm: không login, không refresh token, không gọi GET account của sàn, không bật worker, không sửa task Windows, không đợi lần chạy 12:04.

## 12. Git / WIP / Deployment Reconciliation

Đọc local lúc bắt đầu kiểm tra. Không reset, clean, stash, checkout hay commit.

| Mục | Giá trị |
|---|---|
| HEAD | `d0682f94f8a2b425560235975f934722695dad13` |
| Nhánh | `park-marketing/agent-dang-bai`, không có upstream |
| `origin/main` | `7505e0165351fde4ad05456b55fbac9aeb04ece6`, 2026-10-28, “Merge paper proposal steps onto pi_task” |
| Commit session `2c1ea353` | nằm trong HEAD. Không nằm trên remote nào (`git branch -r --contains` rỗng) |
| Commit evaluation `9d28227` | nằm trong HEAD và trong `second-brain/paper-proposal-v1`. Không nằm trên remote. Message: “Compare published research experiments without treating them as performance.” 2026-10-30 15:40 +07 |
| Diff `9d28227..HEAD` trên host, pack PersonalIntelligence, deploy second-brain | rỗng |
| Working tree | 1 031 dòng `git status --short`. Phần lớn là Content, Pharmacy, client. Không đụng trong lần này |
| WIP Second Brain chưa commit | `AgentAuth.cs`, `Program.cs`, `PaperTaskController.cs`, `PersonalIntelligenceAuthorizationExtensions.cs`, `TradingDeskController.cs` (269 dòng thêm, 69 dòng bớt), `BusinessIntentTests.cs`, `IsolatedHostTests.cs` |
| Chưa track | `deploy/second-brain-agent/001_auth_session.sql`, `deploy/second-brain-agent/research-publication-v1/`, `tests/KitPlatform.SecondBrainAgent.Tests/SessionSecurityTests.cs` |

Binary VPS khớp hash gói deploy ngày 30/10, không khớp một bản build lại từ working tree hôm nay. Các DLL cũ hơn còn nằm cạnh và không được process dùng. `5f29e1bd` là commit “Add the Infrastructure compile surface for IPermissionEngine”; thư mục `/opt/second-brain-agent-v1-5f29e1bd` còn DLL cũ, không chạy.

Publication trên VPS khớp năm file JSON của package local. Package local còn tài liệu và `SHA256SUMS.txt` mà thư mục VPS không có. Reader không cần các file tài liệu đó.

## 13. Confirmed Achievements

Những điều sau đang đúng tại lúc kiểm tra:

1. Host Second Brain sống, health tốt, DB đúng cụm 5433, worker tắt, không restart kể từ 30/10.
2. Session security nằm trong binary đang chạy. Không còn session hiệu lực.
3. Publication đúng id, đúng schema, đúng publisher, checksum bốn object khớp manifest.
4. Snapshot SOLUSDT 1 giờ, 2 159 nến liên tục từ 23/07 đến 21/10, checksum khớp publication, chất lượng OHLC sạch trong file.
5. Ba experiment đã được lab ghi nhận, có chia development / out-of-sample theo thời gian, và publication từ chối coi đó là bằng chứng hiệu suất.
6. Máy chiếu đánh giá đã nằm trong process đang phục vụ và được thiết kế để không biến publication thành một claim lợi nhuận.
7. Không có kết nối sàn và không có lệnh live trong dữ liệu đã đọc.
8. Cache paper cục bộ giữ thêm 271 nến giờ liên tục đến 15:00 UTC ngày 01/11.

## 14. Known Gaps and Risks

1. Collector VPS đã biến mất khỏi systemd và journal sau boot 29/10. Số nến BTCUSDT cũ không được đọc lại vì nằm ở phía cổng 5432, và cổng đó bị loại khỏi phạm vi.
2. Publication đứng ở 30/10 trong khi cache máy trạm đã tới 01/11. Nhãn `FRESH` không đo tuổi thật.
3. 211 nến extension trong report không kèm file nến. Không tính lại được continuity của riêng dải đó.
4. Đuôi cache dừng ở nến 15:00 UTC ngày 01/11. Task 11:12 hôm nay không ghi log.
5. Log task có 31 khoảng im hơn 90 phút trong 153 lần chạy. Cache lịch sử vẫn liền mạch, nên các lần im không đục lỗ phần đã được kéo lại. Đuôi hiện tại thì chưa được kéo.
6. Evaluation hôm nay không có response 200. Kết luận projector là suy từ DLL và source đã khóa hash, không phải một body mới.
7. Không có equity curve, không có Sharpe, không có CAGR trong engine đã đọc. Expectancy có trong kết quả engine nhưng không được đưa vào object publication mà API đánh giá dùng. Max drawdown có trong `observed` và bị projector bỏ qua.
8. Phí 5 bps và slippage 1 bp là giả định, không phải biểu phí tài khoản.
9. Ba lệnh DB là fixture cùng một giờ khớp. Không dùng chúng làm lịch sử giao dịch.
10. Ba refresh token mồ côi còn hạn đến 06/11. Role DB có TRUNCATE. Catalog quyền lệch với mảng quyền operator.
11. Commit đang chạy trên VPS chưa có trên remote. Working tree bẩn 1 031 đường. Publish từ cây này sẽ kéo WIP không thuộc evaluation.
12. `sb_agent` ghi được ledger. Một lỗi ứng dụng có thể sửa dữ liệu fixture. Lần này không thấy dòng mới sau 30/10.

## 15. Actual Research Value

Trả lời thẳng các câu hỏi dữ liệu.

1. Dữ liệu dùng được cho nghiên cứu nào? Cho backtest một symbol `SOLUSDT`, khung 1 giờ, từ 2026-07-23 đến 2026-10-21 trên snapshot đã khóa checksum, và cho phần nối đến 2026-11-01 15:00 UTC trên cache máy trạm. Không dùng được cho đa symbol, đa timeframe, hay cho BTC, vì collector BTC trên VPS không còn và database của nó không được đọc.

2. Đủ backtest chưa? Đủ cho một lần backtest 1 giờ của ba luật đã có, trên một cửa sổ khoảng 13 tuần. Chưa đủ để coi kết quả là ổn định. Publication tự ghi mẫu quan sát không phải lợi thế bền.

3. Đánh giá nhiều điều kiện thị trường chưa? Chưa. Chỉ một symbol, một khung, một lần cắt 70/30 vào 2026-09-24 09:00 UTC. Các bucket tuần là cách nhóm lệnh, không phải các chế độ thị trường được thử riêng.

4. Tính sau phí và slippage chưa? Engine lab có trừ phí và slippage theo tỷ lệ giả định 0.0005 và 0.0001, kiểu fill next-open. Đó không phải phí và trượt giá đo từ tài khoản Binance. Paper extension không phát sinh lệnh để áp các giả định đó.

5. Max drawdown, Sharpe, profit factor, expectancy? Sharpe và CAGR không có trong engine đã đọc. Expectancy được engine tính rồi không đưa vào bản publication mà màn đánh giá dùng. Profit factor và max drawdown có trong file lab dưới dạng quan sát, bị đánh dấu không phải bằng chứng, và API đánh giá cố ý trả `NOT_IN_SOURCE` cho drawdown, expectancy và equity. Chưa thể dùng các số đó để chọn chiến lược.

6. So sánh khách quan được chưa? So sánh được ở mức “cùng snapshot, cùng cách chiếu, cả ba đều không đủ điều kiện chọn”. Chưa so sánh được bằng một bảng hiệu suất đã kiểm lại.

7. Đủ tin để sang paper trading chưa? Chưa, nếu paper trading nghĩa là một chiến lược đã được chọn và chạy mô phỏng có kiểm soát. Một task paper đã chạy và cố ý không gửi lệnh. Extension đã công bố kết thúc bằng 0 lệnh và câu không được chọn. Task hôm nay còn không ghi được vòng mới.

8. Giữ gì, sửa gì? Giữ host Second Brain, cụm DB 5433, publication đã khóa checksum, snapshot SOLUSDT, engine lab và projector biết từ chối claim. Sửa collector đang đứt, đưa nến và danh sách lệnh vào nơi VPS đọc được, tính lại metric từ nến thay vì tin nhãn `FRESH`, và tách commit Second Brain khỏi working tree Content trước khi deploy tiếp.

## 16. Recommended Next Steps

Chỉ đề xuất. Lần này không thực hiện.

1. Giữ nguyên service VPS. Đừng publish từ working tree đang bẩn.
2. Đưa commit `9d28227` và phần session đã chạy lên một nhánh riêng, không trộn Content/Pharmacy, rồi mới xét deploy tiếp.
3. Quyết định số phận collector. Hoặc khôi phục thu nến có chủ đích trên một database không phải `novixa_prod` và không phải cổng đang cấm, hoặc ghi nhận pipeline VPS đã dừng và chỉ duy trì lab máy trạm.
4. Sửa task `SecondBrainPaperOpsV1` sau khi đọc vì sao lần 11:12 trả `0x800710E0`. Đừng bật live để “chữa” lỗi đó.
5. Khi collector ổn, xuất publication mới từ snapshot cộng cache, kèm file nến, rồi để evaluation đọc bản mới. Giữ rule không biến số liệu thành claim cho đến khi có đủ mẫu và một lần tính lại độc lập.
6. Thu hẹp quyền `sb_agent`, hết hạn hoặc thu hồi ba refresh không session, và chỉnh catalog quyền cho khớp operator. Việc đó cần một thay đổi được duyệt riêng.
7. Chưa nối sàn, chưa bật worker, chưa gửi lệnh.

## 17. Evidence Appendix

| Thời điểm | Kiểm tra | Kết quả |
|---|---|---|
| 11:23:12 +07 | `hostname`, `date -Is`, `systemctl show second-brain-agent` | Host đúng. PID 78259. Start 2026-10-30 15:58. NRestarts 0. WD bản `9d28227` |
| 11:23 +07 | `GET /api/health`, `GET /api/health/db` | HTTP 200. Worker false. DB `second_brain_agent_v1` |
| 11:23 +07 | GET không token các route research, trading, task, intent | 401. `GET /` 200 |
| 11:25 +07 | `sha256sum` bốn DLL và UI | Khớp gói deploy 30/10 |
| 11:25 +07 | Quét UTF-16 DLL | Có chuỗi evaluation và session. Không có `MayExposeUser` |
| 11:27 +07 | SHA-256 năm file publication | Khớp manifest |
| 11:27 +07 | `systemctl list-timers --all`, unit files, journal trading | Không còn collector VPS |
| 11:27 +07 | `pg_lsclusters` | 5432 main online, 5433 sbagent online. Chỉ truy vấn 5433 |
| 11:29 +07 | `pg_control_system` trên 5433 | Identifier `7702079316921501630`, khớp env |
| 11:29 +07 | `query_to_xml(count)` mọi bảng user | Không có nến. Ledger dừng 30/10. Xem mục 6 |
| 11:31–11:32 +07 | users, sessions, refresh, trades, orders, events | 0 session sống. 3 refresh không session. 3 trade fixture |
| 11:32:43 +07 | `operator.roles`, `operator.permissions` | ADMIN và bốn quyền personal intelligence |
| Local, cùng buổi | `checksum_bars` trên `SOLUSDT.json` | Khớp `126a733b…`. 2 159 nến, 0 gap, 0 OHLC hỏng |
| Local | `market-cache.json`, `ops.log` | 271 nến đến 2026-11-01 15:00 UTC. 153 lần SUCCEEDED. Lần cuối 16:04 UTC ngày 01/11 |
| 11:35 +07 | `Get-ScheduledTaskInfo SecondBrainPaperOpsV1` | Last 11:12:50 +07, result `0x800710E0`. Log không đổi |

Phân biệt đã dùng trong báo cáo:

- Observation: lệnh, file, checksum, count SQL, log.
- Inference: hành vi HTTP 200 của evaluation nếu có token, vì không tạo token. Hành vi `FRESH` lấy từ code reader cộng file health. Việc ba refresh không session không đổi được access token lấy từ contract đã cài, không từ một lần thử.

Không in mật khẩu, JWT, refresh token, hash token, email, hay chuỗi connection đầy đủ.
