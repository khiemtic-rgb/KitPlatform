# NOVIXA — Knowledge & Staff: hiện trạng và kế hoạch hoàn thiện

Ngày: 2026-10-31. Loại: audit chỉ đọc và kế hoạch hoàn thiện. Không phải thiết kế module mới.

Nguồn định hướng: [NOVIXA_PRODUCT_MASTER_PLAN.md](../02-product/NOVIXA_PRODUCT_MASTER_PLAN.md) bản 1.1. Bằng chứng vận hành W5: [NOVIXA_W5_PRODUCTION_READINESS_V1.md](./NOVIXA_W5_PRODUCTION_READINESS_V1.md), [NOVIXA_W5_INITIAL_KNOWLEDGE_CATALOG_QUALIFICATION_V1.md](./NOVIXA_W5_INITIAL_KNOWLEDGE_CATALOG_QUALIFICATION_V1.md).

Lượt này không gọi production, không chạy migration, không sửa code. Trạng thái production lấy từ báo cáo sẵn sàng ngày 2026-10-31. Những chỗ báo cáo đó không đo thì ghi UNKNOWN.

---

## 1. Tóm tắt

**Knowledge** có nền ba tầng trong source và đã qua test trên staging: version, pin theo tenant, policy chỉ được thu hẹp, cổng an toàn, vòng đời xuất bản. Production chưa có các bảng đó. Chưa có rule PUBLISHED, chưa có pin, chưa có quyền publish hay approve trong bảng permission. 35 rule seed đều còn cần nguồn. Hệ thống đang đúng khi không gợi ý sản phẩm.

**Staff** có Learning OS trong source và trong manifest production tới file 191: hồ sơ nhân viên, bài học L0–L6, ghi danh, quiz, năng lực, đánh giá có người xác nhận, bậc nghề, ghi nhận, quan sát, hộp thư nội bộ. Checklist mở/đóng ca đang chạy theo chi nhánh và ngày. Việc giao theo nhân viên và ca (`operational_task`) và checklist khóa theo ca POS có trong source và test cô lập, chưa có trên production, và việc giao chưa có màn hình admin.

**Hai nhánh chưa khép vòng.** Bài L5 là markdown tĩnh. Không có tham chiếu `KR_*` từ Learning sang rule tri thức. Điểm gợi ý cho quản lý lấy từ bài đã học, checklist đóng ca và đơn trên ca người đó mở. Kết quả tư vấn không quay lại để sửa rule. AI không được phép tự publish.

Ba việc cần xử lý trước:

1. Giữ cổng tri thức: không publish, không pin, không cấp quyền khi chưa có nguồn và chưa có người có thẩm quyền.
2. Đưa trách nhiệm ca lên production đúng gói đã duyệt (412, 413), rồi mới có màn hình để dùng việc đã giao.
3. Nối bài học và đánh giá với rule đã pin, chỉ sau khi rule đó được publish và nhà thuốc chấp nhận.

---

## 2. Ma trận hiện trạng

Trạng thái dùng ở đây: IMPLEMENTED, PARTIAL, PLANNED, UNKNOWN, BLOCKED. Một dòng có thể kèm TESTED hoặc STAGING VERIFIED khi có bằng chứng đúng phạm vi đó. PRODUCTION VERIFIED chỉ khi báo cáo production đã thấy.

### 2.1 Knowledge

