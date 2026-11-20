# NOVIXA W5 — Sẵn sàng phát hành có kiểm soát

Ngày đọc: 2026-10-31, khoảng 11:52–12:10 +07. Production không được migrate, không bị restart, không bị ghi.

**Kết luận: READY FOR CONTROLLED PRODUCTION RELEASE**

Chưa phát hành. Cửa sổ hiện tại là ban ngày. `NT_XUANHOA` còn 2 ca `status = 1`. Đêm triển khai phải đọc lại hai điều đó, backup production ngay trước lệnh, và dùng đúng artifact dưới đây. Không rule nào được publish trong đợt này.

## 1. Production đang chạy

| Mục | Bằng chứng |
|---|---|
| Host | `103.200.23.229`, hostname `novixa`. Host key ED25519 `SHA256:7DfjxKHYj+Ip4+fc2YL+8cFp/UeBjr3hQfhGHv0LwQU` |
| API | `kit-platform-api` active, `NRestarts=0`, chạy từ 2026-10-30 00:09:47 +07 |
| Binary | `/var/www/kit-platform/api/KitPlatform.Api.dll` SHA256 `7bbf208b5edbbfc85e2d47aeefb0549b5af1b8ff4280eafa0c54813595906279`, 1 808 896 byte, mtime 2026-10-30 00:09:21 +07 |
| Health | `https://api.novixa.vn/api/health` = 200, `https://app.novixa.vn/` = 200 |
| Database | `novixa_prod`, PostgreSQL 16.14, cổng 127.0.0.1:5432 |
| Ledger | `kit_schema_migrations` 232 dòng. Cuối: `371_inventory_stock_review.sql` lúc 2026-09-27 10:44:40 +07 |
| 411–417 | Cả 7 file vắng trên ledger. `pharmacy_knowledge_rule_version`, adoption, publication event, safety review, `pack_learning.operational_task` chưa có |

Tenant `NT_XUANHOA`: `026a4f39-8717-4f3c-9932-8a911fc10bb6`, status 1. Cùng database còn `DEMO_CLINIC`, `DEMO_FAMILY`, `DEMO_PHARMACY`, `FOS_NHASMOKE56335`, `KIT_LOCAL`, `KIT_MKT`, `NVX-CS08`. Migration là cấp database, không tách được theo tenant. 411–417 không sửa dòng khách hàng, đơn bán, tồn kho hay sản phẩm.

Ca Xuân Hòa: status 1 = 2, status 2 = 203. Không đóng ca.

## 2. Tương thích 411–417

Đọc trên production trước khi diễn tập:

| Migration | Điều kiện đo được | Kết quả |
|---|---|---|
| 411 | `sales_returns` có 22 dòng, chưa có `client_return_id` | Cột mới nullable. Index unique chỉ áp khi key khác null |
| 412 | Checklist 12 dòng, 0 nhóm trùng `(tenant, branch, kind, business_date)`. Index `ux_success_shift_checklist_run_day` đang tồn tại. Chưa có `sales_shift_id` | Index mới tạo được. Index ngày cũ bị drop |
| 413 | `employees`, `branches`, `sales_shifts`, schema `pack_learning` đều có | `CREATE TABLE` được |
| 414 | `pharmacy_knowledge_rule` 35 dòng, chưa có `approval_status` | Default `PENDING_REVIEW`, rồi drop default. Không approve |
| 415 | Bảng review chưa có | Insert 13 cờ `PENDING_REVIEW`, `ON CONFLICT DO NOTHING` |
| 416 | 26 cờ rủi ro, tất cả `is_active = true`. Chưa có `publication_status` | Copy 35 rule sang version `IN_REVIEW`. Backfill 26 cờ đang active thành `PUBLISHED` trên cột mới. `is_active` không đổi. 0 pin |
| 417 | Chưa có bảng publication | Chỉ trigger. Không publish rule |

`416` backfill cờ rủi ro để runtime mới giữ hành vi cờ đang bật. Đây không phải phê duyệt 35 rule và không đụng 13 dòng review. Copilot sản phẩm chỉ đọc version `PUBLISHED` đã được tenant pin. Sau migration, pin = 0 nên quầy không nhận gợi ý sản phẩm từ W5.

