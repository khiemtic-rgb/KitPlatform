# SECOND-BRAIN-V1 — Workspace UX redesign

Ngày: 2026-10-17. Không commit.

## Trải nghiệm đã đổi

Người dùng mở `/second-brain` vào một phiên làm việc, không còn tám tab ngang hàng và không còn sáu ô đếm làm màn hình đầu.

Trên cùng một trang, dải trạng thái nói bốn việc: sàn đang nối hay chưa, sổ nào đang dùng và đó là dữ liệu kiểm thử hay lịch sử của người dùng, số giao dịch cùng dữ liệu còn thiếu, và đúng một bước tiếp theo.

Sáu bước nằm dưới dải đó:

1. Đọc tài khoản sàn, với câu nói rõ Bybit chỉ đọc số dư Unified khi sàn hỗ trợ và không kéo lịch sử lệnh.
2. Chọn sổ thật trong database. Nếu nhập CSV, phải chọn "Lịch sử giao dịch của tôi" hoặc "Dữ liệu kiểm thử" trước khi ghi. Không còn mặc định kiểm thử.
3. Chọn giao dịch đã lưu theo mã và khoảng ngày. Không dán lại CSV.
4. Xác nhận một nhận định ngắn: dự định hoặc điều muốn kiểm chứng, điều khiến nhận định sai, và mức tin cậy nếu muốn. Không bắt phản tư hay quy tắc.
5. Xem dữ liệu đã dùng, nhận định đã xác nhận, lý do rủi ro cho phép hoặc từ chối, và điều chưa biết. Câu chữ nói đây là đối chiếu giao dịch đã lưu với ngưỡng rủi ro, không phải phân tích thị trường live.
6. Nếu rủi ro cho phép, giả lập là một bước phê duyệt riêng và được nói rõ là mô phỏng, không gửi lệnh lên sàn. Nhật ký mở đúng nhận định vừa ghi.

Sổ, nhật ký, rủi ro và kết nối sàn vẫn mở được. Overview, import và biểu mẫu phân tích đầy đủ vẫn còn route, nhưng không còn điều khiển lối vào.

## FACT — đã kiểm trên trình duyệt local

UI `http://127.0.0.1:5174`, API kiểm thử `http://127.0.0.1:60615`, database `kit_pi_validation` cổng 5433. Process API cổng 5290 (pid 11404) không bị dừng.

- `/second-brain` chuyển tới `/second-brain/session`. Với `pi-ui-approve` và chưa có kết nối, bước tiếp theo là "Nối Bybit để đọc tài khoản" và nói việc này không kéo lịch sử lệnh.
- Hai lựa chọn nguồn hiện ra, không lựa chọn nào được chọn sẵn. Nút xem trước và ghi bị khóa cho đến khi chọn. Một dòng CSV được ghi với lựa chọn "Lịch sử giao dịch của tôi". Thông báo: hợp lệ 1, trùng 0, lỗi 0, đã ghi 1. PostgreSQL xác nhận `source = IMPORT`, `source_type = IMPORT` cho `WS-UX-20261017-1`.
- Bảng giao dịch hiện dòng đó với nhãn "Lịch sử của tôi" và các dòng cũ với nhãn "Dữ liệu kiểm thử". Form nhận định không có ô CSV.
- Xác nhận nhận định trên dòng IMPORT tạo kết quả có dữ liệu quan sát `BTCUSDT BUY 0.01 @ 100` và câu "Không có dữ liệu thị trường live".
- Mức tin cậy 0.2 với ngưỡng 0.55 bị từ chối bằng câu "Mức tin cậy thấp hơn ngưỡng đã đặt." Không có nút giả lập.
- Mức tin cậy 0.8 được cho phép, kèm quy mô 1 so với trần 1000, đòn bẩy 1 so với trần 2. Hộp xác nhận nói đây là mô phỏng và không gửi lệnh lên sàn. Sau phê duyệt, phiên hiện "Đã ghi giả lập."
- Nhật ký mở `/second-brain/journal?decision=389441e8-21cc-4586-91b1-2e70def68318`. Dòng đầu là nhận định vừa ghi. Ngăn chi tiết có nguồn "Lịch sử của tôi", bằng chứng `WS-UX-20261017-1`, kết quả "Đã ghi giả lập. Không gửi lệnh lên sàn.", và phản tư, kinh nghiệm, quy tắc đều "Chưa ghi nhận".
- Gửi lại cùng payload từ trình duyệt trả cùng `decisionId`. Đổi nội dung trả HTTP 409. Tìm theo luận điểm chỉ thấy một nhận định.
- `pi-ui-run` chỉ có quyền chạy. API paper trả 403. Giao diện hiện "Tài khoản này được chạy phân tích nhưng chưa được phê duyệt giả lập" và không có nút phê duyệt.
- `pi-ui-b` trên `PI_B` thấy câu module chưa được bật. `GET /summary` trả 403 `Module is not enabled for this tenant.`
- Không có số PnL, tỷ lệ thắng hay giá trị tài sản suy đoán. Không có nút lệnh live.

