# SECOND BRAIN V1 — Multi-exchange connectors, historical backfill, Spot/Futures

Ngày: 2026-10-18. Nhánh `main`, HEAD `8c59ceb4`. Không commit. Không deploy. Không đụng cổng 5290. Không bật giao dịch thật.

## 1. Trạng thái đầu vào và phần đã kế thừa

Working tree đã có Second Brain trong `src/Packs/PersonalIntelligence` và `client/admin/src/modules/second-brain`, cùng migration 391 và 392. Các file này chưa nằm trên HEAD; diff của `router.tsx` so với HEAD vẫn chứa cả khối route Second Brain đã có từ trước. Mission này không stage, không commit, không reset.

Đã kế thừa, không thay thế:

- `IExchangeConnector`, `BybitExchangeConnector`, `BybitSigner`, `BybitPermissionGate`.
- Mã hóa AES-GCM `PersonalIntelligence__ExchangeCredentialKey`. Passphrase OKX nằm trong cùng JSON đã mã hóa. Không thêm cột ciphertext.
- Định danh Bybit `BYBIT:{ENV}:{category}:{execId}` và `ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING`.
- Đường preview/commit đồng bộ tay vẫn giới hạn 31 ngày và 10 trang. Đó là mẫu ngắn trên Phiên làm việc, không phải backfill.
- Tenant isolation bằng `tenant_id`. `RLS_DEFERRED_V1` giữ nguyên. Migration 393 không bật RLS.
- Schema `pack_personal_intelligence`, pack `personal_intelligence`. Không có pack hay database riêng cho từng sàn.

## 2. Kiến trúc connector

Một abstraction `IExchangeAdapter`: probe tài khoản chỉ đọc, và đọc một trang lịch sử theo subtype. `IExchangeAdapterRegistry` chọn Bybit, Binance hoặc OKX. Host cố định trong code. Client không được gửi base URL.

- Bybit vẫn là `IExchangeConnector` cho đường mẫu 31 ngày/10 trang, và đồng thời là `IExchangeAdapter` cho job.
- Binance và OKX chỉ implement `IExchangeAdapter`. Không có service nghiệp vụ riêng.
- Job dùng `ExchangeBackfillWorker` (`BackgroundService`) trong process API. Không có worker deploy riêng. Test host tắt worker bằng `ExchangeBackfillOptions.WorkerEnabled = false` để fixture không bị bơm ngầm.

Credential: API key/secret, và passphrase với OKX, được mã hóa trước khi lưu. Response và log không chứa secret, passphrase hay chữ ký. Ngắt kết nối xóa ciphertext và tạm dừng job đang mở.

## 3. Mapping Spot / Futures

Phân loại xảy ra trong mapper, trước khi ghi sổ.

| Sàn | SPOT | FUTURES, lưu riêng subtype | Không ghi vào SPOT/FUTURES |
|---|---|---|---|
| Bybit | `spot` | `linear`, `inverse` | `option`, margin |
| Binance | `spot` | `usd_m`, `coin_m` | margin, options, portfolio margin |
| OKX | `spot` | `swap` (perpetual), `expiry` (OKX FUTURES có kỳ hạn) | `MARGIN`, `OPTION` |

Dòng Bybit cũ được backfill từ `external_trade_id` phần category. `option` giữ `market_type` NULL. Funding, income, nạp/rút không được ghi thành trade. `realizedPnl` và `positionSide` chỉ lưu khi API trả về. Không tính PnL hợp đồng bằng chênh giá nhân số lượng. Metadata ghi `pnlComputed = false`.

Định danh:

- Bybit: `BYBIT:{ENV}:{category}:{execId}`
- Binance: `BINANCE:{ENV}:{subtype}:{symbol}:{tradeId}`
- OKX: `OKX:{ENV}:{subtype}:{instId}:{tradeId}`

Hai fill cùng order, khác execution id, vẫn là hai dòng. Ghi lại cùng id không đè price, fee, quantity hay timestamp.

## 4. Endpoint lịch sử đã dùng

