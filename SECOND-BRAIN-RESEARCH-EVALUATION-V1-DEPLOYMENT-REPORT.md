# SECOND BRAIN — RESEARCH EVALUATION V1 DEPLOYMENT

Trạng thái: **DEPLOYMENT_COMPLETE_WITH_GAPS**

Agent trên VPS B đang chạy Research Evaluation V1. Ba experiment được so sánh từ publication `pub-e65b6912e02a168d`, cùng snapshot, verdict `NOT_SELECTABLE`. Khoảng trống còn lại là chưa đăng nhập trên trình duyệt để chụp bảng, và chưa có principal chỉ mang `trading.run` để gọi live 403.

## Source

Commit runtime của evaluation: `9d28227b4db504ea4b36d80fae30a146ed20eead`

Parent: `2c1ea353c58fcb1cf74d996bade25df28f34f8af`

Evaluation không gọi API mới của Infrastructure hay Auth đang sửa dở. Host vẫn biên dịch `KitPlatform.Infrastructure`, nên publish từ working tree sẽ kéo WIP đó vào DLL. Bản phát hành được `git archive` từ đúng commit, rồi chỉ phủ năm file session security đã chạy trước đó:

- `AgentAuth.cs`
- `Program.cs`
- `PersonalIntelligenceAuthorizationExtensions.cs`
- `PaperTaskController.cs`
- `TradingDeskController.cs`

Năm file này vẫn chưa commit và vẫn còn nguyên trong working tree. `AuthService.cs` trong cây publish khớp blob HEAD `8eaae2827c4e392d9ee0f333e66fea5d049d3d1c`, khác bản đang sửa. DLL đã publish không chứa `MayExposeUser` hay `AllowsSameTenant`. DLL host có `second_brain_host.auth_session` và `Session is required.`

Không reset, clean, stash, checkout, amend hay commit thêm.

## Package

Publish Release từ cây tách, lúc kiểm tra trước khi copy lên server:

| File | SHA-256 |
|---|---|
| `KitPlatform.SecondBrainAgent.Host.dll` | `f8d87d61d4fd0abea12a08a58dbc1d43fb90f4e9f3df6c801f665ec01c375f3f` |
| `KitPlatform.Packs.PersonalIntelligence.Api.dll` | `9a9188d992a95d9eff63a08fd8fb42cdcc5a9bf538a68e09b6afb151bbe4e94d` |
| `KitPlatform.Packs.PersonalIntelligence.Application.dll` | `ebff1843e3cfb3103af26f9a1df41c28159b0f93b502558aa681201cc37b6fd2` |
| `KitPlatform.Infrastructure.dll` | `ae757eab4a880900083ca20e95dcdde2a9c3f1e5627c45a285839477f636967a` |
| `appsettings.json` | `23a3c08109507c2d8a8a58c7a60a57b4ac7a4418a606b50cf0821b7c74d1d10f` |
| UI `index.html` | `e8c8c387c1c8b15d6b8aa3cc5ea77348dc1951428768a506e372f46b0b058a76` |
| UI `assets/index-CAMJg88Q.js` | `8a5f5f08e418dfb01752fc207bd1613c2e56b402a0aa18aff67b53dc1392c07d` |
| UI `assets/index-BdOndhxL.css` | `98adf782bd027847cfeac99bc2f2735177375ea17bb836601ce1d7290ca699c5` |

`sha256sum -c` trên server: 8/8 OK. `appsettings.json` không có secret. Root publication trong file để trống; process vẫn lấy root từ env.

## Triển khai

Thời điểm process mới: 2026-10-30 15:58:00 +07.

| Mục | Giá trị |
|---|---|
| PID | 78259, NRestarts 0 |
| Working directory | `/opt/second-brain-agent-v1-9d28227/host` |
| DLL đang dùng | `f8d87d61d4fd0abea12a08a58dbc1d43fb90f4e9f3df6c801f665ec01c375f3f` |
| Health | HTTP 200, worker false, backfill false |
| DB health | HTTP 200, database `second_brain_agent_v1` |
| Hosted services | DataProtection và GenericWebHost |
| Web root | `/opt/second-brain-agent/ui` |
| Publication root | `/opt/second-brain-research-publication-v1` |
| Worker env | `false` |

Chỉ restart `second-brain-agent`. `agent.env` không đổi. Không migration.

