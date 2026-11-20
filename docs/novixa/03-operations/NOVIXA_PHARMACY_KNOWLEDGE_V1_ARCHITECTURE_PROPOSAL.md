# NOVIXA — Kiến trúc hệ tri thức nhà thuốc V1

Ngày: 2026-11-01. Loại: đề xuất kiến trúc và sản phẩm. Không triển khai code, không migration, không publish, không pin, không cấp quyền.

Phạm vi: năm lớp Global Knowledge, Pharmacy-owned Knowledge, Knowledge Contribution, Knowledge Intelligence, Knowledge Runtime, cùng vòng Staff và trải nghiệm Admin của chủ nhà thuốc.

Nguồn đã đối chiếu, không viết lại: `NOVIXA_KNOWLEDGE_K1_GOVERNANCE_SPEC_V1.md`, `NOVIXA_KNOWLEDGE_UX_BLUEPRINT_V1.md`, `NOVIXA_KNOWLEDGE_STAFF_COMPLETION_PLAN.md`, schema `414`–`417` và `423`, API publication/adoption, Copilot và cổng an toàn trên working tree `E:\KitPlatform`.

Trạng thái đề xuất: `READY_FOR_PRODUCT_REVIEW`.

Governance K1 đã có trên working tree được giữ nguyên. Đề xuất này không mở `DRAFT → PUBLISHED`, không bỏ nguồn `ACCEPTED`, không cho pin `KR_OTHER`, và không cho nội dung nhà thuốc tự tạo đè cổng an toàn.

---

## 1. Hiện trạng

### 1.1 Đã có trên working tree, chưa phải production

| Lớp | Đã có | Chưa có |
|---|---|---|
| Global Knowledge | Version `DRAFT → IN_REVIEW → PUBLISHED → RETIRED`. Không có lối tắt `DRAFT → PUBLISHED`. Nguồn trên version: `UNVERIFIED` / `ACCEPTED` / `REJECTED`. Publish đòi `IN_REVIEW`, nguồn `ACCEPTED`, đủ trường trích dẫn, không phải `KR_OTHER`. Nội dung đã publish bị đóng băng. | 35 rule seed vẫn thiếu nguồn đã chấp nhận. Chưa có rule chuyên môn nào được publish trong lát này. Lịch sử từng lần kiểm chứng nguồn chưa thành bảng riêng. |
| Pharmacy adoption | Pin đúng một version `PUBLISHED`, còn hiệu lực, nguồn `ACCEPTED`, không phải `KR_OTHER`, có ghi chú pin. Thu hồi pin. Policy chỉ được thu hẹp. Publish version mới không đổi pin. | Chưa pin rule thật. Chưa có trạng thái “từ chối áp dụng” kèm lý do. Copilot không đọc lại `source_verification_status`; cổng đó nằm ở API pin. |
| Quyền | Ba claim nền tảng tách nhau: review, verify-source, publish. Chúng không suy từ vai trò ADMIN. Pin: claim `pharmacy.knowledge.approve` hoặc vai trò `ADMIN` của đúng tenant trên token. Tenant lấy từ `tenant_id` của JWT. | Chưa cấp quyền trên tài khoản nào. Working tree vừa ánh xạ ADMIN của tenant sang pin; việc đó đang chờ review, chưa phải quyết định đã chốt tên tài khoản. |
| Runtime | Copilot chỉ đọc pin `APPROVED` đúng version `PUBLISHED` còn hiệu lực của đúng tenant. Không fallback catalog chung. `KR_OTHER` bị loại. `stop_sale`, `refer_medical`, `refer_pharmacist` không trả danh sách OTC thường. | Gợi ý chưa mang mã rule và version ra quầy. |
| Staff | Learning `/people`, bài L0–L6, việc vận hành và checklist là module riêng. | Bài học không có `rule_code`. Không tự tạo enrollment, task hay checklist từ rule. |

Production Staff V1 không có bảng version tri thức. `414`–`417` và `423` không nằm trong manifest production. Đề xuất này không coi production đã chạy Global Knowledge.

### 1.2 Chưa có, và không được suy ra từ bảng hiện tại

- Tri thức do nhà thuốc soạn, version riêng, và kích hoạt riêng.
- Đóng góp có đồng ý, thu hồi, và phạm vi sử dụng.
- Pipeline đánh giá, khử nhận dạng, và đề xuất của AI.
- Nhật ký dùng tại quầy để đo hiệu quả.
- Kho dữ liệu thứ hai. Mọi lớp mới, nếu được duyệt, nằm trong PostgreSQL của KIT Platform, schema nhà thuốc hiện có.

---

## 2. Kiến trúc đích

Năm lớp dùng chung một nền, không dùng chung một vòng đời.