| Luồng | Endpoint | Host |
|---|---|---|
| Bybit spot, linear, inverse | `GET /v5/execution/list` | `https://api.bybit.com`, testnet `https://api-testnet.bybit.com` |
| Binance spot | `GET /api/v3/myTrades` | `https://api.binance.com`, testnet `https://testnet.binance.vision` |
| Binance USD-M | `GET /fapi/v1/userTrades` | `https://fapi.binance.com` |
| Binance COIN-M | `GET /dapi/v1/userTrades` | `https://dapi.binance.com` |
| Binance futures testnet | cùng path USD-M/COIN-M | `https://testnet.binancefuture.com` |
| OKX spot, SWAP, FUTURES | `GET /api/v5/trade/fills-history` | `https://www.okx.com`. Testnet thêm header `x-simulated-trading: 1` |

Đọc tài khoản:

- Bybit: `GET /v5/user/query-api`, `/v5/account/info`, `/v5/account/wallet-balance`. Từ chối key có Withdraw hoặc không read-only.
- Binance: `GET /sapi/v1/account/apiRestrictions`, `GET /api/v3/account`. Từ chối `enableWithdrawals` và chuyển nội bộ. Nếu endpoint quyền không đọc được, trạng thái là chưa xác minh.
- OKX: `GET /api/v5/account/config`, `GET /api/v5/account/balance`. OKX không trả quyền của key, nên `ReadOnlyVerified` luôn false.

Không có endpoint đặt lệnh, hủy lệnh, chuyển hoặc rút.

## 5. Giới hạn và phân trang

Đối chiếu tài liệu V5/Binance ngày 2026-10-18. Đây là giới hạn đã mã hóa, chưa được xác nhận bằng tài khoản thật của người dùng.

- Bybit: `category` bắt buộc, `limit` tối đa 100, `start`+`end` tối đa 7 ngày, cursor `nextPageCursor`. UTA (`uta=1`) đi lùi khoảng 730 ngày rồi `PARTIAL`. `uta=0` hoặc thiếu `uta` chỉ 7 ngày và nói rõ không giả định 730 ngày.
- Binance spot: bắt buộc `symbol`, `limit` tối đa 1000, `startTime`–`endTime` tối đa 24 giờ. Symbol lấy từ số dư khác 0 ghép USDT, USDC, FDUSD, BTC. Symbol đã bán hết không được phát hiện. Mốc dừng 2017-07-14. Job kết thúc `PARTIAL` vì danh sách symbol không phải toàn bộ đời tài khoản. Mã `-1121` bỏ qua symbol, không làm fail cả luồng.
- Binance USD-M và COIN-M: bắt buộc symbol, cửa sổ 7 ngày, chỉ khoảng 6 tháng (183 ngày trong code). Symbol lấy từ `GET /fapi/v1/income` hoặc `/dapi/v1/income`. Income không được ghi thành trade. Hết phạm vi là `PARTIAL` với cutoff.
- OKX: `instType` SPOT, SWAP hoặc FUTURES, `limit` 100, `begin`/`end`, trang tiếp bằng `after` = tradeId cũ nhất của trang đầy. Giữ khoảng 90 ngày, cửa sổ 7 ngày. Hết phạm vi là `PARTIAL`.

Đường mẫu Bybit vẫn `MaxPages = 10` và `MaxRangeDays = 31`. Job nền không dùng hai hằng số đó.

## 6. Job, checkpoint, resume, retry, incremental

Mỗi luồng là exchange + connection + `market_type` + `product_subtype`. Bảng `exchange_backfill_job`. Trạng thái: `QUEUED`, `RUNNING`, `COMPLETED`, `INTERRUPTED`, `PARTIAL`, `FAILED`, `PAUSED`.

