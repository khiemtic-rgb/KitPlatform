# Second Brain — nghiệm thu đồng bộ và E2E local

Nhiệm vụ: `SECOND-BRAIN-PLATFORM-SYNC-AND-LOCAL-E2E-ACCEPTANCE-001`  
Ngày: 2026-10-22  
Phạm vi: kiểm tra Git và runtime local. Không deploy, không push, không commit, không migration.

Ba kết luận độc lập:

| Kết luận | Giá trị |
|---|---|
| `GIT_SYNC_STATUS` | Mới ở local. Chưa commit. Chưa có bằng chứng đã push. |
| `LOCAL_E2E_STATUS` | `PASS` |
| `DEPLOY_STATUS` | `NO-GO` |

## A. Git / đồng bộ

### Repository

| Mục | Giá trị |
|---|---|
| Root | `E:/KitPlatform` |
| Branch | `recovery/production-source-20261021` |
| HEAD | `8c59ceb4f3be537898106ec058ea547406f722e5` |
| HEAD message | `fix(content): treat a video without a file as failed` (2026-10-16) |
| Local `main` | cùng commit `8c59ceb4` |
| Upstream của branch hiện tại | không có |
| `origin/main` đang cache local | `084373fa67b19e0f478da20b5b053de8ba17852e` (`chore(famixa): update stats snapshot`, 2026-10-17) |
| So với cache đó | 152 commit sau, 6 commit trước. Không fetch, không pull. |

`git ls-files` không có `src/Packs/PersonalIntelligence`, `tests/KitPlatform.PersonalIntelligence.Tests`, `client/admin/src/modules/second-brain`. `git grep HEAD` không thấy `ResearchPublicationController`. Không có commit nào chứa controller này.

### Phân loại từng nhóm

| Nhóm | Trạng thái | Bằng chứng |
|---|---|---|
| Backend Personal Intelligence: options, policy, DI, DTO, `FileResearchPublicationReader`, controller, test | `WORKTREE_PRESENT`, `NOT_COMMITTED`, `NOT_PUSHED_OR_UNVERIFIED` | Cả thư mục `src/Packs/PersonalIntelligence/` và `tests/KitPlatform.PersonalIntelligence.Tests/` là untracked (`??`). |
| Bind cấu hình trong host | `WORKTREE_PRESENT`, `NOT_COMMITTED` | `src/KitPlatform.Api/Program.cs` là tracked và modified, chưa staged. Diff này còn chứa thay đổi host của việc khác; không tách riêng trong nhiệm vụ này. |
| Publisher `publish_research_v1.py` và test | `WORKTREE_PRESENT` trên đĩa, `MISSING_FROM_EXPECTED_REPOSITORY` | Nằm ở `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1`. Thư mục đó không phải git repo. Không có trong KitPlatform. |
| UI: `research-view.ts`, `research.api.ts`, `ResearchPanel.tsx`, `TradingPages.tsx`, `operator-notices.ts` | `WORKTREE_PRESENT`, `NOT_COMMITTED`, `NOT_PUSHED_OR_UNVERIFIED` | Cả `client/admin/src/modules/second-brain/` là untracked. |
| Smoke, tài liệu thiết kế và implementation | `WORKTREE_PRESENT`, `NOT_COMMITTED` | Cùng thư mục untracked trên. |

Không có nhóm nào đạt `COMMITTED_LOCAL` hoặc `PUSHED_CONFIRMED`. Không tuyên bố đã đồng bộ remote: không fetch, và mã Research chưa có commit để đối chiếu.

`PUSHED_CONFIRMED` không dùng được. Phần chưa đồng bộ là toàn bộ mã Research API, publisher, UI, smoke và tài liệu của hai nhiệm vụ implementation trước.

## B. Runtime / API

### Nguyên nhân HTTP 404

Process API trước khi khởi động lại là PID `27152`, `dotnet.exe`, lệnh:

`E:\KitPlatform\.dev\sb-trading-ui\api\KitPlatform.Api.dll`