```text
KIT / Global                         Nhà thuốc                         Quầy và đội ngũ
┌──────────────────────────┐        ┌───────────────────────────┐    ┌─────────────────────┐
│ Soạn, review, nguồn      │        │ Đọc bản đã xuất bản       │    │ Học đúng version    │
│ Publish, version, retire │        │ Pin đúng version          │    │ Làm việc tại ca     │
│ Nhận đóng góp đã đồng ý  │        │ Soạn tri thức của mình    │    │ Phản hồi có người   │
│ Đánh giá, không tự publish│       │ Kích hoạt loại được phép  │    │ nhận                │
└────────────┬─────────────┘        └─────────────┬─────────────┘    └──────────▲──────────┘
             │ bản PUBLISHED                       │ pin + bản nhà thuốc          │
             └──────────────┬──────────────────────┘ được phép                   │
                            ▼                                                     │
                 Runtime của đúng tenant                                          │
                 Cổng an toàn đứng trước                                          │
                 Không đọc bản chưa duyệt                                         │
                            │                                                     │
                            └──────────────── phản hồi, không tự sửa rule ────────┘
```

| Lớp | Chủ thể | Đầu ra được runtime đọc | Không được |
|---|---|---|---|
| Global Knowledge | KIT | Version `PUBLISHED` mà tenant đã pin, còn hiệu lực, không phải `KR_OTHER` | Pin hộ nhà thuốc. Sửa bản đã publish. Bỏ cổng nguồn |
| Pharmacy-owned Knowledge | Tenant trên token | Chỉ loại đã được phép kích hoạt, đã phê duyệt, còn hiệu lực, của đúng tenant | Tự thành gợi ý lâm sàng. Sửa cổng an toàn. Đọc tenant khác |
| Contribution | Nhà thuốc, từng mục, có đồng ý | Không có đầu ra runtime. Chỉ là hồ sơ gửi cho KIT | Kéo POS, khách, đơn thuốc, lịch sử mua |
| Intelligence | KIT, có người duyệt | Đề xuất để người đưa vào vòng K1 | Tự publish. Tự biến kinh nghiệm thành tri thức lâm sàng |
| Runtime | Không có người “chạy”. Chỉ hợp nhất đầu vào đã duyệt | Câu trả lời có xuất xứ và version | Fallback. Nội dung nháp. Đè `ConsultationSafetyRules` |

---

## 3. Ranh giới miền

### 3.1 Global Knowledge

Giữ K1.

KIT tiếp nhận nguồn, chuẩn hóa, review nội dung, kiểm chứng nguồn, publish, đánh version, retire, và phân phối bằng cách để nhà thuốc pin. Mức áp dụng là số tenant đang pin đúng version, đọc từ `pharmacy_knowledge_adoption`. Phản hồi là bản ghi riêng, không sửa version đã publish.

Năm việc vẫn tách: soạn, review nội dung, chấp nhận nguồn, publish, pin. `ACCEPTED` không phải `PUBLISHED`. Publish không tạo pin.

### 3.2 Pharmacy-owned Knowledge

Mỗi tenant soạn tri thức của mình. Năm loại không được gộp một nhãn “kinh nghiệm”:

| Loại | Việc | Được kích hoạt để làm gì | Không được |
|---|---|---|---|
| Hướng dẫn | Cách giải thích cho nhân viên, không ra quyết định bán | Hiện trong Admin và, khi có quyết định riêng, trong bài học của tenant | Đưa vào danh sách sản phẩm |
| Quy trình vận hành | Các bước trong ca: nhận hàng, đóng ca, bàn giao | Hiện cho nhân viên được phân quyền đọc | Tự tạo task hoặc checklist |
| Operational policy | Quy tắc nội bộ thu hẹp cách làm việc | Áp trong tenant, chỉ thu hẹp, cùng hướng với policy pin hiện tại | Nới keyword, nới category, hạ cổng an toàn |
| Product knowledge | Ghi chú hàng của nhà thuốc: cách trưng bày, hàng hay nhầm | Tra trong Admin và quầy như ghi chú | Tự khớp hoạt chất hoặc tự thêm vào gợi ý |
| Clinical knowledge | Nhận định triệu chứng, hướng xử trí, chống chỉ định | Lưu, review, phê duyệt trong tenant để học nội bộ | Vào Copilot, vào safety gate, hoặc đè Global Knowledge |

Clinical knowledge của nhà thuốc có thể tồn tại như hồ sơ đã phê duyệt. Nó không có đường vào gợi ý sản phẩm và không có đường tắt safety gate trong V1. Muốn một ý từ nhà thuốc thành rule lâm sàng dùng tại quầy, đường duy nhất là Contribution rồi vòng K1 của KIT. Bản KIT publish sau đó vẫn phải được chính tenant pin.