| Năng lực | Trạng thái | Bằng chứng | Đã hoạt động | Còn thiếu |
|---|---|---|---|---|
| Global Knowledge | PARTIAL, TESTED, STAGING VERIFIED, BLOCKED trên production | 35 rule seed từ migration 296–298. Bảng version trong 416. Staging ngày 2026-10-31: 35 version `IN_REVIEW`, 0 publication event. Production: ledger tới 371, chưa có bảng version | Danh tính rule và bản copy chờ duyệt | 0 rule PUBLISHED. `reference_source` không được seed. Qualification: cả 35 rule NEEDS_SOURCE; `KR_OTHER` không đủ điều kiện |
| Pharmacy Knowledge / adoption | IMPLEMENTED, TESTED; vận hành BLOCKED | Bảng adoption, một pin mỗi tenant và `rule_code`. Test cô lập không cho tenant A thấy pin của B. Staging và mô hình production: 0 pin | API pin chỉ nhận version `PUBLISHED` còn hiệu lực | Chưa có version để pin. DEC-015 giao thẩm quyền cho chủ nhà thuốc, chưa gán tài khoản. Quyền `pharmacy.knowledge.approve` chưa có trong permission production |
| Local & Customer Knowledge | PLANNED | Product Master. Không có bảng thu thập có duyệt, không có đường một góp ý thành rule | — | Vòng thu thập, người duyệt, và ranh giới với chuyên môn |
| Rule version | IMPLEMENTED, STAGING VERIFIED | `pharmacy_knowledge_rule_version`, khóa `(rule_code, version)` | Bản `IN_REVIEW` trên staging | Production chưa có bảng |
| Lifecycle | IMPLEMENTED, TESTED, STAGING VERIFIED | `PharmacyKnowledgeApproval.Transition`: DRAFT→IN_REVIEW, IN_REVIEW→PUBLISHED, IN_REVIEW→DRAFT, PUBLISHED→RETIRED, DRAFT→RETIRED, IN_REVIEW→RETIRED. Không có DRAFT→PUBLISHED. Trigger 417 đóng băng nội dung đã publish | Máy trạng thái và sự kiện append-only | UI publish chỉ dùng được khi có `platform.knowledge.publish`. Quyền này chưa được cấp và không nằm trong picker nhà thuốc |
| Clinical review và nguồn | PARTIAL | Hồ sơ qualification là tài liệu, không phải cổng trong API. Publish chỉ đổi `lifecycle_status` và `updated_at` | Phân loại đã viết: không rule nào sẵn sàng xuất bản | API không bắt buộc `reference_source` khác rỗng trước khi publish. 13 cờ review `PENDING_REVIEW` không phải nguồn OTC |
| Tenant operational policy | IMPLEMENTED, TESTED | `RejectPolicyExpansion` chặn thêm keyword, category, hoặc priority nới hơn bản đã pin. Policy lệch version pin bị bỏ | Luật thu hẹp trong code | Chưa dùng tại quầy vì chưa có pin |
| Runtime resolution | IMPLEMENTED trong source W5, TESTED, STAGING VERIFIED | Copilot chỉ đọc pin `APPROVED` và version `PUBLISHED` của đúng tenant. Không global fallback. `KR_OTHER` bị loại | Thiếu pin thì không có danh sách sản phẩm từ rule | Production đang binary cũ, hash `7bbf208b…`, không phải bản W5 `cc4eb7e8…` |
| Safety gate | TESTED; production W5 BLOCKED | `ConsultationSafetyRules` và `PharmacyOtcSuggestionGate`. `stop_sale`, `refer_medical`, `refer_pharmacist` không trả danh sách OTC thường. Test `NovixaOtcSafetyGateIsolatedTests` nằm trong 17 test staging | Cổng cứng và cờ đã publish theo cơ chế hiện có | 26 cờ đang `is_active` sẽ thành `PUBLISHED` trên cột mới khi chạy 416. Đó là giữ hành vi cũ, không phải duyệt 13 cờ đang chờ review |
| Khớp sản phẩm | IMPLEMENTED trong source W5, TESTED | Keyword bắt buộc trên tên, hoạt chất hoặc mô tả. Category không làm điều kiện đủ | Test staging xác nhận sản phẩm chỉ cùng category không được gợi ý | Nếu publish khi keyword còn rộng (`ho`, `cum`, `vitamin`), catalog Xuân Hòa sẽ khớp lệch. Chưa PRODUCTION VERIFIED |
| Audit tri thức | IMPLEMENTED khi W5 chạy | Sự kiện approval và publication append-only, trigger không sửa | Có trên staging sau khi có thao tác; staging sạch còn 0 publication event | Production chưa có bảng sự kiện. Audit này ghi đổi trạng thái, không ghi hiệu quả tư vấn |
| Hiệu quả tri thức | PLANNED | Product Master `SDE-OUT` | — | Không có chỉ số tư vấn thành đơn, bỏ lỡ, hoặc phản hồi để sửa rule |