Diễn tập trên VPS B, database `novixa_w5_upgrade`, 2026-10-31 12:06:14 +07. Schema dựng từ manifest production, bỏ `230`/`231`/`237`, chèn `069` trước `090`, dừng trước 411. `PRE_W5_FILES=220`. Trước W5: 35 rule, 26 cờ, chưa có bảng version. Dump `/var/backups/novixa_w5_upgrade_pre411.dump`, 1 185 995 byte, mode 600.

Sau 411–417: 35 version `IN_REVIEW`, `KR_OTHER` `IN_REVIEW`, 35 rule `PENDING_REVIEW`, 26 cờ `publication_status = PUBLISHED`, 0 adoption, 13 review `PENDING_REVIEW`. Index checklist còn `ux_success_shift_checklist_run_day_legacy` và `ux_success_shift_checklist_run_shift_kind`.

Restore dump vào database khác: 35 rule, 26 cờ, bảng version không còn (`to_regclass` rỗng). `pg_restore` lần đầu lỗi vì user `postgres` không đọc được `/var/backups`; lần sau đọc bản copy ở `/tmp`, `RESTORE_RC=0`. Hai database diễn tập đã drop. Staging `novixa_w5_staging` không nằm trong đường migrate này.

Không copy dữ liệu khách Xuân Hòa sang VPS B. Điều kiện dữ liệu production được đo bằng SELECT. Diễn tập chứng minh DDL và biến đổi trên cùng số rule/cờ của seed.

## 3. Catalog Xuân Hòa

3 038 sản phẩm active. 3 037 không có `category_id`. 3 035 OTC `drug_type = 1`, 3 đơn thuốc. Category thực tế chỉ có `PGN00001`–`PGN00006`, gần như không gắn sản phẩm.

Năm mã `HO_HAP`, `GIAM_DAU`, `DA_DAY`, `VITAMIN`, `NGAO_DUOC` không tồn tại trên tenant. `category_only_active = 0` với cả 35 rule vì không có category để trùng, không phải vì sản phẩm đã được loại đúng. Gate `KHANG_SINH` cũng không bắt được gì khi sản phẩm không có category. Ba sản phẩm `drug_type = 2` bị gate đơn thuốc loại.

`KR_OTHER` từ khóa rỗng, category `{VITAMIN,GIAM_DAU,HO_HAP}`, 0 hit. Code W5 vẫn loại mã này khỏi copilot.

Số liệu cột “OTC bán được” là sản phẩm active, OTC, `drug_type = 1`, có đơn vị tính, còn lô bán được, tên hoặc hoạt chất hoặc mô tả khớp từ khóa. Category không làm sản phẩm đủ điều kiện.