### 3.3 Knowledge Contribution

Nhà thuốc tự nguyện gửi một mục mình đang sở hữu. Gửi là bản chụp tại thời điểm đồng ý, không phải view sống vào dữ liệu vận hành.

Không có chế độ “chia sẻ cả nhà thuốc”. Không có job đọc đơn, khách, toa, hoặc lịch sử mua để rút tri thức.

### 3.4 Knowledge Intelligence

Pipeline đề xuất:

`Capture → Normalize → De-identify → Evaluate → Verify → Approve → Publish`

Bốn tầng nhận thức, không được hiển thị bằng một badge:

| Tầng | Nghĩa | Ai giữ |
|---|---|---|
| Được nhà thuốc đóng góp | Bản chụp đã có đồng ý | Tenant nguồn, trong phạm vi đồng ý |
| Đang được NOVIXA đánh giá | Có người nhận, có thể có đề xuất máy | KIT, chưa phải tri thức chính thức |
| Đã được xác minh | Nguồn `ACCEPTED` trên một version K1 | Người giữ quyền verify-source |
| KIT xuất bản | `PUBLISHED` | Người giữ quyền publish |

Máy được đề xuất nội dung, chỉ ra bản giống nhau, mẫu lặp, mâu thuẫn, và chỗ thiếu. Máy không gọi publish và không ghi `ACCEPTED`.

### 3.5 Knowledge Runtime

Hai đầu vào hợp lệ:

1. Global Knowledge đã pin, đúng version, còn hiệu lực.
2. Pharmacy-owned Knowledge loại được phép, đã phê duyệt và đang kích hoạt, của đúng tenant.

Rule lâm sàng tại quầy chỉ đến từ đầu vào 1. Đầu vào 2 không đứng trong truy vấn gợi ý sản phẩm của V1.

Cổng an toàn hiện tại chạy trước cả hai. Nếu cổng trả dừng bán, chuyển bác sĩ, hoặc chuyển dược sĩ, runtime không ghép thêm hướng dẫn để “làm mềm” quyết định đó.

---

## 4. Trải nghiệm

Chủ nhà thuốc ở lại Admin hiện tại. Không có hệ thống thứ hai và không có màn hình hiện mã quyền.

Một mục **Tri thức** trong Admin nhà thuốc, bốn việc:

| Việc người thấy | Nội dung | Ai vào |
|---|---|---|
| Tri thức nền tảng | Catalog đã xuất bản, nguồn, version, pin, thu hồi pin, policy thu hẹp, lịch sử áp dụng | ADMIN của tenant trên session, hoặc người đang có claim pin |
| Tri thức của nhà thuốc | Soạn, phân loại, version, review, phê duyệt, kích hoạt hoặc ngừng, phân quyền đọc, lịch sử | ADMIN của tenant. Người được giao việc soạn chỉ thấy loại mình được giao |
| Đóng góp cho NOVIXA | Chọn một mục, xem bản sẽ gửi, chọn mục đích, ghi đồng ý, xem và dừng đóng góp | ADMIN của tenant. Nhân viên không gửi hộ |
| Theo dõi và cải tiến | Pin nào đang hiệu lực, đóng góp đang ở tầng nào, phản hồi đã gửi | ADMIN của tenant, chỉ số của tenant mình |

KIT giữ màn `/platform/pharmacy-knowledge` cho review, xác minh nguồn, publish và retire. Chủ nhà thuốc không thấy hàng nháp của nền tảng. Vai trò ADMIN của nhà thuốc không mở màn đó.

Nhãn trên nút là việc (“Ghim phiên bản này”, “Ngừng áp dụng”, “Gửi cho NOVIXA”), không phải tên claim.

Nhân viên quầy không vào bốn việc trên. Họ gặp tri thức ở bài học và ở quầy, đúng version đang được dùng, khi các lớp K3 và K4 được làm sau.

---

## 5. Mô hình dữ liệu

Không tạo database riêng. Không tạo warehouse. Bảng mới, nếu Product Owner duyệt từng milestone, là bảng trong cùng PostgreSQL, cùng ranh giới tenant `tenant_id`, cùng kiểu sự kiện append-only đang dùng cho publication và pin.

Bảng Global và pin hiện có giữ nguyên vai trò:

| Bảng hiện có | Giữ vai trò |
|---|---|
| `pharmacy_knowledge_rule` | Danh tính rule nền tảng |
| `pharmacy_knowledge_rule_version` | Version bất biến sau publish, kèm nguồn |
| `pharmacy_knowledge_adoption` | Pin của một tenant, một version |
| `pharmacy_knowledge_approval_event` | Lịch sử pin |
| `pharmacy_knowledge_publication_event` | Lịch sử publish và retire |
| Policy trên pin | Chỉ thu hẹp |