### 2.2 Staff Enablement

| Năng lực | Trạng thái | Bằng chứng | Đã hoạt động | Còn thiếu |
|---|---|---|---|---|
| Hồ sơ nhân viên | PARTIAL | Bảng `employees`, user, RBAC. Learning roster đọc nhân viên của tenant | Gắn user với nhân viên để tính bằng chứng | Không phải hồ sơ chứng chỉ hành nghề hay chấm công. Mức dùng tại Xuân Hòa: UNKNOWN |
| Onboarding nhân viên quầy | PARTIAL | Migration 177 và bài L0. API `POST me/enroll`. Màn `/people` | Nhân viên có thể vào bài nếu module learning bật | Playbook W0–W4 trong tài liệu khách hàng là onboarding **nhà thuốc**, không phải đường cho nhân viên mới. Tỷ lệ hoàn thành: UNKNOWN |
| Học / training | PARTIAL | Schema `pack_learning` từ 155. Nội dung L0–L6 trong 171–181 và 191. API program, module, quiz, acknowledge. Các file này nằm trong `migration-files.prod.txt` trước 371 | Bài, quiz, xác nhận đã đọc, nội dung tenant sửa được qua API | Mức dùng production: UNKNOWN, lượt này không đo. Nội dung L5 không đọc rule W5 |
| Năng lực | PARTIAL | Bảng competency, roster, mã như `pos_basic`, `tone_of_service`, `grn_receive`, `shift_close` | Mã năng lực gắn nhân viên và được API bằng chứng đọc | Chưa có thang năng lực sinh ra từ rule đã pin |
| Đánh giá | PARTIAL | `GET employees/{id}/evidence`, màn đánh giá. Người có `learning.write` xác nhận điểm. Gợi ý là công thức từ bài học, số ngày đóng checklist, và đơn trên ca do nhân viên đó mở | Có bằng chứng vận hành một phần và bước người xác nhận | Không chấm tình huống tư vấn. Không đọc pin tri thức. Công thức điểm không phải quyết định năng lực đã duyệt |
| Bậc nghề | PARTIAL | API career levels, roster, promotions. Migration 162 | Có bậc và điều kiện thiếu | Chưa nối với tri thức đã pin. Mức dùng: UNKNOWN |
| Ghi nhận | PARTIAL | API recognitions và customer-feedback gần đây. Migration 161, 186 | Quản lý ghi nhận; nhân viên xem badge của mình | Không phát sinh từ kết quả tư vấn đã đo |
| Việc vận hành | IMPLEMENTED trong source, TESTED cô lập, BLOCKED trên production, chưa có UX | Migration 413, không có trong manifest production. Readiness: bảng chưa có trên production. `OperationalTaskService` tạo việc với nhân viên bắt buộc, ca tùy chọn. `CanAssign` chỉ đúng khi có quyền ghi learning. Test cấm mở lại việc `done`. Không có file admin gọi API này | API tạo, liệt kê, đổi trạng thái, ghi chú bằng chứng | Không tự sinh từ ca. Không có màn hình. Production chưa có bảng |
| Ca bán | PRODUCTION VERIFIED cho dữ liệu ca | Readiness: Xuân Hòa 2 ca status 1, 203 ca status 2 lúc 2026-10-31 | Ca POS tồn tại | Ca mở là cửa deploy, không phải bằng chứng trách nhiệm việc đã giao |
| Checklist | PARTIAL | Migration 132 trên production: một run theo chi nhánh, loại, ngày. Mục ghi `checked_by_user_id`. Bằng chứng nhân viên đếm ngày đóng ca qua user đó. Migration 412 thêm `sales_shift_id` và đổi unique index; chưa trong manifest, readiness xác nhận index cũ còn | Tick việc đầu ca và cuối ca; biết ai tick | Chưa khóa theo từng ca POS trên production. Không giao trước một nhân viên chịu trách nhiệm cả checklist |
| Trợ lý nhân viên | PLANNED cho học việc; PARTIAL cho gợi ý điểm | Màn đánh giá gọi gợi ý là công thức bằng chứng, không gọi model xuất bản tri thức. Copilot quầy là API riêng, production chưa chạy binary W5 | Gợi ý điểm để quản lý sửa | Chưa có trợ lý học theo rule đã pin |
| Liên kết dữ liệu vận hành | PARTIAL | Evidence SQL: checklist close trong tháng, chuỗi ngày, số đơn và doanh thu trên ca người đó mở, pulse đánh giá | Ba nguồn trên có trong code | Không có kết quả tư vấn, không có việc 413, không đo quầy Xuân Hòa đã dùng hay chưa |

