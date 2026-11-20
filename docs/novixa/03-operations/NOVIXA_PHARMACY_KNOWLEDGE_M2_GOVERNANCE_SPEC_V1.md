# NOVIXA — Pharmacy-owned Knowledge M2: Governance và lifecycle

Ngày: 2026-11-01. Loại: đặc tả kiến trúc và nghiệp vụ. Không triển khai code, không viết migration, không chạy migration, không cấp quyền, không deploy.

Phạm vi: chuyển Pharmacy-owned Knowledge từ nền nháp M1 sang quản trị trong tenant: gửi duyệt, duyệt, đóng băng version, kích hoạt, ngừng và khôi phục. Không đưa nội dung này vào Copilot, POS, gợi ý, Learning, Customer App hay Second Brain.

Trạng thái: `READY_FOR_PRODUCT_REVIEW`.

Nguồn đã đọc trên `E:\KitPlatform`: M1 (`PharmacyOwnedKnowledge*.cs`, controller, Admin page, API client), migration `424`, `425`, `426`, `TenantContext`, `CoreAuthority`, `DbPermissionsClaimsTransformation`, `AdminTokenRules`, policy K1 trong `PharmacyKnowledgeAuthorizationExtensions`, `PharmacyKnowledgeApproval`, picker quyền, và kiến trúc đã có tại `NOVIXA_PHARMACY_KNOWLEDGE_V1_ARCHITECTURE_PROPOSAL.md`. K1 được dùng làm tham chiếu. Vòng đời K1 không được chép sang M2.

---

## 1. Hiện trạng đã kiểm chứng

### 1.1 M1 đang có

| Thành phần | Bằng chứng | Giới hạn |
|---|---|---|
| Ba bảng | `pharmacy_owned_knowledge`, `pharmacy_owned_knowledge_version`, `pharmacy_owned_knowledge_event` trong `424` | Version chỉ được `DRAFT`. Check `ck_pharmacy_owned_knowledge_draft`. Trigger từ chối rời `DRAFT` |
| Năm loại | `GUIDANCE`, `OPERATING_PROCEDURE`, `OPERATIONAL_POLICY`, `PRODUCT_KNOWLEDGE`, `CLINICAL_KNOWLEDGE` | Mã trong code là `OPERATING_PROCEDURE`. Không có mã `OPERATIONAL_PROCEDURE` |
| Sửa tại chỗ | `UpdateAsync` sửa đúng version có `MAX(version_no)` khi còn `DRAFT` | Chưa có version 2. Chưa có khóa lạc quan |
| Snapshot | `425` thêm `body`, `classification`. `426` thêm `snapshot_fidelity` | `EXACT` là nội dung ghi tại lúc tạo event. `LEGACY_UNAVAILABLE` là event cũ; body suy ra đã bị xóa |
| Lịch sử | Append-only. Trigger cấm `UPDATE`/`DELETE`. Đọc `ORDER BY created_at, id` | Action hiện chỉ `CREATED` và `UPDATED`, cột `VARCHAR(16)` |
| API | `GET/POST /api/pharmacy/owned-knowledge`, `GET/PUT /api/pharmacy/owned-knowledge/{id}` | Không có submit, approve, activate, compare, grant. Không có DELETE |
| Tenant | Controller lấy `_tenant.TenantId` và `_tenant.UserId`. Body không có tenant | SQL lọc `tenant_id`. Chưa có RLS |
| UI | Menu và trang `Tri thức của nhà thuốc`, chỉ `useIsAdmin()` | Danh sách theo loại, một drawer nháp. Chưa lọc theo trạng thái, chưa so sánh version |

Mọi dòng version hiện hữu, nếu `424` đã chạy, đều là `DRAFT` vì check không cho giá trị khác. M2 không được nâng các dòng đó lên `APPROVED`.

### 1.2 Tenant và quyền đang có

- Tenant của request là claim JWT `tenant_id`, qua `CoreAuthority.RequireTenantId`. `RejectTenantSwitch` cấm đổi tenant.
- `DbPermissionsClaimsTransformation` chỉ gắn lại role và permission khi user còn hiệu lực và `user.TenantId` trùng tenant trên token. Lệch tenant thì role và permission bị gỡ.
- `AdminTokenRules.IsAdminPrincipal` nghĩa là JWT ERP đã đăng nhập. Token `customer_app`, `prescriber_portal`, `partner_portal` không phải principal ERP.
- Policy `PharmacyOwnedKnowledgeManage` gọi `CanManage`: principal ERP và role `ADMIN`. Không có permission code riêng cho tri thức nhà thuốc.
- Role `ADMIN` ở đây là role trên token ERP của đúng tenant. Nó không phải quyền publish nền tảng. `CanPublish`, `CanReview`, `CanVerifySource` đều đòi claim tường minh và không nhận role `ADMIN`.
- Claim đã có tên trong code: `pharmacy.knowledge.approve`, `pharmacy.knowledge.review`, `pharmacy.knowledge.verify-source`, `platform.knowledge.publish`.
- `pharmacy.knowledge.approve` có trong picker Admin và nhãn “Duyệt phiên bản tri thức cho nhà thuốc này”. Nhãn đó đang chỉ pin Global. Không có dòng `INSERT` claim này trong `migrations/*.sql`.
- `pharmacy.knowledge.review` và `pharmacy.knowledge.verify-source` được ghi trong code là chưa seed, chưa nằm trong picker. UI K1 dùng `useHasExplicitPermission`.
- Không có bảng grant cho tri thức nhà thuốc. Không có role code `pharmacist` trong lớp quyền tri thức. Tài khoản demo tên `pharmacist` không phải một role được kế thừa.
- `STAFF`, `MANAGER`, `BRANCH_MANAGER` không qua `CanManage`. Test pin cũng xác nhận `MANAGER` và `BRANCH_MANAGER` không kế thừa pin.