Không nhét tri thức nhà thuốc vào `pharmacy_knowledge_rule_version`. Hàng đó đi qua cổng publish nền tảng và trigger đóng băng của KIT. Trộn tenant vào đó sẽ làm một nhà thuốc sửa được catalog chung hoặc bị cổng KIT chặn nhầm.

### 5.1 Thực thể đề xuất

Tên dưới đây là tên miền, chưa phải tên migration.

| Thực thể | Khóa logic | Quan hệ |
|---|---|---|
| Pharmacy knowledge | `tenant_id` + mã nội bộ | Thuộc một tenant. Có loại trong năm loại mục 3.2 |
| Pharmacy knowledge version | knowledge + số version | Nội dung, phân loại, ngày hiệu lực. Version đã phê duyệt không sửa tại chỗ |
| Activation | tenant + knowledge + version | `ACTIVE` hoặc `STOPPED`. Không suy ra từ việc version tồn tại |
| Staff grant | tenant + user + việc | Đọc, soạn, hoặc phê duyệt loại được chỉ định. ADMIN của tenant không cần từng dòng grant cho chính mình |
| Contribution snapshot | id bản chụp | Bản sao nội dung tại lúc gửi. Không trỏ sống sang POS |
| Contribution consent | snapshot + người + thời điểm | Mục đích, phạm vi, câu đã đọc, trạng thái còn hiệu lực hoặc đã dừng |
| Evaluation | snapshot | Tầng đang đánh giá. Gắn người nhận |
| Evaluation note | evaluation | Đề xuất máy hoặc ghi chú người. Có cờ `machine` và `accepted_by_human` |
| Verification | trỏ version K1 | Chính là nguồn `ACCEPTED` / `REJECTED` đã có. Không tạo cổng thứ hai |
| Publication | version K1 | Chính là lifecycle K1 |
| Runtime usage | tenant + xuất xứ + version + thời điểm | Tùy milestone. Chỉ ghi khi một bề mặt đã được phép đọc version đó |
| Feedback | tenant + xuất xứ + version | “Không khớp” hoặc ghi chú. Không sửa version |
| Audit | mọi chuyển trạng thái | Append-only, cùng kiểu publication event và approval event |

Quan hệ đóng góp sang Global Knowledge là một chiều: snapshot có thể được người KIT dùng làm nguyên liệu để soạn một version `DRAFT` mới. Version đó ghi `contribution_id` trong hồ sơ xuất xứ nội bộ. Văn bản publish không mặc định nêu tên nhà thuốc, nhân viên, hay khách.

### 5.2 Khả năng nằm trên KIT Platform

Phù hợp với kiến trúc hiện tại nếu giữ các ràng buộc sau:

- Mọi hàng tenant có `tenant_id` lấy từ token, không lấy từ body.
- Đọc và ghi đi qua pack Pharmacy, cùng cách `PharmacyKnowledgeApprovalService` đang buộc `tenant_id`.
- Version đã phê duyệt dùng trigger đóng băng cùng kiểu migration `417`, chỉ cho đổi trạng thái lifecycle và thời điểm cập nhật.
- Sự kiện không có `UPDATE` và không có `DELETE` nghiệp vụ.
- Không join sang `sales_orders`, khách hàng, đơn thuốc, hay `stock_movements` để sinh tri thức.
- Index theo `(tenant_id, status)` và `(tenant_id, rule_code, version)` là đủ cho catalog một nhà thuốc. Đo lường áp dụng Global là đếm pin, không phải quét toàn bộ giao dịch.

Chưa cần tách kho vì khối lượng V1 là văn bản có chủ đích, không phải log quầy. Nhật ký runtime, nếu làm, phải có giới hạn retention do Product Owner chọn trước khi ghi.

---

## 6. Quyền

### 6.1 Giữ nguyên

| Việc | Ai |
|---|---|
| Review nội dung nền tảng | Claim `pharmacy.knowledge.review` trên token ERP |
| Xác minh nguồn nền tảng | Claim `pharmacy.knowledge.verify-source` |
| Publish và retire nền tảng | Claim `platform.knowledge.publish` |
| Pin Global Knowledge, thu hồi pin, policy thu hẹp, xem catalog đã publish | Claim `pharmacy.knowledge.approve` hoặc vai trò `ADMIN` của đúng tenant trên token |

Ba claim nền tảng không suy từ vai trò ADMIN. STAFF, MANAGER và BRANCH_MANAGER không tự có quyền pin. Token customer, prescriber và partner không phải token ERP nên không pin.

