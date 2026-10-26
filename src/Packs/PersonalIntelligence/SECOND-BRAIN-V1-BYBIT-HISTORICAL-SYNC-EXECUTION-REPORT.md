# SECOND-BRAIN-V1 — Bybit historical sync

Ngày: 2026-10-18. Không commit. Không stage.

Đồng bộ lịch sử khớp lệnh Bybit đã được nối vào Phiên làm việc Second Brain. Kiểm thử fixture và PostgreSQL đã chạy. Một tài khoản Bybit thật chưa được kết nối trong phiên này, nên đồng bộ sàn thật chưa được xác minh.

## API đã dùng

`GET /v5/execution/list` trên host cố định:

- Mainnet: `https://api.bybit.com`
- Testnet: `https://api-testnet.bybit.com`

Mỗi lần xem trước hoặc ghi sổ, connector gọi `GET /v5/user/query-api` trước để xác nhận khóa vẫn chỉ đọc. Query của execution gồm `category`, `startTime`, `endTime`, `limit`, và `cursor` khi Bybit trả `nextPageCursor`. Chữ ký là HMAC-SHA256 của `timestamp + apiKey + recvWindow 5000 + query string`, hex thường. Cursor được percent-encode từ giá trị JSON. Timeout mỗi lần gọi là 10 giây. HTTP 429 và `retCode` 10006 thành `rate_limit`. `retCode` 10002, 10003, 10004, 10005, 10010, 33004 giữ ánh xạ lỗi đã có. API key và secret không đưa vào log, query, exception, frontend hay báo cáo này.

Không gọi API đặt lệnh, chuyển tiền, rút tiền, closed PnL (`/v5/position/closed-pnl`), transaction log, nạp tiền hoặc rút tiền.

## Schema

Migration mới: `migrations/392_pack_personal_intelligence_exchange_execution_sync_v1.sql`. Số 392 trống trước khi thêm. Migration 385–391 không sửa. Manifest: `deploy/ubuntu/migration-files.personal-intelligence.txt`.

`ck_trade_source_type` nay gồm `IMPORT`, `MANUAL`, `DERIVED`, `CORRECTED`, `EXCHANGE`. Bảng `pack_personal_intelligence.exchange_execution_sync` lưu một lần ghi đã commit: connection, person, sổ, môi trường, category, khoảng yêu cầu, khoảng sàn trả về, số trang, số execution lấy được, số dòng mới, số trùng, số không ghi, cờ cắt trang và report JSON. Preview không ghi bảng này. Không bật RLS.

Đã áp migration trên database kiểm thử `kit_pi_validation` tại `127.0.0.1:5433`. Constraint và bảng đã có trên database đó.

## Cách một execution vào sổ

Định danh dòng sổ: `BYBIT:{MAINNET|TESTNET}:{category}:{execId}`, tối đa 200 ký tự. Một order có thể có nhiều `execId`. Mỗi `execId` là một dòng. `orderId` của sàn nằm trong `metadata.exchangeOrderId` vì `trade.order_id` là UUID.

Sổ: một `TradingAccount` cho mỗi person, sàn BYBIT và môi trường. Mã ngoài là `BYBIT:{ENV}:{externalUserId}` khi user id đọc được và đủ ngắn, hoặc `BYBIT:{ENV}:connection:{connectionId}` khi thiếu. Category nằm trong metadata và trong định danh execution, không tách thành nhiều sổ.

Ghi khi `execType` là `Trade`, `AdlTrade`, `BustTrade` hoặc `BlockTrade`, và symbol, side BUY/SELL, giá > 0, số lượng > 0, thời điểm và `execId` ánh xạ được. `Funding` và các loại khác được liệt kê trong báo cáo, không insert vào `trade` và không insert vào `fee`. Phí khớp lệnh chỉ nằm trên `trade.fee_amount` / `trade.fee_currency`. Thiếu `execFee` hoặc `feeCurrency` vẫn ghi dòng nếu các trường còn lại hợp lệ, và được nêu trong `missingFields`. Giá, số lượng, phí và giá trị gốc (cắt 64 ký tự), cùng `isMaker`, `feeRate`, `closedSize` khi API trả, nằm trong metadata. `total_value` dùng `execValue` khi có, nếu không thì số lượng nhân giá.

`ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING`. Đồng bộ lại không cập nhật giá hoặc phí đã lưu. Execution lặp trong cùng một lô chỉ được xét một lần và cộng vào số trùng. Exchange và trading account dùng `DO NOTHING`. Account đã xóa không bị mở lại.

## Phạm vi thời gian và loại sản phẩm

Category được hỗ trợ: `spot`, `linear`, `inverse`, `option`. Category khác bị từ chối trước khi gọi HTTP, với mã `unsupported_category`. Nếu `result.category` khác category đã yêu cầu, trang đó bị từ chối và không trộn sản phẩm.

