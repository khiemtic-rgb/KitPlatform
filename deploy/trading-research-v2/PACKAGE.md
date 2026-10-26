# Trading Research V2 — gói triển khai (chưa được phép chạy)

Mission: `SB-TRADING-DEPLOY-READINESS-004`. Script `build-package.ps1` chỉ publish trên máy đang phát triển. Không SSH, không copy, không restart, không migration.

## Tái tạo

Từ thư mục repository `KitPlatform-sb-research`:

```powershell
powershell -NoProfile -File deploy\trading-research-v2\build-package.ps1
```

Kết quả nằm ở `deploy/trading-research-v2/dist/` (không commit). Hai thư mục:

- `research-host/` — `net10.0`, `linux-x64`, framework-dependent
- `research-dashboard/` — cùng runtime

`dist/SHA256SUMS.txt` là hash của từng file vừa build. Đối chiếu sau khi copy bằng `sha256sum -c SHA256SUMS.txt`.

Bản build local ngày 2026-10-23, 34 file:

- Research Host DLL `bf451d01cad056261f957fe6b7f57d6c5ceac5e682022e624a4dd1425cf0278d`
- Research Dashboard DLL `d801ac46a694911100e1707495059a865c019b0b9bc528bbfde6275a3dd14601`

Build lại từ source sẽ ra hash khác nếu code đổi. Dùng hash trong `dist/SHA256SUMS.txt` của lần build định mang lên, không dùng hash cũ.

Runtime máy chủ đã có từ pilot: `Microsoft.NETCore.App 10.0.9` và `Microsoft.AspNetCore.App 10.0.9`. Postgres của database `trading_research` không đổi. Không có migration mới.

## Artifact

| Thành phần | Phiên bản | Ghi chú |
| --- | --- | --- |
| Research Engine | `research-engine-v2` | Bốn chiến lược. SMA mặc định giữ hash phiên cũ khi không truyền `--strategy`. |
| Research Host | bản publish của mission này | Process một lần. Không mở port. |
| Research Dashboard | bản publish của mission này | Chỉ bind `127.0.0.1` hoặc `localhost`. |
| Admin UI | build `client/admin` | Desk gọi `/api/personal-intelligence/trading-research` và gửi JWT đăng nhập. |
| Schema | `migrations/395_research_trading_v1.sql` đã áp ở pilot | Không áp lại. Không đổi bảng. |

## Cấu hình

Giá trị công khai, đã dùng ở pilot và giữ nguyên nếu chưa có lệnh riêng:

- symbol `BTCUSDT`, interval `1h`, limit `48`, max-stale-hours `72`
- quantity `0.001`, confidence `0.80`, slippage-bps `10`, fee-rate `0.001`, minimum-fee `0`
- leverage `1`, max-notional `1000`, max-leverage `1`, min-confidence `0.50`, session-notional `5000`
- max-http `1`, max-candles `500`, max-runtime-seconds `30`
- Không truyền `--strategy`. Host mới vẫn chạy SMA baseline.
- Không truyền `--live`.

Secret, đặt ngoài source và ngoài gói này:

| Biến | Nơi đặt | Ý nghĩa |
| --- | --- | --- |
| `ConnectionStrings__TradingResearch` hoặc file connection hiện có | `/etc/kit-platform/trading-research.env` đã tồn tại | Chỉ database `trading_research`. Không dùng `novixa_prod`. |
| `TRADING_RESEARCH_DASHBOARD_TOKEN` | env của dashboard, đã có ở pilot | Bảo vệ `GET /api/dashboard`. |
| `Jwt__Secret` | env của dashboard, cùng secret với API, tối thiểu 32 ký tự | Bắt buộc để mở desk. Thiếu thì desk trả 503. |
| `Jwt__Issuer` | cùng issuer với API | |
| `Jwt__Audience` | cùng audience với API | |
| `Dashboard__AllowExecute` | không đặt trên pilot | `true` mới cho phép POST chạy nghiên cứu. Mặc định tắt. |
| `Dashboard__DeskRoot` | thư mục state local của desk | Không trỏ vào database production. |

