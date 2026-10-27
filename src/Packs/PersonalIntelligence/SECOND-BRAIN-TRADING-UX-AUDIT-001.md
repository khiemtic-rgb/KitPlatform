# SECOND-BRAIN-TRADING-UX-AUDIT-001

Ngày: 2026-10-17. Chỉ đọc. Không sửa code, migration, cấu hình hay dữ liệu.

Phạm vi đã mở trên trình duyệt, không bấm lưu, import, phân tích mới hay phê duyệt paper: `http://127.0.0.1:5174` với phiên `pi-ui-approve`. API validation đang là `http://127.0.0.1:51222`. Các báo cáo vận hành cũ được đối chiếu với `TradingPages.tsx`, `ExchangePages.tsx`, `trading.api.ts`, `TradingLayout.tsx` và `TradingDeskStore`.

## 1. Hiện trạng đã triển khai

Second Brain là một nhóm màn trong admin hiện có, không phải sản phẩm riêng. Người dùng đã đăng nhập thấy tám tab ngang hàng: Tổng quan, Sổ giao dịch, Nhập dữ liệu, Nhật ký quyết định, Phân tích, Rủi ro & Paper, Kết nối sàn, Tài khoản sàn. Vào `/second-brain` thì bị chuyển thẳng tới Tổng quan.

Những gì đã có là một sổ quan sát, một vòng ghi quyết định từ form, một cổng paper sau khi risk cho phép, và một cổng đọc Bybit chưa được nối với sổ. Chưa có một nơi trả lời “hôm nay tôi nên xem gì”.

FACT, ngay lúc audit:

- Tổng quan: 201 giao dịch, 201 FIXTURE, 0 nguồn khác, 159 quyết định, 96 paper execution, 16 risk từ chối. Cảnh báo “Chỉ có dữ liệu kiểm thử FIXTURE”. Dòng thiếu dữ liệu: “200 giao dịch chưa có phí. Chưa có dữ liệu sàn thực.”
- Phân tích: form yêu cầu người dùng tự gõ luận điểm, symbol, giá, tin cậy, trần rủi ro, reflection, policy và dán CSV. Lịch sử hiện OperationKey và cơ sở `Fixture`.
- Nhật ký: các dòng đầu là `Operational repeat…`, `Runtime http-veto…`, `UI local fixture thesis`. Loại nhận thức là `HYPOTHESIS`.
- Tài khoản sàn: “Chưa có kết nối để hiển thị.” Không có PnL trên màn này.

INFERENCE: 159 quyết định và 96 paper là kết quả các lần chạy thử và test, không phải nhật ký của một người đang giao dịch. Bằng chứng là luận điểm mang tên operational/runtime.

NOT VERIFIED: một phiên Bybit thật, một file CSV lịch sử của chủ tài khoản, và cảm nhận của người chưa từng thấy các màn này ngoài phiên audit.

## 2. Bản đồ màn hình, API và dữ liệu

| Màn | Route | Component | Người dùng thấy | API | Nguồn |
| --- | --- | --- | --- | --- | --- |
| Tổng quan | `/second-brain/overview` | `TradingOverviewPage` | Sáu số đếm, nguồn gần nhất, danh sách thiếu | `GET .../trading/summary` | Đếm `trade`, `pi_decision`, `trading_execution`, `pi_operation` trong `pack_personal_intelligence` |
| Sổ giao dịch | `/second-brain/ledger` | `TradingLedgerPage` | Bảng fill, lọc, drawer “không phải một quyết định” | `GET .../accounts`, `GET .../trades` | `trade` + `trading_account`. Tài khoản hiện có là `fixture` / `fixture-account` |
| Nhập dữ liệu | `/second-brain/import` | `TradingImportPage` | CSV, xem trước, xác nhận | `POST .../imports/preview`, `POST .../imports` | Ghi `trade`. Không tạo decision |
| Nhật ký | `/second-brain/journal` | `TradingJournalPage` | Bảng luận điểm, drawer bối cảnh/bằng chứng/hành động/kết quả | `GET .../decisions`, `GET .../decisions/{id}` | `pi_decision` và các bảng vòng lặp |
| Phân tích | `/second-brain/agent` | `TradingAgentPage` | Form dài, thẻ “Khuyến nghị” sau khi chạy, lịch sử operation | `POST .../loops`, `GET .../operations` | Người dùng cung cấp CSV và luận điểm. Server ghi decision + operation |
| Rủi ro & Paper | `/second-brain/risk` | `TradingRiskPage` | Bảng operation và bảng paper | `GET .../operations`, `GET .../paper-executions` | `pi_operation`, `trading_execution` mode `PAPER` |
| Kết nối sàn | `/second-brain/exchange` | `TradingExchangePage` | Form Bybit, hướng dẫn key chỉ đọc, bảng kết nối | `GET/POST/DELETE .../exchanges` | `exchange_connection`. Hiện không có dòng |
| Tài khoản sàn | `/second-brain/account` | `TradingExchangeAccountPage` | Snapshot số dư nếu có kết nối | `GET .../exchanges/{id}/account` | `exchange_account_snapshot`. Không suy PnL |

