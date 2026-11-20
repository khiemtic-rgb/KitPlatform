# NOVIXA — Knowledge UX Blueprint V1

Ngày: 2026-11-01. Loại: thiết kế trải nghiệm. Không triển khai code, không đụng database, không publish, không pin, không cấp quyền.

Phạm vi: bốn lớp trải nghiệm từ quản trị nền tảng tới quầy. K1 governance đã có trong working tree và chưa lên production. Blueprint này không gộp bốn lớp vào một Knowledge Center.

Nguồn đã đối chiếu: `NOVIXA_KNOWLEDGE_K1_GOVERNANCE_SPEC_V1.md`, `NOVIXA_KNOWLEDGE_STAFF_COMPLETION_PLAN.md`, màn Admin và API đang có trong `E:\KitPlatform`. Quyết định Product Owner đã chốt được giữ nguyên. Chỗ chưa có API được ghi là chưa có, không được coi là sẵn sàng.

---

## 1. Kiến trúc trải nghiệm

Knowledge là năng lực hỗ trợ quyết định, vận hành và phát triển đội ngũ. Bốn lớp dùng chung một catalog version, nhưng mỗi lớp có việc, quyền và màn riêng.

```text
Nền tảng                         Nhà thuốc                        Quầy và đội ngũ
┌─────────────────────┐         ┌──────────────────────┐         ┌─────────────────────┐
│ A. Platform Admin   │         │ B. Pharmacy catalog  │         │ C. Staff / học      │
│ soạn, review, nguồn │ publish │ xem bản đã xuất bản  │         │ tra đúng version    │
│ xuất bản, thu hồi   │ ──────► │ ghim đúng version    │ pin     │ bài học trỏ version │
└─────────────────────┘         │ thu hẹp, không nới   │ ──────► ├─────────────────────┤
                                └──────────────────────┘         │ D. Tư vấn trên POS  │
                                                                 │ câu hỏi, cảnh báo   │
                                                                 │ gợi ý chỉ khi an toàn│
                                                                 └─────────────────────┘
```

| Lớp | Việc của người dùng | Không làm |
|---|---|---|
| K1 Platform Admin | Đưa một version qua nháp, xét duyệt, kiểm chứng nguồn, xuất bản, thu hồi | Không ghim hộ nhà thuốc. Không sửa bản đã publish |
| K2A Pharmacy UX | Đọc catalog đã xuất bản, hiểu nguồn và version, quyết định ghim hoặc không ghim | Không thấy bản nháp của nền tảng. Không tự nâng pin |
| K2B Runtime | Copilot và chính sách chỉ đọc pin `APPROVED` đúng version `PUBLISHED` còn hiệu lực | Không có fallback toàn cục. Không dùng `KR_OTHER` |
| K3 Staff | Tra hướng dẫn và học đúng version nhà thuốc đang ghim | Không duyệt rule. Không publish. Không tự ghi danh từ một rule |
| K4 Consultation | Trong modal tư vấn POS hiện có: hỏi, cảnh báo, gợi ý sản phẩm khi cổng an toàn cho phép | Không mở thêm app. Không hiện sản phẩm khi phải dừng hoặc chuyển cấp |

Ranh giới triển khai:

| Mã | Nghĩa trong blueprint | Đã có trong working tree |
|---|---|---|
| K1 | Governance nền tảng | API vòng đời, nguồn, cổng publish, trigger đóng băng, hai màn Admin thô |
| K2A | Trải nghiệm nhà thuốc đọc và quyết định | Màn ghim một bảng, chưa có tìm kiếm, chưa có hàng đợi version mới |
| K2B | Adoption và runtime | Pin, policy thu hẹp, Copilot đúng version. Chưa có tín hiệu “version mới” cho người dùng |
| K3 | Nhân viên và học | Learning `/people` độc lập. Không có `KR_*` trong bài học |
| K4 | Tư vấn | Modal tư vấn trong POS. Cổng an toàn đã chặn danh sách OTC. Gợi ý chưa mang mã rule và version |

---

## 2. Personas và quyền