- Lưu credential thành công thì xếp một job FULL cho mỗi subtype được hỗ trợ. Không chạy hết lịch sử trong HTTP request lưu kết nối.
- Unique một phần: chỉ một job mở (`QUEUED`, `RUNNING`, `PAUSED`, `INTERRUPTED`) cho mỗi luồng.
- Worker claim `FOR UPDATE SKIP LOCKED`, một trang mỗi bước, tối đa 20 bước mỗi vòng, delay mặc định 200 ms. Sau trang thành công job về `QUEUED` để các luồng xen kẽ.
- Checkpoint ghi sau transaction insert. Lease hết hạn được đánh `INTERRUPTED` rồi claim lại.
- Rate limit, network, lỗi sàn tạm: backoff lũy thừa, tối đa 12 lần, rồi `FAILED`. Lỗi key, chữ ký, passphrase, quyền, timestamp, IP fail ngay.
- Lỗi inverse không đổi trạng thái spot. Binance futures khi key không có quyền futures được ghi `PARTIAL` ngay, không gọi income hay userTrades.
- Kết nối lại: job `PAUSED` về `QUEUED` và giữ checkpoint. Job `FAILED` được xếp lại. Đã có watermark, `PARTIAL` hoặc `COMPLETED` thì incremental chồng một cửa sổ. Người dùng có thể yêu cầu FULL khi luồng không còn job mở.
- FULL chạm giới hạn retention kết thúc `PARTIAL`, không `COMPLETED`. Incremental đi hết khoảng chồng có thể `COMPLETED` nhưng vẫn lưu cutoff. UI không bịa phần trăm.

## 7. Database

Migration `393_pack_personal_intelligence_exchange_backfill_v1.sql`, đã thêm vào `deploy/ubuntu/migration-files.personal-intelligence.txt`. Không sửa 385–392.

Thêm `trade.market_type`, `product_subtype`, `exchange_code`, check chỉ `SPOT` hoặc `FUTURES` hoặc NULL, và bảng job. Dòng Bybit cũ được phân loại từ external id. `UPDATE` trên database kiểm thử trả 0 dòng vì chưa có trade Bybit khớp điều kiện.

Đã áp trên `kit_pi_validation` tại `127.0.0.1:5433` sau khi `current_database()` và `inet_server_port()` khớp. `pg_policies` của schema = 0. `relrowsecurity` của bảng job = false. Không áp production. Không đụng `NT_XUANHOA`.

## 8. API, UI, sổ

API dưới `/api/personal-intelligence/trading/exchanges`:

- `GET catalog`
- `GET backfill`
- `POST {connectionId}/backfill` với `FULL` hoặc `INCREMENTAL`
- `POST backfill/{jobId}/pause|resume|retry`
- Các route kết nối và preview/commit Bybit cũ giữ nguyên.

Sổ nhận `marketType` và `productSubtype`.

UI, Vite đang chạy `http://127.0.0.1:5174`:

- `/second-brain/exchange`: Bybit, Binance, OKX. OKX có passphrase. Binance/OKX có checkbox xác nhận quyền chưa được sàn chứng minh là chỉ đọc. Bảng job có trạng thái, số mới/trùng/bỏ qua/lỗi/trang, cutoff, tạm dừng, tiếp tục, thử lại, tăng dần, đồng bộ lại.
- `/second-brain/spot` và `/second-brain/futures`: lọc tài khoản, subtype, symbol. Tiến độ từng luồng. Không gộp win rate.
- Phiên làm việc nói rõ form tay là mẫu 31 ngày/10 trang.

Đã mở ba màn này trên UI đang chạy, user `pi-ui-approve`. Khung Spot, Futures và form kết nối hiện đúng. Lưới dữ liệu báo lỗi vì process API `http://127.0.0.1:56101` (pid 20104, DLL cũ trong `.dev\sb-trading-ui\api`) chưa có endpoint mới. Không khởi động lại process đó và không đụng pid 6068 trên cổng 5290. Chưa nhập credential trên UI.

## 9. Kết quả test và build

`dotnet test tests/KitPlatform.PersonalIntelligence.Tests` với `KIT_PI_TEST_CONNECTION` trỏ `kit_pi_validation` cổng 5433:

- Passed 66, Failed 3, Skipped 0, Total 69.
- 3 bài fail là `RuntimeHttpTests`: `The validation API output is missing` vì không có `E:\KitPlatform\.dev\sb-runtime-http\KitPlatform.Api.dll`. Fail trong `InitializeAsync`, trước thân test. Không thuộc mission này và không được sửa bằng cách build lại API runtime.
- Sau đó thêm assert resume vào `MultiExchangeBackfillTests` và chạy lại riêng bài đó: Passed 1.

`npx tsx src/modules/second-brain/trading-routes.smoke.ts`: `trading routes ok`.