| Rule | Loại chuyên môn | Từ khóa active | OTC bán được | Ghi chú catalog |
|---|---|---:|---:|---|
| KR_COUGH_DRY | CANDIDATE_FOR_CLINICAL_REVIEW | 6 | 2 | |
| KR_COUGH_PHLEGM | CANDIDATE_FOR_CLINICAL_REVIEW | 6 | 2 | |
| KR_COUGH | CANDIDATE_FOR_CLINICAL_REVIEW | 130 | 62 | `ho` khớp 122 tên. 57 còn hàng không khớp prospan/decolgen/cam cum. Mẫu: Acyclovir gel, Alpha Choay, An Cung Ngưu Hoàng Hoàn |
| KR_RUNNY_NOSE | CANDIDATE_FOR_CLINICAL_REVIEW | 3 | 3 | |
| KR_NASAL_CONG | CANDIDATE_FOR_CLINICAL_REVIEW | 3 | 3 | |
| KR_HOARSENESS | CANDIDATE_FOR_CLINICAL_REVIEW | 9 | 5 | |
| KR_THROAT_CLEARING | CANDIDATE_FOR_CLINICAL_REVIEW | 8 | 5 | |
| KR_SORE_THROAT | CANDIDATE_FOR_CLINICAL_REVIEW | 4 | 3 | |
| KR_SNEEZING | CANDIDATE_FOR_CLINICAL_REVIEW | 3 | 3 | |
| KR_COMMON_COLD | CANDIDATE_FOR_CLINICAL_REVIEW | 3 | 3 | |
| KR_FLU | CANDIDATE_FOR_CLINICAL_REVIEW | 16 | 11 | `cum` khớp curcumin, Dercumar, Kem UniE Cumin. `cúm` khớp cảm cúm và bộ test Covid |
| KR_FEVER | CANDIDATE_FOR_CLINICAL_REVIEW | 19 | 8 | `hạ sốt` 7, efferalgan 7, panadol 4, paracetamol 1 |
| KR_CHILLS | CANDIDATE_FOR_CLINICAL_REVIEW | 4 | 3 | |
| KR_BODY_ACHE | CANDIDATE_FOR_CLINICAL_REVIEW | 9 | 4 | |
| KR_DIARRHEA | CANDIDATE_FOR_CLINICAL_REVIEW | 1 | 1 | |
| KR_CONSTIPATION | CANDIDATE_FOR_CLINICAL_REVIEW | 2 | 1 | |
| KR_BACK_PAIN | CANDIDATE_FOR_CLINICAL_REVIEW | 5 | 2 | |
| KR_DEHYDRATION | NEEDS_CLINICAL_REVIEW | 6 | 4 | |
| KR_NAUSEA | NEEDS_CLINICAL_REVIEW | 3 | 1 | |
| KR_VOMITING | NEEDS_CLINICAL_REVIEW | 5 | 2 | `non` khớp Philatop Đạm sữa non |
| KR_HEARTBURN | NEEDS_CLINICAL_REVIEW | 2 | 2 | |
| KR_REFLUX | NEEDS_CLINICAL_REVIEW | 1 | 1 | |
| KR_BLOATING | NEEDS_CLINICAL_REVIEW | 3 | 1 | |
| KR_ABDOMINAL | NEEDS_CLINICAL_REVIEW | 1 | 1 | |
| KR_ALLERGY | NEEDS_CLINICAL_REVIEW | 4 | 0 | loratadine 3, cetirizine 1, không còn hàng theo gate |
| KR_ITCHY_SKIN | NEEDS_CLINICAL_REVIEW | 5 | 0 | |
| KR_RASH | NEEDS_CLINICAL_REVIEW | 4 | 0 | |
| KR_HIVES | NEEDS_CLINICAL_REVIEW | 4 | 0 | |
| KR_HEADACHE | NEEDS_CLINICAL_REVIEW | 16 | 10 | Có ibuprofen / brufen |
| KR_JOINT_PAIN | NEEDS_CLINICAL_REVIEW | 8 | 5 | |
| KR_TOOTHACHE | NEEDS_CLINICAL_REVIEW | 5 | 2 | |
| KR_FATIGUE | NEEDS_CLINICAL_REVIEW | 63 | 24 | `vitamin` khớp 61 tên |
| KR_DIZZINESS | NEEDS_CLINICAL_REVIEW | 62 | 24 | `vitamin` khớp 61 tên |
| KR_INSOMNIA | NEEDS_CLINICAL_REVIEW | 1 | 0 | |
| KR_OTHER | NOT_ELIGIBLE | 0 | 0 | Không đưa vào đường gợi ý |

Cả 35 thiếu `reference_source`. Không có quyền `pharmacy.knowledge.approve` hay `platform.knowledge.publish` trong `permissions`, và không có grant. Không cấp thêm. Không publish. Không sửa tên sản phẩm, category hay nội dung rule.

Danh sách dược sĩ phải giữ trước khi pin bất kỳ rule nào:

- Không pin `KR_OTHER`.
- Không duyệt `KR_COUGH` khi còn từ khóa `ho`.
- Không duyệt `KR_FLU` khi còn từ khóa `cum` không dấu.
- `vitamin` trên `KR_FATIGUE` và `KR_DIZZINESS` đang quét rộng trên catalog này.
- `acc` khớp “macca”. `non` khớp “sữa non”.
- Nhóm dị ứng, ibuprofen, PPI, domperidone, mất ngủ, nôn, đau bụng vẫn ở `NEEDS_CLINICAL_REVIEW` như hồ sơ thẩm định. Hồ sơ đầy đủ: `NOVIXA_W5_INITIAL_KNOWLEDGE_CATALOG_QUALIFICATION_V1.md`.

## 4. Artifact được nghiệm thu

Chỉ bản này được đưa lên production:

`/var/www/kit-platform-w5/api/KitPlatform.Api.dll`

SHA256 `cc4eb7e82ae927f2f612b68c1f0ea14950fa2646486346a4137f4a7eaf6fecf7`