Ánh xạ ADMIN của tenant sang pin đang ở working tree và đang chờ review sản phẩm. Nếu review đó không nhận, lớp pin quay lại chỉ claim tường minh. Đề xuất này không nhân ánh xạ đó thành quyền publish.

### 6.2 Đề xuất cho tri thức của nhà thuốc

Chủ nhà thuốc là vai trò `ADMIN` của tenant trên session. Người đó quản lý đủ vòng soạn, phân loại, review, phê duyệt, kích hoạt, ngừng, phân quyền đọc và xem lịch sử của tenant mình.

Nhân viên chỉ làm việc được giao bằng grant. Grant không suy từ `sales.pos` hay `sales.read`.

| Việc trên tri thức nhà thuốc | ADMIN tenant | Người được grant | STAFF không grant |
|---|---|---|---|
| Xem hướng dẫn và quy trình đang kích hoạt | Có | Nếu grant đọc | Không |
| Soạn nháp loại không lâm sàng | Có | Nếu grant soạn đúng loại | Không |
| Phê duyệt và kích hoạt loại không lâm sàng | Có | Chỉ khi có grant phê duyệt đúng loại | Không |
| Soạn hoặc phê duyệt clinical knowledge | Có, chỉ trong hồ sơ nội bộ | Chỉ khi Product Owner cho một grant riêng | Không |
| Đưa clinical knowledge vào Copilot hoặc safety gate | Không | Không | Không |
| Gửi đóng góp | Có | Không | Không |
| Đọc tenant khác | Không | Không | Không |

Chưa đặt mã claim mới trong đề xuất này. Tên mã, và việc grant nằm ở bảng riêng hay ở permission hiện có, là quyết định Product Owner trước milestone tương ứng. Không seed và không cấp trong tài liệu.

---

## 7. Vòng đời

### 7.1 Global Knowledge

Giữ máy trạng thái K1.

`DRAFT → IN_REVIEW → PUBLISHED → RETIRED`, có đường trả `IN_REVIEW → DRAFT`. Nguồn đổi trạng thái riêng, không đổi lifecycle. Sửa nội dung khi chưa publish thì nguồn đã `ACCEPTED` trở lại `UNVERIFIED`.

Pin là vòng đời khác: không pin, `APPROVED`, `RETIRED`. Không pin nghĩa là runtime không dùng. Không tự nâng version.

### 7.2 Pharmacy-owned Knowledge

```text
DRAFT → IN_REVIEW → APPROVED → ACTIVE
          │            │          │
          └→ DRAFT     └→ RETIRED └→ STOPPED
```

- `DRAFT → APPROVED` không có.
- `APPROVED` đóng băng nội dung version đó.
- `ACTIVE` chỉ gán được cho version `APPROVED`, loại được phép, còn hiệu lực.
- Clinical knowledge dừng ở `APPROVED` trong hồ sơ. Không có trạng thái `ACTIVE` trên đường Copilot.
- `STOPPED` và `RETIRED` còn đọc được. Không xóa.
- Sửa nội dung tạo version mới ở `DRAFT`. Version cũ đang `ACTIVE` không tự trỏ sang version mới.

### 7.3 Contribution và intelligence

```text
Chọn mục → bản chụp → đồng ý → CONTRIBUTED
    → người KIT nhận → IN_EVALUATION
        → hoặc trả lại / dừng theo thu hồi
        → nếu thành nguyên liệu: version K1 DRAFT
            → Verify (ACCEPTED) → Publish (PUBLISHED)
```

Tầng “được đóng góp” không phải tầng “đã xác minh”. Tầng “đã xác minh” không phải tầng “đã xuất bản”. Publish vẫn là hành động người, trên cổng K1.

Dừng đóng góp chuyển consent sang đã dừng và cấm dùng snapshot cho evaluation mới. Version K1 đã `PUBLISHED` từ trước đó không bị xóa và không bị sửa. Cách xử lý bản đang `IN_EVALUATION` khi consent dừng là quyết định còn mở ở mục 13.

---

## 8. Đồng ý và quyền riêng tư

### 8.1 Cái gì được gửi

Chủ nhà thuốc chọn từng mục. Màn hình gửi chỉ hiện các trường nội dung của mục đó: loại, tiêu đề, thân bài, phân loại, version. Không hiện và không đính kèm mã khách, tên khách, số điện thoại, địa chỉ, nhân viên, số đơn, dòng hàng đã bán, tồn kho, hay toa thuốc.

Nếu thân bài có các dấu hiệu đó, bước khử nhận dạng phải xong và được người chấp thuận nhìn lại trước khi snapshot thành `CONTRIBUTED`. Máy được đánh dấu chỗ nghi là thông tin nhận diện. Người xác nhận bản đã khử. Không có bước “máy tự xóa rồi tự gửi”.

### 8.2 Ai chấp thuận, để làm gì

