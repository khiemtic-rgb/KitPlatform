# Second Brain V1 — operational state

Cập nhật: 2026-10-18, sau đồng bộ lịch sử khớp lệnh Bybit.

- Lối vào: `http://127.0.0.1:5174/second-brain` mở `/second-brain/session`. Start/stop: `scripts/second-brain-trading-ui-start.ps1` và `scripts/second-brain-trading-ui-stop.ps1`. Proxy mặc định 5290 không đổi. Phiên này không thấy process trên cổng 5290 và không khởi động lại process đó.
- Phiên làm việc gồm dải trạng thái và sáu bước trên một trang: đọc tài khoản sàn, chọn sổ, chọn giao dịch đã lưu, xác nhận nhận định, bằng chứng và rủi ro, giả lập cùng nhật ký của phiên đó.
- Tab còn lại: sổ, nhật ký, rủi ro và giả lập, kết nối sàn. Các route overview, import, agent, account vẫn mở được nhưng không còn là tám tab ngang hàng.
- Đọc/ghi sổ: `api/personal-intelligence/trading`. `POST .../loops` nhận `tradeIds` để dùng giao dịch đã lưu. CSV fixture vẫn dùng được. Import không tạo Decision. Đồng bộ sàn cũng không tạo Decision, Policy hoặc Paper Execution.
- Nguồn giao dịch: `FIXTURE`, `IMPORT`, `EXCHANGE`. CSV chỉ ghi `FIXTURE` hoặc `IMPORT`. Execution Bybit ghi cả `source` và `source_type` là `EXCHANGE`. Migration `392_pack_personal_intelligence_exchange_execution_sync_v1.sql` đã áp trên `kit_pi_validation`. Định danh dòng sàn là `BYBIT:{ENV}:{category}:{execId}`. Ghi lặp dùng `ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING`.
- Kết nối sàn: `api/personal-intelligence/trading/exchanges`. Bybit V5 chỉ đọc, host `https://api.bybit.com` và `https://api-testnet.bybit.com`. Số dư Unified khi sàn hỗ trợ. Lịch sử khớp lệnh: `GET /v5/execution/list`, xem trước `POST .../executions/preview`, ghi `POST .../executions`, lần gần nhất `GET .../executions/latest`. Category `spot`, `linear`, `inverse`, `option`. Một lần gọi sàn tối đa 7 ngày. Một yêu cầu ứng dụng tối đa 31 ngày và 10 trang (1000 execution). Không có lệnh live.
- Database kiểm thử hiện không có kết nối Bybit và không có trade `EXCHANGE`. Đồng bộ bằng tài khoản sàn thật chưa được xác minh.
- Paper: `POST .../decisions/{id}/paper`, quyền `personal_intelligence.trading.approve`. Sau khi ghi, kết quả operation và outcome được cập nhật để nhật ký hiện đúng phiên. Trading Agent vẫn `recommend_only`.
- Module `personal_intelligence`. Tenant `PI_A` bật. Tenant `PI_B` không bật. Database kiểm thử: `127.0.0.1:5433` / `kit_pi_validation`.
- Idempotency vòng lặp: `OperationKey` do hệ thống giữ, không hiện trên phiên làm việc. Cùng khóa và cùng payload trả lại quyết định cũ. Payload khác trả 409.
- API kiểm thử Second Brain đang chạy: pid 20104, `http://127.0.0.1:56101`. UI pid 9104, cổng 5174. PostgreSQL kiểm thử pid 9264, cổng 5433.
- Chưa có repository cho `pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot`, `funding_payment`. Funding từ execution list không được ghi vào sổ.
- Báo cáo phiên này: `SECOND-BRAIN-V1-BYBIT-HISTORICAL-SYNC-EXECUTION-REPORT.md`.