## FACT — API, DTO và dữ liệu

- `TradingLoopRequest` thêm `TradeIds`. Khi có danh sách này, vòng lặp đọc các fill thuộc tenant và sổ đã xác thực, không chèn thêm dòng fixture và không bắt CSV.
- `TradingLoopResult` thêm `RiskReasons`. Lý do từ chối được diễn giải bằng tiếng Việt trong outcome.
- Phản tư và quy tắc chỉ được ghi khi người dùng thực sự nhập. Phiên làm việc gửi hai trường này rỗng.
- Nguồn bối cảnh của phiên dùng giao dịch đã lưu lấy theo `source` của các fill. Một nguồn thì ghi nguồn đó. Hai nguồn thì ghi `MIXED`.
- `POST .../decisions/{id}/paper` vẫn không nhận tên người duyệt. Policy phê duyệt kiểm tra danh tính. Sau khi ghi, `pi_operation.result` và `pi_outcome.summary` được cập nhật để nhật ký không còn nói "chưa ghi giả lập".
- Không có migration mới. Ràng buộc nguồn vẫn là `FIXTURE` và `IMPORT`. Không sửa migration 385–391. Không ghi `platform_events`. Không bật RLS.
- Test `FullyQualifiedName!~RuntimeHttpTests`: 54 passed, 0 failed. Gồm vòng lặp trên giao dịch đã lưu, veto không tạo execution, retry không nhân đôi, payload khác trả 409, paper bị chặn khi chỉ có quyền chạy, và `PI_B` bị chặn khi module tắt.
- `RuntimeHttpTests` không chạy lại trong đợt này.

## INFERENCE

- Dải trạng thái "Chưa có" kết nối là đúng với số dòng `exchange_connection` đã là 0 ở lần kiểm trước và với trang không hiện snapshot. Đợt này không đếm lại bảng đó.
- `pi-ui-run` không thấy bước kết nối lên đầu vì tài khoản đó không có quyền thêm kết nối. Bước tiếp theo chuyển sang chọn giao dịch đã lưu. Thẻ kết nối vẫn nói tài khoản không được thêm kết nối.

## NOT VERIFIED

- Không có kết nối Bybit thật và không có số dư thật. Không tuyên bố đã đọc tài khoản sàn hay đã phân tích thị trường.
- Không kiểm tra lại Famixa, NOVIXA, KIT_MKT bằng bộ test của các module đó. Các file nghiệp vụ của chúng không được sửa. `router.tsx` chỉ thêm route phiên làm việc của Second Brain.
- Build production của Vite admin vẫn có lỗi import cũ ở `pos-customer-context.api.ts`. Dev server của phiên này vẫn phục vụ được trang.
- `RuntimeHttpTests` không được chạy lại.

## Giới hạn còn lại

- Kết nối sàn không đồng bộ lịch sử lệnh. Lịch sử vẫn vào sổ bằng CSV hoặc bằng dữ liệu đã lưu.
- Đối chiếu hiện tại dùng giao dịch đã lưu, nhận định của người dùng và ngưỡng rủi ro. Không có market snapshot, funding hay tin tức.
- Giả lập không gửi lệnh. Không có live execution trong giao diện.
- Phản tư, kinh nghiệm và quy tắc không còn là bước bắt buộc. Nếu chưa có, nhật ký ghi "Chưa ghi nhận".