Người chấp thuận là ADMIN của tenant trên token. Đồng ý ghi:

- định danh user và tenant trên token, phục vụ kiểm tra, không đưa vào văn bản publish;
- id snapshot;
- thời điểm;
- mục đích được chọn trong danh sách cho phép;
- câu xác nhận người đã đọc.

Mục đích đề xuất, chưa chốt: cải thiện tri thức dùng chung cho các nhà thuốc NOVIXA, sau khi KIT kiểm chứng và xuất bản. Không gồm huấn luyện mô hình trên dữ liệu thô, không gồm chia sẻ lại cho bên thứ ba, không gồm dùng để chấm nhân viên hay chấm nhà thuốc.

### 8.3 Thu hồi

Trong phạm vi làm được:

- Dừng ngay việc dùng snapshot cho evaluation mới.
- Gỡ mục khỏi hàng đợi chưa có người nhận.
- Giữ bản ghi đồng ý và bản ghi đã dừng, vì đó là bằng chứng.
- Không xóa version Global đã publish. Xuất xứ nội bộ vẫn trỏ snapshot để giải trình, văn bản đã publish không chứa tên nhà thuốc trừ khi đồng ý riêng cho việc ghi công, và việc ghi công cũng là quyết định còn mở.

Không hứa xóa bản sao đã nằm trong ghi chú của người review nếu ghi chú đó đã được lưu như audit. Ghi chú đó không được chứa dữ liệu khách hàng ngay từ bước nhận.

### 8.4 Cấm tự động

Không đồng bộ POS, khách hàng, đơn thuốc, lịch sử mua, tồn kho, ca bán, checklist, hay bài học sang lớp Contribution. Không suy đồng ý từ việc nhà thuốc đang pin Global Knowledge.

---

## 9. Quản trị AI

AI trong lớp Intelligence là người soạn phụ, không phải người duyệt.

| Được đề xuất | Không được ghi |
|---|---|
| Các snapshot hoặc rule giống nhau | `lifecycle_status = PUBLISHED` |
| Mẫu lặp giữa nhiều đóng góp đã đồng ý | `source_verification_status = ACCEPTED` |
| Bản nháp chuẩn hóa để người sửa | Pin `APPROVED` |
| Mâu thuẫn với rule đã publish hoặc với cổng an toàn | Sửa `ConsultationSafetyRules` |
| Chỗ thiếu nguồn, thiếu phạm vi, thiếu đối tượng loại trừ | Xóa thông tin nhận diện mà không có người xác nhận |
| Gợi ý mở version K1 mới | Biến một snapshot thành clinical recommendation |

Mỗi đề xuất máy lưu kèm mô hình không cần nêu trong V1 nếu chưa có nhà cung cấp được chọn. Bắt buộc lưu: thời điểm, đầu vào là id snapshot hoặc id version, và việc chưa có người nhận. Khi người nhận một đề xuất, ghi user đó. Đề xuất không người nhận thì không đi tiếp trên pipeline.

Kinh nghiệm chưa kiểm chứng giữ nhãn “được đóng góp” hoặc “đang đánh giá”. Nhãn đó không được hiển thị ở quầy như khuyến cáo chuyên môn.

13 cờ `pharmacy_safety_flag_review` vẫn `PENDING_REVIEW`. Chúng không phải nguồn cho pipeline này.

---

## 10. An toàn runtime

Thứ tự cố định khi một ca tư vấn hỏi gợi ý:

1. Cổng `ConsultationSafetyRules` và `PharmacyOtcSuggestionGate`.
2. Nếu cổng không cho danh sách OTC thường, dừng. Không đọc pin, không đọc tri thức nhà thuốc.
3. Nếu cổng cho phép, chỉ đọc pin Global: đúng tenant, `APPROVED`, đúng `rule_version`, version `PUBLISHED`, còn hiệu lực, khác `KR_OTHER`.
4. Không đọc version mới hơn chưa được pin.
5. Không đọc nháp, bản `IN_REVIEW`, bản nguồn `UNVERIFIED` hoặc `REJECTED` để bù vào.
6. Pharmacy-owned Knowledge không tham gia bước 3 trong V1.
7. Policy nhà thuốc chỉ bỏ bớt keyword hoặc category, không thêm.

Provenance trên mọi câu runtime trả về, khi lớp đó được làm: nguồn `GLOBAL` hoặc `PHARMACY`, mã, version, tenant. V1 của runtime lâm sàng chỉ có nguồn `GLOBAL`.

Thiếu pin nghĩa là không có gợi ý từ rule. Đó là trạng thái đúng, không phải lỗi cần fallback.