### 1.3 K1 không được dùng làm máy trạng thái M2

K1 trên version là `DRAFT → IN_REVIEW → PUBLISHED → RETIRED`, có đường trả `IN_REVIEW → DRAFT`. Nguồn `ACCEPTED` là cổng khác. Pin là bản ghi `pharmacy_knowledge_adoption` với `APPROVED` hoặc `RETIRED`, không phải trạng thái của version.

M2 không ghi vào `pharmacy_knowledge_rule`, `pharmacy_knowledge_rule_version`, `pharmacy_knowledge_adoption`, `pharmacy_knowledge_approval_event`, `pharmacy_knowledge_publication_event`. `ACTIVE` của nhà thuốc không đồng nghĩa pin `APPROVED`.

### 1.4 Việc M2 chưa có, và không được suy ra

- Chưa có `IN_REVIEW`, `APPROVED`, kích hoạt, ngừng, trả lại, version 2.
- Chưa có permission soạn, gửi, duyệt, kích hoạt, đọc cho tri thức nhà thuốc.
- Chưa có ủy quyền theo người và theo loại.
- Copilot đọc pin Global. `GetKnowledgeRulesAsync` không đọc `pharmacy_owned_knowledge`. M2 giữ nguyên điều đó.

---

## 2. Quyết định đặc tả

Các quyết định dưới đây là đề xuất để Product Owner duyệt. Chúng chưa được code.

1. Vòng đời **nội dung** của một version là `DRAFT`, `IN_REVIEW`, `APPROVED`.
2. `ACTIVE` và `STOPPED` là trạng thái **áp dụng** của một version đã `APPROVED`, không ghi đè `lifecycle_status`.
3. `CLINICAL_KNOWLEDGE` đi được tới `APPROVED` trong hồ sơ tenant. M2 từ chối kích hoạt loại này.
4. Kích hoạt version mới không tự ngừng version cũ. Đây là nghiệm thu đã ghi trong kiến trúc V1.
5. Sửa một version đã `APPROVED` bị cấm. Sửa sau duyệt tạo version `DRAFT` mới.
6. Không dùng `pharmacy.knowledge.approve` cho duyệt nội bộ. Claim đó giữ cho pin Global.
7. `ADMIN` của đúng tenant làm đủ việc M2 trong tenant mình, trừ kích hoạt lâm sàng. `MANAGER`, dược sĩ và `STAFF` chỉ làm việc khi có grant từng người, từng loại, từng việc.
8. M2 không đọc và không ghi runtime. Cờ kích hoạt trong M2 là hồ sơ, chưa được Copilot, POS hay Learning đọc.

`RETIRED` của kiến trúc V1 không vào M2. Ngừng áp dụng dùng `STOPPED` và khôi phục được. Rút vĩnh viễn một version đã duyệt là quyết định mở ở mục 14.

---

## 3. Lifecycle

### 3.1 Hai lớp trạng thái

Người dùng thấy một nhãn. Dữ liệu giữ hai lớp để version đã duyệt không mất nhãn `APPROVED` khi được áp dụng.

| Nhãn trên UI | Version | Áp dụng | Loại lâm sàng |
|---|---|---|---|
| Nháp | `DRAFT` | Không có | Được |
| Chờ duyệt | `IN_REVIEW` | Không có | Được |
| Đã duyệt | `APPROVED` | Chưa có dòng áp dụng | Được. Dừng tại đây |
| Đang áp dụng | `APPROVED` | `ACTIVE` | Không có nhãn này |
| Ngừng áp dụng | `APPROVED` | `STOPPED` | Không có nhãn này |

### 3.2 Ma trận chuyển version

Chỉ các ô Có được thực hiện. Mọi ô khác bị từ chối, kể cả chuyển tới chính trạng thái hiện tại.

| Từ \ Tới | DRAFT | IN_REVIEW | APPROVED |
|---|---|---|---|
| DRAFT | Sửa nội dung tại chỗ. Không phải transition | Có. Gửi duyệt | Không |
| IN_REVIEW | Có. Trả về sửa | Không | Có. Duyệt |
| APPROVED | Không | Không | Không. Đóng băng |

