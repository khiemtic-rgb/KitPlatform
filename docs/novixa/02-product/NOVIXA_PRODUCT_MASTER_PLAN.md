# NOVIXA — Product Master Plan V1.1

**Mã:** NVX-PRD-MASTER · **Ngày:** 2026-10-31 · **Bản:** 1.1 cùng ngày · **Phạm vi:** nhà thuốc trên `E:\KitPlatform`  
**Vai trò:** nguồn định hướng sản phẩm. Không thay kiến trúc as-built, không thay catalog Phase 1, không phải báo cáo deploy.

Đọc tài liệu này trước khi mở một nhiệm vụ lớn. Xác định phase, capability ID, hiện trạng, phần còn phải làm, và giới hạn không được vượt.

---

## Mục lục

1. Cách dùng và bốn lớp tài liệu
2. Tầm nhìn
3. Mô hình vận hành sản phẩm
4. Bản đồ phân hệ
5. Lộ trình
6. Hiện trạng theo capability
7. W1–W6, Customer App, các mảng đã từng được báo cáo xong
8. Decision Register
9. Chỉ số và nghiệm thu
10. Cách duy trì
11. Mâu thuẫn và việc còn mở

---

## 1. Cách dùng và bốn lớp tài liệu

| Lớp | Tài liệu này | Tài liệu giữ nguyên |
|---|---|---|
| Product Bible | Mục 2–5 và Decision Register | — |
| Technical Design | Không viết lại ở đây | [KIT-BP-ASBUILT](../03-solution/kitplatform-enterprise-blueprint-asbuilt-v2.1.md), [solution-architecture-v1.md](../03-solution/solution-architecture-v1.md) |
| Delivery Plan | Mục 5 | Epic P2 trong `02-product/success-p2-*` vẫn là lịch sử epic, không phải roadmap mới |
| Product Status | Mục 6–7 | Báo cáo vận hành từng đợt giữ nguyên làm bằng chứng |

Vòng phát triển: **Assess → Plan → Improve → Grow**. Một capability chỉ sang Grow khi có số đo. Hiện hầu hết chỉ số nghiệp vụ là **NEED BASELINE**.

Chuỗi quản trị: Vision → Problems → Outcomes → Policies → Domains → Capabilities → Workflows → Rules → Features → UX → Technology → Data → AI → Metrics.

**Không thuộc Novixa nhà thuốc:** Famixa, Local OS (Thái Nguyên Life), Family OS. Chúng nằm cùng kernel, có luật pack riêng, không đưa vào phase của tài liệu này.

---

## 2. Tầm nhìn

### 2.1 Định vị đã chốt trong nhiệm vụ này

**NOVIXA — Nền tảng vận hành và trí tuệ kinh doanh nhà thuốc.**

Không phải POS, không phải ERP, không phải chatbot.

NOVIXA giúp nhà thuốc vận hành tốt hơn, giúp nhân viên bán và phục vụ tốt hơn, giúp chủ quản lý khi vắng mặt, và giảm sự phụ thuộc vào một người.

Giá trị khác biệt:

**NOVIXA tích lũy kinh nghiệm của nhà thuốc thành năng lực chung của hệ thống, để mỗi nhân viên mới có một trợ lý giàu ngữ cảnh ngay từ những ngày đầu.**

Nhân viên phải tiếp nhận khách, hiểu nhu cầu, được hướng dẫn, và bán phù hợp mà không cần người kèm suốt ca.

### 2.2 Bài toán

- Nhà thuốc nhỏ phụ thuộc chủ hoặc dược sĩ chính.
- Nhân viên đổi liên tục, trình độ không đều.
- Người mới không làm được nếu không có người kèm.
- Kinh nghiệm chủ nằm trong đầu, chưa thành hệ thống.
- Chủ không thấy hết hoạt động khi vắng.
- Dữ liệu bán, tồn, khách chưa thành quyết định.
- Cần tăng doanh thu mà không hạ chất lượng tư vấn và an toàn chuyên môn.

### 2.3 Kết quả cần có

| Kết quả | Chưa có số |
|---|---|
| Người mới xử lý được tình huống phù hợp sớm hơn | NEED BASELINE |
| Chủ bớt việc phải đứng cạnh quầy | NEED BASELINE |
| Gợi ý sản phẩm đúng nhu cầu và đúng tồn | NEED BASELINE |
| Ca nguy hiểm được chuyển dược sĩ hoặc cấp cứu, không bị gợi ý OTC | Có luật trong code; chưa đo trên quầy thật |
| Kinh nghiệm cửa hàng được duyệt rồi mới vào đường bán | Quy trình W5 có; chưa có rule nào được publish |

### 2.4 Định vị cũ vẫn còn trong repo

[vision-and-strategy-v1.md](../01-company/vision-and-strategy-v1.md) và [product-overview-v1.md](./product-overview-v1.md) mô tả Novixa là ERP + POS + CRM + app, khác biệt bằng FEFO, dữ liệu một mối, O2O. Phần đó vẫn đúng với **nền vận hành**. Nó không còn là câu định vị chính. Decision `DEC-001`.

---

## 3. Mô hình vận hành sản phẩm

Ba tầng tri thức phục vụ một động cơ quyết định bán. Doanh thu không được đi trước cổng an toàn.

```
Global Knowledge ── publication
Pharmacy Knowledge ── tenant pin + policy
Local & Customer Knowledge ── ngữ cảnh, không tự thành chuyên môn
        │
        ▼
Safety floor (cứng) → Need → Match → Tồn / giá → Playbook → Copilot
        │
        ▼
Nhân viên thực hiện · Chủ thấy ngoại lệ · Hệ thống học sau khi có người duyệt
```

Quy trình tri thức:

AI tiếp nhận → phân loại → chuẩn hóa → xác minh nguồn → kiểm duyệt → phê duyệt → phiên bản hóa → sử dụng → đánh giá kết quả.

Giao dịch bán, ý kiến nhân viên, và hồ sơ khách **không** tự trở thành tri thức chuyên môn đã xác minh.

Ba tầng W5 đã có trong code, giữ nguyên:

- Global publication (`pharmacy_knowledge_rule_version`, sự kiện publication).
- Tenant adoption (một pin mỗi tenant và `rule_code`).
- Tenant operational policy (chỉ được thu hẹp, không nới keyword, category, hay priority).