Quyền dưới đây là claim tường minh. Vai trò ADMIN không suy ra các quyền tri thức. Picker nhà thuốc hiện chỉ có `pharmacy.knowledge.approve`. Hai mã review và verify-source có trong policy của working tree, chưa được seed và chưa được cấp.

| Persona | Việc | Quyền cần có | Không suy từ |
|---|---|---|---|
| Người xét nội dung nền tảng | Gửi `DRAFT → IN_REVIEW`, trả `IN_REVIEW → DRAFT`, sửa nội dung chưa publish | `pharmacy.knowledge.review` | ADMIN, publish, verify, approve |
| Người kiểm chứng nguồn | Ghi trích dẫn và đặt `ACCEPTED` hoặc `REJECTED` | `pharmacy.knowledge.verify-source` | ADMIN, review, publish, approve |
| Người xuất bản nền tảng | `IN_REVIEW → PUBLISHED` khi nguồn đã `ACCEPTED`, hoặc `PUBLISHED → RETIRED` | `platform.knowledge.publish` | ADMIN, review, verify, approve |
| Chủ nhà thuốc | Đọc catalog đã publish, ghim đúng version, thu hồi pin, thu hẹp policy | `pharmacy.knowledge.approve` | `sales.pos`, `sales.read`, ADMIN |
| Dược sĩ / nhân viên quầy | Tư vấn trong POS, thấy cảnh báo, không duyệt rule | `sales.pos` cho quầy | approve, publish, review, verify |
| Nhân viên học | Vào `/people` theo quyền học hiện có | `learning.read` hoặc đường đọc học đang có | approve, publish |
| Quản lý đội ngũ | Giao bài, đánh giá, ghi nhận | `learning.write` | Không chấm năng lực bằng doanh số. Không publish rule |

Một người có thể giữ hai claim sau này. Hai việc vẫn là hai lần bấm và hai dấu vết. Publisher không tự biến bản chưa `ACCEPTED` thành đã xuất bản.

DEC-015 giữ nguyên: chủ thể pin là thẩm quyền nhà thuốc, chưa gán tài khoản. DEC-016 giữ nguyên: thứ tự rule xem trước là theo từng nhà thuốc, không có danh sách cứng.

---

## 3. Hành trình

### 3.1 Nền tảng đưa một rule ra ngoài

1. Người review mở catalog nền tảng, sửa nội dung khi còn nháp hoặc đang xét, gửi xét duyệt.
2. Người nguồn nhập trích dẫn, định danh, URL hoặc mã hồ sơ, cơ quan, vị trí bằng chứng, ngày văn bản nếu có. Bấm Chấp nhận hoặc Từ chối. Không có giá trị sẵn để chấp nhận.
3. Sửa keyword, lý do, triệu chứng, nhóm hàng hoặc từ loại trừ làm mất `ACCEPTED`. Phải kiểm chứng lại.
4. Người publish chỉ thấy nút Xuất bản khi vòng đời là `IN_REVIEW`, nguồn `ACCEPTED`, và mã không phải `KR_OTHER`. Xác nhận một lần.
5. Thu hồi là hành động riêng từ `PUBLISHED`. Bản retired còn trong lịch sử, không sửa nội dung.

### 3.2 Nhà thuốc tiếp nhận

1. Chủ nhà thuốc mở catalog đã xuất bản của đúng tenant. Bản nháp và bản đang xét không có ở đây.
2. Lọc theo triệu chứng hoặc nhóm hàng, mở chi tiết: lý do, từ khóa, nguồn, version, hiệu lực.
3. Nếu chưa ghim: nhập ghi chú pin rồi bấm Ghim. Ghi chú pin không thay trích dẫn chuyên môn.
4. Nếu đã ghim version cũ và nền tảng đã xuất bản version mới: pin giữ version cũ. Màn hình nói rõ “đang dùng version X, version Y đã xuất bản”. Người dùng tự ghim Y nếu muốn. Không có nút “cập nhật tất cả”.
5. Thu hẹp: tắt gợi ý sản phẩm, thêm từ loại trừ, hạ ưu tiên. Không thêm từ khóa hay nhóm hàng.
6. Thu hồi pin là xác nhận riêng. Copilot của tenant đó ngừng dùng rule đó. Tenant khác không đổi.

