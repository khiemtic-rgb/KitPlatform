# NOVIXA — Knowledge K1: governance và kiểm chứng nguồn

Ngày: 2026-11-01. Loại: đặc tả triển khai. Không phải migration, không xuất bản, không cấp quyền.

Nguồn: [NOVIXA_KNOWLEDGE_STAFF_COMPLETION_PLAN.md](./NOVIXA_KNOWLEDGE_STAFF_COMPLETION_PLAN.md) mục K1, [NOVIXA_W5_INITIAL_KNOWLEDGE_CATALOG_QUALIFICATION_V1.md](./NOVIXA_W5_INITIAL_KNOWLEDGE_CATALOG_QUALIFICATION_V1.md), schema `414`–`417` và API publication/adoption trên working tree. Không đọc production trong lượt này.

Trạng thái: `READY_FOR_K1_IMPLEMENTATION`. Cổng nguồn là việc code được phép làm. Việc gắn nguồn, chấp nhận chuyên môn, và cấp quyền vẫn chưa được làm.

---

## 1. Kiến trúc hiện tại và bản đồ file

Ba việc không được gộp:

| Việc | Bảng / API | Ai được gọi | Không làm |
|---|---|---|---|
| Xuất bản Global Knowledge | `pharmacy_knowledge_rule_version.lifecycle_status` | `POST /api/platform/pharmacy-knowledge/versions/{ruleCode}/{version}/publish` | Không bật Copilot |
| Nhà thuốc ghim một version | `pharmacy_knowledge_adoption` | `POST /api/pharmacy/knowledge-rules/{ruleCode}/approval` | Không đổi lifecycle |
| Copilot đọc | join pin `APPROVED` với đúng `rule_version` còn `PUBLISHED` | `SymptomTaxonomyRepository.GetKnowledgeRulesAsync` | Không fallback sang catalog chung |

### Vòng đời version

`PharmacyKnowledgeApproval.Transition` chỉ cho:

`DRAFT → IN_REVIEW`, `IN_REVIEW → PUBLISHED`, `IN_REVIEW → DRAFT`, `PUBLISHED → RETIRED`, `DRAFT → RETIRED`, `IN_REVIEW → RETIRED`.

`DRAFT → PUBLISHED` ném lỗi. `RETIRED` không đổi trạng thái. `PUBLISHED` không quay lại review.

API hiện chỉ gọi hai đích: publish (`PUBLISHED`) và retire (`RETIRED`). Không có endpoint đưa `DRAFT` sang `IN_REVIEW`. Migration `416` copy rule seed thẳng vào version với `lifecycle_status = 'IN_REVIEW'`.

### File

| Vai trò | File |
|---|---|
| Bảng rule gốc | `migrations/295_pharmacy_symptom_taxonomy_schema.sql` — `source VARCHAR(40) DEFAULT 'novixa'` |
| Seed 32 rule | `migrations/296_pharmacy_symptom_taxonomy_seed.sql` |
| `KR_DEHYDRATION` | `migrations/297_pharmacy_consultation_dehydration_tune.sql` |
| `KR_HOARSENESS`, `KR_THROAT_CLEARING` | `migrations/298_pharmacy_consultation_ent_otc.sql` |
| Cột `reference_source` trên rule gốc | `migrations/414_pharmacy_knowledge_approval.sql` |
| Sự kiện duyệt append-only; hàng chờ cờ an toàn | `migrations/415_pharmacy_knowledge_approval_history.sql` |
| Version, adoption, policy | `migrations/416_pharmacy_knowledge_tenant_governance.sql` |
| Sự kiện publication; đóng băng nội dung đã publish | `migrations/417_pharmacy_knowledge_publication_event.sql` |
| Máy trạng thái và quyền | `src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Knowledge/PharmacyKnowledgeApproval.cs` |
| Publish | `PharmacyKnowledgePublicationService.cs`, `PharmacyKnowledgePublicationController.cs` |
| Pin | `PharmacyKnowledgeApprovalService.cs`, `PharmacyKnowledgeApprovalController.cs` |
| Policy HTTP | `src/KitPlatform.Api/Authorization/PharmacyKnowledgeAuthorizationExtensions.cs` |
| Copilot | `SymptomTaxonomyRepository.GetKnowledgeRulesAsync` |
| Màn | `client/admin/src/modules/pharmacy/KnowledgePublishPage.tsx`, `KnowledgeApprovalPage.tsx` |
| Bài học | `pack_learning.module` trong `migrations/155_learning_os_schema.sql` — không có `rule_code` |