Không ghi secret, mật khẩu, API key Binance, hoặc connection string vào file này.

## Quyền API

| Đường | Ai được gọi |
| --- | --- |
| `GET /api/dashboard` | Đọc research. Nếu có `TRADING_RESEARCH_DASHBOARD_TOKEN` thì phải gửi đúng Bearer token đó. |
| `GET/POST /api/personal-intelligence/trading-research/*` | JWT nền tảng, role `ADMIN` hoặc permission `personal_intelligence.trading.run`. |
| POST chạy task hoặc batch | Thêm `Dashboard__AllowExecute=true`. Thiếu quyền thì 403. Token sai thì 401. Chưa cấu hình JWT thì 503. |
| `GET https://api.binance.com/api/v3/klines` | Công khai, không API key, không dữ liệu tài khoản. |

Không có cờ nào biến desk thành API công khai.

## Thứ tự khi được phê duyệt riêng

1. Dừng ở bước này nếu chưa có lệnh deploy. Không chạy phần dưới từ mission 004.
2. Build bằng script trên và giữ `SHA256SUMS.txt`.
3. Không đổi timer, không đổi `/etc/kit-platform/api.env`, không restart `kit-platform-api`.
4. Nếu chỉ cập nhật host: thay binary trong `/opt/kit-platform/trading-research/` sau khi giữ bản cũ. Giữ nguyên đối số pilot. Không thêm `--strategy` và không tăng `--max-http`.
5. Nếu cập nhật dashboard: thay binary loopback `127.0.0.1:5088`, thêm `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience` vào env của dashboard (không ghi vào repo). Giữ `Dashboard__AllowExecute` tắt cho đến khi có lệnh bật desk.
6. Không áp migration.
7. Admin UI chỉ deploy cùng đợt nếu desk được bật. Nếu không, UI production không cần đổi để timer SMA tiếp tục chạy.

## Kiểm tra trước

- `dotnet test` filter research desk, dashboard, ledger, strategy: pass.
- `build-package.ps1` tạo đủ hai thư mục và file hash.
- Gói không chứa `.env`, connection string, hoặc API key.
- Đối số host không có `--live`.

## Kiểm tra sau, chỉ khi deploy đã được duyệt

- `GET /api/dashboard` không token thì 401 nếu token dashboard đang bật.
- `POST /api/personal-intelligence/trading-research/tasks` không JWT thì 401.
- JWT không có `personal_intelligence.trading.run` thì 403.
- Timer vẫn `COMPLETED` hoặc `REPLAYED`, fill mới của SMA baseline là 0 khi không có nến mới.
- Host từ chối `--live`.
- Database vẫn là `trading_research`. `novixa_prod` không có connection mới.

## Rollback

Kích hoạt khi health timer không còn `COMPLETED`/`REPLAYED`, dashboard loopback không trả 200, hoặc một lần chạy tạo fill ngoài SMA baseline.

1. Dừng timer (`systemctl stop kit-trading-research.timer`) rồi trả binary host và dashboard về bản đã lưu trước khi copy.
2. Không sửa schema. Không restore database trừ khi một lần chạy đã ghi dòng mới ngoài ý muốn. Nếu cần, restore dump `/var/backups/kit-trading-research/` đã có từ pilot, rồi đối chiếu số dòng candle, signal, paper_fill.
3. Bỏ `Dashboard__AllowExecute` nếu lỡ bật.
4. `daemon-reload` và start lại đúng unit cũ. Không restart `kit-platform-api`.
5. Xác nhận một lần timer replay không tạo fill mới.

## Chưa được phê duyệt

- Chạy script deploy hoặc copy artifact lên VPS.
- Bật `Dashboard__AllowExecute` trên host production.
- Đổi timer từ SMA 48 nến sang bốn chiến lược hoặc 600 nến.
- Bật `PersonalIntelligence:ExposeExchangeRead` trên API production.
- Mở cổng 5088, 5432, 5433 ra internet.
