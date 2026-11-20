# SECOND BRAIN AGENT V1 — ACCEPTANCE REMEDIATION PLAN

Ngày: 2026-10-30. Nguồn đối chiếu: commit `5f29e1bdf184364ad646e6cd4e622cdb0d0b3278`. Không deploy, không restart, không sửa env, không gọi API trả phí, không thu hồi token, không đổi dữ liệu.

## Kết luận điều tra

`PARTIALLY_ACCEPTED` không được gỡ bằng cách chỉ thêm API key. Intent thiếu mô hình là sự thật, nhưng sau khi mô hình trả lời, bước lưu task vẫn từ chối tenant `SECOND_BRAIN` vì neo sổ paper bị khóa cứng `KIT_MKT`. Publication `MISSING` vì root đang rỗng. Ba refresh token còn hiệu lực đến 2026-11-06 và không có API thu hồi nếu không cầm token thô.

## 1. Nguyên nhân từng điểm nghẽn

### LLM và intent

`modelConfigured` là `BusinessIntentModelOptions.IsConfigured`. Cả bốn điều kiện phải đúng: `Enabled`, `BaseUrl`, `ApiKey`, `Model` đều có giá trị sau khi trim.

Section cấu hình: `PersonalIntelligence:BusinessIntent`. Trên env của service đó là:

- `PersonalIntelligence__BusinessIntent__Enabled`
- `PersonalIntelligence__BusinessIntent__BaseUrl`
- `PersonalIntelligence__BusinessIntent__ApiKey`
- `PersonalIntelligence__BusinessIntent__Model`

Bốn khóa này không có trong env đang chạy. Mặc định `Enabled=false` và ba chuỗi rỗng, nên `GET /api/personal-intelligence/intent/status` trả `modelConfigured=false`. `POST /intent` đi vào nhánh `Unavailable` trước khi mở HTTP. Không có handler nào chạy và `pi_task` không tăng. Đây đúng contract: không có fallback theo từ khóa.

### Task

Không có `POST` tạo task. `PaperTaskController` chỉ `GET /api/personal-intelligence/tasks` và `GET /api/personal-intelligence/tasks/{taskId}`.

Đường duy nhất ghi `pi_task` là `POST /api/personal-intelligence/intent` khi kế hoạch chọn capability `personal_intelligence.trading.paper.evaluate`, handler trả `approval_required`, risk được phép, và `PaperDecisionRecorder.RecordProposedAsync` thành công. Sau đó `PaperTaskStore.ContinueProposedAsync` mới insert.

`POST /api/personal-intelligence/trading/loops` tạo decision và paper execution nhưng không tạo task. Bản ghi `ACCEPTANCE-20261030-paper` đi theo đường này, nên `pi_task` vẫn 0 dù paper đã `accepted`.

`RecordProposedAsync` chỉ tìm neo:

- `tenant_code = 'KIT_MKT'`
- `exchange_code = 'PAPER_LEDGER'`
- `external_account_id = 'KIT_MKT-PAPER-LEDGER'`
- person gắn với user đang đăng nhập

Operator đang chạy thuộc `SECOND_BRAIN`, account `paper-fixture`. Câu SQL đó không khớp. Khi intent tới bước lưu, nó ném `Tenant này không có neo sổ paper nội bộ. Decision không được lưu.` Task không được tạo. Cấu hình LLM không đổi điều kiện này.

### Research publication

`PersonalIntelligence:ResearchPublication:Root` được bind từ `PersonalIntelligence__ResearchPublication__Root`. Runtime trả `publicationConfigured=false` và `readError.code=MISSING` với message `Publication is missing.`

`FileResearchPublicationReader` trả đúng message đó khi root rỗng hoặc thư mục không tồn tại, trước khi tìm `manifest.json`. Không có API ingest. Mỗi lần đọc mở file trên đĩa. Không có gói publication `research-publication/v1` trong cây release.

### Refresh token

Bảng `second_brain_host.refresh_token` chỉ có `token_hash`, `user_id`, `expires_at`, `revoked_at`. Token thô không được lưu. Hash là SHA-256 hex. Login insert một dòng. `POST /api/auth/refresh` thu hồi hash được trình và cấp cặp token mới. `POST /api/auth/logout` thu hồi hash được trình. Không có API liệt kê hay thu hồi theo user.