`414`–`417` không nằm trong `deploy/ubuntu/migration-files.prod.txt`. Production Staff V1 không được sửa trong K1.

### Cột nguồn đang có

| Chỗ | Ý nghĩa thực | Dùng lại cho K1 |
|---|---|---|
| `pharmacy_knowledge_rule.source` | Nhãn hệ thống, mặc định `novixa`. Seed không ghi đè | Giữ làm xuất xứ catalog. Không phải căn cứ chuyên môn |
| `pharmacy_knowledge_rule.reference_source` | Một chuỗi TEXT trên bản gốc. Seed không gán. Bản gốc không bị trigger 417 đóng băng | Bản nháp làm việc. Không phải bằng chứng của version đã publish |
| `pharmacy_knowledge_rule_version.source` | Copy của nhãn `novixa` lúc `416` chạy. Không có cột `reference_source` | Không đổi nghĩa cột này |
| `pharmacy_knowledge_adoption.reference_source` | Ghi chú lúc pin. Pin `APPROVED` bắt buộc chuỗi khác rỗng, tối đa 500 ký tự. `PreserveReference` giữ giá trị cũ, không ghi đè | Giữ cho nhà thuốc. Không thay nguồn của version |
| `pharmacy_knowledge_approval_event.reference_source` | Bản sao append-only của chuỗi pin | Lịch sử pin |
| `pharmacy_safety_flag_review.source_ref` | Trỏ `ConsultationSafetyRules.cs`. 13 cờ đều `PENDING_REVIEW` | Không dùng làm nguồn của 35 rule. Copilot không đọc bảng này để lấy danh sách OTC |

`PublishAsync` chỉ đổi `lifecycle_status` và `updated_at`, rồi ghi `pharmacy_knowledge_publication_event`. Không đọc `reference_source`.

Pin không tự chạy khi publish. Copilot join `v.version = a.rule_version`. Version mới không thay pin đang trỏ version cũ.

Trigger `417` từ chối sửa mọi cột của version `PUBLISHED` hoặc `RETIRED`, trừ `lifecycle_status` và `updated_at`. Xóa hai trạng thái đó cũng bị từ chối.

---

## 2. Vòng đời K1

Giữ bốn trạng thái. Không thêm trạng thái. Bốn việc khác nhau bằng quyền và sự kiện, không bằng một bước gộp.

```
DRAFT --content review--> IN_REVIEW --platform publish--> PUBLISHED --platform retire--> RETIRED
         ^                      |
         +---- trả về ----------+
```

Chấp nhận nguồn là một bản ghi trên version khi version còn `DRAFT` hoặc `IN_REVIEW`. Nó không đổi `lifecycle_status`.

| Bước | Việc | Kết quả | Không được |
|---|---|---|---|
| Soạn | Tạo version mới ở `DRAFT` | Nội dung và nguồn ở trạng thái `UNVERIFIED` | Sửa version đã `PUBLISHED` |
| Content review | `DRAFT → IN_REVIEW` hoặc trả `IN_REVIEW → DRAFT` | Version đủ trường để chuyên môn đọc | Đặt `ACCEPTED`. Không publish |
| Professional approval | Ghi nguồn và `source_verification_status` | `ACCEPTED` hoặc `REJECTED` | Đổi lifecycle. `ACCEPTED` chưa phải `PUBLISHED` |
| Platform publication | `IN_REVIEW → PUBLISHED` | Đóng băng nội dung và nguồn. Ghi publication event | `DRAFT → PUBLISHED`. Publish khi nguồn chưa `ACCEPTED` |
| Retire | `PUBLISHED → RETIRED` | Version còn đọc được trong lịch sử | Xóa. Mở lại. Sửa nội dung |
| Tenant adoption | Pin `APPROVED` đúng một `(rule_code, version)` đã `PUBLISHED` và còn hiệu lực | Copilot của đúng tenant đọc pin đó | Pin version `IN_REVIEW`. Pin hộ tenant khác. Tự đổi pin khi có version mới |

