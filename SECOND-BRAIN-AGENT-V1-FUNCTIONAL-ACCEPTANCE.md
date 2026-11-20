# SECOND BRAIN AGENT V1 — FUNCTIONAL ACCEPTANCE

Thời điểm: 2026-10-30 10:00:41–10:00:44 +07. VPS B `157.10.53.182`. Không restart, không đổi systemd/env/secret, không migration, không đụng VPS A, nginx, `kit-platform-api` hay PostgreSQL port 5432.

## Kết luận

`PARTIALLY_ACCEPTED`

Đăng nhập, trạng thái agent, paper proposal và ranh giới live/sàn đã chạy trên service đang phục vụ. Vòng đời nhiệm vụ không tạo được bản ghi: API nhiệm vụ chỉ đọc, còn đường tạo qua intent dừng vì chưa cấu hình mô hình ngôn ngữ.

## 1. Runtime snapshot

| Mục | Kết quả |
|---|---|
| Service | `second-brain-agent` active |
| PID | 62086, `NRestarts=0` |
| Binary | `/opt/second-brain-agent-v1-5f29e1bd/host/KitPlatform.SecondBrainAgent.Host.dll` |
| SHA-256 | `336ca77d5d376f9661b8d51448f626965e1e3e93ab8f35c1e2683d0f5a810263` |
| Listen | `127.0.0.1:5095`. Port 5096 không listen |
| Postgres / API / nginx | PID 43039 / 37553 / 34942, không đổi |
| Socket của PID 62086 sau bài test | một kết nối `127.0.0.1` tới port 5433 |
| Socket tới 5432 hoặc `103.200.23.229` | không có |

`/api/health` HTTP 200: `status=ok`, service `second-brain-agent`, database `second_brain_agent_v1`, `workerEnabled=false`, `backfillWorkerRegistered=false`. Hosted service chỉ có DataProtection và GenericWebHost.

`/api/health/db` HTTP 200: `status=ok`, database `second_brain_agent_v1`, `value=1`, `workerEnabled=false`. Endpoint này từ chối khi port hoặc system identifier lệch, nên HTTP 200 xác nhận đúng cluster port 5433.

## 2. API và contract đã kiểm tra

Nguồn là commit `5f29e1bdf184364ad646e6cd4e622cdb0d0b3278`.

| API | Contract |
|---|---|
| `POST /api/auth/login` | `username`, `password`, `tenantCode`. Operator có sẵn: `sb-operator`, tenant `SECOND_BRAIN` |
| `GET /api/auth/me` | Hồ sơ operator, cần bearer |
| `POST /api/auth/logout` | Thu hồi refresh token |
| `GET /api/system/tenant-platform` | Module đang bật |
| `GET /api/personal-intelligence/trading/summary` | Cần quyền trading run |
| `POST /api/personal-intelligence/trading/loops` | Paper fixture. `live=true` trả 400 trước khi ghi |
| `POST /api/personal-intelligence/trading/decisions/{id}/paper` | Ghi execution `mode=PAPER` |
| `GET /api/personal-intelligence/tasks` | Chỉ đọc. Không có POST tạo nhiệm vụ |
| `POST /api/personal-intelligence/intent` | Tạo nhiệm vụ chỉ khi mô hình trả kế hoạch thực thi được |
| `GET /api/personal-intelligence/intent/status` | `modelConfigured`, publication |
| `GET /api/personal-intelligence/trading/exchanges` | Danh sách kết nối đã lưu. Không gọi sàn |
| `GET .../exchanges/catalog` và `.../backfill` | Catalog tĩnh và job đã lưu |

Không gọi `POST .../exchanges`, `POST .../exchanges/test`, backfill start, hay execution sync.

## 3. Test cases

| ID | Case | Kết quả |
|---|---|---|
| A1 | Service, PID, hash, health, DB health | PASS |
| A2 | Summary không token | PASS, HTTP 401 |
| A3 | Login operator hiện có | PASS, HTTP 200. Password không xuất hiện trong phản hồi |
| A4 | `GET /api/auth/me` | PASS. Role `ADMIN`. Bốn quyền personal intelligence |
| A5 | Logout | PASS, HTTP 204. Token của lượt này đã `revoked_at` |
| B1 | Worker và backfill worker | PASS, cả hai `false` |
| B2 | Module tenant | PASS. Chỉ `personal_intelligence` |
| B3 | Summary, accounts, exchange list, backfill list | PASS. 0 kết nối sàn, 0 job backfill |
| C1 | `GET /api/personal-intelligence/tasks` | PASS. Danh sách rỗng, khớp `pi_task=0` |
| C2 | Tạo nhiệm vụ qua intent | NOT_SUPPORTED. HTTP 200, `outcome=unavailable`, `pi_task` vẫn 0 |
| D1 | Tạo paper proposal `ACCEPTANCE-20261030-paper` | PASS, HTTP 200. `paperExecuted=false` trước khi duyệt |
| D2 | Gửi lại cùng payload | PASS. Cùng `decisionId` |
| D3 | Đổi thesis cùng operation key | PASS, HTTP 409 |
| D4 | `live=true` | PASS, HTTP 400 `Live execution is not available.` Không có dòng `pi_operation` |
| D5 | Duyệt paper | PASS, HTTP 200, `mode=PAPER`, `alreadyRecorded=false` |
| D6 | Đọc lại decision và database | PASS. Decision `accepted`, order `paper`, execution `PAPER` |
| E1 | Sự kiện của lượt paper | PASS. Sáu event, `actor_type=HUMAN`, source `operational-loop` |
| F1 | Không socket 5432, không `novixa_prod`, không VPS A | PASS |
| F2 | `exchange_connection` trước và sau | PASS, vẫn 0 |
| F3 | Execution mode khác `PAPER` và `OBSERVED` | PASS, vẫn 0 |
| R1 | Research publication và paper status | NOT_SUPPORTED. `availability=MISSING` vì publication root trống |