Không ghim là cách từ chối hôm nay. Không có trạng thái “đã từ chối” trong dữ liệu. Xem mục 9.

### 3.3 Nhân viên trong ca

1. Trong POS, nhân viên mở modal tư vấn hiện có. Không chuyển sang một trung tâm tri thức.
2. Cảnh báo an toàn đứng trên mọi gợi ý sản phẩm.
3. Khi cần đọc vì sao có gợi ý, một dòng lý do và version đang ghim mở ngay trong modal. Bài học đầy đủ nằm ở `/people`, chỉ khi bài đó đã được gắn đúng version.
4. Nhân viên không thấy nút publish, pin, hay sửa rule.

### 3.4 Quản lý học

1. Bài học trỏ `rule_code` và version đang `PUBLISHED` và đang được tenant pin. Không copy `reason_vi` sang markdown rồi sửa lệch.
2. Khi pin đổi version hoặc version retired, bài cũ còn đó và được đánh dấu không còn là bản đang dạy. Không tự trỏ sang pin mới. Không tự ghi danh, không tự tạo việc, không tick checklist.

---

## 4. Information architecture và navigation

Giữ điều hướng hiện có. Thêm mục chỉ khi lớp đó được làm, và chỉ hiện khi có đúng claim.

| Mục nav | Đường dẫn hiện có | Ai thấy | Lớp |
|---|---|---|---|
| Tri thức dùng chung | `/platform/pharmacy-knowledge` | review hoặc verify-source hoặc publish | A |
| Duyệt tri thức | `/pharmacy/knowledge` | `pharmacy.knowledge.approve` | B |
| POS | `/sales/pos` | quầy | D, và lối tắt C |
| Đào tạo và các mục `/people` | `/people`, `/people/learn`, việc, đánh giá, nghề, ghi nhận, thư | quyền học hiện có | C |

Không thêm mục “Knowledge Center”. Không đưa review hay verify vào picker cho đến khi Product Owner chốt tên quyền và người giữ quyền.

Trong một màn, thứ tự thông tin:

1. Trạng thái quyết định (vòng đời, nguồn, pin, cảnh báo an toàn).
2. Việc tiếp theo đúng quyền.
3. Nội dung chuyên môn.
4. Lịch sử và chi tiết nguồn, mở khi cần.

---

## 5. Screen inventory

### A. Platform Admin

| Màn | Hiện trạng | Việc của blueprint |
|---|---|---|
| Catalog version | Có. Một bảng, drawer | Thêm lọc client theo mã, vòng đời, trạng thái nguồn. 35 rule không cần API tìm kiếm mới |
| Drawer chi tiết | Có. Vòng đời, nguồn, nút theo quyền | Giữ một drawer. Không tách ba ứng dụng |
| Soạn nội dung chưa publish | API `PUT .../content` có. UI chưa có | Thêm khối sửa trong drawer, chỉ khi có quyền review và vòng đời là `DRAFT` hoặc `IN_REVIEW` |
| Kiểm chứng nguồn | Có form và hai nút | Giữ. Không điền sẵn |
| Xuất bản / thu hồi | Có, có điều kiện | Thêm hộp xác nhận |
| Lịch sử xuất bản | Có, chỉ sự kiện publish và retire | Ghi rõ đây không phải lịch sử sửa nội dung. Lịch sử kiểm chứng nguồn chưa có bảng sự kiện |

### B. Pharmacy

| Màn | Hiện trạng | Việc của blueprint |
|---|---|---|
| Catalog đã publish | Có. Một bảng version và nhãn pin | Tách ba nhóm nhìn: đang dùng, chưa ghim, có version mới hơn pin |
| Chi tiết version | Có drawer: lý do, từ khóa, nhóm, pin, policy | Thêm nguồn và version đang hiệu lực. Không hiện bản chưa publish |
| Ghim / thu hồi pin | Có | Thêm xác nhận. Giữ bắt buộc ghi chú pin |
| Policy thu hẹp | Có khi đang ghim đúng version | Giữ. Nhãn nói rõ không được nới |
| Lịch sử adoption | Có danh sách sự kiện của tenant | Giữ trong drawer |

