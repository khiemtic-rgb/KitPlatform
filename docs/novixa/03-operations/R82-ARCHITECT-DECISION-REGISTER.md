# R82 — Bảng đăng ký quyết định Architect

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

**Nguồn:** Mục 7 của `docs/novixa/03-operations/R74-PRODUCTION-READONLY-PREFLIGHT-DESIGN.md`, đối chiếu mục 1–6 của cùng tài liệu và Decision Register R82 đã được Architect xem xét.

**Phạm vi chấp thuận:** Chấp thuận thiết kế ở cấp đặc tả cho R82-ADR-01 đến R82-ADR-09. Việc chấp thuận không xác nhận production readiness, không cho phép mở kết nối production và không cho phép tạo role, schema, view, grant hoặc triển khai migration 386.

**`PRODUCTION_EXECUTION_APPROVED = NO`**

## Bảng tổng hợp quyết định

| ID | Nội dung | Quyết định Architect |
|---|---|---|
| R82-ADR-01 | Đường JOIN của `kit_mkt_admin_users` | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-02 | SELECT hai cột `role_permissions` dùng cho JOIN | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-03 | Hai dạng `ledger_386` và cách diễn giải filename | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-04 | Hai GUC không phải ranh giới chống ghi | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-05 | Quản lý mật khẩu, `VALID UNTIL`, `statement_timeout` ngoài tài liệu | `ACCEPTED_AT_SPECIFICATION_LEVEL` — chỉ chấp thuận nguyên tắc |
| R82-ADR-06 | Phụ thuộc quyền catalog và `pg_get_constraintdef` | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-07 | Ba cột `tenants` nằm ngoài ma trận GRANT | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-08 | Tiêu chí xác nhận bốn unique constraints | `ACCEPTED_AT_SPECIFICATION_LEVEL` |
| R82-ADR-09 | Chấp thuận đặc tả không đồng nghĩa production sẵn sàng | `ACCEPTED_AT_SPECIFICATION_LEVEL` |

---

## R82-ADR-01 — Đường JOIN `kit_mkt_admin_users`

**Quyết định:** Chấp thuận đường JOIN qua `user_roles` và `roles.tenant_id`. Không đọc hoặc sử dụng `users.tenant_id`.

### Đặc tả được chấp thuận

View `preflight_ro.kit_mkt_admin_users` sử dụng đường nối:

1. `user_roles.user_id = users.id`
2. `roles.id = user_roles.role_id`
3. `tenants.id = roles.tenant_id`

Điều kiện lọc:

- `tenants.tenant_code = 'KIT_MKT'`
- `tenants.deleted_at IS NULL`
- `roles.role_code = 'ADMIN'`

Các cột xuất:

- `user_id`
- `username`
- `user_status`
- `user_deleted_at`
- `role_id`
- `role_status`

Không thêm điều kiện `users.tenant_id = tenants.id`; không cấp quyền SELECT cho `users.tenant_id`.

User đã xóa vẫn có thể xuất hiện nếu còn bản ghi liên kết phù hợp. Mọi `roles.status` đều được đưa qua view, không tự bổ sung bộ lọc trạng thái.

### Điều kiện và rủi ro còn lại

Số lượng user ADMIN thực tế trên production chưa được xác minh. RLS trên các bảng identity chưa được kiểm tra. Nếu RLS chặn owner và view không trả dữ liệu, phải coi đó là trạng thái đóng, không cấp `BYPASSRLS` để vượt qua.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-02 — SELECT hai cột `role_permissions` chỉ dùng cho JOIN

**Quyết định:** Chấp thuận SELECT theo cột đối với `role_permissions.role_id` và `role_permissions.permission_id`, dù hai cột này không xuất ra kết quả view.

### Đặc tả được chấp thuận

`famixa_preflight_owner` chỉ nhận `GRANT SELECT` trên đúng các cột cần thiết, không có `GRANT OPTION`, không nhận SELECT toàn bảng.