Không có `DRAFT → ACTIVE`, `IN_REVIEW → ACTIVE`, `APPROVED → DRAFT`. Kích hoạt không phải transition của version.

### 3.3 Ma trận áp dụng

Chỉ version `APPROVED`, loại khác `CLINICAL_KNOWLEDGE`.

| Từ | Tới | Việc |
|---|---|---|
| Chưa áp dụng | `ACTIVE` | Kích hoạt đúng version này |
| `ACTIVE` | `STOPPED` | Ngừng đúng version này |
| `STOPPED` | `ACTIVE` | Khôi phục đúng version này |
| `ACTIVE` | `ACTIVE` | Từ chối. Không ghi event mới |
| Version `DRAFT` hoặc `IN_REVIEW` | `ACTIVE` | Từ chối |
| `CLINICAL_KNOWLEDGE` | `ACTIVE` hoặc `STOPPED` | Từ chối ở service và ở database |

Kích hoạt version 2 không cập nhật dòng áp dụng của version 1. Muốn chỉ còn một bản đang dùng, người có quyền ngừng phải ngừng từng version.

### 3.4 Ai được chuyển

Chủ thể luôn là user trên token, tenant trên token, principal ERP.

| Việc | ADMIN đúng tenant | Người có grant đúng việc và đúng loại | STAFF, MANAGER, dược sĩ không grant |
|---|---|---|---|
| Soạn và sửa `DRAFT` | Có | Grant soạn | Không |
| Gửi duyệt | Có | Grant gửi | Không |
| Trả lại | Có | Grant duyệt | Không |
| Duyệt | Có | Grant duyệt | Không |
| Tạo version nháp sau khi đã có bản duyệt | Có | Grant soạn | Không |
| Kích hoạt, ngừng, khôi phục | Có, trừ lâm sàng | Grant kích hoạt, trừ lâm sàng | Không |
| Đọc nháp và hồ sơ duyệt | Có | Grant soạn hoặc grant duyệt | Không |
| Đọc bản đang áp dụng, loại không lâm sàng | Có | Grant đọc | Không |
| Đọc lâm sàng | Có | Grant lâm sàng | Không |
| Cấp và thu grant | Có | Không | Không |

ADMIN không cần dòng grant cho chính mình. Grant không suy từ `sales.pos`, `sales.read`, `success.checklist`, hay role `MANAGER`.

### 3.5 Điều kiện từng việc

Gửi duyệt:

- Version đang `DRAFT` của đúng tenant.
- `kind`, `title`, `body` hợp lệ theo rule M1. `classification` vẫn được null.
- `updated_at` client gửi khớp dòng hiện tại.
- Cùng transaction: chuyển sang `IN_REVIEW`, ghi event `SUBMITTED` với snapshot `EXACT` của đúng bốn trường nội dung.

Trả lại:

- Version đang `IN_REVIEW`.
- Có ghi chú không rỗng, tối đa 2000 ký tự.
- Cùng transaction: chuyển về `DRAFT`, ghi event `RETURNED` kèm snapshot `EXACT` của nội dung đang bị trả và ghi chú. Không đổi body trong câu lệnh trả.

Duyệt:

- Version đang `IN_REVIEW`.
- Không có sửa nội dung nào chen giữa lúc gửi và lúc duyệt, vì trigger đóng băng body khi không còn `DRAFT`.
- Cùng transaction: chuyển sang `APPROVED`, ghi event `APPROVED` với snapshot `EXACT`.
- Không tạo version mới. Không tạo dòng `ACTIVE`.

Tạo version mới:

- Knowledge đã có ít nhất một version `APPROVED`.
- Chưa có version `DRAFT` mở và chưa có version `IN_REVIEW` của cùng knowledge.
- Insert `version_no = MAX + 1`, trạng thái `DRAFT`, nội dung khởi tạo bằng bản người chọn, mặc định là version `APPROVED` mới nhất. Bản nguồn không đổi.
- Event `CREATED` của version mới là snapshot `EXACT`.

Sửa nháp:

- Chỉ version `DRAFT`, cùng quy tắc độ dài M1.
- Vẫn sửa tại chỗ. Event `UPDATED` là snapshot `EXACT` sau sửa.
- Version đã từng `APPROVED` không bao giờ trở lại `DRAFT`.

Kích hoạt:

- Version `APPROVED`, cùng tenant, loại không lâm sàng.
- Chưa có dòng áp dụng `ACTIVE` cho đúng version đó.
- Ghi dòng áp dụng và event áp dụng trong cùng transaction.
- Không đổi version khác.

Ngừng và khôi phục:

- Ngừng chỉ từ `ACTIVE` của đúng version. Khôi phục chỉ từ `STOPPED` của đúng version.
- Version vẫn `APPROVED`. Event cũ không bị xóa.

### 3.6 Từ chối