Ba khoảng trống từng được nêu, đối chiếu code hiện tại:

1. Trách nhiệm với nhân viên và ca: còn. Checklist production ghi người tick và ngày, chưa ghi ca POS. Việc 413 có nhân viên bắt buộc và ca tùy chọn, nhưng chưa lên production và chưa có màn hình.
2. Quản lý công việc vận hành: còn thiếu phần dùng được. API và test đã có. Chưa có hàng đợi việc trên ca, chưa có UI, chưa có trên production.
3. Phụ thuộc quản lý giao và theo dõi: còn. Không có chỗ nào tự tạo việc khi mở ca. Gán việc cần quyền ghi learning. Nhân viên có thể tự ghi danh học; việc vận hành thì không tự xuất hiện.

---

## 3. Khoảng trống

| Khoảng trống | Loại | Hệ quả | Không biến thành |
|---|---|---|---|
| 0 rule PUBLISHED, 0 pin, quyền chưa cấp | Governance / Safety | Quầy không được gợi ý từ rule. Đúng với DEC-006 và DEC-015 | Script publish hoặc gán quyền cho đủ demo |
| 35 rule chưa có nguồn; API không chặn publish khi nguồn trống | Governance / Safety | Người có quyền publish có thể xuất bản bản chưa có trích dẫn | Tự điền nguồn |
| Keyword rộng trên catalog Xuân Hòa | Data | Publish `KR_COUGH` khi còn từ `ho` sẽ gợi ý sai nếu pin | Sửa từ khóa khi chưa có dược sĩ |
| Category nhà thuốc gần như trống; loại kháng sinh theo category không ăn | Data | Cổng category không bảo vệ catalog thật | Sửa hàng loạt category trong nhiệm vụ này |
| 13 cờ review còn PENDING_REVIEW | Governance / Safety | Không dùng làm nguồn gợi ý | Duyệt cờ cho đủ test |
| Local knowledge chưa có vòng duyệt | Business, Workflow | Phản hồi và doanh số có thể bị hiểu nhầm là chuyên môn | Coi một đơn hàng là rule |
| Checklist chưa theo ca trên production | Workflow, Data | Hai ca trong một ngày không tách trách nhiệm | Tự đóng ca để deploy |
| Việc theo nhân viên chưa có UI và chưa có trên production | UX, Technical | API không thành công cụ quản lý | Module task mới. Bảng đã nằm trong Learning |
| Bài L5 và rule W5 không tham chiếu nhau | Workflow | Nhân viên học một quy trình chữ, quầy sau này có thể chạy rule khác | Sinh bài học tự động từ rule chưa publish |
| Đánh giá không có tình huống tư vấn | Business, Data | Điểm kiến thức phản ánh bài học và checklist, không phản ánh quyết định tại quầy | Dùng doanh số thay xác minh chuyên môn |
| Không có vòng phản hồi sửa tri thức | Workflow | Rule sai không có đường từ quầy về review | AI tự sửa hoặc tự publish |
| Mức dùng Learning và checklist tại Xuân Hòa | Data | UNKNOWN | Kết luận nhà thuốc đã hoặc chưa đào tạo |
| Hiệu quả copilot và SLO | Data | UNKNOWN trên production vì binary W5 chưa chạy | Đặt phần trăm mục tiêu |