Trong `preflight_ro.canon_grants`:

- `role_permissions.role_id` dùng để JOIN với `roles.id`.
- `role_permissions.permission_id` dùng để JOIN với `permissions.id`.
- `role_permissions.id` được xuất thành `grant_id`.
- `role_permissions.created_at` không được cấp quyền.
- `role_permissions` không có `tenant_id`; tenant được xác định qua `roles.tenant_id`.

`famixa_canon_preflight_ro` không có quyền bảng hoặc quyền cột trực tiếp trên các bảng nghiệp vụ.

`has_table_privilege(..., 'SELECT')` của owner trên `role_permissions` phải là false; quyền đọc chỉ nằm ở mức cột.

### Điều kiện và rủi ro còn lại

Quyền thực tế trên production chưa được đo. Checklist quyền cột phải được thực hiện trước khi kết luận preflight đạt.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-03 — Hai dạng `ledger_386` và quy tắc diễn giải filename

**Quyết định:** Chấp thuận hai dạng view `ledger_386`, lựa chọn theo kết quả `to_regclass('public.kit_schema_migrations')` tại thời điểm thiết lập.

### Đặc tả được chấp thuận

**Trường hợp bảng ledger tồn tại:**

- Owner chỉ nhận SELECT trên cột `filename`.
- `filename` chỉ được dùng trong điều kiện `EXISTS` để kiểm tra đúng tên `386_pack_content_famixa_canon_administer.sql`.
- Không cấp hoặc tham chiếu `applied_at`.
- View có `ledger_relation_exists = TRUE` và `filename_recorded` là boolean.

**Trường hợp bảng ledger không tồn tại:**

- Không cấp quyền trên ledger.
- View không tham chiếu `public.kit_schema_migrations`.
- View trả `ledger_relation_exists = FALSE`, `filename_recorded = NULL`.

Cả hai dạng view sử dụng nhánh `UNION ALL ... WHERE false` như đặc tả R74.

### Quy tắc diễn giải

`filename_recorded = true` chỉ chứng minh ledger có ghi đúng tên file đó. Nó không chứng minh migration đã thực sự được thực thi thành công.

`filename_recorded = false` hoặc `NULL` cũng không chứng minh migration chưa chạy.

Việc xác nhận trạng thái permission và grant phải dựa trên `canon_permission` và `canon_grants`. Content runner hiện tại không ghi ledger này, và migration 386 cũng không tự ghi ledger.

### Điều kiện và rủi ro còn lại

Sự tồn tại và nội dung của ledger trên production chưa được xác minh. Chấp thuận thiết kế không đồng nghĩa với triển khai migration 386.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-04 — `statement_timeout` và `default_transaction_read_only`

**Quyết định:** Chấp thuận rằng hai thiết lập này là mặc định vận hành của phiên, không phải ranh giới bảo mật chống ghi không thể vượt qua.

### Đặc tả được chấp thuận

Trên role `famixa_canon_preflight_ro`:

- Đặt `statement_timeout` thành giá trị hữu hạn, khác 0.
- Đặt `default_transaction_read_only = on`.
- Đặt `search_path = preflight_ro`.

Các giá trị này là cấu hình phiên. Role truy vấn có thể thay đổi `statement_timeout` hoặc tắt `default_transaction_read_only` trong phiên.

Ranh giới chống ghi dựa trên:

1. Quyền cột được cấp chính xác.
2. Không cấp quyền DML.
3. Role truy vấn chỉ được SELECT các view đã định nghĩa.
4. Các view sử dụng hình dạng không tự cập nhật được theo đặc tả.

Checklist phải kiểm tra `rolconfig`, các thiết lập phiên và `transaction_read_only` trong transaction kiểm tra đầu tiên. Các kiểm tra này không chứng minh những phiên sau không thể thay đổi GUC.

### Điều kiện và rủi ro còn lại