`KR_OTHER` không đi qua cổng publish, kể cả khi có nguồn `ACCEPTED`. Copilot đã loại mã này bằng `PharmacyKnowledgeCopilotQuery.OperationallyExcludedRuleCode`. K1 thêm chặn ở publish để nguồn không biến mã này thành version xuất bản.

Sửa keyword, category, `reason_vi`, symptom, hoặc exclude trên version chưa publish thì xóa `ACCEPTED`: trạng thái nguồn về `UNVERIFIED`, `verified_by` và `verified_at` về trống. Hôm nay chưa có API sửa nội dung version. Quy tắc này đi cùng API sửa đầu tiên, không phải một đường tắt sau này.

---

## 3. Nguồn bắt buộc trước khi publish

Nguồn nằm trên **version**, cùng hàng `pharmacy_knowledge_rule_version`, để trigger `417` đóng băng nó lúc publish. Không để nguồn đã chấp nhận chỉ trên bảng rule gốc.

| Trường | Bắt buộc khi `ACCEPTED` | Ghi chú |
|---|---|---|
| `reference_source` | Có | Câu trích dẫn người đọc được. Cùng nghĩa với cột TEXT của migration `414`, nhưng trên version |
| `source_identity` | Có | Số văn bản, mã monograph, hoặc định danh ổn định khác. Không điền `novixa` |
| `source_url` hoặc `document_ref` | Một trong hai | URL hoặc mã hồ sơ nội bộ. Không bịa URL |
| `issuing_organization` | Có | Cơ quan hoặc tài liệu ban hành |
| `source_published_on` | Không | Chỉ ghi khi tài liệu có ngày. Không suy ra từ `effective_from` của version |
| `evidence_note` | Có | Mục hoặc vị trí trong nguồn ủng hộ rule. Không chép nguyên hướng dẫn |
| `source_verification_status` | `ACCEPTED` | `UNVERIFIED` (mặc định), `ACCEPTED`, `REJECTED` |
| `verified_by`, `verified_at` | Có | Người và thời điểm chấp nhận. Không lấy từ user publish nếu đó là một hành động khác |

`source = 'novixa'` không đủ. Chuỗi pin của nhà thuốc không đủ. `reason_vi` không đủ.

`REJECTED` chặn publish. Version có thể bị trả về `DRAFT`. Chấp nhận lại là một lần ghi mới sau khi nội dung hoặc nguồn đổi.

---

## 4. Ma trận quyền

Hai quyền hiện có. Cả hai đều đòi token ERP **và** claim permission. Vai trò ADMIN một mình không đủ. Không có grant trong migration.

| Việc | Quyền hiện có | API |
|---|---|---|
| Xem và publish / retire catalog chung | `platform.knowledge.publish` | `/api/platform/pharmacy-knowledge/...` |
| Pin, thu hồi pin, policy thu hẹp, xem catalog đã publish của đúng tenant | `pharmacy.knowledge.approve` | `/api/pharmacy/knowledge-rules/...` |

`platform.knowledge.publish` không nằm trong picker nhà thuốc. `pharmacy.knowledge.approve` có trong picker module `pharmacy` và chưa được cấp.

K1 thêm hai policy, cùng kiểu ERP cộng claim, không seed, không gắn ADMIN, không đưa vào picker cho đến khi Product Owner bảo:

| Việc | Mã đề xuất | Được | Không được |
|---|---|---|---|
| Content review | `pharmacy.knowledge.review` | `DRAFT → IN_REVIEW`, `IN_REVIEW → DRAFT` | Publish, pin, đặt `ACCEPTED` |
| Professional approval | `pharmacy.knowledge.verify-source` | Ghi nguồn, `ACCEPTED` hoặc `REJECTED` | Publish, pin, sửa version đã publish |

Một người có thể giữ hai quyền sau này. Hai việc vẫn là hai lần gọi và hai dấu vết. Publisher không tự biến một version chưa `ACCEPTED` thành đã xuất bản bằng cách vừa giữ quyền publish.

Không cấp quyền nào trong implementation K1.

---

## 5. Tiêu chí publish

`POST .../publish` trả 400 khi một điều sau đúng:

