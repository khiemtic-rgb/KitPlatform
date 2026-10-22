# SECOND-BRAIN-TRADING-EXCHANGE-CONNECTION-V1 — EXECUTION REPORT

Cập nhật: 2026-10-17. Repository: `E:\KitPlatform`. Không commit.

## Màn hình và route

Trong nhóm Second Brain:

- `/second-brain/exchange` — Kết nối sàn
- `/second-brain/account` — Tài khoản sàn

Các route sẵn có vẫn còn: overview, ledger, import, journal, agent, risk. Ledger vẫn hiển thị các dòng `FIXTURE`.

API:

- `GET /api/personal-intelligence/trading/exchanges`
- `GET /api/personal-intelligence/trading/exchanges/{id}/account`
- `POST /api/personal-intelligence/trading/exchanges/test`
- `POST /api/personal-intelligence/trading/exchanges`
- `POST /api/personal-intelligence/trading/exchanges/{id}/refresh`
- `DELETE /api/personal-intelligence/trading/exchanges/{id}`

Quyền mới, không gắn vào `trading.approve` hay `trading.run`:

- `personal_intelligence.exchange.connect` — kiểm tra, lưu, làm mới, ngắt
- `personal_intelligence.exchange.read` — xem kết nối và snapshot

Role `PI_EXCHANGE` trên tenant `PI_A` được gán cho `pi-ui-approve` bởi script local. `pi-ui-run` không có quyền này.

## Connector

Adapter: `IExchangeConnector`, triển khai thật duy nhất `BybitExchangeConnector`.

Host cố định:

- Mainnet `https://api.bybit.com`
- Testnet `https://api-testnet.bybit.com`

Client không gửi base URL. Endpoint chỉ đọc, đối chiếu tài liệu Bybit V5 tại `bybit-exchange.github.io` trong phiên này:

- `GET /v5/user/query-api`
- `GET /v5/account/info`
- `GET /v5/account/wallet-balance?accountType=UNIFIED` chỉ khi `uta=1`

Chữ ký HMAC-SHA256 của `timestamp + apiKey + recvWindow + query`. Timeout 10 giây. Không có endpoint đặt lệnh, chuyển tiền hoặc rút tiền.

Lưu credential chỉ khi `readOnly=1`, object `permissions` có mặt, và không có quyền `Withdraw`. `readOnly=0`, thiếu metadata, key hết hạn, retCode sai key/chữ ký/quyền/rate limit đều không tạo kết nối thành công. `uta` khác 1 thì không gọi wallet-balance và không điền số dư.

## Bảo mật

API secret và API key đầy đủ nằm trong ciphertext AES-256-GCM. Khóa 32 byte lấy từ `PersonalIntelligence__ExchangeCredentialKey`, không nằm trong database hay Git. Script local ghi khóa vào `.dev/sb-trading-ui/exchange-key.local`. Path này khớp `.gitignore`. Thiếu khóa thì lưu trả 503, không fallback plaintext.

Database chỉ giữ mask `••••` + 4 ký tự cuối, fingerprint SHA-256, và ciphertext. Response không có secret, ciphertext hay signature. Log của request test không chứa fixture key/secret đã nhập trên UI. Ngắt kết nối xóa nonce, ciphertext và tag; snapshot và sổ giao dịch được giữ.

## Migration

`migrations/391_pack_personal_intelligence_exchange_connection_v1.sql` được thêm và ghi vào `deploy/ubuntu/migration-files.personal-intelligence.txt`. Không sửa 385–390.

Đã áp trên `kit_pi_validation` cổng 5433 sau khi `current_database()` và `inet_server_port()` khớp. Không dùng `kitplatform` hay `kitplatform_test`. Bảng: `exchange_connection`, `exchange_account_snapshot`, `exchange_asset_balance`. Snapshot là append-only.

## Kết nối và số dư