Quyền: mọi tab sổ và phân tích cần `personal_intelligence.trading.run` ở API. Paper cần thêm `personal_intelligence.trading.approve`. Kết nối cần `personal_intelligence.exchange.connect`; xem snapshot cần `exchange.read` hoặc `exchange.connect`. Module `personal_intelligence` tắt thì layout chặn cả nhóm. `pi-ui-run` không phê duyệt paper và không quản lý key. `PI_B` không vào được module.

Trống và lỗi đã có câu chữ: sổ trống, chưa có quyết định, chưa có kết nối, phí “Chưa ghi nhận”, số dư “Chưa được sàn cung cấp”. Fixture được gắn nhãn vàng. Kết nối sàn từ chối key sai bằng câu của sàn, và không tạo dòng thành công khi kiểm tra thất bại. Điều đó đã được chứng minh với key giả trong báo cáo exchange; audit này không nhập key.

## 3. Đã chạy, chưa chạy, chưa xác minh

Đã chạy trên UI và PostgreSQL validation, theo các báo cáo trước cộng lần đọc này:

- Đếm tổng quan từ database, có cờ chỉ-fixture.
- Đọc sổ, mở chi tiết một fill như quan sát.
- Xem trước và ghi CSV. Import không biến fill thành quyết định.
- Chạy phân tích từ CSV người dùng dán, giữ OperationKey khi thử lại, từ chối payload khác.
- Phê duyệt paper sau risk cho phép. User chỉ có quyền run bị 403. Live bị từ chối.
- Màn kết nối từ chối key giả và không lưu. Chưa có snapshot sàn.

Chưa hoạt động như một sản phẩm dùng hằng ngày:

- Không đọc lịch sử lệnh từ Bybit. Kết nối chỉ đọc key, loại tài khoản và số dư Unified.
- Không biến số dư hay sổ thành khuyến nghị. `TradingAgent` nhận luận điểm và giá do người dùng gõ, rồi trả `recommend_only`.
- `pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot`, `funding_payment` vẫn chỉ có schema.
- Policy trong chi tiết quyết định không có nội dung rule gắn với decision.

Chưa xác minh: kết nối Bybit thật, số dư thật, CSV lịch sử thật, và việc một người mới tự tìm được đường mà không đọc báo cáo kỹ thuật.

## 4. Vấn đề UX quan trọng nhất

### P0 — Mở vào một bảng đếm, không có việc tiếp theo

Tác động: người dùng không biết bắt đầu bằng kết nối sàn, nhập lịch sử, hay viết phân tích.

Bằng chứng: `/second-brain` chuyển tới Tổng quan. Màn đó chỉ có sáu `Statistic` và một cảnh báo fixture. Không có nút “Kết nối sàn”, “Xem một giao dịch”, hay “Việc nên làm hôm nay”. Kết nối sàn nằm ở tab thứ bảy.

### P0 — “Phân tích” bắt người dùng làm phần thông minh

Tác động: hệ thống trông như một agent, nhưng người dùng phải soạn luận điểm, reflection, policy, giá và dán lại CSV đã có trong sổ.

Bằng chứng: form Phân tích có 14 nhãn, gồm “Luận điểm”, “Ghi chú reflection”, “Đề xuất policy”, “CSV quan sát”. Cảnh báo trên màn nói kết quả phụ thuộc CSV đã cung cấp. Thẻ sau khi chạy tiêu đề “Khuyến nghị” nhưng không hiện câu giải thích của agent, không liệt kê fill nào là bằng chứng, và không nói điều gì chưa biết ngoài một chuỗi `unknowns`. Lịch sử chỉ còn OperationKey, `Fixture`, và đã/chưa paper.