Từ chối khi: sai tenant, không phải principal ERP, thiếu quyền, sai trạng thái nguồn, lệch `updated_at`, body rỗng, loại không thuộc năm mã, xóa version, sửa body của version không còn `DRAFT`, kích hoạt lâm sàng, tự kích hoạt lúc duyệt, hoặc body yêu cầu chứa `lifecycleStatus` / `tenantId`. Lỗi nghiệp vụ trả 400. Không thấy dòng của đúng tenant trả 404. Xung đột đồng thời trả 409.

---

## 4. Versioning

### 4.1 Mô hình

```text
Knowledge item
  ├─ Version 1  DRAFT  → IN_REVIEW → APPROVED     đóng băng
  │                 └→ DRAFT (trả lại, rồi gửi lại)
  ├─ Version 2  DRAFT  → … → APPROVED             đóng băng
  └─ Áp dụng    (item, version 1) ACTIVE
                (item, version 2) ACTIVE hoặc STOPPED
```

- Một knowledge có nhiều version. `UNIQUE (knowledge_id, version_no)`.
- Trong một knowledge chỉ có tối đa một `DRAFT` và tối đa một `IN_REVIEW`. Hai partial unique index.
- `DRAFT` sửa tại chỗ. Đó là phần M1 được giữ cho đến khi gửi.
- Lúc gửi, event `SUBMITTED` là nội dung được trình. Sau câu lệnh đó, trigger cấm đổi `kind`, `title`, `body`, `classification`, `tenant_id`, `knowledge_id`, `version_no`.
- `APPROVED` giữ cấm đó vĩnh viễn. Không có `DELETE` version.
- Event lịch sử vẫn append-only. Snapshot `SUBMITTED` và `APPROVED` không bị sửa khi người sau đó sửa một version nháp khác.
- `LEGACY_UNAVAILABLE` chỉ áp cho event đã được `426` đánh dấu. M2 không suy lại body cho các event đó và không dùng chúng làm căn cứ duyệt.

### 4.2 Hai người cùng sửa hoặc cùng duyệt

M1 chưa có token xung đột. `UpdateAsync` ghi đè version 1 nếu còn `DRAFT`.

M2 thêm `expectedUpdatedAt` trên mọi sửa nháp và mọi transition. Câu lệnh là `UPDATE ... WHERE id = @Id AND tenant_id = @TenantId AND lifecycle_status = @From AND updated_at = @Expected`. Đúng một dòng thì ghi event rồi commit. Không dòng nào thì 409, không ghi event.

Hai người duyệt cùng lúc: một transaction thắng và chuyển sang `APPROVED`. Transaction kia không còn thấy `IN_REVIEW` khớp `updated_at`, nên không ghi event `APPROVED` thứ hai.

Hai người tạo version mới cùng lúc: partial unique trên `DRAFT` để một insert thất bại. Không có hai nháp mở.

Hai người kích hoạt hai version khác nhau: cả hai được, vì M2 cố ý cho nhiều version `ACTIVE`. Hai người kích hoạt cùng một version: một lệnh thắng trên trạng thái nguồn, lệnh kia nhận 409.

Không có merge nội dung. Người nhận 409 phải đọc lại.

---

## 5. Quyền và ủy quyền

### 5.1 Không suy từ role nền tảng

Role `ADMIN` chỉ có tác dụng khi claims transformation đã xác nhận user thuộc tenant trên token. ADMIN của tenant A không quản trị tenant B. ADMIN không được `platform.knowledge.publish`, `pharmacy.knowledge.review`, hay `pharmacy.knowledge.verify-source`.

`pharmacy.knowledge.approve` không được mở thêm nghĩa “duyệt tri thức nhà thuốc”. Pin Global và duyệt nội bộ là hai việc.

### 5.2 Sáu việc

Tên mã dưới đây là đề xuất. Chưa seed, chưa gắn vào picker, chưa cấp cho user nào.

| Việc | Mã đề xuất | Ghi chú |
|---|---|---|
| Soạn | `pharmacy.owned.draft` | Tạo, sửa nháp, mở version mới |
| Gửi duyệt | `pharmacy.owned.submit` | Không bao hàm duyệt |
| Duyệt | `pharmacy.owned.approve` | Duyệt hoặc trả lại |
| Kích hoạt | `pharmacy.owned.activate` | Kích hoạt, ngừng, khôi phục. Vô hiệu với lâm sàng |
| Đọc | `pharmacy.owned.read` | Chỉ bản đang `ACTIVE`, loại không lâm sàng |
| Lâm sàng | `pharmacy.owned.clinical` | Soạn, gửi, duyệt, đọc hồ sơ lâm sàng. Không kích hoạt |

### 5.3 Grant

Đề xuất bảng `pharmacy_owned_knowledge_grant`: `tenant_id`, `user_id`, `action`, `kind`, `granted_by`, `created_at`. Unique theo tenant, user, action, kind. Không có `UPDATE` nội dung grant. Thu hồi là xóa dòng grant hoặc đánh dấu thu hồi bằng event; đề xuất dùng event thu hồi append-only và một cờ `revoked_at` để kiểm tra nhanh. Product Owner chốt một trong hai ở mục 14. Đặc tả không chọn xóa vật lý làm mặc định.