DLL session cũ còn tại `/opt/second-brain-agent-v1-session-security/host`, hash `ff37afc7489f65d501ac0d132a5137ce45043724a91e0f29bf19294c17adeabc`. Drop-in và `index.html` trước khi đổi nằm ở `/var/backups/second-brain-agent-dropin-before-eval-9d28227.conf` và `/var/backups/second-brain-ui-index.before-eval-9d28227.html`.

Peer không đổi PID: `kit-platform-api` 37553, nginx 34942, `postgresql@16-sbagent` 43039, `postgresql@16-main` 34958. `users` vẫn là 1. Manifest publication vẫn `cf0661e9a64f2a40ded3202b9a1dca4621839a5be44b45b0aed57f969095c578`.

## API

Đăng nhập `sb-operator` / `SECOND_BRAIN`: HTTP 200. JWT có `sid`, không có claim `role`. Quyền trong token: `trading.run`, `trading.approve`, `research.read`, `exchange.read`. Profile DB vẫn có role `ADMIN`; token không mang role đó.

`GET /api/personal-intelligence/research/evaluations` không token: 401. Có token: 200.

| Trường | Kết quả |
|---|---|
| availability | `FRESH` |
| publication | `pub-e65b6912e02a168d` |
| sameSnapshot | true |
| snapshot | `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3` |
| verdict | `NOT_SELECTABLE` |
| confidence | `LOW` |
| mode | `publication_projection` |
| limitations | `NOT_A_PERFORMANCE_CLAIM`, `NOT_LIVE_TRADING`, `NO_EQUITY_CURVE`, `NO_RECOMPUTE`, `NOT_PAPER_LEDGER` |

Ba dòng, cùng snapshot, `liveOrders=false`, `exchangeOrdersSent=0`, mọi metric `NOT_IN_SOURCE`:

- `EXP-TREND-SMA-10-30-V1`
- `EXP-BREAKOUT-DONCHIAN-20-V1`
- `EXP-REVERSION-RANGE-20-V1`

`EXP-TREND-SMA-10-30-V1` riêng: 200, `NOT_SELECTABLE`. `sma-cross-v1`: 404. Body không có `sma-cross-v1` và không có `api_key`.

`POST /api/personal-intelligence/trading/exchanges` với body rỗng: 403. Logout: 204.

## UI

Agent phục vụ UI mới: `GET /` HTTP 200, script `index-CAMJg88Q.js`. File đó có `So sánh evaluation`, `Đây là chiếu publication`, `NOT_A_PERFORMANCE_CLAIM` và `Không có trong nguồn`. Trình duyệt qua tunnel tới `127.0.0.1:5095` mở được trang đăng nhập Second Brain.

Chưa đăng nhập trên trình duyệt nên chưa chụp bảng sau login. Payload mà bảng đọc đã được kiểm ở API ở trên. Chuỗi `NOT_IN_SOURCE` không nằm trong JS; màn hình dùng câu `Không có trong nguồn. Không được tính.`

## An toàn

Trước và sau smoke, số dòng không đổi:

- `exchange_connection` 0
- `trade` 3
- `pi_decision` 3
- `pi_task` 0
- `pi_events` 18
- refresh legacy còn mở, `session_id` null: 3
- `auth_session` đang active sau logout: 0

Journal của service không có `RiskEngine`, `PaperDecision`, `ExchangeBackfill`. Env không có khóa model. Không đụng cổng 5432, `novixa_prod`, VPS A, hay `kit-platform-api`.

Operator live có cả `research.read` lẫn `trading.run`, nên lượt gọi 200 không phải bài kiểm tra `trading.run` một mình. Policy trong file đã phủ chỉ nhận `research.read` cho evaluation, và đó là file mà test local đã chặn `trading.run` với 403. Không tạo user mới để thử lại trên server.

## WIP được giữ

Working tree vẫn ở commit `9d28227`. Năm file session ở trên vẫn modified, chưa commit. `AuthService.cs`, `AuthContracts.cs`, `DashboardService.cs` và phần WIP Content, Pharmacy, client khác không được đưa vào package và không bị xóa.

## Việc còn lại

Đăng nhập overview trên UI đang phục vụ để nhìn ba dòng và snapshot trên màn hình. Muốn chứng minh live 403 của `trading.run` thì cần một principal không có `research.read`; không tạo principal đó trong lượt này.
