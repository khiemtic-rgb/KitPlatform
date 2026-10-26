# Second Brain Trading — kiểm kê giao diện

Ngày: 2026-10-22. Phạm vi là module `personal_intelligence` trong repository này. Không deploy, không đụng VPS, không gửi lệnh sàn.

Đăng nhập dùng màn login của admin. Second Brain không có tài khoản riêng. Vào `/second-brain` chuyển tới `/second-brain/session`. API yêu cầu policy `personal_intelligence.trading.run`, `trading.approve`, `exchange.read` hoặc `exchange.connect`. Ẩn nút trên giao diện không thay cho các policy đó.

## Bảng màn hình

| Màn | Route | Việc của người dùng | Frontend | API | Nguồn | Đang chạy | Còn thiếu |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Phiên làm việc | `/second-brain/session` | Nối sàn, chọn sổ, ghi nhận định, paper | Có bước tiếp theo từ dữ liệu thật | summary, accounts, trades, exchanges, loops | `pack_personal_intelligence` | Có, nếu API và quyền đủ | Không hiện ba thí nghiệm research lab |
| Tình hình | `/second-brain/overview` | Thấy số đã lưu và việc tiếp theo | Đọc summary, kể cả khi số giao dịch bằng 0 | `GET .../trading/summary` | Đếm trade, decision, paper, operation | Có | Không có giờ thu thập research lab |
| Sổ | `/second-brain/ledger` | Lọc và xem fill | Bảng và drawer | trades, accounts | `trade` | Có | Không phải research ledger |
| Spot / Futures | `/second-brain/spot`, `/futures` | Xem thị trường đã lưu | Màn riêng | trades theo loại | `trade` | Có route | Không phải biểu đồ nghiên cứu |
| Nhập | `/second-brain/import` | Xem trước và ghi CSV | Form, báo số hợp lệ và trùng | imports/preview, imports | `trade` | Có | Không tạo decision |
| Nhật ký | `/second-brain/journal` | Xem nhận định đã ghi | Bảng và chi tiết | decisions | `pi_decision` | Có | Không phải journal paper ops của research lab |
| Phân tích | `/second-brain/agent` | Gửi nhận định do người dùng nhập | Form, `live: false` | `POST .../loops` | Người dùng và trade đã lưu | Có | Không so sánh out-of-sample, không xếp hạng |
| Rủi ro và paper | `/second-brain/risk` | Xem lần risk và paper đã lưu | Hai bảng | operations, paper-executions | `pi_operation`, `trading_execution` | Có | Không có PnL nghiên cứu |
| Kết nối sàn | `/second-brain/exchange` | Nối key chỉ đọc | Form, không hiện lại secret | exchanges | `exchange_connection` | Có | Không có nút live |
| Tài khoản sàn | `/second-brain/account` | Xem snapshot số dư | Có khi đã nối | exchanges/{id}/account | snapshot sàn | Có | Không suy PnL |

## Việc chưa có trong module này

Ba thí nghiệm `EXP-TREND-SMA-10-30-V1`, `EXP-BREAKOUT-DONCHIAN-20-V1`, `EXP-REVERSION-RANGE-20-V1` và health paper ops nằm ở kho nghiên cứu riêng, không có API trong `TradingDeskController`. Nối chúng vào admin là thay đổi kiến trúc. Đợt này không làm. Màn Tình hình ghi blocker đó thay vì bịa số.

Không có màn Settings riêng. Trần notional, đòn bẩy và tin cậy là ô trên phiên làm việc, gửi kèm từng lần phân tích, không phải chính sách đã phê duyệt.

## Rủi ro

- Số trên Tình hình là đếm bản ghi. Dữ liệu kiểm thử được gắn cảnh báo. Mẫu paper dưới 30 được ghi là chưa đủ để kết luận.
- `DemoPaperExecution` và `RiskEngine` từ chối mode Live.
- Vòng 003: cụm riêng `E:\kit-pi-validation-pg` nghe `127.0.0.1:5433`, database `kit_pi_validation`. Đăng nhập `pi-ui-approve` mở được session và overview. Số overview khớp summary. `pi-ui-run` bị 403 khi phê duyệt paper. `pi-ui-b` bị 403 vì module tắt. Lúc vòng 003, build admin còn 332 lỗi ngoài module này. Research lab vẫn không có API đọc.
- Sau vòng 003, `npm run build` của admin thoát 0 và cả hai project TypeScript thoát 0. Chặn 332 lỗi đã hết. Kết luận phát hành vẫn NO-GO vì research lab chưa có hợp đồng đọc.