DLL host lúc đó ghi lúc 2026-10-22 09:52:58, trước controller. `GET http://127.0.0.1:58903/api/personal-intelligence/research/publication` trả 404. Cùng path qua Vite `http://127.0.0.1:5174/api/...` cũng 404. Proxy `/api` của `vite.second-brain.config.ts` trỏ đúng API local; 404 không phải trang SPA của Vite.

Sau khi build lại đúng thư mục output đó, request ẩn danh trên cả hai địa chỉ trả 401. Route đã được nạp. Không phải sai route template.

Nguyên nhân: runtime đang dùng build cũ.

### Process sau khi xác minh

Chỉ dừng PID `27152` sau khi đối chiếu command line với marker `.dev\sb-trading-ui\api\KitPlatform.Api.dll`. Không chạy script start đầy đủ, vì script đó đổi cổng, đổi JWT và ghi database validation. Không đụng PostgreSQL.

| Process | PID | Executable | Port |
|---|---|---|---|
| API Second Brain local | `25288` | `dotnet.exe`, DLL `E:\KitPlatform\.dev\sb-trading-ui\api\KitPlatform.Api.dll` | `127.0.0.1:58903` |
| UI Vite | `20260` | `node.exe`, `vite.second-brain.config.ts` | `127.0.0.1:5174` |
| PostgreSQL | `16208` | `postgres.exe` | `0.0.0.0:5432` và `[::]:5432`, không đụng |

Pack DLL `KitPlatform.Packs.PersonalIntelligence.Api.dll` sau build (11:56) chứa `personal-intelligence/research` và `ResearchPublicationController`.

Root publication chỉ đặt bằng biến môi trường của process này:

`PersonalIntelligence__ResearchPublication__Root=E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1\output\publication`

Không ghi vào `appsettings` hay file cấu hình dùng chung. Manifest đã có sẵn trước khi trỏ root. Python đối chiếu hash: `manifest_ok True`. Không tạo dữ liệu giả. `pids.json` chỉ cập nhật `apiPid` thành `25288`; UI PID giữ `20260`.

### Năm endpoint

Phiên `pi-ui-approve`, tenant `PI_A`. Quyền `personal_intelligence.trading.run` bật; policy Research cho phép quyền này. Không in mật khẩu hay token.

| Endpoint | HTTP | Kết quả |
|---|---|---|
| `GET /api/personal-intelligence/research/publication` | 200 | `FRESH`, schema `research-publication/v1`, id `pub-2685c4437898bc17`, `publishedAt` `2026-10-22T04:17:38.851734+00:00`. Bốn hash khớp manifest. Lab object `PRESENT_UNTIMED`. |
| `GET .../experiments` | 200 | `FRESH`. ID thật: `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, `EXP-REVERSION-RANGE-20-V1`. |
| `GET .../experiments/EXP-TREND-SMA-10-30-V1` | 200 | `FRESH`. `EXPERIMENT_NOT_APPROVED`, `INSUFFICIENT_EVIDENCE`, `NOT_A_PERFORMANCE_CLAIM`. |
| `GET .../paper/status` | 200 | `FRESH`. Health `OK`, runner `SUCCEEDED`, cycle `PAPER_SIMULATED`, `lastRunAt` `2026-10-22T04:04:02.073622+00:00`, live orders `false`. `FRESH` ở đây chỉ là health nguồn `OK`. |
| `GET .../paper/report` | 200 | `FRESH`. `PAPER_EXTENSION_NOT_A_SELECTION`, `NOT_A_PERFORMANCE_CLAIM`, `sampleWarning` `INSUFFICIENT_SAMPLE_DO_NOT_SELECT` trên cả ba chiến lược. |

Hash manifest:

- lab `8f19e6e010999637fe2b1b793106ea2c22b5def60e06fbb8b06dcb6267d5714b`
- status `d30dbec52887987b35312db1fc0294d4b510459ba2927823838c3df07b501ff0`
- report `03016968bc6cece235252434428029050cb7d309b6ce6fc9c86d0ce709bdaf58`
- health `a6ac13e4af955d62b45129875d6c7999fee39bd1cd895390f1d32f4e547b1462`

Không thấy path, secret, API key, journal, cache. Report nguồn có giá trị `inSampleOutOfSample` là `SEE_FROZEN_LAB_RESULT_NOT_THIS_EXTENSION`. Đây là chữ của nguồn, không phải field path `frozen_lab_result`. UI lọc mọi chuỗi chứa `frozen_lab_result`, nên cột đó hiện “Chưa ghi nhận”. Không sửa reader hay UI trong nhiệm vụ này.

Trading summary cùng phiên: HTTP 200, `290 / 289 / 1 / 0 / 236 / 138 / 29`. Không cộng số liệu research.

## C. UI / tests

### UI

`http://127.0.0.1:5174/second-brain/overview`, phiên `pi-ui-approve`.