- Version không tồn tại.
- Trạng thái hiện tại không phải `IN_REVIEW`.
- `rule_code` là `KR_OTHER`.
- `source_verification_status` khác `ACCEPTED`.
- Thiếu `reference_source`, `source_identity`, `issuing_organization`, `evidence_note`, `verified_by`, hoặc `verified_at`.
- Cả `source_url` và `document_ref` đều trống.
- `source_identity` hoặc `reference_source` chỉ là `novixa`.
- Người gọi không có `platform.knowledge.publish` trên token ERP.

Khi đủ điều kiện: đổi lifecycle, ghi một dòng `pharmacy_knowledge_publication_event`, commit. Trigger giữ nội dung và các cột nguồn. Pin của mọi tenant không đổi.

Retire vẫn chỉ từ `PUBLISHED`, cùng quyền publish, không cần xét lại nguồn. Version retired còn trong bảng và trong lịch sử.

Pin vẫn chỉ nhận version `PUBLISHED` còn trong khoảng `effective_from` / `effective_to`, và vẫn bắt buộc chuỗi `reference_source` của chính pin. Policy vẫn không được thêm keyword, category, hoặc priority cao hơn bản đã pin.

Cổng an toàn `ConsultationSafetyRules` và `PharmacyOtcSuggestionGate` không đổi. 13 cờ `pharmacy_safety_flag_review` không được chuyển khỏi `PENDING_REVIEW`.

---

## 6. 35 rule seed

Đếm từ seed: 32 trong `296`, `KR_DEHYDRATION` trong `297`, `KR_HOARSENESS` và `KR_THROAT_CLEARING` trong `298`. Insert không có cột nguồn. `source` nhận mặc định `novixa`. Không có URL, cơ quan ban hành, ngày văn bản, người kiểm chứng, hay `reference_source`.

Không rule nào được điền nguồn trong tài liệu này. Không rule nào được coi là đã duyệt.

| Lớp | Số | Mã |
|---|---:|---|
| Source available | 0 | — |
| Source missing | 35 | Cả catalog |
| Source insufficient | 35 | Chỉ có nhãn `novixa`. Nhãn này không đưa rule nào sang lớp available |
| Requires professional review | 34 | Mọi mã trừ `KR_OTHER` |
| Not eligible for publication now | 35 | Thiếu nguồn đã `ACCEPTED` |

Trong 34 mã cần chuyên môn đọc, nhãn nội dung giữ từ hồ sơ qualification. Nhãn đó không phải duyệt:

**Đủ cấu trúc để đọc, vẫn cần nguồn và người chuyên môn (17):** `KR_COUGH_DRY`, `KR_COUGH_PHLEGM`, `KR_COUGH`, `KR_RUNNY_NOSE`, `KR_NASAL_CONG`, `KR_SORE_THROAT`, `KR_SNEEZING`, `KR_COMMON_COLD`, `KR_FLU`, `KR_FEVER`, `KR_BODY_ACHE`, `KR_CHILLS`, `KR_DIARRHEA`, `KR_CONSTIPATION`, `KR_BACK_PAIN`, `KR_HOARSENESS`, `KR_THROAT_CLEARING`.

**Cần đọc lâm sàng riêng trước khi xét nguồn (17):** `KR_HEADACHE`, `KR_NAUSEA`, `KR_VOMITING`, `KR_HEARTBURN`, `KR_REFLUX`, `KR_BLOATING`, `KR_ABDOMINAL`, `KR_ALLERGY`, `KR_ITCHY_SKIN`, `KR_RASH`, `KR_HIVES`, `KR_JOINT_PAIN`, `KR_TOOTHACHE`, `KR_FATIGUE`, `KR_DIZZINESS`, `KR_INSOMNIA`, `KR_DEHYDRATION`.

**Không đủ điều kiện với nội dung seed hiện tại:** `KR_OTHER` (từ khóa rỗng, ba nhóm hàng). Nguồn sau này không tự làm mã này được publish.

---

## 7. Knowledge và Staff

Bài `pack_learning.module` chỉ có `body_markdown`. Không có `KR_*` trong migration learning.

Hợp đồng cho giai đoạn sau, không nằm trong lát code K1 đầu:

- Một bài đã được người có quyền học duyệt có thể trỏ `rule_code` và `version`.
- Lúc gắn, version đó phải đang `PUBLISHED` và tenant đó phải đang pin đúng version.
- Một bài một trỏ chính. Không copy `reason_vi` vào markdown rồi sửa lệch.
- Khi pin đổi version hoặc version retired, trỏ cũ giữ nguyên và API đọc đánh dấu không còn là bản đang dạy. Không tự trỏ sang pin mới.
- Gắn trỏ không tạo enrollment, không tạo operational task, không tick checklist.