`TradingAgent` còn ghi rationale dạng “Based on N observed fill(s). This does not create a decision”, trong khi vòng `POST .../loops` lại tạo decision từ thesis người dùng gõ. UI không đưa câu rationale đó ra. Người dùng có thể tưởng máy đã quyết.

### P0 — Hai khái niệm tài khoản không gặp nhau

Tác động: nối sàn xong vẫn không thấy sổ hay phân tích đổi. Nhập CSV vẫn phải chọn `fixture-account`.

Bằng chứng: sổ và import dùng `trading_account` (`fixture` / `fixture-account`). Kết nối sàn dùng bảng `exchange_connection` và màn Tài khoản sàn đang trống. Không có liên kết trên UI giữa một snapshot Bybit và một dòng sổ. Import mặc định nguồn `FIXTURE`, nên một CSV thật vẫn có thể bị dán nhãn kiểm thử nếu người dùng không đổi ô chọn.

### P1 — Nhật ký là nhật ký của máy thử, không phải của một quyết định đang theo dõi

Tác động: 159 quyết định không giúp ôn lại một thương vụ. Người dùng không đi được từ một fill sang bối cảnh của chính fill đó.

Bằng chứng: bảng nhật ký mở đầu bằng `Operational repeat` và `Runtime http-veto`. Cột loại là `HYPOTHESIS`, một từ của mô hình dữ liệu. Drawer có đủ ô bối cảnh, bằng chứng, hành động, kết quả, experience, policy, nhưng không có đường từ sổ sang drawer, và policy thường là “Chưa ghi nhận”.

### P1 — Rủi ro là danh sách khóa, không phải lời giải thích

Tác động: “16 risk từ chối” trên tổng quan không nói vì sao, và không dẫn tới việc sửa.

Bằng chứng: Rủi ro & Paper lặp lại OperationKey và “Cho phép/Từ chối”. Không hiện trần notional, đòn bẩy hay độ tin cậy đã dùng. Paper là một nút ở màn Phân tích, chỉ sau khi đã chạy xong và có quyền approve. Người chỉ có quyền run không thấy giải thích tại sao nút biến mất.

### P2 — Ngôn ngữ vận hành lọt ra sản phẩm

Tác động: OperationKey, FIXTURE, HYPOTHESIS, `recommend_only` và tên tab “Phân tích” không nói được việc của người dùng.

Bằng chứng: Phân tích hiện nguyên `Key: f43d6f7d…` và nút “Thử lại cùng OperationKey”. Đó là cơ chế idempotency, không phải một việc giao dịch. Layout khi thiếu quyền chỉ nói thiếu `personal_intelligence.trading.run`, dù cửa vào còn nhận quyền exchange.

## 5. Trải nghiệm nên có

Một luồng, tên là **Phiên làm việc**. Mở Second Brain không vào bảng đếm. Màn đầu trả lời ba câu, bằng dữ liệu đang có:

1. Tài khoản nào đang nối, số liệu mới đến lúc nào, và cái gì sàn không trả.
2. Sổ đang là kiểm thử, lịch sử đã nhập, hay vẫn trống.
3. Một việc tiếp theo duy nhất.

Thứ tự việc, hiện thành các bước trên cùng một trang, không phải tám tab phải đoán:

1. **Nối để đọc.** Bybit, Testnet hoặc Mainnet, key chỉ đọc. Kết quả là trạng thái kết nối, không phải một màn form tách khỏi phần còn lại.
2. **Chọn sự thật của sổ.** Nếu chưa có lịch sử, mời nhập CSV và bắt buộc chọn nhãn “lịch sử của tôi” hoặc “dữ liệu kiểm thử” trước khi ghi. Không mặc định FIXTURE. Import xong dẫn tới đúng các dòng vừa vào, không tạo quyết định.
3. **Chọn một việc để xem.** Một symbol, một ngày, hoặc một cụm fill. Hệ thống xếp các fill đó thành bối cảnh và nói rõ đây là quan sát. Nó không tự viết luận điểm.
4. **Người dùng xác nhận một giả thuyết.** Ô ngắn: định làm gì, điều gì làm giả thuyết sai, độ tin cậy. Reflection và policy không bắt buộc ở bước này.
5. **Risk nói một câu.** “Chặn vì quy mô vượt trần” hoặc “Trong trần đã đặt”. Nếu được phép và người có quyền approve, nút paper đứng ngay đó, với câu “đây là ghi giả lập, không gửi sàn”.
6. **Nhật ký của chính việc đó.** Sau khi xác nhận, mở đúng quyết định vừa tạo: quan sát nào, giả thuyết nào, risk nào, paper nào, cái gì chưa biết. Không thả người dùng vào danh sách 159 dòng test.

