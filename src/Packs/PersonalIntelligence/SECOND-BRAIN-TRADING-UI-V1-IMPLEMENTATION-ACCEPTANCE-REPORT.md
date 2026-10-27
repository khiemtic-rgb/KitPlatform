# SECOND-BRAIN-TRADING-UI-V1 — IMPLEMENTATION & ACCEPTANCE REPORT

Ngày ghi: 2026-10-17. Trading Module V1 chưa hoàn thành. PASS chỉ gắn với kiểm thử đã chạy.

## 1. Route, trang và API

FACT. Giao diện nằm trong `client/admin`, không phải một frontend mới.

- Sidebar: module `secondBrain`, hiện khi tenant bật `personal_intelligence`.
- `/second-brain/overview` — tổng quan.
- `/second-brain/ledger` — sổ giao dịch và chi tiết.
- `/second-brain/import` — xem trước và ghi CSV.
- `/second-brain/journal` — danh sách và chi tiết quyết định.
- `/second-brain/agent` — chạy phân tích, retry cùng OperationKey, phê duyệt paper.
- `/second-brain/risk` — risk đã lưu và paper execution.

Không có nút live.

## 2. Endpoint, DTO và repository

FACT. Controller mới `TradingDeskController`, route `api/personal-intelligence/trading`. Tenant lấy từ danh tính. Policy `personal_intelligence.trading.run` và module gate áp cho mọi request đọc/ghi. Đọc qua `ITradingDesk` / `TradingDeskStore`.

- `GET summary`, `accounts`, `trades`, `trades/{id}`, `decisions`, `decisions/{id}`, `operations`, `operations/{operationKey}`, `paper-executions`.
- `POST imports/preview` và `POST imports`. CSV tối đa 1 MB. Parser là `HistoricalTradeImport`. Nguồn chỉ `FIXTURE` hoặc `IMPORT`. Trùng theo `(trading_account_id, external_trade_id)`, không ghi đè.
- `POST decisions/{decisionId}/paper` yêu cầu thêm `personal_intelligence.trading.approve`.

FACT. `POST loops` vẫn là vòng phân tích. Nếu body có `ApprovedBy`, server bỏ chuỗi client gửi và chỉ ghi user id khi danh tính có quyền approve. Không có quyền đó thì HTTP 403 và không tạo paper. Phân tích không gửi `ApprovedBy` vẫn tạo decision, không tạo execution. Paper sau đó đi qua endpoint riêng, và chỉ khi `RiskAllowed` đã lưu là true.

## 3. Luồng đã chạy

FACT. Qua Kestrel trên `127.0.0.1`, database `kit_pi_validation`:

- Không token: `GET summary` → 401.
- Có quyền: summary trả `tradeCount` từ PostgreSQL.
- Phân tích không phê duyệt: `paperExecuted = false`.
- User chỉ có quyền run, gọi paper → 403.
- User có quyền approve: paper trả `mode = PAPER`. PostgreSQL có một execution của đúng `decision_id` và tenant.

FACT. Bộ operational TestServer vẫn qua: cùng OperationKey trả kết quả cũ; payload khác → 409; risk veto không có execution; live → 400 và không có decision. Các bài đó nay gửi thêm quyền approve khi body còn cờ phê duyệt, vì server không còn nhận chuỗi `ApprovedBy` của client làm căn cứ.

FACT. Smoke `trading-routes.smoke.ts` xác nhận sáu route và mã module trong source. Passed.

NOT VERIFIED. Thao tác chuột trên trình duyệt. Proxy Vite mặc định trỏ `http://localhost:5290`, là process pid 11404. Process đó không được restart và không chứa bản build này. Tài khoản validation không có mật khẩu đăng nhập thật, nên không đăng nhập UI bằng tài khoản đó.

## 4. Build và test

FACT. `dotnet build` API ra thư mục riêng: 0 Error. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests` với `kit_pi_validation` cổng 5433: Passed 35, Failed 0, Skipped 0. Process kiểm thử và thư mục output đã được dọn. pid 11404 vẫn chạy.

FACT. `npx tsc -b --pretty false --noEmit` trong `client/admin` thất bại vì lỗi có sẵn ở procurement, sales và content. Log không có file `second-brain`. Không sửa các lỗi đó.

## 5. Dữ liệu trên màn hình

FACT. Số trên tổng quan là đếm dòng `trade`, `pi_decision`, `trading_execution` và cờ `RiskAllowed` trong `pi_operation`. Không có PnL, số dư hay tỷ lệ thắng. Nếu mọi giao dịch có `source = FIXTURE`, API trả `onlyFixtureData = true` và UI hiện nhãn kiểm thử. Import không biến trade thành decision.

## 6. Đăng nhập, quyền, module, tenant, approval

FACT. Backend: 401 khi không token; 403 khi thiếu quyền paper; module gate vẫn là `RequirePersonalIntelligenceModule`. Query có `tenant_id` từ `ITenantContext`. UI ẩn nút paper khi user không có quyền approve, và chặn trang khi module tắt hoặc thiếu quyền run. Việc chặn UI chưa được bấm trên trình duyệt.

## 7. Chưa hỗ trợ

Chưa có nguồn sàn thật. `pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot`, `funding_payment` vẫn không có repository. Rule text của policy không gắn khóa với decision trong schema, nên chi tiết quyết định hiện trạng thái policy từ operation và ghi “chưa ghi nhận” cho rule. Không có live trading.

## 8. Git

FACT. Branch `main`, HEAD `8c59ceb4f3be537898106ec058ea547406f722e5`. Không stage, commit, reset, clean, stash. Hash `ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`. Không sửa `vite.config.ts`, nên proxy 5290 của công việc đang chạy giữ nguyên.

## 9. Cách mở trong môi trường phát triển

1. Giữ process API hiện tại trên cổng 5290.
2. Build API ra một thư mục khác và chạy process đó chỉ trên `127.0.0.1`, cổng trống, connection tới `127.0.0.1:5433` / `kit_pi_validation`. JWT để ở biến môi trường của process đó.
3. Trỏ dev server admin tới cổng của process mới. Lệnh mặc định `npm run dev` trong `client/admin` vẫn proxy sang `localhost:5290`.
4. Đăng nhập bằng tài khoản tenant đã bật `personal_intelligence` và có quyền `personal_intelligence.trading.run`.
5. Mở `http://127.0.0.1:5173/second-brain/overview`.
