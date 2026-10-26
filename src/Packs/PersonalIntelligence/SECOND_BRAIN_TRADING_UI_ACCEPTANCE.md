# Second Brain Trading — nghiệm thu giao diện

Ngày: 2026-10-22, cập nhật sau vòng 003. Kết luận: **NO-GO**.

## Đã xác minh

- Cụm kiểm thử là thư mục riêng `E:\kit-pi-validation-pg`. Lần chạy trước ghi trong `postmaster.opts`: chỉ nghe `127.0.0.1`, cổng 5433. `pg_hba.conf` chỉ trust localhost. Trên cổng này chỉ thấy database `kit_pi_validation` trong nhóm tên production đã hỏi. Không phải cụm `C:\novixa-rr001-lab\pgdata`.
- Script UI bắt cổng 5433 vì connection string và câu SQL đều từ chối khi `current_database()` khác `kit_pi_validation` hoặc `inet_server_port()` khác 5433.
- Đã khởi động đúng cụm đó bằng `pg_ctl -D E:\kit-pi-validation-pg` với `-c listen_addresses=127.0.0.1 -c port=5433`. Không sửa `postgresql.conf`. Dịch vụ `postgresql-x64-18` vẫn nghe 5432, process 16208. Cổng 5433 là process 28104.
- Script `scripts/second-brain-trading-ui-start.ps1` chạy xong. API local `http://127.0.0.1:58903`. UI `http://127.0.0.1:5174`. Script ghi lại mật khẩu tài khoản kiểm thử trên `kit_pi_validation`.
- Đăng nhập form bằng tenant `PI_A` và user `pi-ui-approve` vào được admin, hiện "Xin chào, pi-ui-approve". Chế độ Email Kit với email kiểm thử trả 401 vì form gửi email vào trường username.
- `/second-brain/session` mở được, sổ đang chọn là dữ liệu kiểm thử, chưa có kết nối sàn.
- `/second-brain/overview` hiện 290 giao dịch đã ghi, 289 dữ liệu kiểm thử, 1 CSV, 0 đồng bộ từ sàn, 236 nhận định, 138 giả lập, 29 risk từ chối. Cùng các số đó từ `GET /api/personal-intelligence/trading/summary` với phiên vừa đăng nhập. Nguồn gần nhất `FIXTURE`, `2026-10-18T04:26:35.440611+00:00`. `onlyFixtureData` là false vì có 1 dòng CSV. Hai dòng dữ liệu thiếu trên màn khớp `missingData`.
- Màn không ở trạng thái trống vì `tradeCount` là 290. Nhánh số 0 vẫn được smoke kiểm: có notice chưa có giao dịch và research lab chưa nối.
- Hai màn đã tải không hiện lỗi API. Summary trả 200.
- `pi-ui-run` đọc summary trả 200. `POST /api/personal-intelligence/trading/decisions/00000000-0000-0000-0000-000000000001/paper` trả 403 trước khi vào hành động phê duyệt. `pi-ui-b` đọc summary trả 403, message "Module is not enabled for this tenant."
- Không gửi lệnh sàn. Không dùng credential production.

## Test theo quy trình repo

- `npm run build` trong `client/admin`: mã thoát 2. `tsc --noEmit` báo 332 lỗi. Vite không chạy. Cùng số lỗi với vòng 002 trong ngày. 0 lỗi trong `second-brain`.
- Phân nhóm: `modules/content` 324, procurement 3, sales 2, `shared/api` 2, inventory 1. File nhiều nhất là `ContentFamixaSeriesTab.tsx` với 154 lỗi, mở đầu bằng biến không dùng (`TS6133`). Nhóm mã: `TS18048` 76, `TS2345` 66, `TS2580` 60, `TS6133` 47, `TS2322` 29, `TS2339` 20, `TS2554` 10, `TS2307` 8, còn 16 lỗi mã khác.
- `TS2580` là `process` (50) và `Buffer` (10). `TS2307` gồm `node:crypto`, `node:fs`, `node:path` trong script content, cộng `@/shared/display/number` và `./api-client`. Đó là script Node và import gãy đang bị `tsc` của app admin kiểm, không phải lỗi phát sinh của Second Brain.
- Quy trình chính thức: `client/admin/package.json` script `build` là `tsc --noEmit && vite build`. Workflow `admin-typecheck.yml` chạy `npm ci` rồi `npx tsc --noEmit` trên Node 22. Không tắt kiểm tra này.
- `npx tsx src/modules/second-brain/trading-routes.smoke.ts`: mã thoát 0, in `trading routes ok`.
- `dotnet test tests/KitPlatform.PersonalIntelligence.Tests --filter FullyQualifiedName~SecondBrainV1Tests`: Passed 6, Failed 0. Cảnh báo `NU1903` Newtonsoft.Json 10.0.3 và `CS4014` vẫn có từ trước, không sửa trong vòng này.
- Bộ integration cần `KIT_PI_TEST_CONNECTION` chưa chạy lại, để không ghi thêm vào database vừa dùng cho đối chiếu màn hình.
- Knowledge System, Research Lifecycle, Research Validation, Risk Feasibility, Research Lab: **NOT_RUN**. Không sửa các pipeline đó.

## Research Lab và Paper Ops

Không có controller Personal Intelligence nào đọc research lab hoặc paper ops. Dữ liệu nằm ở file trong kho nghiên cứu: `research-lab-v1/output/LAB_RESULT.json`, `PAPER_OPS_STATUS.json`, `PAPER_RESEARCH_REPORT.json`, và `state/paper-ops`. Frontend không được đọc đường dẫn máy. Vòng này không thêm adapter, endpoint hay migration.

Điều kiện cho một nhiệm vụ sau: API đọc thuộc pack Personal Intelligence, policy quyền rõ, mỗi bản ghi có nguồn và mốc thời gian, nơi lưu không phải đường dẫn đĩa máy dev. Khi chưa có hợp đồng đó, màn Tình hình tiếp tục nói ba thí nghiệm research lab không phải nguồn của summary.

Không có API lưu cấu hình nghiên cứu. Không thêm trang Settings trống.

## Đã đổi và chỉ đã kiểm

Đổi: khởi động cụm kiểm thử local đã có, chạy script UI nên mật khẩu ba user kiểm thử trên `kit_pi_validation` được ghi lại, cập nhật ba hồ sơ này.

Không đổi: mã nguồn admin, Famixa, cấu hình PostgreSQL, dịch vụ cổng 5432, VPS, migration, journal, snapshot, research ledger, API key sàn.

## Blocker trước GO

1. Làm `npm run build` thoát 0. Phạm vi là Famixa/content và các file sales, procurement, inventory, shared API trong log build. Không sửa hàng loạt trong nhiệm vụ Second Brain.
2. Research lab giữ trạng thái chưa nối cho đến khi có nhiệm vụ hợp đồng đọc riêng.

## Bằng chứng sau mục 1

`npm run build` trong `client/admin` đã thoát 0. `npx tsc --noEmit` thoát 0. `npx tsc -p tsconfig.scripts.json --noEmit` thoát 0. Vite đã chạy. 0 lỗi TypeScript, kể cả `second-brain`. Mục 1 ở trên là trạng thái vòng 003, không viết lại. Kết luận nghiệm thu vẫn **NO-GO** vì mục 2 còn.
