# SECOND-BRAIN-TRADING-UI-V1 — LOCAL ACCESS REPORT

Ngày ghi: 2026-10-17. PASS chỉ gắn với kiểm thử đã chạy. Không có live trading.

## Khởi chạy và dừng

FACT. Script không đổi `vite.config.ts`, không đổi `client/admin/.env.development`, không đổi connection string mặc định của API, và không đụng process pid 11404.

- Bật: `powershell -NoProfile -ExecutionPolicy Bypass -File E:\KitPlatform\scripts\second-brain-trading-ui-start.ps1`
- Tắt: `powershell -NoProfile -ExecutionPolicy Bypass -File E:\KitPlatform\scripts\second-brain-trading-ui-stop.ps1`

Script start build API ra `E:\KitPlatform\.dev\sb-trading-ui\api`, chọn cổng loopback còn trống cho API, chạy admin bằng `vite.second-brain.config.ts` trên `127.0.0.1:5174` nếu cổng đó trống. Proxy của phiên này trỏ tới API validation. Proxy mặc định `5290` của `vite.config.ts` không đổi. JWT signing key được tạo trong bộ nhớ của process và issuer là `PiValidationLocal`. Log: `.dev\sb-trading-ui\api.log`, `api.err.log`, `ui.log`, `ui.err.log`.

Phiên đang chạy lúc ghi báo cáo:

- Giao diện: `http://127.0.0.1:5174/login`
- Trading: `http://127.0.0.1:5174/second-brain/overview`
- API validation: `http://127.0.0.1:59461`
- PID API validation: 6712. PID UI: 22912. PID 11404 vẫn là `KitPlatform.Api`.

Lần start sau có thể chọn cổng API khác. Đọc dòng `UI:` do script in ra.

## Đăng nhập

FACT. Login dùng `POST /api/auth/login` hiện có, mật khẩu bcrypt trên `kit_pi_validation`, không có endpoint bypass.

Ba tài khoản dùng chung một mật khẩu. Mật khẩu nằm trong file bị Git bỏ qua: `E:\KitPlatform\.dev\sb-trading-ui\local-access.txt`. `git check-ignore` trả về đúng path đó. File không được stage.

| Tenant | Username | Quyền |
| --- | --- | --- |
| `PI_A` | `pi-ui-approve` | `personal_intelligence.trading.run` và `personal_intelligence.trading.approve`. Không có ADMIN. |
| `PI_A` | `pi-ui-run` | chỉ `personal_intelligence.trading.run` |
| `PI_B` | `pi-ui-b` | chỉ `personal_intelligence.trading.run`. Module `personal_intelligence` không bật. |

Trên trang đăng nhập chọn **Mã cửa hàng**, nhập tenant, username và mật khẩu trong file trên.

## Màn hình đã mở trên trình duyệt

FACT. Trình duyệt mở `http://127.0.0.1:5174`. Login form gửi tới `/api/auth/login` của phiên 5174 và nhận 200. Header hiện tenant và user.

- `PI_A` / `pi-ui-approve`: sidebar có **Second Brain**. Sáu mục Tổng quan, Sổ giao dịch, Nhập dữ liệu, Nhật ký quyết định, Phân tích, Rủi ro & Paper đều mở được.
- Tổng quan: 150 giao dịch, 150 FIXTURE, nguồn khác 0, nhãn “Chỉ có dữ liệu kiểm thử FIXTURE. Chưa có dữ liệu sàn thực.” Không có PnL.
- Sổ giao dịch: bảng từ PostgreSQL, phí thiếu hiện “Chưa ghi nhận”, dòng import hiện phí. Chi tiết ghi “Có — đây không phải một quyết định”.
- Import: CSV sai cột báo lỗi cột bắt buộc. CSV fixture hợp lệ xem trước “Hợp lệ 1 · Trùng 0”, xác nhận “Đã ghi 1 dòng”.
- Nhật ký: chi tiết có HYPOTHESIS, bối cảnh OBSERVATION, bằng chứng, hành động, kết quả, reflection, policy `proposed`, experience “Chưa ghi nhận”.
- Phân tích: chạy một operation fixture, cơ sở Fixture, risk cho phép, paper chưa phê duyệt. Retry giữ OperationKey `045476ebdbb04952814aae71c53edc3f`. PostgreSQL có đúng 1 dòng `pi_operation` cho key đó. Hộp thoại phê duyệt hiện và sau khi xác nhận màn hình hiện PAPER.
- Rủi ro: có dòng “Từ chối”, dòng “Cho phép”, và bảng paper mode `PAPER`. Không có nút live.
- `pi-ui-run`: đăng nhập được, chỉ role `PI_TRADING`, không có nút phê duyệt paper và không có nút live.
- `pi-ui-b`: sidebar không còn Second Brain. Mở thẳng `/second-brain/overview` hiện module chưa được bật.

## API đã gọi trên process validation

FACT. Cùng process `127.0.0.1:59461`, database `kit_pi_validation`:

- Không token: summary 401.
- Preview CSV sai: 400. Preview hợp lệ: 1 dòng. Commit: inserted 1. Commit lại: duplicate 1.
- Cùng OperationKey và cùng payload: cùng `decisionId`. Payload khác: 409. Phân tích không tạo paper.
- `pi-ui-run` gọi paper: 403. `pi-ui-approve` gọi paper: mode `PAPER`.
- Notional vượt trần: `riskAllowed = false`, paper 400.
- `live = true`: 400.
- `requestedTenantId` của tenant khác: 403.
- `pi-ui-b`: summary 403.

## Build và test

FACT. `dotnet build` API ra `.dev\sb-trading-ui\api`: 0 Error. `dotnet test` Personal Intelligence, loại `RuntimeHttpTests` vì bộ đó cần output folder khác: Passed 32, Failed 0. Vite dev trên 5174 phục vụ được các màn hình. `npx tsc -b` toàn admin vẫn thất bại từ trước bởi procurement, sales và content; không chạy lại trong nhiệm vụ này và không sửa các lỗi đó.

## Dữ liệu

FACT. Số trên tổng quan đếm từ `kit_pi_validation`. Một dòng cũ có `source = fixture` viết thường được tính là FIXTURE. Không có nguồn sàn thật. Import mới mang nhãn FIXTURE. Không có kết quả thị trường live.

## Sửa trong phạm vi để màn hình đọc được dữ liệu

- `TradeRowDto`, `TradingAccountDto`, `PaperExecutionRowDto` đổi sang class có property để Dapper đọc được `timestamptz`. Record constructor trước đó làm sổ giao dịch lỗi.
- Đếm và lọc nguồn FIXTURE không phân biệt hoa thường.
- Hộp thoại phê duyệt paper dùng `App.useApp().modal` của Ant Design. `Modal.confirm` tĩnh không hiện dialog trong app này.

## Git

FACT. HEAD `8c59ceb4f3be537898106ec058ea547406f722e5`. Không stage, commit, reset, clean, stash. Hash `ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`. `vite.config.ts` và `.env.development` không đổi.

## Cách mở

1. Giữ API cổng 5290.
2. Nếu `http://127.0.0.1:5174/login` còn mở thì dùng luôn. Nếu không, chạy script start và đọc URL nó in.
3. Chọn **Mã cửa hàng**. Tenant `PI_A`, username `pi-ui-approve`, mật khẩu trong `.dev\sb-trading-ui\local-access.txt`.
4. Mở **Second Brain**, hoặc vào `/second-brain/overview`.