---

## 4. Mô hình vận hành mục tiêu

Vòng cần khép, khi đủ điều kiện từng chặng:

Tri thức đã xuất bản và nhà thuốc đã pin → bài học và hướng dẫn đúng version đó → nhân viên làm tại ca → đánh giá bằng bài học, checklist, việc và tình huống → phản hồi về chỗ rule hoặc bài không khớp → người có thẩm quyền sửa version mới. Bản cũ đã publish không bị sửa nội dung tại chỗ.

| Vai trò | Việc | Không làm |
|---|---|---|
| Platform | Giữ version, lifecycle, cô lập tenant, sự kiện audit, cổng an toàn, quyền publish và approve tách nhau | Không publish hộ, không pin hộ, không lấy tri thức tenant này cho tenant khác |
| Chủ nhà thuốc | Quyết định nhà thuốc có chấp nhận version đã publish hay không (DEC-015). Cùng người chuyên môn phù hợp xem xét thứ tự theo nhà thuốc (DEC-016) | Không sửa Global Knowledge đã publish. Không thay quyền `platform.knowledge.publish` |
| Dược sĩ phụ trách | Thẩm định chuyên môn khi được nhà thuốc giao trong quy trình review. Nhận ca mà nhân viên không đủ thẩm quyền | Không tự thành người publish nền tảng chỉ vì vai trò chuyên môn |
| Nhân viên | Học bài được gán, làm checklist và việc của mình, dừng và chuyển người khi cổng an toàn yêu cầu | Không duyệt rule, không publish |
| AI Copilot | Trích nhu cầu, gợi ý trong tập rule đã pin và còn hiệu lực, gợi ý điểm đánh giá để người xác nhận | Không publish, không pin, không biến doanh số thành chuyên môn, không bỏ cổng an toàn để bớt thao tác |

Safety floor đứng trước doanh thu. Local và customer knowledge không tự thành tri thức chuyên môn.

---

## 5. Lộ trình hoàn thiện

Không ghi ngày và không ước lượng công. Workstream sau chỉ mở khi acceptance của workstream phụ thuộc đã đạt. CRM Customer App không nằm trong lộ trình này và không phải cửa chờ của K1.

### K1 — Knowledge foundation và governance

Mục tiêu: catalog có nguồn, có người review, và máy trạng thái không bị vòng tránh.

Hiện trạng: schema và lifecycle đã có trên staging. Production chưa có. 0 PUBLISHED. Quyền chưa cấp.

Cần hoàn thiện:

- Giữ 35 rule ở `IN_REVIEW` cho đến khi có nguồn và người thẩm định.
- Chốt, bằng quyết định Product Owner, việc publish có bị chặn khi `reference_source` trống hay không. Hôm nay API không chặn.
- Không cấp `platform.knowledge.publish` và `pharmacy.knowledge.approve` trong im lặng.
- Không duyệt 13 cờ review chỉ để test xanh.

Phụ thuộc: không phụ thuộc Staff.

Acceptance: vẫn không có đường DRAFT → PUBLISHED; sự kiện publication chỉ xuất hiện khi một người có quyền publish chủ động chuyển; `KR_OTHER` không vào copilot; tenant này không đọc pin của tenant khác.