Không global fallback. Không đọc tri thức tenant khác. `stop_sale`, `refer_medical`, `refer_pharmacist` ẩn danh sách OTC. `KR_OTHER` không đi vào copilot.

`pharmacy.knowledge.approve` là quyền nhà thuốc chấp nhận áp dụng một version. `platform.knowledge.publish` là quyền xuất bản Global Knowledge cho cả nền tảng. Hai quyền không thay nhau. DEC-015: tại Xuân Hòa, chủ nhà thuốc là chủ thể có thẩm quyền chung cho việc nhà thuốc chấp nhận tri thức. Quyết định đó không gán tài khoản và không đổi RBAC. DEC-016: thứ tự xem xét rule theo từng nhà thuốc; không có danh sách ưu tiên cứng cho mọi tenant; doanh số không thay xác minh chuyên môn.

---

## 4. Bản đồ phân hệ

Domain là năng lực nghiệp vụ. Menu và project code chỉ là bằng chứng, không phải domain.

### 4.1 Pharmacy ERP & Operations

POS, catalog, tồn theo lô và hạn, FEFO, mua hàng, trả hàng, chi nhánh, ca bán, checklist ca, công nợ, báo cáo, GPP ở mức vận hành, multi-tenant và multi-branch.

### 4.2 Customer CRM & Engagement

Customer 360, Customer App, lịch sử mua, loyalty, phân khúc, chăm sóc, nhắc thuốc, hồ sơ gia đình, chat và đặt hàng, giữ chân, campaign.

Customer App là năng lực chiến lược (DEC-018): CRM, chăm sóc sau bán, lịch sử mua, loyalty, nhắc thuốc, chat, đặt hàng, hồ sơ gia đình khi còn trong phạm vi, và giữ quan hệ với nhà thuốc. Chưa triển khai đại trà vì chưa đủ ổn định. Việc đó không chứng minh khách không cần, app không có giá trị, hay nhà thuốc không muốn hướng dẫn khách.

Mục tiêu: hoàn thiện thành kênh tương tác và chăm sóc đáng tin, pilot được, rồi mới nhân rộng. Ưu tiên chức năng đã có. Catalog cũ liệt kê **12 bề mặt**. Đó là danh mục màn hình, không phải chứng nhận PRD đã xong. Chi tiết mục 7.2 và 7.4.

### 4.3 Knowledge Management

| Nhánh | Việc của nhánh | Không được làm |
|---|---|---|
| Global Knowledge | Thuốc, triệu chứng, luật an toàn, nguồn, version, lifecycle | Tenant không sửa nội dung đã publish |
| Pharmacy Knowledge | Quy trình, kinh nghiệm chủ, chính sách bán, cách tư vấn riêng, adoption, policy. Chủ nhà thuốc quyết định nhà thuốc có áp dụng hay không | Không hạ safety floor, không dùng tri thức nhà thuốc khác, không tự cấp quyền |
| Local & Customer Knowledge | Cách nói địa phương, thói quen, câu hỏi lặp, phản hồi, ngữ cảnh khách | Không tự thành tri thức chuyên môn đã xác minh |

### 4.4 Pharmacy Intelligence & Sales Decision Engine

Need Recognition, Customer Context, Safety & Qualification Gate, Product Matching, Product Availability, Commercial Intelligence, Price & Margin, Alternative Product, Sales Playbook, Sales Copilot, Guided Selling, Consultation Outcome.

Nhân viên cần: hỏi gì, nhu cầu là gì, khi nào chuyển dược sĩ, sản phẩm nào đủ điều kiện, còn hàng và giá nào, sản phẩm thay thế nào, giải thích thế nào, kết quả tư vấn được ghi lại.

Guided Selling (DEC-017) là cả hành trình phục vụ, không phải API gợi ý. Bốn tiêu chí cùng lúc: tiện việc thật, hiểu đúng ngữ cảnh, ít thao tác, kết quả phù hợp và chính xác. Chưa chốt nằm trong POS hay một màn riêng. Có thể tích hợp POS, màn chuyên biệt, hoặc kết hợp sau khi thử. Không hỏi lại thông tin đã có căn cứ. Khi cần dược sĩ hoặc bác sĩ thì chuyển người, không cố gợi ý bán. Bớt thao tác không được bỏ bước an toàn.

Tối ưu doanh thu hoặc biên lợi nhuận không được vượt cổng phù hợp và an toàn.

### 4.5 Staff Enablement

Hồ sơ nhân viên, onboarding, học, năng lực, lộ trình nghề, ghi nhận, việc được giao, trách nhiệm ca, checklist theo người và ca, đánh giá, gợi ý đào tạo, trợ lý cho nhân viên.

Mục tiêu là người mới làm được việc, không phải bộ HR đầy đủ.

### 4.6 Owner Intelligence

Dashboard chủ, trí tuệ bán hàng, tồn, khách, nhân sự, cảnh báo, khuyến nghị, luật của chủ, ủy quyền, ngoại lệ.

Chủ cần biết chuyện gì đang xảy ra, việc gì phải xử lý, việc gì giao được cho nhân viên.

### 4.7 Shared Platform

Multi-tenant, identity và quyền, tổ chức và chi nhánh, module registry, audit, thông báo, tích hợp, API, điều phối AI, quản trị dữ liệu, bảo mật, quan sát hệ thống.

### 4.8 Future expansion

Clinic Portal, telemedicine, nối bác sĩ và nhà thuốc: ghi là hướng dài, **không** chiếm backlog nhà thuốc hiện tại. Đơn thuốc và cổng người kê đơn đã có pilot riêng (`rx-prescription-module-v1.md`), không biến thành chương trình telemedicine.

---

## 5. Lộ trình

Các phase dưới đây là cấu trúc giao trong nhiệm vụ Product Master. Chúng **không** có nghĩa là phase trước đã xong. Hạng mục sau được phép song song khi dependency đã đủ.

```
P0 Nền ERP/POS/CRM
  └─ P1 Vận hành chắc (ca, checklist, trả hàng, việc)
       └─ P2 Tri thức W5 (publish, pin, policy, safety)
            └─ P3 Quyết định bán (hành trình Guided Selling; Copilot chỉ là một lát)
                 ├─ P4 Tri thức địa phương và học việc
                 └─ P5 Trí tuệ cho chủ
                      └─ P6 Học liên tục và nhân rộng
```

