# NOVIXA-VIDEO-TENANT-BOUNDARY-F03-CLOSURE-MATRIX-01

Ngày: 2026-10-14

Tài liệu này chỉ gom bằng chứng đã có để đóng F-03. Không phải audit mới, không thay thế các báo cáo nguồn, không phải đặc tả triển khai thứ hai.

Không kết nối database. Không chạy SQL. Không sửa code, schema, migration, manifest, hay các báo cáo nguồn. Không commit, không push, không deploy.

## Nguồn đã dùng

| Báo cáo | Worktree đã đọc |
|---|---|
| `NOVIXA-VIDEO-TENANT-BOUNDARY-WP1-IMPLEMENT-01-REPORT.md` | `E:\KitPlatform-wt-content-tenant-boundary-wp1-01` |
| `NOVIXA-VIDEO-TENANT-BOUNDARY-WP1-ARCHITECT-REVIEW-01-REPORT.md` | `E:\KitPlatform-wt-content-tenant-boundary-wp1-01` |
| `NOVIXA-VIDEO-TENANT-BOUNDARY-WP2A-OWNERSHIP-AUDIT-01-REPORT.md` | `E:\KitPlatform-wt-content-tenant-boundary-wp2a-audit-01` |
| `NOVIXA-VIDEO-TENANT-BOUNDARY-WP2B-OWNERSHIP-DECISION-ANALYSIS-01-REPORT.md` | `E:\KitPlatform-wt-content-tenant-boundary-wp2b-analysis-01` |
| `NOVIXA-VIDEO-TENANT-BOUNDARY-WP3A-F03-IMPLEMENTATION-SPEC-01-REPORT.md` | `E:\KitPlatform-wt-content-tenant-boundary-wp3a-01` |
| `NOVIXA-VIDEO-TENANT-BOUNDARY-WP3A-LEGACY-DATA-INVENTORY-PLAN-01-REPORT.md` | `E:\KitPlatform-wt-content-tenant-boundary-wp3a-01` |

Định nghĩa F-03 được các báo cáo WP-2A, WP-2B và WP-3A dẫn từ `NOVIXA-VIDEO-TENANT-BOUNDARY-REMEDIATION-01-AUDIT.md` trên worktree release. Ma trận này dùng đúng lời các báo cáo đó đã trích, không đọc lại audit để mở finding.

Trạng thái đã xác nhận, lấy từ baseline của WP-2A, WP-2B và WP-3A: commit WP-1 là `121553e4b654f501df50b3217df2bcbf76795b3a`, parent `ec452bda2143756ad0c09843548e7f5f94fd1f5a`. Commit đó không sửa `video_production`, `video_visual_contract`, hay `KitVideoVisionRepository`.

Ba nhãn trạng thái trong ma trận:

| Nhãn | Nghĩa trong tài liệu này |
|---|---|
| `PASS` | Điều kiện đóng của dòng đã đủ bằng chứng. Không còn việc F-03 cho dòng đó. |
| `BLOCKED` | Hướng xử lý đã có trong đặc tả, nhưng thiếu dữ liệu kiểm kê hoặc thiếu thay đổi đã được chứng minh. Không được điền bằng suy đoán. |
| `OPEN` | Quyết định phụ chưa được chấp nhận, hoặc việc làm phụ thuộc quyết định đó. |

## 1. Phạm vi F-03 và các quyết định đã khóa

### Phạm vi

F-03, theo lời WP-3A trích audit remediation: `video_visual_contract` bị ghi đè theo `(production_id, shot_code)`. Hướng sửa tối thiểu trong audit: sau khi chốt khóa sở hữu, production phải thuộc tenant, upsert chỉ chạy khi production thuộc caller, và không dùng `ON CONFLICT DO UPDATE` trên khóa không có tenant. Test gốc của audit: tenant B không đổi `contract_json` hay `updated_at` của A; GET attempts của production A trả 404; hai tenant tạo hai contract cùng `shot_code` chỉ khi mỗi production thuộc tenant đó.

Đối tượng của F-03 là `video_production` của KIT Video Engine và `video_visual_contract` trỏ tới production đó. WP-2B đã tách đối tượng này khỏi `series_build` và khỏi Production OS / `video_production_shot`. Shot không có `production_id` nối về engine.