Sang K2 khi: có ít nhất một version được người có thẩm quyền platform publish, với nguồn đã được chấp nhận, và nhà thuốc có chủ thể được phép pin. Chưa có thì K2 dừng ở trạng thái an toàn hiện tại.

### K2 — Adoption và runtime

Mục tiêu: nhà thuốc chỉ chạy version đã publish, đã pin, còn hiệu lực, và policy chỉ thu hẹp.

Hiện trạng: code và 17 test tích hợp trên `novixa_w5_staging` đã chứng minh pin, policy, cổng an toàn và khớp keyword. Production chưa deploy binary `cc4eb7e8…` và chưa apply 414–417.

Cần hoàn thiện:

- Deploy đêm theo gói sẵn sàng đã viết, khi ca đóng và backup restore được. Không làm trong tài liệu này.
- Chủ nhà thuốc pin từng rule sau khi version đã publish. Thứ tự theo nhà thuốc, không theo danh sách cứng.
- Rà keyword rộng và dữ liệu category trước khi pin rule tương ứng.
- Xác nhận copilot trả rỗng khi không có pin.

Phụ thuộc: K1 cho từng rule được pin. Deploy production là việc vận hành đã có điều kiện riêng, không phải quyết định sản phẩm mới.

Acceptance: một tenant pin không làm tenant khác thấy rule đó; policy không thêm keyword hoặc category; `pregnant`, `unconscious`, `chest_pain` không trả danh sách OTC thường; sản phẩm không được gợi ý chỉ vì cùng category.

Sang S3 phần tri thức khi K2 có ít nhất một rule đã pin trên nhà thuốc pilot. S1 và S2 không chờ K2.

### S1 — Năng lực và học

Mục tiêu: nhân viên mới học được việc vận hành trên nội dung đã có, và quản lý đánh giá được bằng chứng học cùng checklist.

Hiện trạng: Learning OS nằm trong manifest production. Bài L0–L6, quiz, năng lực, bậc, ghi nhận, đánh giá có người xác nhận đã có API và màn `/people`. Mức dùng tại Xuân Hòa UNKNOWN.

Cần hoàn thiện:

- Xác minh trên tenant pilot rằng chương trình pharmacy đang bật và một nhân viên hoàn thành được L0 tới bài quầy mà vai trò đó cần. Không suy từ sự có mặt của migration.
- Giữ đánh giá là người xác nhận. Gợi ý điểm chỉ là gợi ý.
- Không viết lại nội dung L5 thành bản sao của rule chưa publish.

Phụ thuộc: module learning của tenant. Không phụ thuộc W5.

Acceptance: một nhân viên thử vào được bài, làm quiz hoặc xác nhận, và một người có quyền ghi xem được bằng chứng học cùng số ngày checklist của đúng nhân viên đó. Không lộ doanh thu cửa hàng cho tài khoản chỉ có `sales.pos`.

### S2 — Việc trong ca và trách nhiệm ca

Mục tiêu: một ca có checklist của ca đó và việc giao cho đúng nhân viên, không chỉ một checklist mỗi ngày cho cả chi nhánh.

Hiện trạng: 132 đang là mô hình ngày. 412 và 413 có trong source, test cô lập `NovixaOpsV2IsolatedTests`, chưa trong manifest production, chưa có UI cho việc.

Cần hoàn thiện:

- Đưa 412 và 413 lên production trong cửa sổ đã duyệt, cùng điều kiện backup. Hai file đã nằm trong gói 411–417 của báo cáo sẵn sàng.
- Thêm lối dùng trên admin cho API việc đã có, trong Learning, không tạo module task khác.
- Giữ ca là tùy chọn ở dữ liệu cho việc không thuộc ca, và bắt buộc trên UX khi việc thuộc ca. Quy tắc bắt buộc này cần Product Owner xác nhận trước khi sửa hành vi.
- Không tự sinh việc hàng loạt khi mở ca, trừ khi Product Owner yêu cầu.