### C. Staff

| Màn | Hiện trạng | Việc của blueprint |
|---|---|---|
| `/people` và bài học | Có, không biết `KR_*` | Một thẻ “bản đang dạy” trên bài đã được gắn version |
| Tra cứu lúc làm việc | Chưa có | Một popover trong modal POS, không phải màn mới |
| Đánh giá | Có gợi ý điểm từ bài, checklist, đơn | Không đổi công thức trong K3. Tình huống tư vấn là việc sau, khi đã có pin |
| Phản hồi “rule không khớp” | Chưa có API | Không vẽ nút ghi dữ liệu cho đến khi có quyết định và API |

### D. Consultation

| Màn | Hiện trạng | Việc của blueprint |
|---|---|---|
| `PosConsultationModal` trong POS | Có bốn phần: khách, triệu chứng, câu hỏi, sản phẩm | Giữ modal. Không thêm wizard mới |
| Cảnh báo | Có headline, cờ, Alert khi không gợi ý sản phẩm | Giữ ưu tiên trên cùng |
| Danh sách sản phẩm | Có khi `suggest` trả về | Thêm version lên lý do khi API có trường đó |
| Tư vấn gần đây | `RecentConsultationsList` trên POS | Giữ mức an toàn đã lưu. Không biến thành kho rule |

---

## 6. Wireframe cấu trúc

Ký hiệu: `[ ]` nút, `( )` chỉ đọc, `…` vùng mở khi cần. Ant Design hiện có: `Card`, `Table`, `Drawer`, `Descriptions`, `Tag`, `Result`, `Form`, `Alert`, `message`. POS giữ lớp `pc-*`.

### A. Catalog nền tảng

```text
Card: Tri thức dùng chung
  dòng phụ: ba quyền tách nhau; Copilot chưa dùng cho đến khi nhà thuốc ghim
  lọc: mã | vòng đời | nguồn
  bảng: Mã | Version | Vòng đời | Nguồn | Hiệu lực | [Xem]

Drawer
  (Vòng đời) (Nguồn) (Người xác minh) (Thời điểm)
  (Trích dẫn, định danh, URL, mã hồ sơ, cơ quan, ngày, vị trí)
  (Lý do, từ khóa, nhóm, loại trừ, triệu chứng, ưu tiên)
  nếu review và chưa publish:
    [Sửa nội dung…]
  nếu verify và chưa publish:
    form nguồn trống
    [Chấp nhận nguồn] [Từ chối nguồn]
  nếu publish và đủ cổng: [Xuất bản]
  nếu publish và PUBLISHED: [Thu hồi]
  lịch sử xuất bản
```

Sửa nội dung, chỉ trong drawer:

```text
Lý do
Từ khóa
Từ loại trừ
Nhóm hàng
Triệu chứng
[Lưu nháp]
dòng phụ: lưu các trường này sẽ đưa nguồn về UNVERIFIED
```

Không có ô sửa priority hay ngày hiệu lực ở lát này. API content hiện không nhận hai trường đó.

### B. Catalog nhà thuốc

```text
Card: Tri thức của nhà thuốc
  dòng phụ: pin không tự đổi
  ba đoạn, cùng một bảng lọc:
    Đang dùng          version pin = version dòng, APPROVED, còn hiệu lực
    Chưa ghim          không có pin APPROVED
    Có bản mới hơn     có pin version cũ, dòng này là version publish mới hơn

  bảng: Mã | Version | So với pin | Hiệu lực | [Xem]

Drawer
  (Version này) (Version đang ghim hoặc "chưa ghim")
  (Lý do, từ khóa, nguồn trích dẫn, hiệu lực)
  nếu chưa ghim version này và còn hiệu lực:
    ghi chú pin
    [Ghim version này]
  nếu đang ghim version này:
    [Thu hồi pin]
    tắt gợi ý sản phẩm
    từ loại trừ thêm
    ưu tiên chỉ được hạ
    [Lưu thu hẹp]
  lịch sử của nhà thuốc
```