| Phase | Phụ thuộc | Được làm song song | Chưa được coi là xong |
|---|---|---|---|
| 0 Platform & Pharmacy Foundation | Kernel multi-tenant | Gia cố báo cáo, RBAC | Vì production đang bán |
| 1 Operational Excellence | POS, ca, tồn đã chạy | Checklist theo ca, trả hàng idempotent, việc trong ca | Vì migration 411–413 chưa lên production |
| 2 Knowledge Foundation | Symptom taxonomy đã seed | UI duyệt khi đã có quyền, không đổi RBAC trong im lặng | Vì 0 rule PUBLISHED, 0 pin, chưa deploy W5. Chủ nhà thuốc có thẩm quyền chấp nhận áp dụng; publish nền tảng là quyền khác |
| 3 Guided Selling | P2 có version đã publish và nhà thuốc đã pin | Thiết kế hành trình DEC-017. Không bật gợi ý khi chưa đủ W5 | API suggest không phải Guided Selling đã xong |
| 4 Local Knowledge & Learning | P3 có kết quả tư vấn để học; người duyệt đứng ở giữa | Học việc trên nội dung vận hành đã có. Thứ tự rule theo từng tenant | Thu thập AI và dữ liệu khách không tự thành chuyên môn |
| 5 Owner Intelligence | Số từ P0–P1 đáng tin | Cockpit đọc báo cáo đã có | KPI chủ chưa có baseline |
| 6 Scale | Một nhà thuốc vận hành ổn mô hình P2–P5 | Thêm tenant sau khi playbook triển khai được viết | Clinic và telemedicine |

Assess của phase hiện tại: nhà thuốc Xuân Hòa đang sống trên Phase 0. Phase 1 và 2 có code và test cô lập hoặc staging. Production chưa nhận 411–417.

Nhánh CRM-APP chạy song song, không chờ W5. Việc của nhánh là ổn định luồng đã có, rồi pilot, rồi mới xét đại trà. Chi tiết mục 7.4. Phase 0–6 không đảo vì nhánh này.

---

## 6. Hiện trạng theo capability

Trạng thái:

- **IMPLEMENTED** — có code đường chính.
- **TESTED** — có test đã chạy và đạt, ghi rõ phạm vi.
- **STAGING VERIFIED** — đã chạy trên staging VPS B.
- **PRODUCTION VERIFIED** — đã thấy trên production, đúng phạm vi ghi kèm.
- **PARTIAL** — chỉ một lát.
- **PLANNED** — mới có trong định hướng.
- **BLOCKED** — vướng điều kiện đã biết.
- **UNKNOWN** — chưa đủ bằng chứng.

Một domain không nhận một trạng thái chung.

Bằng chứng chính dùng ở nhiều dòng:

- Production `103.200.23.229`, 2026-10-31: API health 200, ledger tới `371`, binary `7bbf208b…`, database `novixa_prod`. Xem [NOVIXA_W5_PRODUCTION_READINESS_V1.md](../03-operations/NOVIXA_W5_PRODUCTION_READINESS_V1.md).
- Staging W5, cùng ngày: binary `cc4eb7e8…`, 17 test tích hợp trên `novixa_w5_staging`, chưa phải production.
- Chẩn đoán 2026-10-08: [KIT-PLATFORM-NOVIXA-PRODUCT-VALUE-DIAGNOSTIC-V1.md](../../architecture/KIT-PLATFORM-NOVIXA-PRODUCT-VALUE-DIAGNOSTIC-V1.md). Phần trăm trong đó là ước lượng diagnostic, không phải đo mới.
- Catalog Phase 1: [module-catalog-v1.md](./module-catalog-v1.md), [PHASE_SCOPE.md](../../../client/admin/PHASE_SCOPE.md). Dấu ✅ của catalog là phạm vi Phase 1 lúc viết, không phải nghiệm thu 2026-10-31.

### 6.1 ERP & Operations

| ID | Năng lực | Mục tiêu | Trạng thái | Bằng chứng | Khoảng trống | Bước tiếp | Nghiệm thu |
|---|---|---|---|---|---|---|---|
| ERP-POS | Bán tại quầy | Bán đúng tồn, đúng giá, đúng ca | PRODUCTION VERIFIED cho việc nhà thuốc đang bán; không tái kiểm từng màn trong lượt này | Đơn và ca trên `NT_XUANHOA`; module `/sales` | Sự cố POS đã có runbook riêng | Giữ khi deploy W5 | Ca bán và đơn không lệch sau cắt |
| ERP-CAT | Danh mục hàng | Mã, tên, hoạt chất, đơn vị | PRODUCTION VERIFIED cho sự tồn tại | 3.038 SP active Xuân Hòa; 3.037 không category | Category mã PGN, không phải mã rule | Không sửa catalog trong lúc chưa được giao | Chủ cửa hàng xác nhận nhóm hàng |
| ERP-INV | Tồn, lô, hạn, FEFO | Không bán lô hết hạn | IMPLEMENTED; PRODUCTION dùng tồn thật | Lô còn hàng được đếm gián tiếp qua gate OTC; `SellableBatchRules` | Chưa đo tỷ lệ bán nhầm hạn | Không đổi công thức tồn | Lô hết hạn không đứng trong gợi ý |
| ERP-PRC | Mua hàng và NCC | Nhập đúng, công nợ NCC | IMPLEMENTED theo catalog | `PHASE_SCOPE` mục mua hàng | Chưa audit lại GRN trong lượt này | UNKNOWN cho mức dùng thực | Đối soát 1 PO |
| ERP-RET | Trả hàng | Trả một lần cho một khóa client | IMPLEMENTED + TESTED ở nhánh idempotency; cột `client_return_id` chưa có trên production | Migration 411 chưa trong ledger; 22 dòng `sales_returns` đã có | 411 chưa apply | Đêm, sau backup | Trùng khóa không tạo phiếu thứ hai |
| ERP-BR | Chi nhánh | Nhiều cửa trên một tenant | IMPLEMENTED | Bảng `branches`; unique ca mở theo kho | Mức dùng nhiều cửa của Xuân Hòa: UNKNOWN | Không mở scope chuỗi | — |
| ERP-SHF | Ca bán | Mở và đóng ca | PRODUCTION VERIFIED là dữ liệu ca tồn tại | 2 ca status 1, 203 ca status 2 lúc 2026-10-31 | Ca mở là cửa chặn deploy đêm | Không tự đóng ca | Ca status 1 = 0 trước cắt đêm |
| ERP-CHK | Checklist ca | Việc đầu ca và cuối ca theo ca POS | PARTIAL | EP02 lab; migration 132 trên production; 412 chưa apply | Checklist chưa buộc theo `sales_shift_id` trên production | 412 trong cửa sổ đêm | 0 dòng trùng khóa ngày |
| ERP-FIN | Công nợ và báo cáo | Chủ xem doanh thu, công nợ, tồn | PARTIAL / PRODUCTION cho Wave 1 | `REPORTS_WAVE1.md`; STAFF không được xem doanh thu | Biên lợi nhuận, P&L: PLANNED | Giữ RBAC | User `sales.pos` không thấy báo cáo doanh thu |
| ERP-GPP | GPP và tuân thủ | Vận hành đúng tinh thần GPP | PARTIAL | Tài liệu `06-compliance`; không có module GPP khép kín | Hồ sơ kiểm tra định kỳ: UNKNOWN | Không khai đã đạt GPP | Biên bản ngoài phần mềm |
| ERP-MT | Multi-tenant | Mỗi nhà thuốc một tenant | PRODUCTION VERIFIED | 8 tenant trên `novixa_prod`, trong đó `NT_XUANHOA` | Migration W5 là cấp database | Không trộn tenant | Pin và tồn không lộ tenant khác |