Bybit: một lần gọi tối đa 7 ngày khi có cả `startTime` và `endTime`. Ứng dụng: khoảng người chọn tối đa 31 ngày, chia thành các cửa sổ 7 ngày, cửa sổ mới nhất được gọi trước. Mỗi trang 100 execution. Mỗi yêu cầu tối đa 10 trang, tức 1000 execution. Nếu bị cắt, `resumeBefore` là `execTime` cũ nhất đã lấy. Đặt mốc kết thúc tại mốc đó để lấy phần cũ hơn. Execution đúng một timestamp với mốc đó có thể được lấy lại; dòng đã lưu không bị ghi đè.

`execType` không được gửi lên query, để Funding vẫn xuất hiện trong báo cáo thay vì bị lọc mất ở sàn.

## API ứng dụng

Trên `api/personal-intelligence/trading/exchanges/{connectionId}`:

- `GET .../executions/latest` — quyền `personal_intelligence.exchange.read`
- `POST .../executions/preview` — quyền `personal_intelligence.exchange.connect`
- `POST .../executions` — cùng quyền connect

Preview đếm dòng đã có và không ghi. Commit ghi trade và một dòng `exchange_execution_sync` trong một transaction. Lỗi trước khi ghi (rate limit, JSON hỏng, quyền) không ghi trade. Tenant `PI_B` không bật module, nên HTTP trả 403 trước handler. Gọi service khi connection không thuộc người dùng hiện tại trả 404.

Đồng bộ không tạo Decision, Policy, Paper Execution, position, portfolio, PnL, tỷ lệ thắng hay Market Snapshot. Trading Agent vẫn `recommend_only`.

## Giao diện

Phiên làm việc giữ sáu bước. Khi đã có kết nối đang dùng, bước đọc tài khoản hiện trạng thái chỉ đọc, lần kiểm tra số dư, và lần đồng bộ lịch sử gần nhất. Người dùng chọn category, từ ngày, đến ngày. Mặc định category `linear` và 6 ngày gần nhất tính đến hôm nay. `Xem trước` chưa ghi sổ. `Đồng bộ vào sổ` chỉ bật sau khi đã xem trước. Báo cáo hiện category, số dòng sẽ ghi hoặc đã ghi, số trùng, số không ghi, số execution, số trang, khoảng yêu cầu, khoảng sàn trả về, trường thiếu, issue và ghi chú cắt trang.

Sau khi ghi, phiên đặt sổ Bybit vừa dùng, khoảng ngày vừa chọn, nguồn `EXCHANGE`, bỏ chọn dòng cũ, tải lại sổ và cuộn tới bước chọn giao dịch. Form CSV không được mở.

Ba nhãn nguồn: `Dữ liệu kiểm thử`, `CSV do người dùng nhập`, `Đồng bộ từ sàn`. Bộ lọc sổ có cả ba. Category của dòng sàn hiện cạnh nhãn nguồn. CSV import chỉ còn hai lựa chọn kiểm thử và CSV.

Khi chưa có kết nối, phiên nói chưa có kết nối và lần cập nhật lịch sử là chưa ghi nhận. Nút mở cấu hình kết nối thay cho form đồng bộ. CSV chỉ tự mở khi sổ trống và chưa có kết nối. Khi sổ đã có dòng, CSV nằm sau nút `Nhập CSV vào sổ này`.

## Cách dùng

1. Chạy `scripts/second-brain-trading-ui-start.ps1`. UI local: `http://127.0.0.1:5174/second-brain/session`. API kiểm thử của phiên này: `http://127.0.0.1:56101`.
2. Đăng nhập tenant `PI_A` bằng tài khoản có `personal_intelligence.exchange.connect`.
3. Mở `Kết nối sàn`. Tạo API key Bybit chỉ đọc. Không bật Contract Trade, Spot Trade, Options, Transfer hoặc Withdraw. Chọn Testnet hoặc Mainnet, kiểm tra, rồi lưu. Secret không hiện lại sau khi lưu.
4. Về Phiên làm việc. Chọn loại sản phẩm và khoảng ngày. Bấm `Xem trước`, đọc số dòng và các execution không ghi được, rồi bấm `Đồng bộ vào sổ`.
5. Bảng giao dịch mở đúng sổ, nguồn `Đồng bộ từ sàn` và khoảng ngày vừa chọn. Chọn các dòng đó để tạo bối cảnh và xác nhận nhận định. Đồng bộ không tự tạo nhận định.
6. Nếu báo cáo nói chưa lấy hết khoảng, đặt mốc kết thúc tại thời điểm nó chỉ và đồng bộ lại. Dòng đã có giữ nguyên.

## FACT — build và test fixture

- Build API của `scripts/second-brain-trading-ui-start.ps1`: 0 lỗi. 66 cảnh báo có sẵn, ngoài phạm vi này.
- `ExchangeExecutionSyncTests` cùng các test kết nối sàn trong collection `PiExchange`: 26 passed, 0 failed.
- Bộ Personal Intelligence, trừ `RuntimeHttpTests`: 60 passed, 0 failed. `RuntimeHttpTests` không chạy trong phiên này.
- `tsc` của admin không báo lỗi trên file Second Brain. Bản production Vite của toàn admin không được dùng làm nghiệm thu vì lỗi có sẵn ngoài pack này.