Giá trị timeout cụ thể thuộc ADR-05. Thuộc tính role thực tế trên production chưa được xác minh.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-05 — Mật khẩu, `VALID UNTIL` và `statement_timeout`

**Quyết định:** Chấp thuận nguyên tắc quản lý các giá trị nhạy cảm qua kênh bên ngoài tài liệu thiết kế. Chưa chọn hoặc phê duyệt giá trị cụ thể trong register này.

### Đặc tả được chấp thuận

Các giá trị áp dụng cho `famixa_canon_preflight_ro` phải được quản trị viên thiết lập qua kênh phù hợp, bên ngoài:

- Tài liệu thiết kế.
- `api.env`.
- `secrets.generated`.

Các mục cần thiết lập:

- Mật khẩu cho role đăng nhập.
- `VALID UNTIL` có thời hạn.
- `statement_timeout` hữu hạn, khác 0.

Role truy vấn có `LOGIN` và `CONNECTION LIMIT 2`.

Checklist phải xác nhận `rolconnlimit = 2` và `rolvaliduntil` có giá trị, còn hiệu lực tại thời điểm kiểm tra.

### Điều kiện và rủi ro còn lại

Register này không chứa, không đề xuất và không đọc mật khẩu, thời hạn cụ thể hoặc giá trị timeout.