## 4. Bằng chứng

Login user `22222222-2222-4222-8222-222222222222`, tenant `11111111-1111-4111-8111-111111111111`, tenant code `SECOND_BRAIN`. Tài khoản dùng cho paper là `Paper fixture` / `paper-fixture`, không tạo account mới.

Paper:

- `loopId` `060aaf44-b3ea-4c56-9212-a907ef6ade65`
- `decisionId` `97e4b995-ee71-48c3-9664-5b8d07277243`
- `executionId` `c7705c8f-a04d-49bd-9b2a-e6071710c88d`
- Thesis `ACCEPTANCE-20261030 paper proposal fixture`
- Order: `paper|BTCUSDT|BUY|0.100000000000`
- Database: `accepted|paper|PAPER`
- `pi_operation` lúc `2026-10-30 10:00:43.433792+07`
- Outcome API: «Đã ghi giả lập. Không gửi lệnh lên sàn.»
- Unknowns: fixture không phải thị trường live, không có export sàn

Đếm trước → sau: `exchange_connection` 0 → 0, `pi_task` 0 → 0, order `paper` 1 → 2, execution `PAPER` 1 → 2, execution live 0 → 0. Một order paper đã có từ trước `2026-10-29` và không bị sửa.

Sự kiện 10:00:43 +07: `ContextObserved`, `EvidenceRecorded`, `DecisionMade`, `ActionTaken`, `OutcomeObserved`, `ExperienceDerived`. Không có bảng audit riêng ngoài `pi_events` và `refresh_token`. Không sửa hay xóa log.

Refresh token của lượt này: 1 revoked. Còn 3 token mở của cùng user từ trước; lượt này không thu hồi chúng và không in hash.

## 5. Nghiệp vụ đã chạy

- Health và health database trên DB `second_brain_agent_v1` port 5433.
- Từ chối request không token.
- Login, đọc hồ sơ, logout operator có sẵn.
- Đọc summary, account paper, catalog sàn, danh sách kết nối rỗng, backfill rỗng.
- Tạo, replay, chặn đổi payload, và duyệt một paper proposal fixture.
- Từ chối live trước khi ghi database.
- Ghi và đọc lại order/execution/event trong database riêng.

## 6. Chưa triển khai hoặc chưa xác minh

- Tạo và theo dõi nhiệm vụ. `PaperTaskController` chỉ có GET. `POST /intent` không gọi handler và không ghi `pi_task` khi `modelConfigured=false`.
- Research publication. Root trống, reader trả `MISSING`.
- Đổi tenant trong body intent hoặc loop không được gọi trong lượt này. Source có nhánh 403, nhưng chưa có bằng chứng runtime.
- Hạn dùng của 3 refresh token đang mở không được đọc lại sau khi lệnh đếm hạn bị dừng. Chỉ biết chúng chưa `revoked_at`.

## 7. Lỗi

Không có lỗi HTTP của các đường đã hỗ trợ. Khoảng trống là cấu hình và contract, không phải exception của binary đang chạy.

Nguyên nhân nhiệm vụ không phát sinh: `CatalogGroundedIntentInterpreter` trả `Unavailable` khi mô hình chưa cấu hình, và session không ghi task ở nhánh đó.

## 8. Thứ tự khắc phục đề xuất

1. Giữ nguyên service hiện tại. Không bật worker và không thêm kết nối sàn để làm cho nghiệm thu xanh.
2. Nếu vòng đời nhiệm vụ là điều kiện nghiệm thu, cấu hình mô hình trong phạm vi đã duyệt, hoặc bổ sung contract tạo task không phụ thuộc mô hình ở một release sau. Không thêm đoán từ khóa: source cố ý không làm việc đó.
3. Research chỉ đọc được sau khi có publication root đã duyệt. Thiếu root hiện trả `MISSING` đúng contract.
4. Rà 3 refresh token chưa thu hồi của `sb-operator`. Thu hồi nếu không còn phiên cần giữ. Không làm trong lượt này vì đó là hủy phiên có sẵn.

Bản ghi `ACCEPTANCE-20261030-paper` được giữ lại để truy vết. Không xóa.