### 6.2 CRM & Engagement

| ID | Năng lực | Mục tiêu | Trạng thái | Bằng chứng | Khoảng trống | Bước tiếp |
|---|---|---|---|---|---|---|
| CRM-360 | Hồ sơ khách | Một khách, một lịch sử | IMPLEMENTED | `/customer`, module catalog | Mức dùng Xuân Hòa: UNKNOWN | Không bịa MAU |
| CRM-APP | App khách | Kênh chăm sóc đáng tin để pilot, rồi mới nhân rộng | PARTIAL | 12 route trong catalog; roadmap P12–P18 nói L0 ~45–50% PRD; `app.novixa.vn` HTTP 200 ngày 2026-10-31 chỉ chứng minh site mở | Chưa đủ ổn định để đại trà. Không suy ra là khách không cần | Ổn định luồng đã có theo mục 7.4. Nghiệm thu pilot là OPEN-006. Không thêm tính năng cho đủ vẻ |
| CRM-HIST | Lịch sử mua | Khách và quầy thấy đơn cũ | IMPLEMENTED | Controller purchases được dẫn trong evidence doc | Production usage UNKNOWN | — |
| CRM-LOY | Loyalty | Điểm và hạng | IMPLEMENTED | Catalog và Phase 1 | Phân bổ hạng: UNKNOWN | NEED BASELINE |
| CRM-SEG | Phân khúc | Nhóm khách để chăm sóc | PLANNED | Chưa thấy chương trình phân khúc đã đo | — | Không xây trước khi có câu hỏi nghiệp vụ |
| CRM-CARE | Chăm sóc sau bán | Vòng CS đóng | PARTIAL | Survey và O2O có; CSAT định kỳ chưa | Capability map P2–P3 | — |
| CRM-MED | Nhắc thuốc | Uống đúng và tái mua | IMPLEMENTED | Route `/reminders` | CTR push: UNKNOWN | NEED BASELINE |
| CRM-FAM | Hồ sơ gia đình | Người thân trên app | PARTIAL | Route `/family`; invite nằm ở P15 | Chưa đủ để gọi là hồ sơ sức khỏe gia đình | Gate G2 |
| CRM-CHAT | Chat và đặt hàng | O2O | IMPLEMENTED theo catalog | `/chat`, `/orders`, `/reservations` | Sản lượng O2O: UNKNOWN | — |
| CRM-RETN | Quay lại mua | Giữ khách | PLANNED về chỉ số | Dữ liệu đơn có thể tính được; chưa có baseline | NEED BASELINE | — |
| CRM-CMP | Campaign | Chủ gửi chăm sóc có chủ đích | PLANNED | Roadmap P16 | Không mở campaign khi chưa có đo | — |

### 6.3 Knowledge

| ID | Năng lực | Mục tiêu | Trạng thái | Bằng chứng | Khoảng trống | Bước tiếp | Nghiệm thu |
|---|---|---|---|---|---|---|---|
| KNW-GLB | Global Knowledge | Tri thức nền có version | PARTIAL + TESTED + STAGING VERIFIED | 35 rule seed; 416/417; staging 35 version `IN_REVIEW` | Production chưa có bảng version; 0 PUBLISHED | Không tự publish. Publish nền tảng không phải quyền của chủ nhà thuốc | Chỉ `platform.knowledge.publish`. Quyền này chưa được cấp |
| KNW-PHR | Pharmacy Knowledge | Nhà thuốc pin version đã publish | IMPLEMENTED + TESTED; 0 pin trên staging và trên mô hình production | Bảng adoption; test cô lập tenant | Chưa có nhà thuốc nào pin. Chủ nhà thuốc là chủ thể thẩm quyền chấp nhận áp dụng; chưa gán tài khoản | Sau khi có version PUBLISHED, nhà thuốc pin theo DEC-015. Không đổi RBAC trong tài liệu này | Tenant A không thấy pin của B |
| KNW-POL | Policy vận hành | Thu hẹp rule đã pin | IMPLEMENTED + TESTED | Policy từ chối nới keyword, category, priority | Chưa dùng trên quầy | — | Policy version khác pin bị bỏ qua |
| KNW-LOC | Tri thức địa phương và khách | Ngữ cảnh, không phải khuyến cáo | PLANNED | Chưa có vòng thu thập có duyệt | Doanh số và tần suất không thay xác minh | Phase 4. Thứ tự xem xét theo từng tenant | Một góp ý hoặc một đơn hàng không tự thành rule |
| KNW-SAFE | Sàn an toàn | Không bán OTC khi phải dừng hoặc chuyển | TESTED | `ConsultationSafetyRules`; test cổng; 13 cờ review `PENDING_REVIEW` | 26 cờ catalog sẽ thành `PUBLISHED` trên cột mới khi chạy 416, vì chúng đang `is_active` | Không duyệt 13 cờ review cho đủ test | `pregnant`, `unconscious`, `chest_pain` không trả danh sách thường |
| KNW-LIFE | Vòng đời xuất bản | DRAFT → IN_REVIEW → PUBLISHED → RETIRED | IMPLEMENTED + TESTED + STAGING VERIFIED | 417 và test lifecycle | UI quyền chưa có permission seed | Không cấp quyền trong im lặng | Không có đường DRAFT → PUBLISHED |