Access token sống 8 giờ. Refresh sống đến `thời điểm cấp access + 7 ngày` (tức khoảng 7 ngày 8 giờ sau lúc login). Logout không vô hiệu access JWT đã cấp.

## 2. Cấu hình LLM tối thiểu

Wire protocol đã có trong `CatalogGroundedIntentInterpreter`, không phải SDK của một hãng:

- `POST {BaseUrl}/chat/completions`, hoặc `BaseUrl` nếu nó đã kết thúc bằng `/chat/completions`
- Header `Authorization: Bearer {ApiKey}`
- Body: `model`, `temperature=0`, `response_format.type=json_object`, `messages` system+user
- Phản hồi phải có `choices[0].message.content` là JSON, có `goal` khác rỗng và `bindings[]` với `capabilityId` nằm trong catalog

Catalog hiện có ba capability:

- `personal_intelligence.research.publication.read`
- `personal_intelligence.trading.desk.summary.read`
- `personal_intelligence.trading.paper.evaluate`

Không có adapter Anthropic, Gemini, hay header `api-key` kiểu Azure. Một endpoint chỉ dùng được khi nó nói đúng giao thức trên.

Cấu hình thử nghiệm tối thiểu, chỉ sau khi được duyệt và chỉ trong env của `second-brain-agent`:

| Khóa | Giá trị đề xuất |
|---|---|
| `Enabled` | `true` |
| `BaseUrl` | endpoint OpenAI-compatible nội bộ, loopback hoặc mạng đã duyệt |
| `Model` | tên model của endpoint đó, bắt buộc trả JSON |
| `ApiKey` | chuỗi không rỗng. Với endpoint nội bộ đây là khóa cục bộ, không đưa vào git |

Chưa đặt các khóa này. Chưa tạo API key.

Giới hạn sẵn có: connect timeout 5 giây, timeout toàn request 3 phút, không retry, body phản hồi trên 200000 ký tự hoặc content trên 20000 ký tự bị coi là `unreadable` và không chạy handler. Lỗi mạng thành `model_unreachable`. HTTP lỗi thành `model_http_status_{code}`. Không có `max_tokens`, không có ngân sách, không có bộ đếm chi phí.

## 3. Chi phí

Một `POST /intent` đã qua cổng cấu hình là đúng một request chat completions. Không retry nên lỗi không nhân request, trừ khi người gọi gửi lại.

Không có trần token đầu ra. Provider trả phí có thể tính đến giới hạn completion của chính họ. Trần 20000 ký tự ở phía agent áp dụng sau khi đã nhận body, nên không chặn hóa đơn.

Đề xuất giới hạn, theo thứ tự:

1. Dùng endpoint nội bộ không tính phí API. Chi phí là máy chạy model, không phải invoice.
2. Nếu vẫn muốn provider trả phí, source phải thêm `max_tokens` nhỏ và một trần số lần gọi trước khi đưa key vào env. Việc đó là một release mới, không làm trong lượt này.
3. Không bật key trả phí trên env hiện tại khi code chưa có trần.

Không ước lượng số tiền vì model và bảng giá chưa được chọn, và code không ghi usage.

## 4. Contract và test plan cho task

Trạng thái task, tính lại mỗi lần đọc từ decision và số execution `PAPER`:

| Trạng thái | Điều kiện |
|---|---|
| `waiting_approval` | decision `proposed` và 0 execution paper |
| `completed` | decision `accepted` và đúng 1 execution paper |
| `blocked` | decision `rejected` và 0 execution paper |
| `unverified` | lệch liên kết operation/decision, số execution khác 1 khi đã accepted, hoặc status khác ba giá trị trên |

Ba bước trong workflow `second-brain.paper-proposal.v1`: `evaluate` → `decide` → `settle`.

Insert ban đầu ghi `waiting_approval`, rồi `Verify` ghi đè theo bảng trên. `task_key` là `operation_key`. Không có API chuyển trạng thái trực tiếp. Phê duyệt hoặc từ chối đi qua:

- `POST /api/personal-intelligence/trading/decisions/{id}/paper`
- `POST /api/personal-intelligence/trading/decisions/{id}/reject`

Test đầu cuối chỉ có ý nghĩa sau hai thay đổi đã duyệt: LLM cấu hình, và neo sổ không còn khóa `KIT_MKT` đối với host `SECOND_BRAIN`.