Khối “Research Lab / Paper Ops” đọc API: publication `pub-2685c4437898bc17`, `FRESH` với chú thích health nguồn, lab `PRESENT_UNTIMED`, ba experiment ID ở trên, Paper Ops `SUCCEEDED` / `PAPER_SIMULATED` / health `OK`, báo cáo `PAPER_EXTENSION_NOT_A_SELECTION`.

Drawer “Xem” của `EXP-TREND-SMA-10-30-V1` giữ `EXPERIMENT_NOT_APPROVED`, `INSUFFICIENT_EVIDENCE`, `NOT_A_PERFORMANCE_CLAIM`, và ghi win rate / profit factor không phải bằng chứng hiệu suất, không phải khuyến nghị mua/bán. Bảng report giữ `INSUFFICIENT_SAMPLE_DO_NOT_SELECT` và `NOT_REPORTED_SAMPLE_TOO_SMALL`.

Không có nút chạy experiment, phê duyệt hay gửi lệnh. Sổ giao dịch vẫn `290 / 289 / 1 / 0 / 236 / 138 / 29`.

Trạng thái lỗi đã thấy trên cùng trang trước khi đổi process: HTTP 404 vì build cũ. Sau khi đổi, request ẩn danh qua proxy trả 401. Các trạng thái `MISSING`, `STALE`, `UNREADABLE`, `INCONSISTENT`, `UNSUPPORTED_VERSION`, 403 và lỗi mạng nằm trong smoke `research-view.smoke.ts`, đã PASS. Không tái hiện thủ công từng trạng thái đó trên trình duyệt.

### Kiểm thử

| Lệnh | Kết quả |
|---|---|
| `python -m unittest tests.test_publish_research_v1` | `PASS` — 5 tests |
| `python -m unittest tests.test_research_lab_v1 tests.test_paper_ops_v1 tests.test_publish_research_v1` | `PASS` — 14 tests |
| `dotnet test` filter `ResearchPublicationTests\|SecondBrainV1Tests` | `PASS` — 20 passed, 0 failed |
| `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` | `PASS` — 0 errors |
| Build vào `.dev/sb-trading-ui/api` | `PASS` — 0 errors |
| `npx tsx src/modules/second-brain/research-view.smoke.ts` | `PASS` |
| `npx tsx src/modules/second-brain/trading-routes.smoke.ts` | `PASS` |
| `npm run build` trong `client/admin` | `PASS` — exit 0 |

`NOT_RUN`: test Personal Intelligence có ghi database validation (`PostgresIntegrationTests`, `RuntimeHttpTests`, `OperationalDeliveryTests`, các test exchange/backfill). Không chạy migration. Không tuyên bố toàn bộ regression xanh.

Không sửa file nguồn trong nhiệm vụ này. File runtime đã ghi: `.dev/sb-trading-ui/pids.json` và log của process API mới.

## D. Việc còn lại

1. Commit và push chưa làm. Cần một commit riêng cho Research API, UI, smoke, tài liệu, và phần bind trong `Program.cs`, tách khỏi các thay đổi working tree khác. Publisher nằm ngoài repo KitPlatform; cần quyết định repo nào giữ nó.
2. Đối chiếu remote chỉ sau khi được yêu cầu fetch. Cache `origin/main` hiện không chứa mã này.
3. Deploy tiếp tục `NO-GO`.