Phụ thuộc: ca POS đang có. Không phụ thuộc publish tri thức.

Acceptance: hai ca cùng chi nhánh cùng ngày có hai checklist; một việc `done` không quay lại `open`; nhân viên không có quyền ghi không gán việc cho người khác; nhân viên chỉ xem việc của mình trừ người có quyền quản lý.

### S3 — Knowledge và Staff

Mục tiêu: rule đã pin trở thành nội dung học và hướng dẫn, và chỗ không khớp quay về review.

Hiện trạng: chưa tích hợp. Learning không chứa `KR_*`. Đây là khoảng trống thật, không phải nền đã nối mà chưa bật.

Cần hoàn thiện:

- Một bài hoặc một thẻ hướng dẫn trỏ tới `rule_code` và version đã pin, không copy nội dung rồi sửa lệch.
- Khi version retire, bài gắn version đó không còn là bài đang dạy.
- Tình huống đánh giá dùng đúng cổng an toàn của rule đó: chuyển dược sĩ hoặc bác sĩ khi cần, không chấm điểm cao vì đã bán.
- Phản hồi “rule không khớp quầy” tạo mục review cho người có thẩm quyền. Không tự sửa bản đã publish và không tự publish bản mới.

Phụ thuộc: K2 cho rule được dạy. S1 cho chỗ chứa bài. S2 cho việc và checklist nếu tình huống gắn với ca.

Acceptance: nhân viên học đúng version đang pin; tenant khác không thấy bài gắn pin của tenant này; một phản hồi không làm đổi lifecycle.

Có nền nhưng chưa nối: hồ sơ nhân viên, bài học, checklist, bằng chứng bán theo ca, sự kiện audit tri thức. Chưa có: liên kết version, tình huống từ rule, vòng sửa tri thức. Không cần ở giai đoạn này: mô hình năng lực sinh bằng máy học, clinic, telemedicine.

### P1 — Sẵn sàng pilot

Mục tiêu: một nhà thuốc chạy được các tình huống mục 6 trên bản đã deploy, với người hỗ trợ khi lỗi.

Hiện trạng: chưa mở. Xuân Hòa đang bán trên Phase 0. W5 chưa lên production.

Cần hoàn thiện: chọn nhà thuốc, nhóm nhân viên, người hỗ trợ, và cách ghi sự cố. Không thêm tính năng cho đủ vẻ.

Phụ thuộc: S1 và S2 cho pilot vận hành nhân sự. K2 chỉ cho tình huống có dùng tri thức. Pilot học việc thuần có thể chạy trước K2.

Acceptance: các tình huống mục 6 được đi hết hoặc được ghi là chưa có trong sản phẩm, không bị đánh dấu đạt khi người thử phải dùng đường tắt ngoài hệ thống.

---

## 6. Tiêu chí nghiệm thu pilot

Không đặt ngưỡng phần trăm. Mỗi tình huống đạt khi đi hết đường trong sản phẩm và để lại bằng chứng.

| Tình huống | Đạt khi | Chưa đạt nếu |
|---|---|---|
| Nhân viên mới nhận ca | Vào được bài L0 hoặc bài ca, thấy checklist của đúng ca sau khi 412 đã chạy, biết việc được giao cho mình | Chỉ có tài liệu giấy, hoặc checklist vẫn chung một ngày cho cả cửa |
| Tìm và dùng tri thức | Với rule đã pin, nhân viên mở được hướng dẫn đúng version và copilot chỉ gợi ý trong tập đó | API suggest của binary cũ được tính là đã dùng tri thức W5 |
| Vượt năng lực | Ca có cờ chuyển dược sĩ, bác sĩ, hoặc dừng bán không hiện danh sách OTC thường, và nhân viên có chỗ ghi đã chuyển | Hệ thống vẫn đưa sản phẩm để khỏi tắc quầy |
| Quản lý giao việc và theo dõi | Tạo việc cho đúng nhân viên, lọc theo ca, thấy `open` / `in_progress` / `done`, việc xong không tự mở lại | Phải sửa cơ sở dữ liệu hoặc gọi API tay vì chưa có màn hình |
| Đánh giá bằng bằng chứng | Người đánh giá thấy bài đã học, ngày checklist, và có bước xác nhận điểm. Nếu có tình huống rule, tình huống đó thuộc version đang pin | Điểm tự điền từ doanh số và không có người xác nhận |
| Tri thức không phù hợp | Một phản hồi tạo được mục đưa vào review. Bản đã publish không đổi nội dung. Bản mới đi lại lifecycle | Phản hồi ghi đè rule hoặc tự thành `PUBLISHED` |

