# Second Brain Trading — sẵn sàng triển khai

Ngày: 2026-10-22, vòng 003. **Không triển khai.** Trạng thái: NO-GO.

Đăng nhập local đã chạy trên cụm kiểm thử `kit_pi_validation` tại `127.0.0.1:5433`. Màn Phiên làm việc và Tình hình khớp `GET /api/personal-intelligence/trading/summary`. User thiếu quyền bị backend trả 403. Build admin `npm run build` vẫn thoát 2 vì 332 lỗi TypeScript ngoài Second Brain. Research lab vẫn chưa có hợp đồng đọc trong API.

## Biến môi trường

Admin dùng API của Kit Platform đã cấu hình sẵn cho tenant. Module `personal_intelligence` phải bật. Không thêm API key giao dịch vào frontend. Key sàn, nếu có, chỉ nhập trên màn Kết nối sàn và không được trả lại nguyên văn.

Research lab paper ops không thuộc module này. Biến `PAPER_OPS_STATE_DIR` của kho nghiên cứu không làm màn admin chạy.

## Phụ thuộc

Frontend: admin Vite hiện có. Không thêm framework. Backend: pack Personal Intelligence hiện có. Live order vẫn bị `RiskEngine` và `DemoPaperExecution` từ chối.

## Đã kiểm trên máy local, chưa đưa lên VPS

1. Smoke route thoát 0. `SecondBrainV1Tests` Passed 6, Failed 0. Bộ integration PostgreSQL của pack chưa chạy lại trong vòng này.
2. Đăng nhập `pi-ui-approve` trên tenant `PI_A`, mở `/second-brain/session` và `/second-brain/overview`. Số trên Tình hình khớp summary: 290 giao dịch, 289 kiểm thử, 1 CSV, 0 từ sàn, 236 nhận định, 138 giả lập, 29 risk từ chối.
3. `pi-ui-run` đọc được summary (200) và bị 403 khi gọi phê duyệt paper. `pi-ui-b` bị 403 vì module tắt.

## Còn chặn phát hành

`client/admin` script `build` là `tsc --noEmit && vite build`. Workflow `.github/workflows/admin-typecheck.yml` cũng chạy `npx tsc --noEmit`. Cả hai đều dừng ở 332 lỗi hiện có, 0 lỗi trong `second-brain`. Chủ phạm vi sửa là Famixa/content và các file sales, procurement, inventory, shared API nằm trong danh sách lỗi. Không tắt kiểm tra TypeScript để phát hành.

## Bằng chứng build sau vòng 003

`npm run build` trong `client/admin` thoát 0. `npx tsc --noEmit` và `npx tsc -p tsconfig.scripts.json --noEmit` đều thoát 0. 332 lỗi TypeScript đã về 0. Kết luận phát hành vẫn **NO-GO**: research lab vẫn chưa có hợp đồng đọc trong API. Không triển khai.

## Rollback

Giao diện Second Brain không đổi trong vòng 003. Không có migration. Không có đổi schema. Journal, snapshot và research ledger không bị đụng. Cụm `E:\kit-pi-validation-pg` đang nghe `127.0.0.1:5433`. Dừng UI bằng `scripts/second-brain-trading-ui-stop.ps1`. Dịch vụ PostgreSQL cổng 5432 không bị đổi.