1. `GET /intent/status` trả `modelConfigured=true` và tên model. Không in API key.
2. `POST /intent` với một câu nói rõ symbol, side, quantity, price, `mode=paper`, và đủ `maxNotional`, `maxLeverage`, `minConfidence`, `leverage`, `confidence`. Kỳ vọng `decisionPersisted=true`, có `taskId`, task `waiting_approval`, một dòng `pi_task`.
3. `GET /tasks/{taskId}` đọc lại cùng trạng thái.
4. `POST .../decisions/{id}/paper` rồi `GET /tasks/{taskId}` trả `completed`, đúng 1 execution `PAPER`.
5. Một câu `mode=live` không tạo order live và không gọi sàn.
6. Câu thiếu slot không được điền số thay. Handler không chạy.
7. Câu rỗng và câu dài hơn 4000 ký tự không gọi model.
8. Không dùng bản ghi `ACCEPTANCE-20261030-paper` làm task hồi tố. Đường `/loops` không tạo task, và không có lệnh gắn task vào decision cũ.

Nếu chỉ bật LLM mà chưa sửa neo `KIT_MKT`, test 2 phải được coi là xác nhận lỗi đã biết: decision không lưu, `pi_task` vẫn 0. Đó chưa phải nghiệm thu task.

## 5. Research publication

Hợp lệ khi đồng thời:

- Root là thư mục tồn tại, process `sbagent` đọc được.
- `manifest.json` có `schemaVersion` đúng `research-publication/v1`, `publicationId`, `publisher`, `publishedAt`.
- Bốn file và bốn hash SHA-256 trong manifest khớp byte:
  - `lab/LAB_RESULT.json` / `labResultSha256`, có mảng `experiments[]` với `experiment_id` dạng token.
  - `paper/PAPER_OPS_STATUS.json` / `paperStatusSha256`.
  - `paper/PAPER_RESEARCH_REPORT.json` / `paperReportSha256`, có `strategies[]`.
  - `paper/PAPER_OPS_HEALTH.json` / `paperHealthSha256`, trường `health` là `OK` hoặc `STALE`.
- `health=OK` → availability `FRESH`. `health=STALE` → `STALE`. Giá trị khác → `UNREADABLE`.
- Hash lệch → `INCONSISTENT`. Schema khác → `UNSUPPORTED_VERSION`.

Reader chặn path traversal: chỉ nhận đúng các tên file trên. Không có bước import vào database. Cập nhật là thay file trên đĩa. Process đã nhận root thì lần đọc sau thấy file mới, không cần restart chỉ vì đổi nội dung file. Đổi env root thì cần restart, và restart là một phê duyệt riêng.

Nguồn dữ liệu phù hợp là bản xuất thật của publisher `research-lab-v1`. Không soạn JSON giả để làm `FRESH`. Release hiện không chứa bộ file đó. Khi chưa có bản xuất, giữ root rỗng và chấp nhận `MISSING`.

Quy trình khi có bản xuất đã duyệt:

1. Copy nguyên thư mục publication vào một path riêng trên VPS B, owner `sbagent`, không ghi vào `/opt/second-brain-agent` đang chạy.
2. Đối chiếu bốn hash với manifest trước khi trỏ root.
3. Thêm `PersonalIntelligence__ResearchPublication__Root` và restart service trong một cửa sổ đã duyệt.
4. `GET /api/personal-intelligence/research/publication` phải hết `MISSING`. `live_orders` và `exchange_orders_sent` trong file phải được đọc như dữ liệu lab, không được làm host gửi lệnh.

## 6. Ba refresh token

Đọc lúc lập kế hoạch, chỉ `revoked_at` và `expires_at`, không đọc hash. User `22222222-2222-4222-8222-222222222222`.

| Còn mở | Còn hạn | `expires_at` |
|---|---|---|
| có | có | 2026-11-06 04:22:21 +07 |
| có | có | 2026-11-06 04:23:57 +07 |
| có | có | 2026-11-06 05:14:13 +07 |

Dòng thứ tư, hết hạn 2026-11-06 18:00:42 +07, đã `revoked_at` trong nghiệm thu 10:00 cùng ngày.