Đề xuất siết thêm ở milestone runtime: Copilot cũng từ chối pin nếu version không còn `ACCEPTED`. Hôm nay cổng đó nằm ở API pin, vì version đã publish không bị hạ nguồn nhờ trigger đóng băng. Siết ở chỗ đọc là phòng vệ sâu, không đổi hành vi khi dữ liệu đúng cổng.

---

## 11. Staff và học việc

Vòng đề xuất, mỗi mũi tên là một việc của người:

`Knowledge đã pin hoặc đã kích hoạt loại được phép → bài học trỏ đúng version → nhân viên làm tại ca → kết quả người quản lý ghi nhận → phản hồi → version mới`

Ràng buộc lấy từ K1 và kế hoạch Staff, giữ nguyên:

- Một bài đã được duyệt có thể trỏ một `rule_code` và một version.
- Lúc gắn, version đang `PUBLISHED` và tenant đang pin đúng version. Với hướng dẫn của nhà thuốc, lúc gắn thì version đó đang `ACTIVE`.
- Không copy nội dung rồi sửa trong markdown.
- Pin đổi hoặc version retire thì trỏ cũ còn, và được đánh dấu không còn là bản đang dạy. Không tự trỏ sang bản mới.
- Gắn trỏ không tạo enrollment, không tạo operational task, không tick checklist.
- Không chấm năng lực bằng doanh số.
- Không viết lại L0–L6 trong các milestone tri thức.

Phản hồi “không khớp” tạo bản ghi Feedback. Người có quyền mở version mới. Không có job biến phản hồi thành `DRAFT`.

---

## 12. Lộ trình

Mỗi milestone độc lập. Chưa có ngày. Chưa có ước lượng. Milestone sau không mở bằng cách kéo code của milestone trước vào production khi cửa trước chưa đạt.

K1 governance, K2A ba nhóm pin, và K2B runtime pin đã có trên working tree. Chúng là nền của M5 và M6, không phải việc phải làm lại. Staff S1–S2 không bị chặn bởi các milestone này. S3, tức bài học trỏ version, chỉ mở khi đã có một version được pin hoặc một hướng dẫn nhà thuốc đang kích hoạt, và vẫn không tự ghi danh.

### M1 — Pharmacy Knowledge Foundation

Cửa vào: Product Owner chốt năm loại và chốt clinical knowledge không vào Copilot. Không cần rule Global nào đã publish.

Việc: thực thể pharmacy knowledge và version nháp, loại, cô lập tenant, audit tạo và sửa nháp. Chưa kích hoạt. Chưa đóng góp. Chưa gọi AI.

Nghiệm thu: tenant A không đọc bản nháp của tenant B. Clinical knowledge không có API kích hoạt. Không có câu SQL nào trong lát này đọc bảng bán hàng. Cổng an toàn và pin Global không đổi hành vi.

### M2 — Pharmacy Knowledge Management

Cửa vào: M1 đạt. Chủ nhà thuốc đã vào được mục tri thức của tenant, bằng ánh xạ ADMIN đã review hoặc bằng claim được chốt.

Việc: review, phê duyệt, đóng băng version, kích hoạt và ngừng các loại không lâm sàng, grant cho nhân viên, lịch sử. Màn trong Admin hiện tại.

Nghiệm thu: không có `DRAFT → ACTIVE`. Kích hoạt version 2 không tắt version 1 nếu người chưa chọn ngừng version 1, và runtime hướng dẫn không tự đổi trỏ. STAFF không grant thì không phê duyệt. Không tạo task, enrollment, checklist.

### M3 — Knowledge Contribution

Cửa vào: M2 đạt, để có một mục thuộc nhà thuốc để chọn. Không cần Global Knowledge đã publish.

Việc: chọn mục, khử nhận dạng có người xác nhận, đồng ý, snapshot, dừng đóng góp. Không có nút “gửi tất cả”.

Nghiệm thu: snapshot không chứa khóa ngoại sang khách hàng, đơn, toa, ca. Dừng đóng góp chặn evaluation mới. Pin Global không bị coi là đồng ý. Một nhân viên không gửi được.

### M4 — Knowledge Intelligence

Cửa vào: M3 đạt trên môi trường có snapshot giả hoặc snapshot đã đồng ý. Không dùng dữ liệu production để thử máy.

Việc: hàng đợi đánh giá, đề xuất giống nhau, mẫu lặp, mâu thuẫn, độ đủ, bản nháp chuẩn hóa. Người nhận hoặc bỏ đề xuất.

Nghiệm thu: không đề xuất nào tự ghi `PUBLISHED` hoặc `ACCEPTED`. Một snapshot chưa khử nhận dạng không vào evaluate. Đề xuất máy không hiện ở quầy.

### M5 — Global Knowledge Evolution