WP-1 đóng F-01, đóng F-02 cho ba repository contract/prompt, và đóng F-06 ở lớp ứng dụng cho ba bảng event của shot. Cả báo cáo triển khai và architect review đều ghi F-03 còn mở. Review không chạy lại test. Kết quả test WP-1 không phải kết quả runtime của F-03.

### Bốn quyết định đã khóa

Product Owner đã chấp nhận mô hình A. Bốn câu sau khớp đề xuất trong đặc tả WP-3A và không được mở lại:

1. `video_production` thuộc một tenant.
2. `production_code` như `EP01` được phép trùng giữa các tenant.
3. `video_visual_contract` kế thừa ownership của production.
4. Project, universe và canon không tự động thuộc tenant. F-04 là quyết định riêng.

Đặc tả WP-3A và kế hoạch kiểm kê, tại thời điểm viết, vẫn ghi mô hình A chưa được Product Owner phê duyệt. Ma trận này ghi nhận sự chấp nhận đến sau các báo cáo đó. Các mục **DESIGN PROPOSAL** trong đặc tả là giải pháp dự kiến cho bốn câu đã khóa. Chúng chưa được viết thành migration hay diff.

Chấp nhận mô hình A không trả lời câu WP-2B về việc engine production có phải “Production execution” của ADR-03 hay không. Câu đó không còn là điều kiện đóng F-03. Không dùng câu đó để mở work package mới.

### Ngoài F-03

Các báo cáo nguồn giữ nguyên ranh giới sau. Đóng F-03 không đóng các finding này.

| Việc | Finding | Trong F-03 |
|---|---|---|
| `video_project`, `video_universe`, visual system, asset canon, visual universe | F-04 | Không gán tenant |
| `compile-image-prompt` và asset theo `project_code` | F-05 | Không sửa |
| Ba bảng event contract/prompt của shot | F-06 | Không mở lại. Phần đọc SQL trực tiếp `payload_json` vẫn là rủi ro role database |
| Thêm `tenant_id` cho `video_production_event` hoặc event canon | F-07 | Không thêm cột. Insert event production, trong đặc tả, chỉ đứng sau guard owner. Việc đó không đóng F-07 |
| Unique `idempotency_key` của attempt và job, unique shot version, unique fingerprint execution | F-08 | Không drop, không thêm tenant vào các unique này |
| Suy owner từ `series_build_id`, project code, universe code, hoặc `actor` | Không có quy tắc đã phê duyệt | Cấm trong đặc tả và trong kế hoạch kiểm kê |

## 2. Ma trận phát hiện F-03