### 6.4 Sales Decision Engine

| ID | Năng lực | Mục tiêu | Trạng thái | Bằng chứng | Khoảng trống | Bước tiếp |
|---|---|---|---|---|---|---|
| SDE-NEED | Nhận nhu cầu | Hỏi triệu chứng | PARTIAL | Taxonomy symptom trong 295–298 | Chưa đo quầy có dùng | — |
| SDE-CTX | Ngữ cảnh khách | Tuổi, thai, dị ứng | PARTIAL | Cổng hardcoded và exclude keyword runtime | Chưa gắn hồ sơ khách đầy đủ | Không suy từ một đơn cũ thành chống chỉ định |
| SDE-SAFE | Cổng an toàn | Dừng hoặc chuyển | TESTED | W6 test tên `NovixaOtcSafetyGateIsolatedTests` | Chưa PRODUCTION VERIFIED trên binary W5 | Deploy chỉ sau cửa sổ đêm |
| SDE-MATCH | Khớp sản phẩm | Không gợi ý chỉ vì cùng category | IMPLEMENTED trong source W5; TESTED | Keyword bắt buộc; category không làm điều kiện đủ | Catalog Xuân Hòa làm `ho`, `cum`, `vitamin` khớp rộng **nếu** rule được publish | Không publish `KR_COUGH` khi còn từ `ho` |
| SDE-AVL | Còn hàng | Chỉ gợi ý lô bán được | IMPLEMENTED | SQL tồn, OTC, không `KHANG_SINH`, có đơn vị | Xuân Hòa gần như không gán category nên loại kháng sinh theo category không ăn | Rà loại thuốc bằng dữ liệu thật trước khi bật gợi ý |
| SDE-COM | Trí tuệ thương mại | Bán được và còn hàng | PARTIAL | Thứ tự 90 ngày rồi tồn | Chưa có playbook biên | Không đưa margin lên trước safety |
| SDE-PRICE | Giá và biên | Chọn phương án có giá | PARTIAL | Giá bán có trên POS | Margin không phải bộ lọc gợi ý | Quyết định sau nếu chủ muốn thấy biên |
| SDE-ALT | Hàng thay thế | Hết hàng thì phương án khác trong cùng điều kiện | PLANNED | Chưa có luồng riêng ngoài danh sách gợi ý | — | Sau SDE-MATCH |
| SDE-PLAY | Playbook tại quầy | Kịch bản hỏi và giải thích trong hành trình DEC-017 | PLANNED | `sales-playbook-v1.md` là playbook **bán Novixa**, không phải kịch bản tư vấn thuốc | Không trộn hai tài liệu. Chưa chốt một chuỗi màn cố định | Viết playbook quầy khi có rule đã duyệt |
| SDE-COP | Copilot quầy | API gợi ý, không phải Guided Selling | PARTIAL | `POST` suggest; Gemini chỉ extract | Production đang binary cũ, không phải W5 | Không bật gợi ý khi 0 pin hoặc rule chưa publish |
| SDE-GUIDE | Guided selling | Hành trình 10 bước ở mục 7.5 | PLANNED | Chưa có luồng UX khép kín. API suggest không tính là xong | Chưa chọn POS, màn riêng, hay kết hợp | Thiết kế và thử theo DEC-017. Giữ bước an toàn | Nhân viên hoàn thành một ca mẫu mà không bỏ cổng an toàn |
| SDE-OUT | Ghi nhận kết quả tư vấn | Biết tư vấn có thành đơn không | PLANNED | Chưa có chỉ số bỏ lỡ nhu cầu | Phase 3–6 | NEED BASELINE |

### 6.5 Staff, chủ, nền tảng

| ID | Năng lực | Trạng thái | Bằng chứng | Khoảng trống |
|---|---|---|---|---|
| STF-EMP | Hồ sơ nhân viên | PARTIAL | `employees`, user, RBAC | Không phải HR |
| STF-ONB | Onboarding người mới | PARTIAL | Playbook CS là onboarding **nhà thuốc khách**, tuần W0–W4 | Chưa có đường cho nhân viên quầy mới |
| STF-LRN | Học | PARTIAL | Pack learning, migration nội dung | Mức dùng: UNKNOWN |
| STF-TASK | Việc trong ca | IMPLEMENTED; chưa production | Migration 413; test không mở lại việc `done` | Bảng chưa có trên production |
| STF-CHK | Checklist theo người | PARTIAL | Checklist ca có; chưa khóa theo nhân sự và ca trên production | 412 |
| STF-PERF | Đánh giá | PLANNED | Doanh số theo nhân viên có trong báo cáo | Không đủ để gọi là đánh giá năng lực |
| STF-COP | Trợ lý nhân viên | PLANNED | Trùng đích với SDE-COP khi P3 xong | Chưa tách UX học việc |
| OWN-DASH | Dashboard chủ | PARTIAL | Cockpit route; diagnostic nói lab và chưa đo tần suất mở | NEED BASELINE |
| OWN-ALT | Cảnh báo vận hành | PARTIAL | Tồn thấp, cận date, loss prevention EP03 | Mức dùng Xuân Hòa UNKNOWN |
| OWN-REC | Khuyến nghị kinh doanh | PLANNED | Chưa có vòng đề xuất có chủ đóng | Không bịa insight |
| OWN-RUL | Luật của chủ | PLANNED | Policy W5 chỉ thu hẹp rule đã pin | Chưa có luật vận hành ngoài tri thức |
| PLT-IAM | Quyền | PRODUCTION VERIFIED cho RBAC bán hàng | STAFF không xem doanh thu | `pharmacy.knowledge.approve` và `platform.knowledge.publish` **chưa có** trong bảng permission production |
| PLT-AUD | Audit | IMPLEMENTED | Audit log trong kiến trúc | Độ phủ từng hành động tri thức: có sự kiện append-only khi W5 chạy |
| PLT-OBS | Quan sát | PARTIAL | Health endpoint | Chưa có SLO copilot |
| FUT-CLN | Clinic | PLANNED / tách pack | Pack clinic tồn tại | Không kéo vào backlog nhà thuốc |
| FUT-TEL | Telemedicine | PLANNED | Không thấy sản phẩm khám từ xa đã nghiệm thu | Ngoài phạm vi trước mắt |
| FUT-RX | Đơn người kê đơn | PARTIAL | Module e-Rx đánh dấu pilot trong catalog | Không gọi là mạng bác sĩ đã xong |