S3 trong kế hoạch hoàn thiện mới xây liên kết này, sau khi có version đã publish và đã pin.

---

## 8. Lát triển khai

Chỉ trên working tree `E:\KitPlatform`. Không áp migration. Không thêm file vào `migration-files.prod.txt` hay manifest Staff V1. Số migration chọn lúc code, sau `421`, không đụng `411`–`413`.

1. Cột nguồn trên `pharmacy_knowledge_rule_version`, mặc định `UNVERIFIED`, không backfill 35 rule.
2. `PublishAsync` thực hiện mục 5. Giữ `Transition` không có `DRAFT → PUBLISHED`.
3. Endpoint submit và trả review, gọi `Transition` với `IN_REVIEW` hoặc `DRAFT`.
4. Endpoint ghi nguồn, chỉ khi lifecycle là `DRAFT` hoặc `IN_REVIEW`.
5. Policy hai quyền mới, không seed, không picker, không grant.
6. Test mục 9. Không sửa Copilot, không sửa cổng an toàn, không sửa Learning, không sửa Staff V1.

`GetKnowledgeRulesAsync` đang nuốt `UndefinedTable` / `UndefinedColumn` và trả danh sách rỗng. K1 không mở rộng catch đó và không coi danh sách rỗng là đã qua cổng nguồn.

---

## 9. Test trước rollout có kiểm soát

Trên database cô lập đã có `414`–`417`, không trên production:

- `DRAFT` publish → từ chối.
- `IN_REVIEW` với chỉ `source = novixa` → từ chối.
- `IN_REVIEW` thiếu từng trường bắt buộc của mục 3 → từ chối.
- `REJECTED` → từ chối.
- `KR_OTHER` dù nguồn `ACCEPTED` → từ chối.
- `IN_REVIEW` đủ nguồn `ACCEPTED` → `PUBLISHED`, một publication event, sửa keyword bị trigger từ chối.
- Publish version 2 không đổi `adoption.rule_version` của version 1.
- Pin version chưa `PUBLISHED` → từ chối. Tenant A không đọc pin của tenant B.
- Token chỉ có `pharmacy.knowledge.approve` không publish được. Token chỉ có `platform.knowledge.publish` không pin được. ADMIN không có claim thì cả hai đều từ chối.
- `chest_pain`, `unconscious`, và `refer_pharmacist` vẫn không trả danh sách OTC thường.
- Publish không insert `pack_learning.enrollment` và không insert `operational_task`.
- Migration K1 không insert quyền và không update 35 rule thành `ACCEPTED` hoặc `PUBLISHED`.

---

## 10. Quyết định Product Owner còn mở

| Việc | Vì sao chưa chốt |
|---|---|
| Giữ mã `pharmacy.knowledge.review` và `pharmacy.knowledge.verify-source`, hay dùng mã khác | Đặc tả cần hai việc tách khỏi publish và pin. Chưa có quyết định tên quyền |
| Ai được giữ từng quyền, gồm việc một người có được vừa verify vừa publish | DEC-007 vẫn cấm cấp quyền khi chưa có quyết định riêng |
| Tài khoản nào giữ `pharmacy.knowledge.approve` lúc pin | DEC-015 chọn chủ nhà thuốc, chưa gán tài khoản |
| Rule nào được đưa cho chuyên môn trước | DEC-016 theo từng nhà thuốc |
| `KR_OTHER` có được viết lại thành rule khác sau này không | Nội dung seed hiện không đủ điều kiện. Đặc tả chỉ chặn publish nội dung đó |
| Mã category seed có trùng category sản phẩm của nhà thuốc không | Qualification: **NOT VERIFIED**. Cổng nguồn không trả lời câu này |
| Bài học nào được trỏ rule, và ai duyệt bài | S3. K1 chỉ chốt hình dạng trỏ |

Đã chốt bởi nhiệm vụ K1 này, không hỏi lại: rule chuyên môn không có nguồn `ACCEPTED` thì không publish. Không có lối `DRAFT → PUBLISHED`. Không điền nguồn hộ. Không cấp quyền trong lát implementation.