| Mã | Phát hiện | Nguyên nhân | Giải pháp dự kiến | Bằng chứng hiện có | Trạng thái | Điều kiện đóng |
|---|---|---|---|---|---|---|
| F03-S1 | Chưa chọn được owner của `video_production` | Schema không có cột tenant. Shot và `series_build_id` không suy ra một tenant | Mô hình A | WP-2A kết luận chưa chọn được. WP-2B để Product Owner quyết. Product Owner đã chấp nhận bốn câu ở mục 1 | `PASS` | Đã đạt. Không mở lại bốn câu đó |
| F03-S2 | F-04 đến F-08 bị kéo vào cùng quyết định owner | Cùng chữ “production” và cùng project seed `FAMIXA` | Giữ ranh giới mục 1 | WP-2A, WP-2B, WP-3A và quyết định đã khóa cùng loại project, universe, canon khỏi owner của production | `PASS` | Đã đạt. Work package F-03 sau này không sửa các finding đó |
| F03-F1 | `video_production` không có chỗ ghi owner | Migration 333 không có `tenant_id`. Migration 386 không thêm cột này. Không có câu SQL từ cột hiện có trả về một tenant cho mọi `production_id` | Cột `tenant_id UUID NULL` trên `video_production`, FK tới `public.tenants`, trigger cùng họ migration 385. Không suy owner. Thứ tự DDL ở mục 8 của đặc tả WP-3A | WP-2A mục A. WP-3A mục “Hiện trạng” và mục 1. Chưa có file migration | `BLOCKED` | Quyết định phụ unique và không-backfill đã chốt; kiểm kê catalog khớp commit WP-1 hoặc lệch được ghi lại mà không gán owner; migration mới tồn tại đúng thứ tự đã chốt; chụp trước/sau của kế hoạch kiểm kê khớp nếu lần chạy migration được phê duyệt riêng |
| F03-F2 | Upsert ghi đè visual contract của người không phải owner | `UpsertContractAsync` là `INSERT ... ON CONFLICT (production_id, shot_code) DO UPDATE`, không có `tenant_id`. Hai POST `visual/attempts` và `visual/generate` không có `ProductionTenantActionFilter` | `INSERT ... SELECT` khi `video_production.id = @ProductionId AND tenant_id = caller`. `ON CONFLICT DO UPDATE` chỉ kèm `EXISTS` production của caller. Unique contract `(production_id, shot_code)` giữ nguyên | WP-2A mục B, câu SQL. WP-3A mục 4. Không có test committed cho bảng này | `BLOCKED` | Tenant khác không đổi `contract_json`, `prompt_json`, `fingerprint`, `updated_at` của production đã có owner, và không thêm attempt trên production đó. Hai tenant, hai production, cùng `shot_code`, cho hai dòng contract. Ghi vào hàng chưa có owner chờ F03-O1 |
| F03-F3 | Attempt và idempotency không theo owner của production | `video_keyframe_attempt` lọc theo `production_id` hoặc theo key, không join owner. `GetByIdempotencyAsync` trả attempt trước khi xét production của request. `GenerateAsync` trả attempt cũ khi key đã tồn tại | Mọi đọc/ghi attempt trong repository vision kèm production owned. Key của production thuộc tenant khác thì không trả DTO. Không sửa `uq_video_keyframe_attempt_idem` | WP-2A mục B. WP-3A mục 5 và bảng kiểm soát. Va unique toàn cục thuộc F-08 | `BLOCKED` | GET attempts, decide, revalidate, đọc ảnh, `i2v-ready` của production thuộc tenant khác không trả QA, prompt, hay file. Key của tenant khác không trả DTO của tenant đó. Unique idempotency không đổi. Hàng chưa có owner chờ F03-O1 |
| F03-F4 | List và get production trả theo id, không theo tenant | `ListVideoProductionsAsync` và `GetVideoProductionAsync` không có predicate tenant. List sắp `updated_at DESC` | List chỉ hàng `tenant_id = caller`. Get production của tenant khác thì không trả DTO | WP-2A, WP-2B mục 1, WP-3A mục 6 | `BLOCKED` | List của caller không chứa production của tenant khác. Get id của tenant khác không trả hàng đó. Cách đối xử với hàng chưa có owner chờ F03-O1 |
| F03-F5 | Transition, shot state, job, story và motion authorize bằng `production_id` | Các action này không có `ProductionTenantActionFilter`. SQL đã đọc lọc theo id. Biết id là điều kiện ghi hiện tại | Guard `id = @ProductionId AND tenant_id = caller` trước đọc/ghi trên các đường trong bảng kiểm soát của đặc tả. Không đổi unique của job, story, take | WP-3A bảng kiểm soát, cột đường và cột khóa là method đã đọc. Cột điểm kiểm tra là đề xuất | `BLOCKED` | Tenant khác không đổi state, `run_status`, shot state, số event của production đã có owner, và không nhận payload job, story, hay video của production đó. Không thêm override hoặc take. Hàng chưa có owner chờ F03-O1 |
| F03-F6 | Đường ensure gộp mọi tenant vào một `EP01` | `EnsureProductionAsync` thấy `(project, production_code)` thì trả hàng cũ. Unique hiện tại là `(project_id, production_code)`. UI gắn `FAMIXA` / `FAMILY_A` / `EP01` và không gửi series build | Đặc tả mục 3: tìm theo `tenant_id = caller`, không trả hàng NULL hoặc hàng tenant khác, không cập nhật `series_build_id` của hàng đã có | WP-2B mục 1, nhãn INFERENCE cho hai tenant cùng một id. WP-3A xác nhận lại hành vi ensure và unique 333 | `OPEN` | Chỉ đóng sau khi quyết định “mỗi tenant tạo production riêng khi gắn `EP01`” được chốt, rồi ensure làm đúng câu đã chốt. Chưa chốt thì không viết nhánh này |
| F03-F7 | Not-found của production có thể thành 400 hoặc 409 | `ProductionResourceNotFoundException` kế thừa `InvalidOperationException`. Một số `catch` của ensure, transition, shot, attempt và `JobConflictOrBad` trả 400 hoặc 409. `PreflightAsync` có đường bắt lỗi thành HTTP 200 `BLOCKED` | Bắt not-found trước các catch đó. Ba tình huống không owned dùng một message cố định | WP-3A mục 7 và bảng kiểm soát. Message giống nhau cho ba tình huống chưa được chấp nhận | `OPEN` | Chỉ đóng sau khi quyết định message 404 được chốt, rồi các catch đã đọc trả đúng dạng đã chốt và body không chứa payload của hàng không owned |
| F03-F8 | Mọi hàng production hiện tại đều không xác định được owner | Không có cột owner. Kế hoạch kiểm kê chưa chạy. Mọi số đếm là chưa biết | Không backfill là một phương án trong đặc tả, chưa được chấp nhận. Cấm `SET tenant_id` từ series, project, universe, hoặc actor | WP-2A UNKNOWN. WP-3A UNKNOWN. Kế hoạch kiểm kê mục 7: trước migration, mọi production và mọi contract là `owner_undetermined` vì cột chưa có | `BLOCKED` | Báo cáo kiểm kê chỉ đọc đã có, đúng nhãn của kế hoạch, và chính sách hàng cũ đã được chốt. Không điền số trong ma trận này |
| F03-F9 | Chưa có fact PostgreSQL cho F-03 | WP-2A: không có test committed cho `video_visual_contract`. Ma trận test trong đặc tả là đề xuất, chưa viết | Fact trên database disposable đã được phê duyệt, cùng kiểu cổng loopback của gate WP-1, cho các tiêu chí đã chốt | Báo cáo WP-1 ghi test child-contract. Architect review không chạy lại test đó. Không suy kết quả ấy sang F-03 | `OPEN` | Fact F-03 đã chạy trên database disposable được phê duyệt và khớp các tiêu chí nghiệm thu đã có hiệu lực. Không dùng database ứng dụng chung để chứng minh |