Compile các project Personal Intelligence trong lần test: 0 error. Cảnh báo `NU1903` Newtonsoft.Json 10.0.3 là có sẵn ở các project dùng chung, không được nâng trong mission này.

## 10. Đã chứng minh bằng fixture và database kiểm thử

Fixture, không có key thật:

- Chữ ký Binance khớp vector công bố `c8db56825ae71d6d79447849e617115f4a920fa2acdcab2b053c4b2838bd6b71`.
- Chữ ký OKX của payload fixture là `NdVrk9TBwnhatkckQ1LeAoh8CaUl6xgzuDsvXoic9VI=`.
- Mapper tách spot / usd_m / coin_m / swap / expiry. Margin OKX không mappable. PnL futures chỉ là chuỗi nguồn.
- Máy backfill đi hơn 10 trang cursor, hơn 31 ngày, rồi `PARTIAL` đúng cutoff.
- Hai checkpoint OKX không kéo nhau.
- Connector từ chối host tự đặt, map 429 thành `rate_limit`, map passphrase OKX sai, và không ghi secret vào log.

`kit_pi_validation`:

- Lưu kết nối Bybit fixture xếp job spot, linear, inverse.
- Spot ghi hơn 10 trang. Cùng `BYBIT:TESTNET:spot:same-1` chỉ còn 1 dòng sau nhiều vòng, kể cả sau khi resume.
- Inverse lỗi quyền thành `FAILED`. Linear rate limit không thành `FAILED` trong cửa sổ test. Spot không bị FAILED theo.
- Tenant B không có các trade đó.
- Job spot đang `RUNNING` với lease hết hạn được nhận lại, số trang tăng, và không kẹt ở `RUNNING`.

## 11. Chưa kiểm chứng bằng tài khoản thật

Chưa có request đọc nào tới Bybit, Binance hay OKX thật trong mission này. Chưa có backfill đời tài khoản người dùng. Fixture không được dùng để nói là đã kết nối sàn thật.

Chưa xác minh trên tài khoản thật: quyền key, retention 730 ngày / 6 tháng / 3 tháng, symbol discovery bỏ sót cặp đã bán, và hành vi testnet Binance/OKX.

## 12. Còn lại

- Option, margin, options, portfolio margin không được đồng bộ vào sổ. Funding và income không thành trade. UI cần nói các nhóm này chưa có, không báo là đã xong.
- Binance spot không thấy symbol số dư bằng 0. Futures không thấy symbol không xuất hiện trong income 6 tháng.
- Bybit thiếu `uta` chỉ đi 7 ngày.
- Đường mẫu 31 ngày/10 trang cố ý còn trên Phiên làm việc.
- API local pid 20104 chưa nạp code mới. Cổng 5290 không được restart.
- `RuntimeHttpTests` vẫn thiếu thư mục output `.dev\sb-runtime-http`.

## 13. Cách tự kết nối trên máy local

Không dán API key, secret hay passphrase vào chat.

1. Tạo key chỉ đọc, hạn chế IP. Bybit: Read-Only, không Trade, không Transfer, không Withdraw. Binance: tắt Withdraw và Internal Transfer. Muốn đọc USD-M/COIN-M thì Binance bắt bật Enable Futures, và quyền đó không tách khỏi quyền giao dịch; phải tick xác nhận. OKX: có passphrase. OKX không trả quyền key; tick xác nhận key chỉ Read.
2. Khởi động lại API kiểm thử Second Brain bằng script local hiện có, để process nạp bản dựng mới. UI đang là `http://127.0.0.1:5174`. Không trỏ script vào cổng 5290.
3. Vào `/second-brain/exchange`. Chọn sàn và Testnet hoặc Mainnet. Kiểm tra kết nối, rồi lưu. Secret không hiện lại.
4. Bảng đồng bộ phải ở `QUEUED` hoặc `RUNNING`. Đó chưa phải lịch sử xong.
5. Xem `/second-brain/spot` và `/second-brain/futures`. `PARTIAL` kèm cutoff nghĩa là đã đi hết phần API trả được. `FAILED` của một subtype không kết thúc subtype kia.
6. Sau lần đầu, làm mới kết nối chạy incremental. Nút Đồng bộ lại chỉ khi luồng không còn job đang mở.