### C. Thẻ trong bài học và popover quầy

```text
Trên bài đã gắn, trong /people/learn
  (Đang dạy KR_… version N) hoặc (Không còn là bản đang dạy)
  [Đọc hướng dẫn]
  không có nút ghim hay xuất bản

Trong modal POS, trên một gợi ý
  lý do một dòng
  [Vì sao?] → popover: mã, version đang ghim, lý do
  không điều hướng sang Admin
```

### D. Modal tư vấn, giữ bốn phần hiện có

```text
1. Khách
2. Triệu chứng
3. Câu hỏi cần khai thác
4. Hỗ trợ sản phẩm
   nếu stop_sale | refer_medical | refer_pharmacist:
     Alert: Không gợi ý sản phẩm tự động
     không có lưới sản phẩm, không có Thêm vào đơn
   nếu không đủ rule hoặc không khớp hàng:
     Alert: Chưa có sản phẩm tham khảo + lý do API
   nếu được gợi ý:
     lưới sản phẩm, lý do, version
     [Thêm vào đơn] chỉ với dòng nhân viên đã chọn
```

---

## 7. Trạng thái

Dùng `Spin` cho loading, `Result` 403 cho thiếu quyền, `Alert` và `message` cho lỗi API, `Empty` hoặc đoạn chữ trong bảng khi không có dòng. Không thêm thư viện.

| Tình huống | A. Nền tảng | B. Nhà thuốc | C. Nhân viên | D. Tư vấn |
|---|---|---|---|---|
| Loading | Bảng `loading` | Bảng `loading` | Thẻ bài | `Spin` ở phần sản phẩm, như hiện tại |
| Empty | Chưa có version | Chưa có bản publish cho tenant | Bài chưa gắn rule: không hiện thẻ | “Phân tích triệu chứng trước…” hoặc “Chưa có sản phẩm tham khảo” |
| Error | `message` từ API, ở lại màn | `message` từ API | `message` từ API học | `message` hoặc Alert, không xóa triệu chứng đã chọn |
| 403 | Result nêu ba quyền nền tảng, nói ADMIN không thay quyền, chỉ sang màn ghim | Result nêu `pharmacy.knowledge.approve` | Giữ cổng `/people` hiện có | POS không có quyền tri thức thì không hiện nút quản trị |
| Pending | `DRAFT`, `IN_REVIEW`, nguồn `UNVERIFIED` | Chưa ghim | Bài gắn version không còn pin: nhãn không còn đang dạy | Câu hỏi chưa trả lời giữ trong modal |
| Approved | Nguồn `ACCEPTED` chưa có nghĩa là đã publish | Pin `APPROVED` đúng version | Thẻ “đang dạy” chỉ khi pin khớp | Không dùng từ approved cho gợi ý sản phẩm |
| Rejected | Nguồn `REJECTED`, không xuất bản | Không có trạng thái từ chối riêng. Không ghim là chưa dùng | Không có | Không gợi ý không phải là rejected rule |
| Retired | Vòng đời `RETIRED`, chỉ đọc | Pin `RETIRED` hoặc version không còn trong catalog đang dùng | Thẻ “không còn là bản đang dạy” | Copilot không trả rule đó |

`ACCEPTED` trên nguồn và `APPROVED` trên pin là hai từ khác nhau. UI không dùng một nhãn “đã duyệt” cho cả hai.

---

## 8. Tương tác và điểm xác nhận