ADMIN cấp grant trong tenant mình. Grant không vượt việc của người cấp. Không ai tự cấp cho mình trừ ADMIN. Kiểm tra grant đọc bảng trên từng request, không dựa vào cache permission 30 giây.

Dược sĩ trong đặc tả này là một user của tenant được ADMIN cấp grant duyệt đúng loại. Không có role hệ thống nào tự thành dược sĩ duyệt.

### 5.4 Ma trận role

| Việc | ADMIN tenant | MANAGER có grant | Dược sĩ có grant | STAFF không grant | Token không phải ERP |
|---|---|---|---|---|---|
| Soạn loại thường | Có | Nếu có `draft` đúng loại | Nếu có `draft` đúng loại | Không | Không |
| Gửi | Có | Nếu có `submit` đúng loại | Nếu có `submit` đúng loại | Không | Không |
| Duyệt / trả lại | Có | Nếu có `approve` đúng loại | Nếu có `approve` đúng loại | Không | Không |
| Kích hoạt loại thường | Có | Nếu có `activate` đúng loại | Nếu có `activate` đúng loại | Không | Không |
| Đọc bản đang áp dụng | Có | Nếu có `read` đúng loại | Nếu có `read` đúng loại | Không | Không |
| Soạn hoặc duyệt lâm sàng | Có | Chỉ với `clinical` đúng việc | Chỉ với `clinical` đúng việc | Không | Không |
| Kích hoạt lâm sàng | Không | Không | Không | Không | Không |
| Cấp grant | Có | Không | Không | Không | Không |
| Pin hoặc publish Global | Không suy ra từ màn này | Không | Không | Không | Không |

“Có grant” nghĩa là dòng grant còn hiệu lực cho đúng action và đúng kind. Role `MANAGER` một mình không đủ.

Tách người soạn và người duyệt không bắt buộc trong M2. Nhà thuốc một chủ vẫn do ADMIN gửi và tự duyệt. Bắt buộc hai người là quyết định mở.

---

## 6. Năm loại

Mã giữ nguyên M1.

| Mã | Nhãn UI hiện có | Duyệt trong tenant | Kích hoạt trong M2 | Runtime M2 |
|---|---|---|---|---|
| `GUIDANCE` | Hướng dẫn | Có | Có, chỉ ghi hồ sơ | Không đọc |
| `OPERATING_PROCEDURE` | Quy trình vận hành | Có | Có, chỉ ghi hồ sơ | Không tạo task, checklist, enrollment |
| `OPERATIONAL_POLICY` | Operational policy | Có | Có, chỉ ghi hồ sơ | Không nới keyword, category, hay cổng an toàn |
| `PRODUCT_KNOWLEDGE` | Product knowledge | Có | Có, chỉ ghi hồ sơ | Không khớp hoạt chất, không thêm vào gợi ý |
| `CLINICAL_KNOWLEDGE` | Clinical knowledge | Có, hồ sơ nội bộ | Không | Không vào Copilot, POS, safety gate, recommendation |

Clinical knowledge muốn thành căn cứ tại quầy phải đi Contribution, rồi version K1, rồi `verify-source`, rồi `platform.knowledge.publish`, rồi pin của chính tenant. M2 không tạo đường đó và không gọi các API đó.

Policy nhà thuốc trong M2 là văn bản. Không có trình thông dịch. Không sửa policy pin Global.

---

## 7. UI trong Admin hiện tại

Sửa trang `Tri thức của nhà thuốc` và menu đã có. Không thêm ứng dụng Admin mới. Không đưa người dùng sang `/platform/pharmacy-knowledge`.

Menu hiện chỉ mở khi `useIsAdmin()`. M2 đổi điều kiện hiển thị theo đúng việc API cho phép: ADMIN thấy cả mục; người chỉ có grant đọc không thấy nút duyệt; người không có việc nào không thấy menu. Không dùng `useHasPermission` cho các nút này, vì hook đó coi ADMIN là mọi permission và sẽ làm lẫn với publish nền tảng.

### 7.1 Danh sách

Một dòng là một knowledge, không phải `MAX(version_no)`. Danh sách M1 hiện chỉ lấy version số lớn nhất. M2 phải bỏ cách đó, nếu không version 2 đang nháp sẽ che version 1 đang áp dụng.

Cột: tiêu đề, loại, phân loại, nháp đang mở nếu có, các version đang `ACTIVE`, trạng thái version mới nhất. Lọc theo loại và theo nhãn mục 3.1. Clinical không có filter “Đang áp dụng”.

### 7.2 Chi tiết

Drawer hoặc trang con trong cùng module: nội dung version đang chọn, số version, người và thời điểm snapshot, nhãn áp dụng. Version `LEGACY_UNAVAILABLE` trong lịch sử hiện câu đã có: nội dung lịch sử gốc không còn khả dụng. Không hiện body đã backfill.

### 7.3 So sánh