---

## 7. W1–W6, Customer App, các mảng đã từng được báo cáo xong

### 7.1 W1–W6

Repo **không** có định nghĩa chuẩn các sóng sản phẩm W1, W2, W3, W4.

Chữ W1–W4 trong [onboarding-playbook-v1.md](../07-customer/onboarding-playbook-v1.md) là **tuần triển khai một nhà thuốc** (master data, tồn đầu, training, go-live). Không dùng các chữ đó làm tên phase sản phẩm.

| Mã trong code | Nghĩa đã thấy | Trạng thái |
|---|---|---|
| W5 | Publication và pin tri thức. Comment migration 416, 417 | STAGING VERIFIED; BLOCKED trên production đến cửa sổ đêm và ca đóng |
| W6 | Cổng OTC trên đường suggest. Comment test `NovixaOtcSafetyGateIsolatedTests` | TESTED trên staging; không phải module menu |

Đặt tên W1–W4 cho các lát vận hành là **OPEN-001**. Chưa gán.

### 7.2 Mười hai bề mặt Customer App

DEC-018: Customer App là năng lực chiến lược, chưa sẵn sàng đại trà. Nguồn danh mục: `module-catalog-v1.md`. Nguồn mức trưởng thành: `customer-app-roadmap-p12-p18.md` (2026-07-09) nói L0, khoảng 45–50% PRD. Hai nguồn không cùng mức hoàn thành. Trạng thái dưới đây giữ mức thận trọng đã có.

| # | Bề mặt | Catalog viết | Trạng thái Master |
|---|---|---|---|
| 1 | Auth OTP | ✅ | IMPLEMENTED; usage UNKNOWN |
| 2 | Home | ✅ | IMPLEMENTED; usage UNKNOWN |
| 3 | Loyalty | ✅ | IMPLEMENTED; usage UNKNOWN |
| 4 | Reminders | ✅ | IMPLEMENTED; usage UNKNOWN |
| 5 | Orders O2O | ✅ | IMPLEMENTED; sản lượng UNKNOWN |
| 6 | Chat | ✅ | IMPLEMENTED; usage UNKNOWN |
| 7 | Reservations | ✅ | IMPLEMENTED; usage UNKNOWN |
| 8 | Receivables | ✅ | IMPLEMENTED; usage UNKNOWN |
| 9 | Health wallet | ✅ | PARTIAL so với PRD sức khỏe |
| 10 | Family | ✅ trong catalog; P15 mới là mở rộng | PARTIAL |
| 11 | AI health | 🧪 | PARTIAL, rule-based |
| 12 | Profile, consent, push | ✅ | IMPLEMENTED; CTR UNKNOWN |

`app.novixa.vn` trả HTTP 200 ngày 2026-10-31. Đó là site sống, không phải 12 mục đã nghiệm thu.

### 7.3 Những báo cáo “đã xong” cần đọc lại

| Nguồn | Cách Master đọc |
|---|---|
| Diagnostic 2026-10-08, ERP ~85%, app ~70%, AI ~25% | Ước lượng lúc đó. Không cập nhật bằng W5. Không dùng làm % hiện tại |
| EP01 Cockpit, EP02 Checklist “lab ✅” | Code và route có. Tần suất chủ nhà thuốc dùng: UNKNOWN |
| EP03 Loss “prod mig 133” | Schema có thể đã ở production. Hiệu quả phát hiện lệch: UNKNOWN |
| 35 rule seed | Có trên production từ 2026-08-25. Chưa đủ điều kiện publish. 17 candidate, 17 cần duyệt lâm sàng, `KR_OTHER` không đủ điều kiện, cả 35 thiếu nguồn |
| “W5 sẵn sàng phát hành có kiểm soát” | Sẵn sàng **thao tác đêm**, chưa phải đã phát hành, chưa phải tri thức đã duyệt |

### 7.4 Nhánh ổn định Customer App

Không phải phase cấp cao mới. Không chờ W5. Không thêm tính năng cho có vẻ đầy đủ. Thiếu gì khiến pilot không chạy thì ghi vào capability tương ứng, ở trạng thái đề xuất cho đến khi Product Owner nhận vào scope.

| Nhóm | Việc | Trạng thái |
|---|---|---|
| A. Ổn định | Đăng nhập, hồ sơ, lịch sử mua, loyalty, nhắc thuốc, chat, đặt hàng, reservation nếu còn trong phạm vi, push, đồng bộ với nhà thuốc, lỗi khi API không sẵn sàng | Làm trên chức năng đã có. Chưa có bằng chứng pilot |
| B. Dữ liệu và bảo mật | Đúng khách, đúng tenant, đúng quyền, dữ liệu cá nhân, đồng ý và mục đích xử lý | Nguyên tắc đã chốt. Ngưỡng đo: OPEN-006 |
| C. Kiểm thử cả hành trình | Đăng nhập, xem hồ sơ, lịch sử, loyalty, tương tác, đặt hàng, nhận thông báo, nhà thuốc xử lý phía quầy | Chỉ luồng thực sự có. Không giả định đủ 12 mục |
| D. Pilot có kiểm soát | Một nhà thuốc, một nhóm khách, hỗ trợ lỗi, hướng dẫn cài, theo dõi dùng thật | Chưa mở. Phụ thuộc tiêu chí OPEN-006 |
| E. Đại trà | Sau khi luồng chính ổn, quyền đáng tin, nhà thuốc hỗ trợ được, có bằng chứng pilot, có cách chịu tải | Chưa đủ điều kiện |