---

## 7. Phụ thuộc với Customer App

Customer App không thuộc phạm vi làm của kế hoạch này.

Trong Product Master, nhánh ổn định Customer App chạy song song và không chờ W5. Source app khách không gọi API publication, adoption, hay Learning. Vì vậy K1, K2, S1, S2 không phải cửa bắt buộc để sửa đăng nhập, hồ sơ, lịch sử mua, loyalty hay đồng bộ dữ liệu của app.

Phụ thuộc có thật, ở mức vận hành chứ không phải cổng tri thức:

- Chat và đặt hàng chỉ ổn khi nhà thuốc có người nhận và xử lý. Đó là ca và trách nhiệm nhân viên (S2), cộng tiêu chí pilot app ở OPEN-006.
- Nhắc thuốc và chăm sóc sau bán cần quy trình nhân viên, chưa cần rule W5, trừ khi nội dung nhắc được lấy từ tri thức chuyên môn. Hôm nay không thấy đường đó. Nếu sau này app tư vấn thuốc, đường đó phải đi qua K2 và cổng an toàn trước khi bật.

Không đưa hoàn thiện Customer App vào K1–S3.

---

## 8. Quyết định cần Product Owner

Các mục dưới chưa được chốt. Tài liệu này không chọn hộ.

| Việc cần quyết | Vì sao còn mở |
|---|---|
| Publish có bắt buộc `reference_source` khác rỗng hay không | Qualification nói cả 35 rule cần nguồn. API hiện không chặn |
| Tài khoản nào được giữ `pharmacy.knowledge.approve` khi tới lúc pin | DEC-015 đã chọn chủ nhà thuốc là chủ thể thẩm quyền, chưa gán tài khoản và chưa cho phép đổi RBAC trong quyết định đó |
| Rule nào nhà thuốc pilot xem trước | DEC-016 giao cho từng nhà thuốc. Chưa có danh sách của Xuân Hòa |
| Việc thuộc ca có bắt buộc chọn ca trên màn hình hay không | Cột `sales_shift_id` đang tùy chọn |
| Có tự tạo việc khi mở ca hay vẫn do quản lý giao | Code hiện chỉ tạo khi người có quyền ghi learning gọi API |
| Bài học nào được phép gắn với rule đã pin, và ai duyệt bài đó | Chưa có liên kết. Gắn bài là quyết định nội dung, không suy ra từ việc bảng learning đã tồn tại |
| Pilot nhân sự chạy trước hay chỉ chạy sau rule đầu tiên được pin | S1 và S2 không phụ thuộc kỹ thuật vào K2. Chọn cửa pilot là quyết định sản phẩm |
| Biên lợi nhuận có hiện cho chủ trong gợi ý hay không | Đã là OPEN-005. Không mở thêm ở đây |

Đã chốt, không hỏi lại: không tự publish; không tự pin; publish nền tảng và approve của nhà thuốc là hai quyền; thứ tự rule không cứng cho mọi tenant; Guided Selling chưa chốt một kiểu màn hình; Customer App là năng lực chiến lược và chưa đủ ổn định để đại trà; an toàn đứng trước doanh thu.