Chọn hai version của cùng knowledge. So sánh `kind`, `title`, `classification`, `body`. Nếu một bên không còn snapshot `EXACT`, màn hình nói nội dung bên đó không khôi phục được và không bịa diff.

### 7.4 Việc

Nút theo trạng thái và quyền: Lưu nháp, Gửi duyệt, Trả lại, Duyệt, Tạo bản nháp mới, Kích hoạt, Ngừng áp dụng, Áp dụng lại. Trả lại và ngừng bắt buộc nhập ghi chú. Duyệt và kích hoạt dùng hộp xác nhận của `App.useApp().modal.confirm`, cùng cách màn pin đã dùng, vì `Modal.confirm` tĩnh không hiện dưới React 19.

Không có nút Xóa version. Không có nút Kích hoạt trên lâm sàng. Duyệt xong không tự bật Kích hoạt.

### 7.5 Lịch sử

Một dòng thời gian theo `created_at`, `id`: việc, version, actor, thời điểm, snapshot hoặc ghi chú. Dòng áp dụng nằm trong cùng dòng thời gian nhưng mang nhãn riêng, không được trình bày như một snapshot nội dung mới.

### 7.6 Bản đang áp dụng

Đầu chi tiết liệt kê mọi version `ACTIVE` của mục đó. Nếu có hai bản, cả hai đều hiện. Không có câu “bản mới nhất đang dùng” khi người chưa ngừng bản cũ.

---

## 8. Dữ liệu đề xuất

Không thiết kế lại ba bảng. Không sửa file `424`, `425`, `426`. Thay đổi nằm ở một migration mới, tên dự kiến `427_pharmacy_owned_knowledge_governance.sql`, chỉ được tạo khi Product Owner cho triển khai. Migration đó không vào manifest production trong đặc tả này.

### 8.1 Sửa tối thiểu bảng đã có

`pharmacy_owned_knowledge`: giữ nguyên. Vẫn là đầu mục của tenant.

`pharmacy_owned_knowledge_version`:

- Bỏ check chỉ cho `DRAFT`.
- Check mới: `lifecycle_status IN ('DRAFT', 'IN_REVIEW', 'APPROVED')`.
- Thay hàm `pharmacy_owned_knowledge_version_draft_only` bằng hàm đóng băng. Khi trạng thái cũ không còn `DRAFT`, cấm đổi nội dung và danh tính. Chỉ cho đổi `lifecycle_status` theo ma trận mục 3.2, cùng `updated_at`.
- `BEFORE DELETE` trên version raise.
- Partial unique: một `DRAFT` và một `IN_REVIEW` cho mỗi `(tenant_id, knowledge_id)`.

`pharmacy_owned_knowledge_event`:

- Mở check action. Thêm `SUBMITTED`, `RETURNED`, `APPROVED`. Giữ `CREATED`, `UPDATED`.
- Các action này là snapshot nội dung nên `snapshot_fidelity = 'EXACT'` và body không rỗng.
- `RETURNED` thêm cột `note` null được, bắt buộc với action này. Không nhét ghi chú vào `body`.
- Không nới check để activation đội lốt snapshot. Việc áp dụng ở bảng riêng.
- Trigger append-only giữ nguyên sau khi migration thay check. Migration phải thả trigger, sửa check, gắn lại trigger, cùng kiểu `426`.

### 8.2 Bảng mới

`pharmacy_owned_knowledge_activation`

- `id`, `tenant_id`, `knowledge_id`, `version_id`, `status` là `ACTIVE` hoặc `STOPPED`.
- Unique `(tenant_id, knowledge_id, version_id)`.
- Không unique một `ACTIVE` cho cả knowledge.
- Không `DELETE`.

`pharmacy_owned_knowledge_activation_event`

- Append-only: `ACTIVATED`, `STOPPED`, `RESUMED`.
- `tenant_id`, `knowledge_id`, `version_id`, `actor_user_id`, `note`, `created_at`, `id`.
- `STOPPED` và `RESUMED` đòi `note` không rỗng. `ACTIVATED` được note null.
- Không có `body`. Đây không phải snapshot nội dung.

`pharmacy_owned_knowledge_grant`

- Như mục 5.3. Mọi dòng có `tenant_id`. User được grant phải thuộc cùng tenant; kiểm tra lúc ghi, không tin client.

Không thêm cột vào bảng Global. Không thêm khóa ngoại sang đơn bán, khách, toa, tồn kho, ca, learning, task.

### 8.3 Đọc

List không còn `MAX(version_no)` làm hàng duy nhất. Get một knowledge trả version đang chọn, các version tóm tắt, activation của từng version, và history nội dung của đúng tenant.

So sánh đọc hai version cùng `knowledge_id` và cùng `tenant_id`.

---

## 9. API đề xuất

Giữ bốn endpoint M1. Tenant vẫn chỉ từ `TenantContext`. Body không nhận `tenantId` hay `lifecycleStatus`.