| Hành động | Xác nhận | Hệ quả người dùng phải thấy |
|---|---|---|
| Gửi xét duyệt | Không. Một bấm | Vòng đời thành `IN_REVIEW` |
| Trả về nháp | Không | Vòng đời thành `DRAFT`. Nguồn không tự mất nếu nội dung không đổi |
| Lưu sửa nội dung | Một dòng: nguồn đã chấp nhận sẽ về `UNVERIFIED` | Tag nguồn đổi trước khi đóng drawer |
| Chấp nhận nguồn | Không tự chạy. Bấm riêng. Form trống thì báo thiếu trường, không gọi API | Tag `ACCEPTED`, có người và thời điểm |
| Từ chối nguồn | Bấm riêng | Tag `REJECTED`. Không cần đủ trích dẫn |
| Xuất bản | Hộp: “Xuất bản không làm nhà thuốc tự ghim” | Vòng đời `PUBLISHED`. Nút xuất bản biến mất |
| Thu hồi bản publish | Hộp: bản đã ghim ở nhà thuốc không tự đổi, Copilot của tenant còn pin sẽ ngừng khi runtime thấy version không còn `PUBLISHED` | Vòng đời `RETIRED` |
| Ghim | Nút tắt cho đến khi có ghi chú pin | Tag pin đúng version. Tenant khác không đổi |
| Thu hồi pin | Hộp: quầy tenant này sẽ không còn dùng rule | Tag chưa ghim |
| Lưu thu hẹp | Không | Công tắc và từ loại trừ hiện lại đúng giá trị đã lưu |
| Thêm sản phẩm vào đơn | Chỉ dòng đã tick | Không thêm cả danh sách ngầm |

Không có xác nhận hàng loạt. Không có “ghim tất cả rule đã publish”.

---

## 9. API và dữ liệu từng luồng

Đã có, dùng lại:

| Luồng | API |
|---|---|
| Danh sách và một version nền tảng | `GET /api/platform/pharmacy-knowledge/versions` và `.../versions/{rule}/{version}` |
| Gửi xét, trả nháp | `POST .../submit-review`, `POST .../return-draft` |
| Sửa nội dung chưa publish | `PUT .../content` |
| Kiểm chứng nguồn | `PUT .../source` |
| Xuất bản, thu hồi | `POST .../publish`, `POST .../retire` |
| Lịch sử xuất bản | `GET /api/platform/pharmacy-knowledge/history` |
| Catalog tenant, chỉ bản publish | `GET /api/pharmacy/knowledge-rules` |
| Chi tiết tenant được phép thấy | `GET /api/pharmacy/knowledge-rules/{rule}/versions/{version}` |
| Ghim, thu hồi pin | `POST .../approval` với `APPROVED` hoặc `RETIRED` |
| Policy thu hẹp | `POST .../policy` |
| Lịch sử pin của tenant | `GET .../approval` |
| Tư vấn | `GET/POST /api/sales/pos/consultation/...` gồm symptom-options, extract, questions, suggest, sessions |

Chưa có, không được giả là sẵn:

| Nhu cầu UX | Vì sao chưa làm trong blueprint như thể đã có |
|---|---|
| API tìm kiếm server-side | List hiện trả cả catalog. 35 rule lọc trên client |
| Nhật ký từng lần kiểm chứng nguồn | Chỉ có `verified_by` và `verified_at` trên version, bị xóa khi mất `ACCEPTED` |
| Trạng thái “nhà thuốc từ chối rule” | Adoption chỉ có `APPROVED` và `RETIRED`. Không ghim không lưu lý do từ chối |
| Cờ “có version mới hơn pin” | Dữ liệu để so version đã có trên catalog và adoption. Chưa có trường riêng. UI K2A tự so |
| `rule_code` và version trên từng gợi ý POS | `PharmacyConsultationProductSuggestionDto` có `Reason` và `MatchSource`, không có mã rule hay version |
| Bài học trỏ rule | `pack_learning.module` không có cột rule. K1 đã chốt đây là việc sau |
| Phản hồi quầy thành mục review | Không có endpoint |
| Tự ghi danh, tự tạo việc, tự tick checklist từ rule | Bị cấm. Không thiết kế nút đó |

