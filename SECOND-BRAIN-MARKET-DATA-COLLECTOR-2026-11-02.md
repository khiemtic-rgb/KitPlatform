# Market data collector — 2026-11-02

Lần làm: 11:57–12:02 +07. VPS B `157.10.53.182`. Chỉ nến public Binance, không API key, không lệnh, không migration, không đụng publication chiến lược đã khóa, không đụng VPS A.

## 1. Hiện trạng ban đầu và nguyên nhân dừng

Collector `kit-trading-research` không còn unit, timer, process hay journal trên VPS sau boot 2026-10-29. Không có gì tự kéo nó dậy vì unit không được cài lại.

Task Windows `SecondBrainPaperOpsV1` vẫn được đặt mỗi giờ, nhưng lần 11:12 +07 hôm nay trả `0x800710E0` và không ghi log. Cache SOLUSDT dừng ở nến mở 2026-11-01 15:00 UTC.

Publication `pub-e65b6912e02a168d` vẫn nguyên checksum. Nhãn `FRESH` của nó là chữ `health=OK` ghi sẵn ngày 30/10, không đo tuổi nến.

## 2. Đã sửa và hoàn thành

Thêm collector trong repo, không đụng working tree khác:

- `deploy/second-brain-agent/market-data-collector/collector.py`
- `test_collector.py`
- `second-brain-market-data.service`
- `second-brain-market-data.timer`

Collector dùng HTTPS public `api.binance.com/api/v3/klines` và `/api/v3/time`. Từ chối URL có `signature` hoặc host khác. Không có khóa, không có lệnh. `live_orders` luôn false.

Lưu file tại `/var/lib/second-brain-research/market-data/SOLUSDT/1h/`:

- `candles.jsonl` — một nến một dòng, khóa trùng là `open_time`
- `manifest.json` — số nến và checksum
- `health.json` — tính lại mỗi lần chạy
- `runs.jsonl` — nhật ký từng lần chạy

Checksum dùng cùng quy tắc với lab (`open|high|low|close|close_time|volume`). Nến đang mở không được ghi. Nến trùng và giống nhau được bỏ. Nến trùng mà khác giá được giữ bản cũ và đếm `conflicts`. Lỗ hổng giờ được phát hiện và xin lại đúng khoảng đó.

Health không dùng chữ OK cố định. `OK` chỉ khi nến đóng cuối đúng giờ hiện tại và không có lỗ. Lệch từ hai khung giờ trở lên là `STALE`. Có lỗ là `GAP`.

7 test local, `python -m unittest test_collector.py`: tất cả OK. Gồm trùng, xung đột, bù lỗ, bỏ nến chưa đóng, health theo tuổi, phát hiện sửa checksum, từ chối URL có chữ ký.

Publication chiến lược không bị ghi đè. Archive mới là `research-market-archive/v1`, tách khỏi `research-publication/v1`.

## 3. Collector đang chạy ở đâu

| Mục | Giá trị |
|---|---|
| Máy | VPS B, user `sbagent` |
| Chương trình | `/opt/second-brain-market-data/collector.py` |
| Dữ liệu | `/var/lib/second-brain-research/market-data` |
| Service | `second-brain-market-data.service`, oneshot |
| Timer | `second-brain-market-data.timer`, `enabled` |
| Lịch | 2 phút sau boot, rồi mỗi giờ sau lần chạy trước. `Persistent=true` |
| Lần kế, đọc 12:02 +07 | 2026-11-02 13:01 +07 |
| Log | `/var/log/second-brain-market-data/collector.log` và `runs.jsonl` |
| Symbol | `SOLUSDT`, `BTCUSDT`, `ETHUSDT`, cùng khung `1h`, cùng mốc 2026-07-23 10:00 UTC |

Sau reboot, systemd bật lại timer vì unit đã enable. Lần chạy lúc boot tự kéo nến vừa đóng.

## 4. Dữ liệu và chất lượng

Mốc đọc sau reboot, 12:01 +07.

| Mục | Giá trị |
|---|---|
| Symbol / khung | `SOLUSDT` / `1h` |
| Số nến | 2 443, unique `open_time` = 2 443 |
| Open đầu | 2026-07-23 10:00 UTC |
| Open cuối | 2026-11-02 04:00 UTC |
| Lỗ giờ | 0 |
| Xung đột giá | 0 |
| 2 159 nến đầu, đến 2026-10-21 08:00 UTC | checksum `126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3`, khớp snapshot đã khóa |
| Checksum toàn bộ | `8958b3389c0b889d28af07e85eadcad8db10791da6f2583b5798ba1a62bc7778` |
| Health | `OK`, `age_ms=0`, `verified=true` |

Nguồn vào: snapshot 2 159 nến + cache máy trạm 271 nến = 2 430, không trùng, không lỗ. Sync Binance thêm 12 nến còn thiếu đến 03:00 UTC. Sau reboot, lúc 12:00:38 +07, thêm đúng 1 nến 04:00 UTC. Lần chạy ngay sau đó `inserted=0`, checksum không đổi.

## 5. Kiểm thử sau restart VPS B

Reboot lúc 11:58:25 +07. Máy trả SSH lúc 11:58:49 +07, uptime 0 phút.

Trước reboot: Second Brain PID 78259, `kit-platform-api` PID 37553, nginx và cả hai cụm PostgreSQL active. Archive có 4 dòng run, 2 442 nến.

Sau reboot:

| Dịch vụ | Kết quả |
|---|---|
| `second-brain-agent` | active, PID mới 986. `GET /api/health` 200. `GET /api/health/db` 200, database `second_brain_agent_v1`, worker false |
| `postgresql@16-main` và `postgresql@16-sbagent` | active. `start.conf` của cả hai là `auto` |
| `nginx` | active, cổng 80 |
| `kit-platform-api` | active, PID 988, `NRestarts=0`, nghe `127.0.0.1:5000`. `/api/health` và `/api/health/db` đều 200 |
| Timer market data | active, enabled |

Lần do timer sau boot, 12:00:38 +07: `Result=success`, exit 0, `inserted=1`, `duplicate_count=0`, `gap_count=0`, `status=OK`. `verify` trả `verified=true`. Số dòng run tăng từ 4 lên 5, rồi 6 sau một lần kiểm tra idempotent.

## 6. Còn thiếu, cần phê duyệt hoặc quyết định

- BTC và ETH khung 1 giờ đã được thêm sau khi nhịp 13:01 +07 của SOL tự ghi đúng một nến. Mỗi symbol có 2 444 nến liên tục, cùng open đầu và open cuối với SOL, health `OK`. Khung giờ khác vẫn chưa bật.
- Task Windows `SecondBrainPaperOpsV1` vẫn lỗi `0x800710E0`. VPS là nơi giữ dữ liệu liên tục. Task máy trạm không được sửa trong lần này.
- Publication chiến lược `pub-e65b6912e02a168d` giữ nguyên để khỏi gãy checksum. Freshness vận hành nằm ở `health.json` của archive, không nằm ở nhãn `FRESH` cũ.
- Chưa tính walk-forward, Sharpe hay ranking chiến lược. Archive đã đủ nến và checksum để làm việc đó sau, khi có một lần duyệt phương pháp. Chưa gắn kết quả vào API Second Brain vì bản đang chạy không được build lại từ working tree bẩn.
- Không commit, không push. Không migration, không xóa dữ liệu, không nối sàn.