## 14. Git

`main` tại `8c59ceb4`. Không `git add`, không commit, không push, không deploy.

Các path của mission đang dirty hoặc untracked so với HEAD, gồm connector, job, migration 393, manifest personal-intelligence, test, UI Second Brain và route. Working tree khác (Famixa, content, sales) không được sửa cho mission này.

## Nghiệm thu

| Tiêu chí | Kết quả |
|---|---|
| Ba sàn trên cùng pack Personal Intelligence | Có trong code và test fixture. Chưa gọi API thật. |
| Lần kết nối tự xếp full backfill, không nhét vào một HTTP request | Có. Chứng minh bằng save fixture rồi pump riêng. |
| Nhiều trang, nhiều cửa sổ, checkpoint, resume | Có trên fixture và `kit_pi_validation`. Hơn 10 trang, hơn 31 ngày, resume sau lease hết hạn. |
| Spot và Futures tách ở mapper, job, query, UI | Có. Inverse fail không làm fail spot. |
| Subtype giữ riêng | linear, inverse, usd_m, coin_m, swap, expiry. |
| Idempotent, mã hóa, tenant, dữ liệu Bybit cũ | `DO NOTHING`. Passphrase trong payload mã hóa. Tenant B không thấy trade. Migration không viết đè dòng cũ. |
| Giới hạn API không báo Completed giả | FULL có retention kết thúc `PARTIAL` và có cutoff. |
| Không đặt lệnh, không rút, không deploy, không commit | Đúng. |

## Phụ lục 2026-10-18 — API kiểm thử local và quyền chỉ đọc

### Process

Script `scripts/second-brain-trading-ui-stop.ps1` chỉ dừng PID ghi trong `.dev/sb-trading-ui/pids.json` khi command line khớp marker. Đã dừng PID cũ 20104 và 9104. Cổng 5290 vẫn là PID 6068, `KitPlatform.Api.exe` trong `src\KitPlatform.Api\bin\Debug\net10.0`. Không bị restart.

Script `scripts/second-brain-trading-ui-start.ps1` build Debug vào `E:\KitPlatform\.dev\sb-trading-ui\api` (0 error) rồi chạy DLL đó. Không đụng `.dev\sb-runtime-http`.

- UI: `http://127.0.0.1:5174`, PID 25712, `vite.second-brain.config.ts`.
- API: `http://127.0.0.1:61846`, PID 24296, `"dotnet" "E:\KitPlatform\.dev\sb-trading-ui\api\KitPlatform.Api.dll"`.
- Health: `GET /api/health` → `{"status":"ok"}`. `GET /api/health/db` → `{"status":"ok","database":true,"value":1}`.
- Proxy UI `/api` trả cùng catalog với API 61846, nên UI không còn trỏ process 56101.

Database vẫn `kit_pi_validation` cổng 5433. Bảng `exchange_backfill_job` còn. `relrowsecurity` = false. Không áp lại migration và không đụng production.

### API và UI đang chạy

Đăng nhập local `PI_A` / `pi-ui-approve`. Không dùng key sàn thật.

| Route | HTTP |
|---|---|
| `GET /api/personal-intelligence/trading/exchanges/catalog` | 200. Bybit, Binance, OKX. Subtype spot, linear, inverse, usd_m, coin_m, swap, expiry. OKX `requiresPassphrase=true`, `readOnlyVerifiable=true`. |
| `GET .../exchanges/backfill` | 200, danh sách rỗng. |
| `GET .../trades?marketType=SPOT` và `FUTURES` | 200, `total=0`. |
| `POST .../exchanges/test` key quá ngắn | 400 `invalid_key`. Body không chứa secret. |
| OKX thiếu passphrase | 400 `passphrase_required`. |
| Sàn lạ và environment là URL | 400 `unsupported`, trước khi gọi mạng. |

`api.log` không chứa secret fixture. Ba trang `/second-brain/exchange`, `/spot`, `/futures` mở sau đăng nhập, không còn lỗi API cũ. Form có Testnet/Mainnet, API key, API secret. Bảng job trống và nói rõ kết nối chưa phải lịch sử xong. Cảnh báo mới: không bật Enable Futures hoặc Trade để lấy lịch sử.