`KitVideoVisionService.DecideAsync` có route HTTP ngoài action `visual/decide` hay không vẫn là UNKNOWN trong đặc tả. Action `visual/decide` đã đọc gọi `KitVideoPixelService.DecideAsync` và nằm ở F03-F3. Không mở audit route mới. Nếu method còn lại được gọi, cùng điều kiện owner của F03-F3 áp vào lời gọi đó.

Các route `visual/compile`, `qa`, `diagnose`, `revision`, `i2v-package`, `visual/provider`, và `story/validate`, `story/gate`, `story/impact` không có SQL tới `video_production` trong phần đã đọc. `AnalyzeBytesAsync` không đọc attempt hay production. Chúng không thêm tiêu chí nghiệm thu.

## 3. Quyết định phụ còn OPEN

Bốn quyết định đã khóa không nằm trong bảng này.

Đặc tả WP-3A nói chưa chấp nhận các câu phê duyệt thì không có SQL. Thời điểm chốt dưới đây là cổng trước thay đổi tương ứng. Kiểm kê chỉ đọc không chọn các câu này và không gán owner.

| Mã | Quyết định còn mở | Phải chốt trước khi |
|---|---|---|
| F03-O1 | Chính sách không backfill legacy. Hàng `tenant_id IS NULL` bị từ chối đọc/ghi cho đến một quyết định sau, hay được xử lý theo quy tắc khác. Không suy owner trong lúc chờ | Viết migration và viết predicate cho hàng chưa có owner |
| F03-O2 | Mỗi tenant tạo production riêng khi gắn `EP01` trên project seed `FAMIXA` | Sửa `EnsureProductionAsync` và đổi nghĩa nút gắn `EP01` |
| F03-O3 | Cùng một message `404` cho production không tồn tại, thuộc tenant khác, hoặc chưa có owner. Message không nói “unowned” và không nói “thuộc tenant khác” | Sửa cách trả lỗi của các đường ở F03-F7 |
| F03-O4 | Unique owned `(tenant_id, project_id, production_code)` | Viết mục unique của migration. Khóa khác thì viết lại mục 2 của đặc tả trước, không viết file SQL theo khóa cũ của đề xuất |
| F03-O5 | Unique unowned `(project_id, production_code)` cho hàng `tenant_id IS NULL` | Cùng cổng với F03-O4. Chưa được chấp nhận |