Các giá trị đó chưa được chọn qua kênh bên ngoài. Việc chọn các giá trị sau này không tự cấp quyền tạo role hoặc mở kết nối production.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL` — chỉ chấp thuận nguyên tắc quản lý secret.

---

## R82-ADR-06 — Phụ thuộc quyền catalog chưa xác minh

**Quyết định:** Chấp thuận việc coi quyền đọc catalog hiện có và quyền EXECUTE của `pg_get_constraintdef` là các phụ thuộc chưa xác minh. Nếu thiếu quyền cần thiết, phải dừng; không tự mở rộng quyền catalog.

### Đặc tả được chấp thuận

View `preflight_ro.constraints_expected` không nhận GRANT mới trên catalog. Thiết kế dựa vào:

- Quyền SELECT catalog hiện có.
- Quyền EXECUTE hiện có của `pg_get_constraintdef`.

Không cấp quyền trên `pg_authid`, không thay đổi quyền catalog toàn cụm và không mở rộng quyền để làm cho view hoạt động bằng mọi giá.

Nếu production thiếu quyền cần thiết, dừng quy trình thiết lập preflight. Không tự cấp SELECT catalog rộng hơn.

### Điều kiện và rủi ro còn lại

Chưa xác minh trên production:

- Quyền PUBLIC đối với catalog và schema liên quan.
- EXECUTE của `pg_get_constraintdef`.
- Phiên bản PostgreSQL đang chạy.
- Khả năng hỗ trợ `security_barrier` và `security_invoker`.

Nếu máy chủ không chấp nhận các reloption bắt buộc, dừng và không giữ lại một phần view của đợt thiết lập.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-07 — Ba cột `tenants` nằm ngoài ma trận GRANT

**Quyết định:** Chấp thuận không cấp quyền SELECT cho ba cột:

- `tenants.organization_id`
- `tenants.business_vertical`
- `tenants.rx_directory_discoverable`

Không bổ sung các cột này vào ma trận GRANT.

### Đặc tả được chấp thuận

Với mỗi cột còn tồn tại trên production, `has_column_privilege(..., 'SELECT')` phải trả về false cho cả:

- `famixa_preflight_owner`
- `famixa_canon_preflight_ro`

Nếu kết quả true với bất kỳ role nào, checklist không đạt và phải dừng. Không mở rộng quyền chỉ để vượt qua checklist.

Nếu cột không tồn tại trên database thực tế, không có quyền cột để đọc; việc vắng mặt không phải lý do để cấp quyền.

### Điều kiện và rủi ro còn lại

Sự tồn tại của các cột và quyền thực tế trên production chưa được xác minh.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-08 — Tiêu chí đạt của `constraints_expected`

**Quyết định:** Chấp thuận rằng mỗi constraint chỉ đạt khi đồng thời thỏa tất cả điều kiện về loại, trạng thái validate, bảng, tên và thứ tự cột.

### Đặc tả được chấp thuận

Một constraint chỉ đạt khi:

1. `contype = 'u'`.
2. `convalidated = true`.
3. Đúng bảng trong schema `public`.
4. Đúng tên constraint kỳ vọng.
5. Đúng thứ tự cột theo `conkey`.

Bốn constraint kỳ vọng:

| Constraint | Thứ tự cột bắt buộc |
|---|---|
| `uq_permissions_code` | `permission_code` |
| `uq_role_permissions` | `role_id, permission_id` |
| `uq_tenants_code` | `tenant_code` |
| `uq_roles_tenant_code` | `tenant_id, role_code` |

`actual_table` trống không phải tiêu chí duy nhất để kết luận constraint thiếu. Bảng có thể tồn tại trong `public` nhưng không có dòng `pg_constraint` khớp tên; khi đó các trường constraint liên quan vẫn trống và dòng không đạt.

Unique index cùng tên không được chấp nhận thay cho constraint thỏa đủ điều kiện.

Nếu catalog không còn các quyền mà view phụ thuộc, áp dụng ADR-06: dừng, không mở rộng GRANT catalog.

### Điều kiện và rủi ro còn lại

Bốn constraint thực tế trên production chưa được kiểm tra. Chưa có kết quả checklist xác nhận các điều kiện trên.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## R82-ADR-09 — Chấp thuận đặc tả không phải xác nhận production readiness

**Quyết định:** Chấp thuận rõ ràng rằng việc chấp thuận đặc tả chỉ đóng các quyết định ở cấp thiết kế. Nó không xác nhận production đã sẵn sàng.

### Trạng thái hiện tại

Chưa có bằng chứng production xác nhận:

- Phiên bản PostgreSQL và hỗ trợ các reloption cần thiết.
- Quy tắc kết nối `pg_hba`.
- Owner thực tế của các bảng và thuộc tính role.
- Membership trực tiếp hoặc gián tiếp giữa các role.
- RLS trên các bảng identity.
- Quyền PUBLIC trên schema và catalog.
- EXECUTE của `pg_get_constraintdef`.
- Sự tồn tại của `users.kit_account_id`.
- Bốn unique constraints.
- Tenant `KIT_MKT`, permission `content.canon.administer`, các grant và user ADMIN thực tế.
- Sự tồn tại và nội dung của `public.kit_schema_migrations`.

Checklist R74 chưa được chạy trên production.

### Điều kiện và ranh giới thực thi

Chấp thuận R82 không cho phép:

- Kết nối production.
- Chạy SQL trên production.
- Tạo role hoặc schema.
- Tạo view hoặc cấp GRANT.
- Triển khai migration 386.
- Đọc secret hoặc thử thông tin xác thực.

Mọi hoạt động trên production phải được đánh giá và phê duyệt riêng sau khi các điều kiện cần thiết được xác minh.

**Trạng thái:** `ACCEPTED_AT_SPECIFICATION_LEVEL`

---

## Xác nhận cuối cùng

- Cả chín ADR đã được Architect chấp thuận ở cấp đặc tả.
- ADR-05 chỉ chấp thuận nguyên tắc quản lý secret bên ngoài tài liệu; không chứa giá trị secret cụ thể.
- Các điều kiện production chưa được xác minh vẫn còn mở.
- Chưa có phê duyệt kết nối hoặc thực thi production.

`PRODUCTION_EXECUTION_APPROVED = NO`

**Kết luận:** R82 đã được chấp thuận ở cấp đặc tả. Production readiness và quyền thực thi vẫn chưa được phê duyệt.