FACT: Trên UI local, bấm Kiểm tra kết nối với key giả. API log ghi `POST /api/personal-intelligence/trading/exchanges/test` trả 400 sau 339ms. Màn hình hiện đúng câu `API key không hợp lệ.` Câu này chỉ được connector trả khi Bybit trả `retCode` 10003. Danh sách kết nối vẫn trống. `SELECT count(*) FROM pack_personal_intelligence.exchange_connection` = 0.

INFERENCE: Request đó tới `https://api-testnet.bybit.com` và nhận retCode 10003. Host không ghi retCode ra log.

NOT VERIFIED: Chưa có kết nối Bybit thành công và chưa có số dư thật. Không có API key của chủ tài khoản trong phiên này. Mọi số `123.45` trong test là fixture HTTP, không phải số dư sàn.

## Test, build, browser

FACT:

- `BybitConnectorTests` và `ExchangeConnectionApiTests`: ký, map số dư thiếu, từ chối write/withdraw, key sai, chữ ký, quyền, rate limit, mạng, host tùy ý, ciphertext không chứa plaintext, tenant khác không đọc được, snapshot tăng khi refresh, ngắt kết nối xóa ciphertext và giữ snapshot, refresh không tạo decision/policy/operation/order/execution.
- Cả bộ Personal Intelligence trừ `RuntimeHttpTests`: 52 passed, 0 failed.
- Build API vào `.dev/sb-trading-ui/api`: 0 error. Process cổng 5290 pid 11404 không bị dừng.
- UI dev `http://127.0.0.1:5174` render Kết nối sàn và Tài khoản sàn. Ô API Secret là `type=password`. Nút Lưu kết nối disabled trước khi kiểm tra thành công. Sổ giao dịch vẫn có dòng FIXTURE.
- `vite build` production dừng vì `src/shared/api/pos-customer-context.api.ts` không resolve `./api-client`. Lỗi này có trước nhiệm vụ kết nối sàn. Dev server vẫn chạy các màn Second Brain.

`RuntimeHttpTests` không chạy lại vì phụ thuộc DLL runtime riêng.

## Giới hạn

- Chỉ Bybit. Chỉ đọc. Không live execution, không PnL, không tự nâng quyền agent.
- Số dư Unified chỉ được đọc khi `uta=1`. Tài khoản thường không được gán số dư suy đoán.
- `readOnly=1` kèm quyền khác Withdraw vẫn được lưu và liệt kê tên quyền. Không gắn nhãn an toàn nếu thiếu `readOnly` hoặc `permissions`.
- Khóa mã hóa của process local nằm trong file gitignored. Process API khác không có khóa này sẽ từ chối lưu.
- Chưa kiểm chứng một tài khoản Bybit thật.

## Git

Không commit, không stage. 385–390 không có diff nội dung. `391` là file mới. Thay đổi kết nối sàn nằm trong cây Personal Intelligence, `client/admin` Second Brain, `router.tsx`, manifest PI và script start. `migrations/281_pack_content_wave0.sql` vẫn dirty từ trước và không được sửa trong nhiệm vụ này. `.dev/sb-trading-ui/exchange-key.local` bị gitignore.

## Cách tạo key và mở màn hình

1. Trên Bybit, tạo API key và bật Read-Only. Không bật Contract Trade, Spot Trade, Options, Transfer hay Withdraw.
2. Chạy `powershell -File E:\KitPlatform\scripts\second-brain-trading-ui-start.ps1`.
3. Mở `http://127.0.0.1:5174/login`, tenant `PI_A`, user `pi-ui-approve`. Mật khẩu nằm trong file local đã gitignore, không ghi vào báo cáo này.
4. Vào Second Brain → Kết nối sàn. Chọn Bybit và Testnet hoặc Mainnet. Dán API key và secret. Bấm Kiểm tra kết nối. Chỉ bấm Lưu kết nối sau khi kiểm tra hợp lệ.
5. Secret không hiện lại. Tài khoản sàn hiển thị snapshot. Trường sàn không trả được ghi `Chưa được sàn cung cấp`. Ngắt kết nối xóa credential và giữ snapshot cũ, có nhãn đã ngắt.