| Phương thức | Đường dẫn đề xuất | Việc | Quyền |
|---|---|---|---|
| GET | `/api/pharmacy/owned-knowledge` | Danh sách đầu mục | ADMIN, hoặc grant đọc / soạn / duyệt |
| GET | `/api/pharmacy/owned-knowledge/{id}` | Chi tiết và lịch sử | Cùng trên, lọc lâm sàng |
| POST | `/api/pharmacy/owned-knowledge` | Tạo nháp version 1 | Soạn |
| PUT | `/api/pharmacy/owned-knowledge/{id}/versions/{versionId}` | Sửa đúng version `DRAFT` | Soạn |
| POST | `.../versions/{versionId}/submit` | Gửi | Gửi |
| POST | `.../versions/{versionId}/return` | Trả lại, có note | Duyệt |
| POST | `.../versions/{versionId}/approve` | Duyệt | Duyệt |
| POST | `.../versions` | Tạo version `DRAFT` kế tiếp | Soạn |
| GET | `.../versions/{left}/compare/{right}` | So sánh | Người được đọc hai version |
| POST | `.../versions/{versionId}/activate` | Kích hoạt | Kích hoạt. Từ chối lâm sàng |
| POST | `.../versions/{versionId}/stop` | Ngừng | Kích hoạt |
| POST | `.../versions/{versionId}/resume` | Khôi phục | Kích hoạt |
| GET | `/api/pharmacy/owned-knowledge/grants` | Xem grant của tenant | ADMIN |
| POST | `/api/pharmacy/owned-knowledge/grants` | Cấp grant | ADMIN |
| POST | `/api/pharmacy/owned-knowledge/grants/{id}/revoke` | Thu hồi | ADMIN |

PUT M1 hiện sửa “version lớn nhất”. M2 đổi thành sửa đúng `versionId` còn `DRAFT`. Không giữ lối sửa ngầm theo MAX sau khi có version 2.

Mọi lệnh ghi nhận `expectedUpdatedAt`. Không có endpoint xóa version, xóa event, hay kích hoạt hàng loạt.

---

## 10. Audit

Mỗi việc ghi đúng một event trong cùng transaction với đổi trạng thái. Commit chỉ sau insert event. Lỗi insert rollback cả đổi trạng thái, cùng cơ chế M1.

Event nội dung lưu: `knowledge_id`, `version_no`, `tenant_id`, action, `kind`, `title`, `body`, `classification`, `snapshot_fidelity = EXACT`, actor, `created_at`, và `note` khi là `RETURNED`.

Event áp dụng lưu: knowledge, version, tenant, action, actor, note, thời điểm. Không lưu body.

Cấm `UPDATE` và `DELETE` cả hai bảng event. API lịch sử chỉ đọc `tenant_id` của token. Không có API sửa lịch sử.

`audit_logs` nền tảng không thay thế các event này. M2 không bắt buộc ghi song song sang `audit_logs`.

---

## 11. An toàn và cô lập

- Mọi câu SQL ghi và đọc có `tenant_id` của token. Không lấy tenant từ body, query, hay hàng người dùng gửi.
- User khác tenant bị gỡ role trước khi policy chạy.
- Token customer, prescriber, partner không qua `IsAdminPrincipal`.
- Clinical không có hàng activation. Check hoặc trigger database từ chối nếu version đích là `CLINICAL_KNOWLEDGE`.
- Không sửa `SymptomTaxonomyRepository`, `PharmacyConsultationService`, `PharmacyOtcSuggestionGate`, dịch vụ pin, dịch vụ publication.
- Không tạo `operational_task`, enrollment, checklist, bài học.
- Không gọi Customer App hay Second Brain.
- Không đụng `NT_XUANHOA`, `DEMO_PHARMACY`, hay database production trong lúc đặc tả.
- Cô lập vẫn là SQL ứng dụng. M1 chưa có RLS. M2 không tuyên bố có RLS trừ khi một đặc tả sau chọn thêm.

---

## 12. Kịch bản kiểm thử khi được phép triển khai

Đặc tả này chưa chạy các kịch bản đó.

| Nhóm | Kỳ vọng |
|---|---|
| Nháp | Tạo và sửa vẫn một transaction với snapshot `EXACT`. Version vẫn là 1 cho đến khi có version mới |
| Cấm tắt | `DRAFT → APPROVED`, `DRAFT → ACTIVE`, `IN_REVIEW → ACTIVE` đều 400. Database cũng raise |
| Gửi | Sau gửi, body không sửa được. Event `SUBMITTED` giữ đúng bốn trường |
| Trả lại | Về `DRAFT`, có note, snapshot trả lại không đổi. Sửa sau đó tạo event `UPDATED` mới. Snapshot đã gửi vẫn còn |
| Duyệt | `APPROVED` đóng băng. Không tự có dòng `ACTIVE` |
| Version mới | Version 1 giữ nguyên từng trường. Version 2 là `DRAFT`. Không có hai `DRAFT` |
| Xóa | `DELETE` version hoặc event raise |
| Hai người sửa | Người giữ `updated_at` cũ nhận 409. Một người duyệt thắng, người kia 409 và không có event duyệt thứ hai |
| Kích hoạt | Version 2 `ACTIVE` không đổi version 1 đang `ACTIVE`. Ngừng version 1 không xóa event. Khôi phục đòi note |
| Lâm sàng | Duyệt được. Activate trả 400 và không có hàng activation |
| Tenant | Tenant B không đọc item, version, event, activation, grant của A |
| STAFF | Không grant thì mọi API trên trả 403 |
| MANAGER | Role không đủ. Có grant soạn thì sửa được loại đó và không duyệt được loại khác |
| ADMIN tenant khác | 404 hoặc 403, không thấy dữ liệu |
| Legacy | Event `LEGACY_UNAVAILABLE` vẫn không trả body backfill |
| Thứ tự | Cùng `created_at` vẫn xếp theo `id` |
| Rollback ghi | Cố tình làm insert event duyệt thất bại thì version còn `IN_REVIEW` |
| Hồi quy | Test K1 governance và cổng `stop_sale`, `refer_medical`, `refer_pharmacist` không đổi. Copilot không có SQL `pharmacy_owned_knowledge` |