Job pause/resume/retry, hơn 10 trang, hơn 31 ngày, resume không nhân đôi, và `PARTIAL` khi chạm retention vẫn là kết quả fixture trên `kit_pi_validation` từ bài `MultiExchange*`, không phải từ một key sàn thật trên process 61846. Process đó gọi sàn thật, nên không gửi key giả lên Bybit/Binance/OKX.

### Quyền

- Bybit: `readOnly` phải là số 1, `permissions` phải là object có nhóm dạng mảng. Thiếu, rỗng, sai kiểu, hoặc chuỗi `readOnly` → `unverified`, không lưu. `Withdraw` → `withdrawal`. Tên chứa Transfer → `transfer`. Trade hoặc Order dù `readOnly=1` → `not_read_only`.
- OKX: `GET /api/v5/account/config` có `perm` (`read_only`, `trade`, `withdraw`). Chỉ đúng `read_only` thì `ReadOnlyVerified=true` và lịch sử được xếp hàng. `trade` → từ chối. `withdraw` → từ chối. Thiếu, rỗng, sai kiểu, hoặc giá trị lạ → chưa xác minh, phải tick xác nhận mới lưu, và cả Spot lẫn SWAP/expiry thành `PARTIAL` chứ không gọi fills. Số dư không được dùng làm bằng chứng chỉ đọc.
- Binance: `enableWithdrawals`, `enableInternalTransfer`, `permitsUniversalTransfer` bị từ chối. `enableReading` bắt buộc khi đọc được apiRestrictions. `enableFutures` không bao giờ được coi là chỉ đọc. USD-M và COIN-M luôn `PARTIAL` và không gọi userTrades/income: nếu key bật Enable Futures thì lý do là key có thể đặt lệnh; nếu không bật thì lý do là không có quyền futures chỉ đọc. Không có câu hướng dẫn bật Enable Futures. Spot chỉ được xếp hàng khi không bật giao dịch Spot/Margin. Không đọc được apiRestrictions thì mọi luồng lịch sử `PARTIAL`.

### Sẵn sàng cho chủ tài khoản

Có thể nhập key tại `http://127.0.0.1:5174/second-brain/exchange`. Không gửi key vào chat.

Phạm vi được đồng bộ lịch sử:

- Bybit spot, linear, inverse khi `readOnly=1` và permissions không có trade/order/transfer/withdraw.
- Binance Spot khi apiRestrictions chỉ có quyền đọc, không bật giao dịch Spot/Margin, không rút, không chuyển.
- OKX spot, swap, expiry khi `perm` chỉ là `read_only`.

Bị chặn:

- Binance USD-M và COIN-M trong mọi trường hợp hiện tại.
- Binance Spot nếu key bật giao dịch Spot hoặc Margin, hoặc chưa đọc được apiRestrictions.
- OKX nếu `perm` không phải đúng `read_only`.
- Mọi key có rút, chuyển, trade hoặc đặt lệnh.

Sau khi lưu, job ở `QUEUED`/`RUNNING`, hoặc `PARTIAL` nếu luồng bị chặn quyền. Không có trạng thái “lịch sử đã xong” chỉ vì credential đã lưu. Cutoff Bybit 7 hoặc 730 ngày, Binance spot theo symbol và mốc 2017-07-14, OKX khoảng 90 ngày. Hết phạm vi là `PARTIAL`, không phải `COMPLETED`.

Chưa có probe mainnet thật. Lần đọc đầu tiên do chủ tài khoản thực hiện trên UI.

### Test

`dotnet test` filter Exchange và MultiExchange, `KIT_PI_TEST_CONNECTION` trỏ `kit_pi_validation` cổng 5433: Passed 21, Failed 0. Gồm gate quyền an toàn, không an toàn, thiếu và sai dạng. Ba bài `RuntimeHttpTests` vẫn thiếu `E:\KitPlatform\.dev\sb-runtime-http\KitPlatform.Api.dll` và không được sửa trong lần này.

### Git

`main` tại `8c59ceb4`. Không add, không commit, không push, không deploy. Cổng 5290 không đổi. Working tree Second Brain và migration 393 vẫn dirty/untracked so với HEAD, cùng các thay đổi khác không thuộc mission này.