Công thức trong source: `expires_at = lúc cấp access + 8 giờ + 7 ngày`. Suy ra ba token được cấp khoảng 2026-10-29 20:22, 20:23 và 21:14 +07. Access JWT của ba lần đó đã hết sau 8 giờ. Phần còn dùng được là refresh, đến các mốc ở bảng. Không có cột `created_at` nên thời điểm cấp là suy ra, không phải timestamp lưu sẵn.

Không có API revoke an toàn nếu không có token thô. Thu hồi sau này, nếu được duyệt, là một `UPDATE` đặt `revoked_at` cho các dòng còn `NULL` của user này. Không cần in hash. Tác động: ba phiên không refresh được nữa. Access JWT đã hết hạn từ sáng 2026-10-30 nên không có phiên access dài ngày nào của ba token đó còn sống. Logout không đụng access JWT mới cấp trong 8 giờ gần nhất. Lượt này không thu hồi.

## 7. Thay đổi cần có, tách loại

Cấu hình, chưa làm:

- Bốn khóa `PersonalIntelligence__BusinessIntent__*` khi đã chọn endpoint.
- `PersonalIntelligence__ResearchPublication__Root` chỉ khi đã có thư mục publication thật.

Source, release sau, chưa làm:

- Bỏ khóa cứng `KIT_MKT` / `PAPER_LEDGER` / `KIT_MKT-PAPER-LEDGER` trong `PaperDecisionRecorder`, thay bằng neo paper của host `SECOND_BRAIN` hoặc cấu hình tường minh. Không tạo tenant `KIT_MKT` trên database agent để lách.
- Thêm `max_tokens` và trần số lần gọi trước khi dùng provider trả phí.
- Không thêm endpoint tạo task song song. Entry point thiết kế là `/intent`.

Dữ liệu, chưa làm:

- Không sửa hoặc xóa `ACCEPTANCE-20261030-paper`.
- Không bịa file publication.
- Thu hồi ba refresh token chỉ bằng lệnh đã duyệt, không in hash.

## 8. Phê duyệt riêng

1. Chọn endpoint mô hình nội bộ, hoặc chấp nhận một release source có trần token trước provider trả phí.
2. Ghi API key vào env service và restart. Restart làm đổi PID.
3. Sửa neo paper trong source, build release mới, rồi mới nghiệm thu task. Không gộp với thay đổi Pharmacy.
4. Đặt publication root chỉ sau khi có bản xuất lab.
5. Thu hồi ba refresh token bằng SQL có điều kiện `revoked_at IS NULL`.

## Thứ tự triển khai và tiêu chí nghiệm thu

Chưa thực hiện các bước này.

1. **Neo task.** Sửa recorder để host `SECOND_BRAIN` lưu được decision paper mà không cần tenant `KIT_MKT`. Nghiệm thu: test host chứng minh `SECOND_BRAIN` ghi được `pi_task`, và một tenant khác không được ghi vào sổ của nó. Chưa deploy cho đến khi có phê duyệt release.
2. **LLM không tính phí, hoặc LLM có trần.** Env bốn khóa, restart đã duyệt. Nghiệm thu: `modelConfigured=true`, một `POST /intent` paper đủ slot trả `taskId` và `waiting_approval`, không có socket ra sàn, `WorkerEnabled` vẫn false. Nếu chưa sửa neo, bước này dừng ở `decisionPersisted=false` và không được gọi là xong task.
3. **Vòng đời task.** Duyệt paper của task vừa tạo. Nghiệm thu: `GET /tasks/{id}` là `completed`, đúng một execution `PAPER`, live utterance không tạo execution khác `PAPER`/`OBSERVED`.
4. **Publication.** Chỉ khi có bản xuất thật. Nghiệm thu: hết `MISSING`, hash khớp, không phát sinh lệnh sàn.
5. **Refresh token.** Thu hồi ba dòng còn mở. Nghiệm thu: còn 0 dòng `revoked_at IS NULL` của user này, health vẫn 200, không in hash.

Tiêu chí chung cho mọi bước đụng service: PID mới chỉ xuất hiện ở bước restart đã duyệt, hash binary đúng artifact của release đó, port `127.0.0.1:5095`, database `second_brain_agent_v1` port 5433, không có kết nối 5432, `novixa_prod` hoặc `103.200.23.229`.
