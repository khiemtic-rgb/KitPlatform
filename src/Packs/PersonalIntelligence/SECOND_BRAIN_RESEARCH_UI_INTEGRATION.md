# Second Brain — tích hợp UI Research Publication

Ngày: 2026-10-22. Nhiệm vụ: `SECOND-BRAIN-RESEARCH-UI-INTEGRATION-001`.

Publisher, reader, controller, policy và DTO không đổi. Không có migration, deploy, hay sửa cấu hình production.

## Kết luận

Overview đọc năm route GET qua API client hiện có. Sổ giao dịch và Research Lab / Paper Ops nằm hai khối riêng. `FRESH` chỉ được ghi là health nguồn `OK`.

**GO** cho bước nghiệm thu local tiếp theo, sau khi process API local được khởi động lại với root publication. **NO-GO** cho deploy. Màn hình đang chạy chưa thể nghiệm thu trạng thái `FRESH` vì process API hiện tại chưa có route mới.

## File đã sửa

- `client/admin/src/modules/second-brain/research-view.ts`
- `client/admin/src/modules/second-brain/research.api.ts`
- `client/admin/src/modules/second-brain/ResearchPanel.tsx`
- `client/admin/src/modules/second-brain/TradingPages.tsx`
- `client/admin/src/modules/second-brain/operator-notices.ts`
- `client/admin/src/modules/second-brain/trading-routes.smoke.ts`
- `client/admin/src/modules/second-brain/research-view.smoke.ts`
- `src/Packs/PersonalIntelligence/SECOND_BRAIN_RESEARCH_PUBLICATION_IMPLEMENTATION.md` (một mục trỏ sang tài liệu này)

## Endpoint và component

`TradingOverviewPage` giữ các thống kê `TradingSummary`. Bên dưới là `ResearchPanel`.

`researchApi` gọi:

- `GET /api/personal-intelligence/research/publication`
- `GET /api/personal-intelligence/research/experiments`
- `GET /api/personal-intelligence/research/experiments/{experimentId}` khi bấm Xem
- `GET /api/personal-intelligence/research/paper/status`
- `GET /api/personal-intelligence/research/paper/report`

Chỉ khi publication là `FRESH` hoặc `STALE` thì client mới đọc ba route còn lại. Nếu một route trong nhóm đó trả trạng thái khác, UI giấu toàn bộ thân. Id experiment phải khớp `^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$` trước khi gọi chi tiết.

Notice cố định `research-not-connected` không còn gắn vào summary. Summary không cộng số research.

## Cách hiển thị availability

| Trạng thái | Cách hiện |
|---|---|
| Đang tải | Khối Research ở trạng thái loading. |
| 401 | `401. Phiên đăng nhập đã hết hạn.` Không có experiment. |
| 403 | `403. Không có quyền đọc research hoặc module chưa được bật.` |
| Mất mạng | `API research không phản hồi.` |
| HTTP khác, gồm 404 | Mã HTTP và câu không có dữ liệu publication. |
| `MISSING` | `MISSING`. Research Lab chưa được nối. Không có bảng experiment. |
| `UNREADABLE` | `UNREADABLE`. Không hiện experiment hay báo cáo. |
| `INCONSISTENT` | `INCONSISTENT`. Hash không khớp. Không hiện nội dung. |
| `UNSUPPORTED_VERSION` | `UNSUPPORTED_VERSION`. Không hiện nội dung. |
| `FRESH` | Nhãn info, không màu xanh. Câu: FRESH chỉ nghĩa health nguồn là OK. |
| `STALE` | Nhãn vàng. Giữ `lastRunAt` và `lastBarAgeMs` nguồn. |
| `PRESENT_UNTIMED` | Dòng riêng cho object lab: nguồn lab không có timestamp. |

`EXPERIMENT_NOT_APPROVED`, `INSUFFICIENT_EVIDENCE`, `NOT_A_PERFORMANCE_CLAIM` và `sampleWarning` hiện nguyên văn. Chuỗi PnL và profit factor có chú thích là giá trị nguồn, không phải khuyến nghị mua hoặc bán. Health `OK` không dùng nhãn thành công. Không có nút chạy experiment, phê duyệt, hay gửi lệnh.

Chuỗi có `\`, `/`, `..`, `api_key`, `frozen_lab_result`, hoặc `paper_ops_state_dir` bị bỏ trước khi vẽ. Khối `development` chỉ lấy hai cờ bằng chứng hiệu suất.

## Bằng chứng

| Lệnh | Kết quả |
|---|---|
| `npx tsx src/modules/second-brain/research-view.smoke.ts` | PASS, `research view ok`, exit 0. Gồm publication hợp lệ, `MISSING`, `STALE`, `UNREADABLE`, `INCONSISTENT`, `UNSUPPORTED_VERSION`, 401, 403, mất mạng, loading |
| `npx tsx src/modules/second-brain/trading-routes.smoke.ts` | PASS, `trading routes ok`, exit 0. Summary không còn notice research cố định. Fixture notice còn nguyên |
| `npm run build` trong `client/admin` | PASS, exit 0. `tsc --noEmit` và `tsc -p tsconfig.scripts.json --noEmit` nằm trong script. Vite built in 15.99s. Warning CSS `font-size` là cảnh báo minify có sẵn, không phát sinh từ màn này |

Trình duyệt local `http://127.0.0.1:5174/second-brain/overview`: sổ giao dịch vẫn là một khối (290 giao dịch đã ghi, 289 kiểm thử, 1 CSV, 0 đồng bộ sàn, 236 nhận định, 138 giả lập, 29 risk từ chối). Khối Research Lab / Paper Ops đứng riêng và báo `HTTP 404`, không điền số 0 và không nói lab đã nối.

`GET` publication qua `127.0.0.1:5174` và `127.0.0.1:58903` đều trả 404. Process API đang chạy chưa nạp route mới. Nhiệm vụ này không khởi động lại process đó và không ghi root publication vào cấu hình.

## Chưa chạy

- Toàn bộ regression admin ngoài `npm run build` và hai smoke Second Brain: **NOT_RUN**.
- Test backend Personal Intelligence và database kiểm thử: **NOT_RUN**. UI không sửa API.
- Nghiệm thu `FRESH` / `STALE` trên process API đang chạy: **BLOCKED**. Route chưa có trên process đó.

## Trước khi deploy

- Khởi động lại API local với `PersonalIntelligence:ResearchPublication:Root` trỏ tới publication local. Không ghi path này vào cấu hình production.
- Người xem cần ADMIN, `personal_intelligence.trading.run`, hoặc `personal_intelligence.research.read`. Claim research chưa được gán vào role tenant.
- Sau khi API mới phục vụ, xem lại Overview: `FRESH` hoặc `STALE` hiện experiment và paper; `MISSING` vẫn nói chưa nối.
- Không deploy từ nhiệm vụ này.

**GO** cho nghiệm thu local sau khi API local được khởi động lại với root publication. **NO-GO** cho deploy.