Đã đối lại trên đĩa VPS B sau diễn tập. Khác binary production. UI staging đã build với API `http://w5-api.157.10.53.182.sslip.io`. Bản admin production phải trỏ API production lúc đóng gói đêm, từ cùng source đã nghiệm thu, không sửa matching hay rule giữa hai môi trường.

Staging sau khi xóa dòng test `w5*` / `z6*`: 35 rule, 35 version `IN_REVIEW`, 0 publication event, 13 review, 0 adoption. Ba trigger đóng băng đang bật (`tgenabled = O`). Health W5, API cũ và Second Brain đều 200.

## 5. CONNECT trên VPS B

`novixa_prod` ACL: `PUBLIC` còn `TEMP`, `kitplatform` có `CONNECT`. `has_database_privilege(kitplatform, novixa_prod, CONNECT) = true`. Role `novixa_w5_staging` = false. `pg_stat_activity`: `kitplatform` 3 session idle trên `novixa_prod`. Journal `kit-platform-api` từ 11:40 +07: 0 dòng `permission denied`. Health API cũ 200. Second Brain active, HTTP 127.0.0.1:5095 = 200. Không đổi ACL thêm.

## 6. Đêm triển khai

Thứ tự, chỉ sau backup đã restore thử:

1. `411_sales_return_client_return_id.sql`
2. `412_success_checklist_pos_shift.sql`
3. `413_learning_operational_task.sql`
4. `414_pharmacy_knowledge_approval.sql`
5. `415_pharmacy_knowledge_approval_history.sql`
6. `416_pharmacy_knowledge_tenant_governance.sql`
7. `417_pharmacy_knowledge_publication_event.sql`

Không chạy `411_415_pharmacy_ops_v2_down.sql`. Không chạy lại 001–371. Không thêm 411–417 vào `migration-files.prod.txt` trước đêm đó. Ghi ledger sau khi từng file thành công.

Backup: `pg_dump -Fc` `novixa_prod` ra file mode 600 ngoài thư mục app. Restore thử vào database tên mới trên cùng cụm, đếm tenant, rule, sản phẩm active Xuân Hòa, ca status 1. Drop database thử. Giữ dump.

Đối soát sau migrate, cùng tenant `026a4f39-8717-4f3c-9932-8a911fc10bb6`:

- Số sản phẩm active, số lô, số đơn, số ca status 1 không đổi ngoài ca nhà thuốc tự đóng.
- 35 rule, `PENDING_REVIEW`, 0 pin, 0 version `PUBLISHED`.
- 26 cờ `publication_status = PUBLISHED`, `is_active` vẫn true.
- 13 review vẫn `PENDING_REVIEW`.
- Health 200. Login sai mật khẩu trả 401. Một đơn bán đọc và một tồn đọc của Xuân Hòa khớp số trước cắt.
- Second Brain không restart.

Gián đoạn: restart `kit-platform-api` sau khi file mới đã nằm trên đĩa và migration xong. Dự kiến vài giây đến khi health 200. Diễn tập dừng `kit-platform-w5`: health W5 = 000, API cũ vẫn 200, Second Brain active. Sau start, health W5 lần đầu có thể 000 trong khi unit đã active; lần đọc sau là 200. Chừa thời gian đó, không rollback chỉ vì probe đầu.

Dừng nếu: chưa phải đêm hoặc còn ca status 1; health trước cắt khác 200; dump không restore được; hash file copy lên khác `cc4eb7e8…`; bất kỳ file 411–417 lỗi; số sản phẩm hoặc đơn lệch; Second Brain đổi trạng thái.

Rollback: dừng API, trả lại cây `/var/www/kit-platform/api` đã chép sang trước cắt (hash `7bbf208b…`), `pg_restore` dump production vào database mới rồi đổi tên khi API đang dừng, start lại chỉ `kit-platform-api`. Không restore đè bằng cách xóa dữ liệu phát sinh sau cắt nếu nhà thuốc đã bán; khi đó giữ dump và xử lý từng migration ngược thay vì drop. Không restart Second Brain.

## 7. Việc không làm trong lượt này

Không deploy `103.200.23.229`. Không commit. Không cấp quyền. Không publish rule. Không sửa catalog Xuân Hòa. Không đóng 2 ca đang mở.