Năm câu này là một cổng cho gói triển khai: không bắt đầu sửa code, schema, migration, hay manifest của F-03 khi còn câu chưa chốt. Kiểm kê chỉ đọc có thể được phê duyệt môi trường song song, vì kế hoạch kiểm kê không chọn unique và không backfill.

## 4. Blocker kiểm kê database

Kế hoạch `NOVIXA-VIDEO-TENANT-BOUNDARY-WP3A-LEGACY-DATA-INVENTORY-PLAN-01-REPORT.md` chưa được chạy. Mọi ô dưới đây cố ý để trống. Không có số thay thế.

Chưa có phê duyệt bốn mục mà kế hoạch yêu cầu trước câu đọc: host và cổng, `current_database()` phải đọc lại ở câu 0, role chỉ `SELECT`, mục đích ghi rõ là kiểm kê legacy F-03. WP-1 đã từ chối cổng 5432 và database `kitplatform` cho test gate disposable. Kế hoạch kiểm kê không đảo quyết định đó và không chọn database thay thế.

| Thông tin còn thiếu | Giá trị |
|---|---|
| Host, cổng, tên database, role chỉ đọc, văn bản mục đích | Chưa có phê duyệt |
| Kết quả câu 0: `current_database`, cổng, địa chỉ server, `current_user` | Chưa chạy |
| Catalog cột và unique trên database được duyệt có khớp commit WP-1 hay không | Chưa biết |
| `production_rows` | Chưa biết |
| Phân bố `project_code`, `universe_code`, `production_code` | Chưa biết |
| `series_build_id` NULL và `series_build_id` có UUID | Chưa biết. UUID không phải owner |
| `production_code` lặp trên nhiều `project_id` | Chưa biết. Không kết luận trùng tenant |
| Số nhóm vi phạm unique `(project_id, production_code)` | Chưa biết. Đặc tả chỉ cho viết migration khi nhóm này rỗng trên database được duyệt |
| Id production có universe thuộc project khác | Chưa biết |
| `series_link_missing`, series có tenant NULL, series có tenant | Chưa biết. Series có tenant vẫn không phải owner của production |
| `create_actor_blank`, `create_actor_present` | Chưa biết. Không group theo nội dung actor |
| `contract_rows`, nhóm contract trùng `(production_id, shot_code)`, contract không có production | Chưa biết |
| Contract không có shot state, shot state không có contract | Chưa biết. Không phải tenant |
| `attempt_rows`, attempt không có production, attempt không có contract | Chưa biết |
| Số idempotency key không rỗng và số key phân biệt | Chưa biết. Lệch catalog là ghi nhận F-08, không sửa unique trong F-03 |
| `child_count` của shot state, production event, job, story graph, continuity override, asset usage, scene master, shot package, keyframe snapshot, runway take | Chưa biết |
| Danh sách id production, id contract, hash contract, `updated_at` của chụp trước | Chưa có. Chưa có migration để chụp sau |
| Content migration runner có bọc mỗi file trong một transaction hay không | Chưa biết. Mã runner không có trong worktree đặc tả. Thứ tự bước 3 đến 6 của đặc tả không phụ thuộc câu này |

Nếu catalog lệch commit WP-1, hoặc câu đếm `(project_id, production_code)` có dòng, đặc tả yêu cầu dừng và không suy owner. Đó là điều kiện an toàn đã có, không phải tiêu chí mới.

## 5. Tiêu chí nghiệm thu cuối cùng

Chỉ những tiêu chí có bằng chứng rủi ro trực tiếp trong các báo cáo nguồn, và không phụ thuộc một quyết định phụ còn OPEN. Tiêu chí gắn với quyết định OPEN nằm ở mục 3; chúng chỉ có hiệu lực sau khi câu đó được chốt.