Fixture xác nhận: chữ ký và cursor đã encode; cửa sổ 7 ngày; khoảng 10 ngày tách thành hai cửa sổ mới nhất trước; khoảng dài hơn 31 ngày bị từ chối; một order hai execution thành hai định danh; Funding và giá 0 không ghi; phí âm giữ trên dòng trade; thiếu `feeCurrency` vẫn ghi và được liệt kê; category không hỗ trợ và category lệch response bị từ chối trước khi trộn dữ liệu; phân trang theo `nextPageCursor`; `retCode` 10006 và HTTP 429 thành `rate_limit`; JSON hỏng thành lỗi sàn; secret không xuất hiện trong log. Preview ghi 0 trade và 0 dòng sync. Commit ghi 3 dòng `EXCHANGE`/`EXCHANGE`. Commit lần hai thêm 0 và đếm 3 trùng. Giá sửa tay thành 1.23 còn nguyên sau lần đồng bộ sau. Không có decision, policy, operation, order hoặc paper execution chứa các id đó. Tenant khác không đọc được các dòng. Trần 10 trang đặt `truncated` và `resumeBefore`, trang thứ 11 không được ghi.

Các HTTP handler này là fixture. Host trong test đơn vị là testnet; test API dùng MAINNET và assert `api.bybit.com`. Không có request nào tới Bybit thật.

## FACT — trình duyệt local

UI `http://127.0.0.1:5174`, API `http://127.0.0.1:56101`, database `kit_pi_validation` cổng 5433. Cổng 5290 không lắng nghe. Process pid 11404 không có và không được khởi động. API kiểm thử là pid 20104. UI là pid 9104. PostgreSQL kiểm thử là pid 9264.

Đăng nhập `pi-ui-approve` trên `PI_A`.

- Phiên hiện `Kết nối sàn: Chưa có` và lần cập nhật lịch sử `Chưa ghi nhận`.
- Câu đọc tài khoản nói có thể đồng bộ lịch sử khớp lệnh đã có, không đặt lệnh, không chuyển tiền, không rút tiền, và đồng bộ không tạo nhận định.
- Sổ đang mở phân loại nhiều nguồn: Kiểm thử 271, CSV 1, Đồng bộ từ sàn 0. Câu thiếu dữ liệu nói chưa có execution đồng bộ từ sàn và CSV không được tính là dữ liệu sàn.
- Bảng có nhãn `CSV do người dùng nhập` và `Dữ liệu kiểm thử`. Form CSV không tự mở. Nút `Nhập CSV vào sổ này` vẫn có.
- Bộ lọc có `Dữ liệu kiểm thử`, `CSV do người dùng nhập` và `Đồng bộ từ sàn`. Lọc `Đồng bộ từ sàn` hiện `Chưa có giao dịch trong phạm vi này.` Lọc CSV hiện đúng một dòng `BTCUSDT BUY` nhãn `CSV do người dùng nhập`.
- Chọn dòng CSV đó và nhập giả thuyết làm nút `Xác nhận nhận định` hết khóa. Dòng được bỏ chọn trước khi rời trang. Không bấm xác nhận, không tạo nhận định.
- Trang kết nối sàn hiện hướng dẫn khóa chỉ đọc và bảng `Chưa có kết nối sàn`. Form đồng bộ trong phiên chỉ hiện khi đã có kết nối đang dùng, nên `Xem trước` và `Đồng bộ vào sổ` chưa được bấm trên trình duyệt.

Database kiểm thử lúc nghiệm thu: không có dòng `exchange_connection` Bybit, không có trade `source = EXCHANGE`. Các dòng `FIXTURE`/`IMPORT` có sẵn không bị đổi bởi phiên trình duyệt này.

## Chưa xác minh

- Kết nối và đồng bộ bằng API key Bybit thật. Kết quả fixture không được dùng để tuyên bố việc đó đã thành công.
- Cursor nhiều trang của response Bybit thật. Mã giả định `nextPageCursor` trong JSON chưa được percent-encode sẵn.
- Toàn bộ lịch sử tài khoản. Endpoint này chỉ là execution của một category trong khoảng đã lấy, và có thể bị cắt ở 1000 execution hoặc 31 ngày.
- Funding, closed PnL, transaction log, số dư lịch sử, nạp tiền, rút tiền, position và portfolio. Funding không được cộng vào phí.
- PnL và tỷ lệ thắng. Chưa có quy tắc tính đã xác minh, nên giao diện không tính các số đó.
- Market Snapshot. Không có nguồn thị trường được gắn vào lần đồng bộ này.
- Hành vi khi một execution và mốc `resumeBefore` trùng đúng một timestamp trên dữ liệu sàn thật.