### 7.5 Hành trình Guided Selling

Mục tiêu Phase 3 là nhân viên đi hết hành trình, không phải gọi xong API gợi ý.

1. Tiếp nhận khách.
2. Nhận diện nhu cầu.
3. Hỏi phần còn thiếu.
4. Xác định bối cảnh và an toàn.
5. Chuyển dược sĩ hoặc bác sĩ khi cần.
6. Tìm sản phẩm đủ điều kiện.
7. Kiểm tra tồn và giá.
8. Xem lựa chọn thay thế còn phù hợp.
9. Hướng dẫn giải thích.
10. Ghi kết quả tư vấn.

Không bắt buộc một chuỗi màn hình cố định. Vị trí UI là backlog thiết kế, không còn là câu hỏi mở về nguyên tắc. `SDE-COP` là lát gợi ý. `SDE-GUIDE` vẫn PLANNED. Không publish rule, không cấp quyền duyệt, không bật gợi ý khi W5 chưa đủ điều kiện.

---

## 8. Decision Register

### 8.1 Đã thống nhất

| ID | Quyết định | Nguồn | Tác động |
|---|---|---|---|
| DEC-001 | Định vị là nền tảng vận hành và trí tuệ kinh doanh nhà thuốc; khác biệt là biến kinh nghiệm thành năng lực cho người mới | Nhiệm vụ Product Master 2026-10-31 | GTM cũ vẫn nói FEFO/O2O; không xóa, nhưng không dùng làm câu định vị chính |
| DEC-002 | Phục vụ nhà thuốc nhỏ và chuỗi nhỏ | ICP và nhiệm vụ này | Không ưu tiên tập đoàn đa công ty |
| DEC-003 | Ba nhánh tri thức cùng nuôi Sales Decision | Nhiệm vụ này | Local knowledge không được publish thành chuyên môn |
| DEC-004 | Safety floor đứng trước doanh thu | W5 và cổng OTC | Không tối ưu margin bằng cách nới cổng |
| DEC-005 | Bán hàng không phải bằng chứng chuyên môn | Qualification V1 | Không học rule từ doanh số |
| DEC-006 | Không tự publish tri thức lâm sàng | W5, qualification | 0 rule PUBLISHED là đúng, không phải thiếu sót cần vá bằng script |
| DEC-007 | Không cấp `pharmacy.knowledge.approve` hay `platform.knowledge.publish` khi chưa có quyết định riêng | Production readiness | UI duyệt không mở bằng cách gắn ADMIN |
| DEC-008 | Tenant isolation; không global fallback | Test W5 | Copilot thiếu pin thì không gợi ý sản phẩm |
| DEC-009 | `KR_OTHER` không vào đường gợi ý | Code và test | Kể cả khi có người pin nhầm |
| DEC-010 | Category không làm sản phẩm đủ điều kiện gợi ý | Sửa matching W5 | Catalog Xuân Hòa không có HO_HAP, GIAM_DAU, DA_DAY, VITAMIN, NGAO_DUOC |
| DEC-011 | Production chỉ ban đêm khi nhà thuốc đóng cửa, sau backup và restore thử | Readiness V1 | 2026-10-31 giữa ngày: chưa cắt |
| DEC-012 | Không đụng dữ liệu Xuân Hòa để demo; không dùng DB production làm máy thử | Luật tenant | Staging là database riêng |
| DEC-013 | Một repository `E:\KitPlatform` | Luật làm việc | Không worktree, không repo thứ hai |
| DEC-014 | Giảm audit lẻ; Product Status còn hiệu lực thì không rà cả repo | Mục 10 | Task mới đọc Master trước |
| DEC-015 | Tại Xuân Hòa, chủ nhà thuốc là chủ thể thẩm quyền chung để nhà thuốc chấp nhận áp dụng tri thức. Không gán một nhân viên, một cá nhân, hay một tài khoản. Không đổi RBAC | Alignment 1.1, Product Owner | `pharmacy.knowledge.approve` khác `platform.knowledge.publish`. Publish Global Knowledge không suy ra từ quyết định này |
| DEC-016 | Thứ tự xem xét rule theo nhu cầu từng nhà thuốc. Không có danh sách ưu tiên cứng cho mọi tenant | Alignment 1.1 | Có thể gợi ý nhóm và thứ tự theo bối cảnh. Doanh số không thay xác minh. Không tự publish, không tự pin |
| DEC-017 | Guided Selling cân bằng tiện việc, đúng ngữ cảnh, ít thao tác, và kết quả chính xác. Chưa chốt POS hay màn riêng | Alignment 1.1 | Bớt thao tác không được bỏ an toàn. Chọn UI để backlog thiết kế |
| DEC-018 | Customer App là năng lực chiến lược. Chưa đại trà vì chưa đủ ổn định | Alignment 1.1 | Không diễn giải thành khách không cần hoặc app không có giá trị |

### 8.2 Đề xuất chưa được phê duyệt

| ID | Đề xuất | Vì sao chưa đưa vào scope |
|---|---|---|
| PROP-001 | Gán W1–W4 cho các lát POS, kho, CRM, nhân sự | Chưa có tên trong repo |
| PROP-002 | Sửa từ khóa `ho`, `cum`, `vitamin` trước khi cho duyệt | Sửa nội dung chuyên môn cần dược sĩ, không phải việc kỹ thuật |
| PROP-003 | Gán category thật của Xuân Hòa vào rule | Category cửa hàng gần như trống; sửa dữ liệu hàng là quyết định vận hành |
| PROP-004 | Bật copilot sản phẩm ngay sau khi cài W5 | 0 pin là trạng thái an toàn |
| PROP-005 | Đo baseline onboarding và tỷ lệ chuyển đổi trong 30 ngày sau go-live W5 | Chưa có sự kiện để đo |
| PROP-006 | Đưa Clinic hoặc telemedicine vào phase gần | Nhiệm vụ này xếp vào future |

### 8.3 Còn mở