---

## 13. Migration, tác động, rollback

`424`, `425`, `426` không sửa. Chúng không có trong `deploy/ubuntu/migration-files.prod.txt` tại thời điểm đặc tả. `427` cũng không được thêm vào manifest production, staff, pharmacy-ops, content, family-os, hay local-os khi mới tạo file.

Tác động dữ liệu khi `427` được áp, sau này, trên một database đã có M1:

- Mọi version đang `DRAFT` vẫn `DRAFT`. Không backfill sang `APPROVED`.
- Event `CREATED` và `UPDATED` giữ nguyên, kể cả dòng `LEGACY_UNAVAILABLE`.
- Chưa có hàng activation. Chưa có mục nào đang áp dụng.
- Hàm đóng băng thay hàm “không được rời draft”. Sau migration, service M2 mới được phép chuyển trạng thái. Nếu chỉ chạy migration mà chưa có service M2, API cũ vẫn chỉ sửa `DRAFT` và sẽ nhận lỗi database nếu cố tình ghi trạng thái khác.

Rollback:

- Trước khi có version nào rời `DRAFT`, có thể gỡ bảng mới và phục hồi check cùng hàm của `424`. Đó là thao tác có chủ đích trên database thử, không phải script tự chạy.
- Sau khi đã có version `APPROVED` hoặc hàng activation, không có rollback im lặng về check chỉ-`DRAFT`. Muốn lùi phải giữ các hàng đó và đưa code đọc được chúng, hoặc phục hồi backup đã chụp trước migration. Đặc tả không chọn xóa version đã duyệt để ép về M1.
- Production và staging không áp `427` trong nhiệm vụ này. Chưa có gì để rollback trên hai môi trường đó.

---

## 14. Quyết định còn mở

| Mã | Câu hỏi | Đề xuất mặc định nếu Product Owner không chọn khác |
|---|---|---|
| OPEN-M2-1 | Tên mã quyền và việc có seed vào catalog `permissions` hay không | Sáu mã mục 5.2. Chưa seed cho đến một milestone triển khai được duyệt riêng |
| OPEN-M2-2 | Thu hồi grant bằng cờ `revoked_at` hay event thu hồi không sửa dòng | Cờ `revoked_at` cộng event append-only |
| OPEN-M2-3 | `OPERATIONAL_POLICY` có được ủy quyền cho dược sĩ hoặc MANAGER không | Duyệt và kích hoạt policy chỉ ADMIN. Soạn có thể ủy quyền |
| OPEN-M2-4 | Có bắt buộc người duyệt khác người soạn không | Không bắt buộc trong M2 |
| OPEN-M2-5 | Có thêm `RETIRED` để khóa vĩnh viễn một version đã duyệt không | Không trong M2. `STOPPED` khôi phục được |
| OPEN-M2-6 | Có cần `effective_from` / `effective_to` trên version nhà thuốc không | Không. Áp dụng là hồ sơ `ACTIVE`/`STOPPED`. M1 không có hai cột này |
| OPEN-M2-7 | Nhiều version `ACTIVE` cùng lúc có còn đúng sau khi có màn runtime không | Giữ đúng kiến trúc V1 trong M2. Runtime sau này không tự đổi trỏ |
| OPEN-M2-8 | Lâm sàng đã `APPROVED` có một trạng thái “đang dùng nội bộ” mà runtime không đọc không | Không. Dừng ở `APPROVED` |
| OPEN-M2-9 | Ghi chú kích hoạt có bắt buộc không | Ngừng và khôi phục bắt buộc. Kích hoạt lần đầu không bắt buộc |

Các quyết định mở không chặn việc review đặc tả. Chúng chặn triển khai của đúng phần đó.

---

## 15. Ranh giới đã giữ trong lần viết này

Chỉ thêm tài liệu này. Không sửa code, không sửa `424`/`425`/`426`, không tạo file migration, không chạy database, không kết nối production hay staging, không cấp quyền, không deploy, không stage, commit hay push.