Copilot đọc `pharmacy_knowledge_adoption` nối đúng `rule_version`, tenant, `APPROVED`, `PUBLISHED`, còn hiệu lực, và loại `KR_OTHER`. Publish version 2 không sửa pin version 1. Blueprint K2B chỉ làm cho sự thật này nhìn thấy được.

---

## 10. Safety UX

Cổng `PharmacyOtcSuggestionGate` không hiện danh sách OTC thường khi mức là `stop_sale`, `refer_medical`, hoặc `refer_pharmacist`. Modal đã có Alert “Không gợi ý sản phẩm tự động” qua `blocksProductSupport`. Blueprint giữ Alert đó trên phần sản phẩm và không thêm bước bấm để “xem dù vậy”.

Tình huống không có lưới sản phẩm và không có “Thêm vào đơn”:

| Mức | Ví dụ đã có trong luật | Người dùng thấy |
|---|---|---|
| `stop_sale` | Ngất, co giật | Dừng bán, gọi cấp cứu. Không có sản phẩm |
| `refer_medical` | Đau ngực | Chuyển cơ sở y tế. Không có sản phẩm |
| `refer_pharmacist` | Mang thai, cho con bú, trẻ dưới 2 tuổi, khó nuốt | Chuyển dược sĩ. Không có sản phẩm |

Các trường hợp sau cũng không có danh sách sản phẩm, nhưng là “chưa có gợi ý”, không phải cảnh báo đỏ:

- Chưa chọn triệu chứng.
- Triệu chứng không thuộc chế độ gợi ý OTC.
- Không có pin, pin không còn hiệu lực, hoặc hết từ khóa sau policy.
- Khớp category mà không khớp từ khóa. Category không đủ để gợi ý.
- `KR_OTHER`.

Dòng miễn trừ hiện có giữ nguyên: hỗ trợ sàng lọc, không thay đánh giá của dược sĩ. Nhân viên vẫn tự tìm trên POS nếu cần bán bằng tay. Hệ thống không chèn sản phẩm vào đơn.

13 cờ `pharmacy_safety_flag_review` đang `PENDING_REVIEW` không phải nguồn của 35 rule và không được vẽ thành nút duyệt trong các lớp này.

---

## 11. Mobile và quầy

| Bề mặt | Cách dùng | Blueprint |
|---|---|---|
| Admin nền tảng và nhà thuốc | Bàn làm việc | Bảng và drawer 520–560px như hiện tại. Dưới 768px, drawer full-width, bảng giữ cột mã, version, trạng thái, nút Xem |
| POS | Máy quầy, có thể là máy tính hoặc tablet ngang | Modal tư vấn là bề mặt chính. Popover “Vì sao” không được đẩy nhân viên sang route khác |
| `/people` | Điện thoại nhân viên có thể mở học | Thẻ version một cột. Không nhét form ghim vào bài học |

Không thiết kế app khách. Customer App không đọc publication, adoption, hay Learning.

---

## 12. Acceptance theo lớp

### A. Platform Admin

- Thiếu claim thì 403, kể cả ADMIN.
- Review không thấy Chấp nhận nguồn hay Xuất bản.
- Verify không thấy Gửi xét duyệt hay Xuất bản.
- Publish không thấy form nguồn.
- `DRAFT` không xuất bản được.
- `KR_OTHER` không có nút Xuất bản.
- Sửa nội dung chưa publish đưa `ACCEPTED` về `UNVERIFIED`.
- Bản `PUBLISHED` và `RETIRED` không có form sửa.
- Không có nút ghim trên màn nền tảng.

### B và runtime K2

- Nhà thuốc chỉ thấy version `PUBLISHED`.
- Ghim yêu cầu ghi chú và version còn hiệu lực.
- Sau khi publish version 2, nhãn pin vẫn là version 1 cho đến khi người có quyền ghim version 2.
- Tenant A không thấy lịch sử pin của tenant B.
- Policy không có ô thêm từ khóa hoặc nhóm hàng.
- Tắt gợi ý sản phẩm làm Copilot không trả sản phẩm của rule đó.
- `chest_pain`, `unconscious`, `pregnant` không trả danh sách OTC thường.

### C. Staff