Tổng quan cũ trở thành một dải trạng thái trên Phiên làm việc: số fill, tỷ lệ còn thiếu phí, số giả thuyết đang mở, số paper. Các số không còn là trang chủ.

Nhiều sàn sau này là một danh sách tài khoản trong cùng phiên, mỗi tài khoản có trạng thái nối, độ mới và sổ của nó. V1 chỉ cần một tài khoản Bybit và một sổ. Chưa tách thêm màn cho từng sàn.

Nên hướng dẫn hoặc làm giúp: chọn nhãn dữ liệu, lấy fill đã lưu thay vì dán lại CSV, tạo OperationKey ở server và giấu khỏi UI, mở đúng quyết định vừa ghi.

Nên bỏ khỏi mặt người dùng: nút thử lại OperationKey, mã key, từ HYPOTHESIS như nhãn chính, và việc bắt gõ reflection với policy trước khi có giả thuyết. Nên trì hoãn: PnL, đọc lịch sử lệnh từ sàn, bộ nhớ dài hạn, và mọi thứ đặt lệnh thật. Những việc đó chưa có nguồn dữ liệu trung thực.

## 6. Một đợt cải tiến và cách nghiệm thu

Chỉ một đợt: **Phiên làm việc Second Brain**. Không tách thành từng màn vá nút.

Phạm vi được đụng: `client/admin/src/modules/second-brain`, API `personal-intelligence/trading` hiện có, và bản sao nhãn trên các DTO đang trả về. Không đụng auth Pharmacy, Famixa, NOVIXA, `DashboardService`, manifest prod/family/content/local, hay process cổng 5290.

Xong khi tất cả các điều sau đúng trên `kit_pi_validation`, với user `pi-ui-approve`, và không cần đọc báo cáo kỹ thuật:

- Mở `/second-brain` thấy việc tiếp theo, không thấy sáu ô đếm là nội dung chính.
- Khi chưa nối sàn, việc tiếp theo là nối Bybit chỉ đọc. Tài khoản sàn trống không còn là một tab người dùng phải tự tìm.
- Import không còn mặc định FIXTURE. Một CSV ghi là lịch sử không bị gắn nhãn kiểm thử.
- Chọn các fill đã có trong sổ rồi xác nhận giả thuyết, không phải dán lại CSV và không phải gõ reflection hay policy để bấm được.
- Sau khi chạy, màn hình nói fill nào là bằng chứng, risk vì sao, và cái gì chưa biết. Không hiện OperationKey.
- Paper vẫn là bước riêng, vẫn cần quyền approve, vẫn không có live.
- `pi-ui-run` không phê duyệt được. `PI_B` vẫn bị chặn module.
- Nhật ký mở từ chính giả thuyết vừa tạo. Danh sách test cũ không phải thứ đầu tiên người dùng phải hiểu.
- Không có số PnL nào được tính từ số dư hoặc từ sổ fixture.

## 7. Giới hạn cần nói thẳng trên sản phẩm

- Sổ hiện tại là dữ liệu kiểm thử trong PostgreSQL, không phải lịch sử sàn.
- Kết nối Bybit, khi có, chỉ chứng minh key đọc được và, với Unified Account, đọc số dư. Nó không đồng bộ lệnh và không chứng minh hiệu suất.
- Phân tích không đọc thị trường live và không tự nghĩ ra khuyến nghị. Nó ghi lại giả thuyết của người dùng và đối chiếu trần rủi ro.
- Paper là một dòng giả lập trong database.
- “Chưa ghi nhận” nghĩa là hệ thống không có trường đó, không phải là số không.