| ID | Câu hỏi | Ai quyết |
|---|---|---|
| OPEN-001 | Tên chính thức của các hạng mục trước W5 | Product |
| OPEN-005 | Có hiện biên lợi nhuận cho chủ trong gợi ý hay không | Chủ nhà thuốc |
| OPEN-006 | Bộ tiêu chí nghiệm thu và điều kiện triển khai Customer App: luồng nào phải ổn trước pilot, kiểm thử chức năng và dữ liệu, bảo mật và quyền, đồng bộ với nhà thuốc, tiêu chí pilot, điều kiện đại trà. Không đặt ngưỡng phần trăm khi chưa có baseline | Product Owner |

OPEN-002 đóng bằng DEC-015. Thẩm quyền chấp nhận áp dụng tại nhà thuốc là chủ nhà thuốc. Publish nền tảng vẫn là quyền khác và chưa được cấp.

OPEN-003 đóng bằng DEC-016. Không có rule ưu tiên cố định cho mọi tenant.

OPEN-004 đóng ở nguyên tắc bằng DEC-017. Chọn POS, màn riêng, hay kết hợp nằm ở backlog thiết kế, chưa phải quyết định sản phẩm đã chốt.

---

## 9. Chỉ số và nghiệm thu

Không có baseline trong repo cho các số dưới đây. Không đặt mục tiêu phần trăm.

| Nhóm | Chỉ số | Trạng thái |
|---|---|---|
| Người mới | Thời gian đến ca đầu tự xử lý tình huống phù hợp | NEED BASELINE |
| Người mới | Tỷ lệ tình huống phù hợp tự xử lý được | NEED BASELINE |
| Bán hàng | Tư vấn thành đơn | NEED BASELINE |
| Bán hàng | Bỏ lỡ nhu cầu | NEED BASELINE |
| Gợi ý | Đúng hàng, đúng tồn, không chỉ đúng category | Có test; chưa có mẫu quầy |
| An toàn | Escalation đúng | Có test cổng; chưa có mẫu quầy |
| Khách | Quay lại | NEED BASELINE |
| Chăm sóc | Hiệu quả nhắc và CS | NEED BASELINE |
| Ca | Checklist và việc hoàn thành | NEED BASELINE |
| Chủ | Thời gian phải đứng cạnh quầy | NEED BASELINE |
| Lỗi | Tư vấn sai và lệch vận hành | NEED BASELINE |
| App pilot | Tỷ lệ cài trên nhóm khách được mời | NEED PILOT DATA |
| App pilot | Tỷ lệ kích hoạt tài khoản | NEED PILOT DATA |
| App pilot | Tỷ lệ khách có hoạt động thực tế | NEED PILOT DATA |
| App pilot | Tần suất dùng chức năng chính | NEED PILOT DATA |
| App pilot | Tỷ lệ đặt hàng hoặc tương tác qua app | NEED PILOT DATA |
| App pilot | Tỷ lệ quay lại mua của nhóm pilot | NEED PILOT DATA |
| App pilot | Tỷ lệ lỗi theo luồng | NEED PILOT DATA |
| App pilot | Tỷ lệ thông báo gửi và nhận thành công | NEED PILOT DATA |
| App pilot | Khối lượng hỗ trợ nhà thuốc phải bỏ ra | NEED PILOT DATA |

Nghiệm thu một capability chỉ khi có bằng chứng đúng trạng thái đã ghi. Thiếu bằng chứng thì giữ UNKNOWN hoặc PARTIAL.

---

## 10. Cách duy trì

1. Task lớn ghi phase và capability ID trong phần mở đầu.
2. Trước khi đề xuất xây mới, tìm ID trong mục 6. Nếu đã có, mở rộng ID đó.
3. Không rà lại toàn bộ repo khi dòng status còn dẫn đúng file bằng chứng và chưa có thay đổi đụng capability đó.
4. Bằng chứng lỗi thời khi: production đã cắt, schema đổi, hoặc báo cáo được thay bởi báo cáo mới hơn.
5. Cuối nhiệm vụ lớn, sửa dòng status và thêm đường dẫn bằng chứng. Không tạo master thứ hai.
6. Đổi hướng thì thêm dòng Decision Register. Không sửa thầm mục 2.
7. Ý mới ngoài roadmap ghi PROP, không tự thành việc phải làm.
8. Không đánh dấu IMPLEMENTED chỉ vì có thư mục, route, bảng, hoặc migration.
9. Không đánh dấu PRODUCTION VERIFIED chỉ vì health 200.
10. Hội thoại và prompt không được mâu thuẫn với Master. Nếu mâu thuẫn, cập nhật Register trước khi làm.

Hết hiệu lực một dòng khi điều kiện ở cột bằng chứng không còn đúng. Khi đó hạ về UNKNOWN cho đến khi có bằng chứng mới, thay vì giữ trạng thái cũ.

---

## 11. Mâu thuẫn và việc còn mở

| Mâu thuẫn | Cách đọc |
|---|---|
| Vision 1.0 nói khác biệt là FEFO và O2O | Đúng với nền. Định vị hiện tại là DEC-001 |
| Catalog Customer App gần như ✅, roadmap nói L0 | Tin roadmap cho mức PRD; tin catalog cho việc màn hình tồn tại |
| Diagnostic ghi AI copilot ~25% | Trước W5. Source hiện tại có cổng và pin; production chưa chạy binary đó |
| Success map ưu tiên Clinic song song P2 | Master này không kéo Clinic vào backlog nhà thuốc |
| Sẵn sàng phát hành W5 | Thao tác đã diễn tập. Tri thức chưa được duyệt. Hai việc khác nhau |
| Category rule và category Xuân Hòa | Năm mã rule không tồn tại trên tenant. Khớp từ khóa là đường thật, và một số từ khóa đang quá rộng |

Việc còn mở: OPEN-001, OPEN-005, OPEN-006. OPEN-002, OPEN-003 và nguyên tắc OPEN-004 đã vào DEC-015, DEC-016, DEC-017. Cửa sổ đêm cho bản W5 trên staging vẫn là việc vận hành, chưa phải quyết định sản phẩm mới.

Tài liệu kỹ thuật không bị thay bởi file này: blueprint as-built, module catalog, PHASE_SCOPE, qualification 35 rule, readiness production W5.