- Nhân viên không thấy nút publish hoặc pin.
- Bài chưa được gắn rule không hiện thẻ version.
- Bài gắn version không còn pin hiện “không còn là bản đang dạy”.
- Mở hướng dẫn từ POS không đổi route bắt buộc.
- Gắn bài không tạo enrollment, không tạo operational task, không tick checklist.

### D. Consultation

- Cảnh báo đứng trước sản phẩm.
- Ba mức dừng hoặc chuyển cấp không có lưới sản phẩm và không có nút thêm vào đơn.
- Khi được gợi ý, thêm vào đơn chỉ gồm dòng đã chọn.
- Khi API đã có version trên gợi ý, popover hiện đúng version đang pin, không hiện version mới chưa ghim.

---

## 13. Roadmap

Thứ tự là phụ thuộc sản phẩm, không phải ngày. Không deploy production từ blueprint này. K1 trong working tree chưa phải bản production.

### Việc K1 còn lại trên UX, trước khi gọi lớp này là đủ dùng

API soạn nội dung đã có, màn chưa có. Lịch sử kiểm chứng nguồn chưa có bảng. Phần này khép trải nghiệm nền tảng đã xây, không phải K2.

### K2A — Pharmacy Knowledge UX

Làm trên `/pharmacy/knowledge`. Ba nhóm đang dùng, chưa ghim, có bản mới. Hiện nguồn và version. Xác nhận ghim và thu hồi pin. Lọc client. Chưa cần API mới nếu chỉ so version catalog với pin.

Cửa vào: đã có ít nhất một version `PUBLISHED` với nguồn `ACCEPTED`, và đã có chủ thể được phép giữ `pharmacy.knowledge.approve`. Chưa có thì màn nhà thuốc ở trạng thái empty an toàn, không seed pin.

### K2B — Tenant adoption và runtime

Runtime pin và Copilot đã có trong working tree. Lớp này không viết một màn mới. Việc UX là câu “quầy đang dùng version nào” trên drawer nhà thuốc, và bảo đảm publish version mới không đổi pin. Kiểm thử bằng suite tenant knowledge đã có, không bằng cách pin 35 rule seed.

### K3 — Staff và học

Chỉ sau khi một rule của nhà thuốc pilot đã được pin. Thẻ version trên bài được phép gắn. Popover trong POS. Không viết lại L0–L6. Không nối đánh giá với doanh số. Phản hồi “không khớp” chờ API và quyết định người nhận.

### K4 — Consultation

Giữ `PosConsultationModal`. Không thêm bước. Việc mới chỉ là hiện version trên lý do gợi ý sau khi DTO suggest có mã rule và version, và kiểm tra lại ba mức an toàn không có lưới sản phẩm. Cổng an toàn không chờ K3.

---

## 14. Quyết định Product Owner còn cần

Blueprint không chọn hộ.

| Việc | Vì sao còn mở |
|---|---|
| Giữ mã `pharmacy.knowledge.review` và `pharmacy.knowledge.verify-source` hay đổi tên | Working tree đang dùng hai mã này. Chưa seed, chưa cấp |
| Ai giữ từng quyền, một người có được vừa verify vừa publish không | Chưa gán tài khoản |
| Tài khoản nào giữ `pharmacy.knowledge.approve` | DEC-015 đã chọn chủ nhà thuốc, chưa gán |
| Rule nào nhà thuốc xem trước | DEC-016, theo từng nhà thuốc |
| Có lưu “từ chối áp dụng” kèm lý do hay chỉ cần không ghim | Chưa có trạng thái từ chối |
| Nhân viên quầy có được thấy trích dẫn đầy đủ hay chỉ lý do và version | Chưa chốt mức chi tiết tại quầy |
| Bài nào được trỏ rule, ai duyệt bài | Đã mở từ K1 và kế hoạch S3 |
| `KR_OTHER` có được viết lại thành rule khác không | Nội dung hiện không đủ điều kiện publish |
| Category seed có khớp category hàng của nhà thuốc không | Qualification: chưa kiểm chứng |