Cửa vào: K1 vẫn là cổng publish. Có ít nhất một đường phản hồi hoặc một snapshot được người KIT chọn làm nguyên liệu. Không đòi 35 rule phải được publish trước.

Việc: từ nguyên liệu đã đánh giá, người KIT mở version `DRAFT` mới và đi đúng review, nguồn, publish. Theo dõi số tenant đang pin từng version. Phản hồi gắn version, không sửa version.

Nghiệm thu: publish vẫn từ chối khi thiếu `ACCEPTED`, khi là `KR_OTHER`, và khi không đứng ở `IN_REVIEW`. Publish không đổi pin. Văn bản publish không kéo theo tên nhà thuốc nếu không có đồng ý ghi công riêng. 35 rule seed không bị điền nguồn trong milestone này.

### M6 — Runtime Integration và cải tiến liên tục

Cửa vào: K2B còn đúng pin và cổng an toàn. M2 đạt nếu muốn hiện hướng dẫn nhà thuốc ở một bề mặt không phải gợi ý sản phẩm. K3 chỉ cần khi muốn thẻ trên bài học.

Việc: hợp nhất có xuất xứ; hướng dẫn và quy trình đang `ACTIVE` hiện ở chỗ được phép, tách khỏi gợi ý sản phẩm; phản hồi quay về người có quyền; siết đọc Copilot theo nguồn `ACCEPTED` nếu Product Owner nhận phòng vệ sâu.

Nghiệm thu: ba mức dừng hoặc chuyển cấp vẫn không có danh sách sản phẩm. Clinical knowledge của nhà thuốc vẫn không nằm trong truy vấn gợi ý. Không fallback. Không tự tạo việc học. Đổi pin không đổi bài học đang trỏ version cũ.

---

## 13. Quyết định Product Owner còn mở

Đề xuất này không chọn hộ.

| Việc | Vì sao còn mở |
|---|---|
| Nhận hay không ánh xạ vai trò ADMIN của tenant sang quyền pin | Working tree đã làm để chủ nhà thuốc khỏi xin từng claim. Chưa phải quyết định đã gán tài khoản |
| Tên claim cho soạn, phê duyệt và kích hoạt tri thức nhà thuốc | Chưa seed mã mới. Có thể đi bằng grant trong tenant thay vì permission toàn nền tảng |
| MANAGER hoặc dược sĩ phụ trách có được phê duyệt hướng dẫn nội bộ không | Hôm nay chỉ ADMIN tenant và grant tường minh được đề xuất |
| Clinical knowledge của nhà thuốc có bao giờ được đưa vào một đường quầy sau V1 không | V1 cấm. Mở sau này vẫn phải qua K1 và pin, không qua kích hoạt nội bộ |
| Danh sách mục đích đồng ý cuối cùng, gồm có được ghi công nhà thuốc không | Đề xuất chỉ một mục đích cải thiện tri thức dùng chung, không ghi công |
| Khi dừng đóng góp, bản đang `IN_EVALUATION` bị hủy hay được KIT giữ đến hết lượt review | Cả hai đều làm được. Cần chọn trước M3 |
| Ai ở KIT được nhận hàng chờ đánh giá | Tách khỏi người publish. Chưa gán người |
| Một người có được vừa verify-source vừa publish không | Đang mở từ K1 |
| Tài khoản nào giữ các claim nền tảng và claim pin | DEC-015 chọn chủ nhà thuốc cho việc pin, chưa gán tài khoản |
| Rule Global nào một nhà thuốc xem trước | DEC-016, theo từng nhà thuốc |
| Có lưu “từ chối pin” kèm lý do không | Chưa có trạng thái đó |
| Nhân viên quầy thấy trích dẫn đầy đủ hay chỉ lý do và version | Đang mở từ blueprint |
| Bài nào được trỏ rule, ai duyệt bài | S3 |
| `KR_OTHER` có được viết lại thành rule khác không | Nội dung seed hiện không đủ điều kiện |
| Category seed có khớp category hàng của nhà thuốc không | Qualification: chưa kiểm chứng |
| Có ghi runtime usage không, giữ bao lâu | Chưa cần cho M1–M5. Ghi sớm sẽ thành log vận hành |
| Biên lợi nhuận trong gợi ý | OPEN-005, không mở thêm ở đây |

Đã chốt, không hỏi lại trong đề xuất này: không publish khi thiếu nguồn `ACCEPTED`; không có `DRAFT → PUBLISHED`; không tự pin; không tự điền nguồn; publish nền tảng và pin nhà thuốc là hai việc; an toàn đứng trước danh sách sản phẩm; không lấy dữ liệu POS, khách, toa, lịch sử mua làm nguyên liệu; không tự tạo task, enrollment hoặc checklist từ tri thức; Customer App không phải cửa của các milestone này.