1. Một `video_production` đã có owner thì owner đó là một tenant. Hàng chưa có owner không bị gán tenant từ `series_build_id`, project code, universe code, hay actor.
2. Cùng `production_code` không bị cấm chỉ vì tenant khác đã dùng mã đó. Hình unique cụ thể chờ F03-O4 và F03-O5.
3. Tenant này không ghi đè visual contract của production đã có owner là tenant khác: không đổi `contract_json`, `prompt_json`, `fingerprint`, `updated_at`.
4. Đọc attempt của production thuộc tenant khác không trả QA, prompt, hay ảnh của hàng đó. Hai tenant có hai contract cùng `shot_code` khi mỗi contract nằm trên production của tenant đó. Unique contract vẫn là `(production_id, shot_code)`.
5. List production của caller không chứa production của tenant khác. Get id của tenant khác không trả hàng đó.
6. Tenant khác không đọc payload và không ghi transition, shot state, job, story graph, continuity override, hay runway take của production đã có owner. Unique của các bảng đó không đổi.
7. `video_project`, `video_universe`, visual system, và canon không được thêm `tenant_id` bởi thay đổi F-03.
8. F-05 không sửa. Ba bảng event đã đóng ở WP-1 không mở lại. Không thêm `tenant_id` vào `video_production_event` để gọi là đã đóng F-07. Không sửa unique F-08.
9. Fact kiểm các tiêu chí trên chạy trên database disposable đã được phê duyệt. Kết quả test lịch sử của WP-1 không thay fact này.

Chưa phải tiêu chí nghiệm thu cho đến khi quyết định phụ tương ứng được chốt:

- Từ chối đọc/ghi mọi hàng `tenant_id IS NULL`, vì F03-O1 còn mở.
- Nút gắn `EP01` tạo một production mới cho từng tenant, vì F03-O2 còn mở.
- Cùng một chuỗi `404` cho không tồn tại, tenant khác, và chưa có owner, vì F03-O3 còn mở.
- Đúng hai unique một phần `(tenant_id, project_id, production_code)` và `(project_id, production_code)`, vì F03-O4 và F03-O5 còn mở.

Không thêm tiêu chí RLS. Đặc tả không bật RLS. Guard ứng dụng không chặn role SQL đọc thẳng bảng. Rủi ro đó đã được ghi là phần còn mở của F-06 và F-07, không phải tiêu chí đóng F-03.

## 6. Các bước còn lại

Thứ tự theo phụ thuộc. Bước sau không bắt đầu khi bước trước còn thiếu đối với chính việc đó.

1. Chốt F03-O1 đến F03-O5. Không viết SQL, không sửa code, không sửa migration, không sửa manifest trước cổng này.
2. Phê duyệt môi trường chỉ đọc theo bốn mục của kế hoạch kiểm kê, rồi chạy đúng các câu trong kế hoạch đó. Ghi kết quả bằng các nhãn đã có. Không gán owner. Không điền các ô mục 4 của ma trận này trước khi có kết quả.
3. Nếu catalog không khớp commit WP-1, hoặc đã có hai hàng cùng `(project_id, production_code)`, dừng. Không viết migration theo mục 8 của đặc tả.
4. Sau bước 1 và bước 3 đạt: viết một gói triển khai đúng các quyết định đã khóa, đúng các quyết định phụ vừa chốt, và đúng các phần đặc tả WP-3A không bị các câu đó sửa. Gói đó không thuộc tài liệu này.
5. Chạy fact PostgreSQL của các tiêu chí đã có hiệu lực trên database disposable được phê duyệt. Không dùng kết quả test WP-1.
6. Nếu một lần chạy migration được phê duyệt riêng: đối soát chụp trước và chụp sau theo kế hoạch kiểm kê. Lệch thì đối soát fail. Không `UPDATE` để làm số khớp.

## 7. Điều kiện kết thúc

Khi mọi tiêu chí nghiệm thu ở mục 5 đã đạt, kể cả tiêu chí nào ở mục 3 đã trở thành tiêu chí có hiệu lực sau khi được chốt, F-03 được đóng.

Không mở lại F-03 trừ khi có bằng chứng mới làm thay đổi tính đúng đắn hoặc an toàn của bốn quyết định đã khóa, hoặc của một tiêu chí đã đạt.

Đóng F-03 không mở work package để kiểm lại cùng các phát hiện này, và không đóng F-04, F-05, F-06, F-07, F-08.
