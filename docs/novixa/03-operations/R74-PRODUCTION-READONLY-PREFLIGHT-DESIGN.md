**Kết luận: đặc tả cột đã được Architect chấp thuận ở cấp đặc tả qua [R82](R82-ARCHITECT-DECISION-REGISTER.md). `PRODUCTION_EXECUTION_APPROVED = NO`**

Đặc tả tám view và ma trận cột dưới đây khép lỗ danh sách quyền của bản trước. Chín điểm ở mục 7 đã được ghi thành R82-ADR-01 đến R82-ADR-09 với trạng thái `ACCEPTED_AT_SPECIFICATION_LEVEL`. Chấp thuận đó không xác nhận production readiness. Chưa tạo role, chưa tạo schema, chưa tạo view, chưa mở kết nối, chưa chạy SQL, và không có secret nào được đọc. Tài liệu này không xác minh production. Mục 8 ghi mẫu SQL cho các kiểm tra còn thiếu. Việc ghi mẫu không chạy các câu đó.

Tên cột nghiệp vụ lấy từ `migrations/002_identity.sql`, `migrations/098_schema_migrations.sql`, `migrations/183_people_auth_selfie_customer_feedback.sql` và `migrations/228_kit_accounts_memberships.sql`. `public.roles` không có `deleted_at`. `public.user_roles` và `public.role_permissions` không có `tenant_id`. Cột catalog là cột hệ thống PostgreSQL mà thiết kế constraint đã dùng; hình dạng catalog trên production chưa được xác minh.

## 1. Hai role và mặc định phiên

Chỉ role đăng nhập được dùng cho preflight. Role ứng dụng `kitplatform` không tham gia.

`famixa_preflight_owner` là `NOLOGIN`, `NOINHERIT`, `NOSUPERUSER`, `NOBYPASSRLS`, `NOCREATEDB`, `NOCREATEROLE`, `NOREPLICATION`. Role này sở hữu schema `preflight_ro` và cả tám view. Superuser `postgres` chỉ dùng một lần để tạo role, cấp đúng các cột ở mục 3, rồi `SET ROLE famixa_preflight_owner` trước khi `CREATE VIEW`. Owner của view phải là role này. View do `postgres` tạo sẽ chạy với quyền superuser và đi vòng qua RLS.

`famixa_canon_preflight_ro` là role truy vấn: `LOGIN`, `NOINHERIT`, `NOSUPERUSER`, `NOBYPASSRLS`, `NOCREATEDB`, `NOCREATEROLE`, `NOREPLICATION`, `CONNECTION LIMIT 2`.

Quản trị viên đặt các mục sau trên đúng role truy vấn, ngoài `api.env`, `secrets.generated` và ngoài tài liệu này:

- Mật khẩu.
- `VALID UNTIL`.
- `ALTER ROLE famixa_canon_preflight_ro SET statement_timeout` bằng một giá trị hữu hạn, khác 0.
- `ALTER ROLE famixa_canon_preflight_ro SET default_transaction_read_only = on`.
- `ALTER ROLE famixa_canon_preflight_ro SET search_path = preflight_ro`.

`statement_timeout` là giới hạn vận hành cho thời gian một câu lệnh. Nó thuộc nhóm user-set. Chính role truy vấn có thể `SET statement_timeout = 0` trong phiên. Nó không phải cơ chế chống ghi không thể vượt qua.

`default_transaction_read_only = on` là mặc định phiên, đặt tại `rolconfig` của role truy vấn như câu lệnh trên. Checklist có kiểm tra mặc định này. Tham số cũng thuộc nhóm user-set, nên role truy vấn có thể `SET default_transaction_read_only = off` trong phiên. Nó không phải ranh giới bảo mật. Ranh giới chống ghi là tập quyền cột, việc không cấp DML, và hình dạng view không tự cập nhật.

`search_path` của role truy vấn là `preflight_ro`. `pg_catalog` vẫn được PostgreSQL tìm ngầm. Mọi quan hệ trong view đều viết đủ schema. `pg_authid` không đọc được với role thường.

Mỗi view được tạo với `security_barrier = true` và `security_invoker = false`. View chạy bằng quyền của owner. Role truy vấn chỉ có `SELECT` trên view, không có `GRANT OPTION`, và không có `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES` hay `TRIGGER`. `MAINTAIN` không được cấp. Câu kiểm chỉ gọi quyền đó khi `server_version_num >= 170000`, như Q6. `security_barrier` áp điều kiện lọc của view trước điều kiện do phiên truy vấn đưa vào. Nếu máy chủ từ chối reloption `security_invoker`, dừng và không giữ view nào của đợt này: gỡ view `preflight_ro` vừa tạo trong lần thử đó, không tạo lại theo mặc định khác.

`db_identity` không có bảng nền, nên không có nhánh `UNION ALL … WHERE false`. Bảy view còn lại thêm một nhánh `UNION ALL` chỉ gồm hằng `NULL` đúng kiểu, với `WHERE false`. Nhánh này không tham chiếu bảng. View có `UNION` không còn là view tự cập nhật được.

`NOINHERIT` chỉ chặn việc tự động dùng quyền của role khác. Nó không chặn `SET ROLE`. Lớp chặn `SET ROLE` là cấm membership trực tiếp và gián tiếp. Role truy vấn không là thành viên của owner. Cả hai role không có đường membership tới nhau, tới `kitplatform`, `postgres`, `pg_read_all_data`, `pg_write_all_data`, `pg_monitor` hay `pg_signal_backend`.

`REVOKE ALL ON SCHEMA preflight_ro FROM PUBLIC`.

## 2. SQL của tám view

SQL này là đặc tả. R82 đã chấp thuận đặc tả ở cấp thiết kế. Chấp thuận đó không phải lệnh chạy SQL, không mở kết nối production, và không cho phép tạo role, schema, view, grant hoặc triển khai migration 386. File này không chạy SQL.

Trong bảy view dữ liệu, nhánh `UNION ALL … WHERE false` ở cuối là một phần của định nghĩa, không phải chú thích.

### `preflight_ro.db_identity`

Không có bảng, không có `JOIN`, không có `WHERE`. Biểu thức:

| Cột xuất | Biểu thức |
|---|---|
| `database_name` | `current_database()::text` |
| `server_addr` | `inet_server_addr()` |
| `server_port` | `inet_server_port()` |
| `current_user_name` | `current_user::text` |
| `in_recovery` | `pg_is_in_recovery()` |
| `server_version` | `current_setting('server_version')` |
| `transaction_read_only` | `current_setting('transaction_read_only')` |

`current_user` trong view vẫn là người dùng của phiên. Với `security_invoker = false`, giá trị này là `famixa_canon_preflight_ro` khi role đó truy vấn, không phải owner.

```sql
CREATE VIEW preflight_ro.db_identity
WITH (security_barrier = true, security_invoker = false) AS
SELECT current_database()::text AS database_name,
       inet_server_addr() AS server_addr,
       inet_server_port() AS server_port,
       current_user::text AS current_user_name,
       pg_is_in_recovery() AS in_recovery,
       current_setting('server_version') AS server_version,
       current_setting('transaction_read_only') AS transaction_read_only;
```

### `preflight_ro.constraints_expected`

Không đọc bảng nghiệp vụ. Tập dẫn là bốn hằng. `LEFT JOIN` giữ một dòng khi bảng hoặc constraint không khớp. Dòng vẫn hiện không có nghĩa là constraint đạt.

| Nguồn | Cột dùng | Việc dùng |
|---|---|---|
| Hằng | `expected_name`, `expected_table` | Bốn tên và bốn bảng kỳ vọng |
| `pg_catalog.pg_namespace` | `oid`, `nspname` | `nspname = 'public'` |
| `pg_catalog.pg_class` | `oid`, `relname`, `relnamespace` | `relname` bằng bảng kỳ vọng, trong schema `public` |
| `pg_catalog.pg_constraint` | `oid`, `conname`, `contype`, `convalidated`, `conrelid`, `conkey` | Khớp tên trên đúng quan hệ. Không đọc `pg_index` |
| `pg_catalog.pg_attribute` | `attrelid`, `attnum`, `attname`, `attisdropped` | Tên cột theo thứ tự `conkey`, bỏ cột đã drop |
| Hàm | `pg_get_constraintdef(pg_constraint.oid)` | Định nghĩa nguyên văn |

Cột xuất: `expected_name`, `expected_table`, `expected_schema`, `actual_table`, `contype`, `convalidated`, `constraint_definition`, `column_names`.

`column_names` là `string_agg(attname, ', ')` theo thứ tự `conkey`. Một dòng chỉ đạt khi đồng thời đủ các điều kiện sau: `contype = 'u'`, `convalidated = true`, đúng bảng (`actual_table` bằng `expected_table` và `expected_schema = 'public'`), đúng tên (`pg_constraint.conname` khớp `expected_name`), và đúng thứ tự cột. Chuỗi cột đạt, theo đúng thứ tự `conkey`: `permission_code` cho `uq_permissions_code`; `role_id, permission_id` cho `uq_role_permissions`; `tenant_code` cho `uq_tenants_code`; `tenant_id, role_code` cho `uq_roles_tenant_code`.

`actual_table` trống không phải tiêu chí duy nhất để kết luận constraint bị thiếu. `actual_table` là `pg_class.relname` của bảng kỳ vọng. Bảng còn trong `public` mà không có dòng `pg_constraint` khớp tên thì `actual_table` vẫn là tên bảng, còn `contype`, `convalidated`, `constraint_definition` và `column_names` trống; dòng đó không đạt. Constraint sai kiểu, chưa validate, sai bảng, sai tên hoặc sai thứ tự cột cũng không đạt. Unique index cùng tên mà không có dòng `pg_constraint` thỏa đủ các điều kiện trên thì không thay constraint.

```sql
CREATE VIEW preflight_ro.constraints_expected
WITH (security_barrier = true, security_invoker = false) AS
SELECT expected.expected_name,
       expected.expected_table,
       'public'::text AS expected_schema,
       pg_class.relname::text AS actual_table,
       pg_constraint.contype::text AS contype,
       pg_constraint.convalidated,
       pg_get_constraintdef(pg_constraint.oid) AS constraint_definition,
       (
           SELECT string_agg(pg_attribute.attname::text, ', ' ORDER BY key_item.ordinality)
           FROM unnest(pg_constraint.conkey) WITH ORDINALITY AS key_item(attnum, ordinality)
           JOIN pg_catalog.pg_attribute AS pg_attribute
             ON pg_attribute.attrelid = pg_constraint.conrelid
            AND pg_attribute.attnum = key_item.attnum
            AND NOT pg_attribute.attisdropped
       ) AS column_names
FROM (VALUES
    ('uq_permissions_code'::text, 'permissions'::text),
    ('uq_role_permissions'::text, 'role_permissions'::text),
    ('uq_tenants_code'::text, 'tenants'::text),
    ('uq_roles_tenant_code'::text, 'roles'::text)
) AS expected(expected_name, expected_table)
LEFT JOIN pg_catalog.pg_namespace AS pg_namespace
  ON pg_namespace.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS pg_class
  ON pg_class.relnamespace = pg_namespace.oid
 AND pg_class.relname = expected.expected_table
LEFT JOIN pg_catalog.pg_constraint AS pg_constraint
  ON pg_constraint.conrelid = pg_class.oid
 AND pg_constraint.conname = expected.expected_name
UNION ALL
SELECT NULL::text, NULL::text, NULL::text, NULL::text,
       NULL::text, NULL::boolean, NULL::text, NULL::text
WHERE false;
```

Owner không nhận `GRANT` mới trên catalog cho view này. View dùng quyền `SELECT` catalog mà `PUBLIC` có sẵn, cộng `EXECUTE` sẵn có của `pg_get_constraintdef`. Hai quyền sẵn có đó trên production là chưa xác minh. Không cấp quyền trên `pg_authid`. Không đổi quyền catalog toàn cụm.

### `preflight_ro.kit_mkt_tenants`

Bảng `public.tenants`. Không `JOIN`.

| Cột | Việc dùng |
|---|---|
| `id` | Xuất |
| `tenant_code` | Xuất và `WHERE tenant_code = 'KIT_MKT'` |
| `status` | Xuất |
| `deleted_at` | Xuất. Không lọc xóa; tenant đã xóa vẫn hiện |

```sql
CREATE VIEW preflight_ro.kit_mkt_tenants
WITH (security_barrier = true, security_invoker = false) AS
SELECT tenants.id,
       tenants.tenant_code,
       tenants.status,
       tenants.deleted_at
FROM public.tenants AS tenants
WHERE tenants.tenant_code = 'KIT_MKT'
UNION ALL
SELECT NULL::uuid, NULL::varchar, NULL::smallint, NULL::timestamptz
WHERE false;
```

### `preflight_ro.kit_mkt_admin_roles`

| Cột | Việc dùng |
|---|---|
| `roles.id` | Xuất `role_id` |
| `roles.tenant_id` | Xuất; `JOIN tenants.id = roles.tenant_id` |
| `roles.role_code` | Chỉ `WHERE role_code = 'ADMIN'` |
| `roles.status` | Xuất `role_status` |
| `tenants.id` | Chỉ `JOIN` |
| `tenants.tenant_code` | Chỉ `WHERE tenant_code = 'KIT_MKT'` |
| `tenants.status` | Xuất `tenant_status` |
| `tenants.deleted_at` | Xuất `tenant_deleted_at`. Không lọc xóa |

Mọi `roles.status` đều qua. `roles` trong source không có `deleted_at`, nên view không tham chiếu cột đó.

```sql
CREATE VIEW preflight_ro.kit_mkt_admin_roles
WITH (security_barrier = true, security_invoker = false) AS
SELECT roles.id AS role_id,
       roles.tenant_id,
       roles.status AS role_status,
       tenants.status AS tenant_status,
       tenants.deleted_at AS tenant_deleted_at
FROM public.roles AS roles
JOIN public.tenants AS tenants
  ON tenants.id = roles.tenant_id
WHERE tenants.tenant_code = 'KIT_MKT'
  AND roles.role_code = 'ADMIN'
UNION ALL
SELECT NULL::uuid, NULL::uuid, NULL::smallint, NULL::smallint, NULL::timestamptz
WHERE false;
```

### `preflight_ro.canon_permission`

Bảng `public.permissions`. Không `JOIN`.

| Cột | Việc dùng |
|---|---|
| `id` | Xuất |
| `permission_code` | Xuất và `WHERE permission_code = 'content.canon.administer'` |
| `permission_name` | Xuất |
| `module_name` | Xuất |

```sql
CREATE VIEW preflight_ro.canon_permission
WITH (security_barrier = true, security_invoker = false) AS
SELECT permissions.id,
       permissions.permission_code,
       permissions.permission_name,
       permissions.module_name
FROM public.permissions AS permissions
WHERE permissions.permission_code = 'content.canon.administer'
UNION ALL
SELECT NULL::uuid, NULL::varchar, NULL::varchar, NULL::varchar
WHERE false;
```

### `preflight_ro.canon_grants`

Mọi grant của đúng permission `content.canon.administer`, mọi tenant. Không lọc `deleted_at`.

| Cột | Việc dùng |
|---|---|
| `role_permissions.id` | Xuất `grant_id` |
| `role_permissions.role_id` | Chỉ `JOIN roles.id = role_permissions.role_id` |
| `role_permissions.permission_id` | Chỉ `JOIN permissions.id = role_permissions.permission_id`. Không xuất |
| `permissions.id` | Chỉ `JOIN` |
| `permissions.permission_code` | Chỉ `WHERE permission_code = 'content.canon.administer'` |
| `roles.id` | Xuất `role_id`; cũng là vế `JOIN` với `role_permissions.role_id` |
| `roles.tenant_id` | Xuất `tenant_id`; `JOIN tenants.id = roles.tenant_id` |
| `roles.role_code` | Xuất |
| `roles.status` | Xuất `role_status` |
| `tenants.id` | Chỉ `JOIN` |
| `tenants.tenant_code` | Xuất |
| `tenants.status` | Xuất `tenant_status` |
| `tenants.deleted_at` | Xuất `tenant_deleted_at` |

`role_permissions` không có `tenant_id`. Tenant lấy từ `roles.tenant_id`.

```sql
CREATE VIEW preflight_ro.canon_grants
WITH (security_barrier = true, security_invoker = false) AS
SELECT role_permissions.id AS grant_id,
       roles.tenant_id,
       tenants.tenant_code,
       tenants.status AS tenant_status,
       tenants.deleted_at AS tenant_deleted_at,
       roles.id AS role_id,
       roles.role_code,
       roles.status AS role_status
FROM public.role_permissions AS role_permissions
JOIN public.permissions AS permissions
  ON permissions.id = role_permissions.permission_id
JOIN public.roles AS roles
  ON roles.id = role_permissions.role_id
JOIN public.tenants AS tenants
  ON tenants.id = roles.tenant_id
WHERE permissions.permission_code = 'content.canon.administer'
UNION ALL
SELECT NULL::uuid, NULL::uuid, NULL::varchar, NULL::smallint, NULL::timestamptz,
       NULL::uuid, NULL::varchar, NULL::smallint
WHERE false;
```

### `preflight_ro.kit_mkt_admin_users`

Đường nối đã chốt, không đọc `users.tenant_id`:

`user_roles.user_id = users.id`, rồi `roles.id = user_roles.role_id`, rồi `tenants.id = roles.tenant_id`.

| Cột | Việc dùng |
|---|---|
| `users.id` | Xuất `user_id`; `JOIN user_roles.user_id = users.id` |
| `users.username` | Xuất |
| `users.status` | Xuất `user_status` |
| `users.deleted_at` | Xuất `user_deleted_at`. User đã xóa vẫn hiện |
| `user_roles.user_id` | Chỉ `JOIN` |
| `user_roles.role_id` | Chỉ `JOIN roles.id = user_roles.role_id` |
| `roles.id` | Xuất `role_id` |
| `roles.tenant_id` | Chỉ `JOIN tenants.id = roles.tenant_id` |
| `roles.role_code` | Chỉ `WHERE role_code = 'ADMIN'` |
| `roles.status` | Xuất `role_status` |
| `tenants.id` | Chỉ `JOIN` |
| `tenants.tenant_code` | Chỉ `WHERE tenant_code = 'KIT_MKT'` |
| `tenants.deleted_at` | Chỉ `WHERE deleted_at IS NULL` |

```sql
CREATE VIEW preflight_ro.kit_mkt_admin_users
WITH (security_barrier = true, security_invoker = false) AS
SELECT users.id AS user_id,
       users.username,
       users.status AS user_status,
       users.deleted_at AS user_deleted_at,
       roles.id AS role_id,
       roles.status AS role_status
FROM public.users AS users
JOIN public.user_roles AS user_roles
  ON user_roles.user_id = users.id
JOIN public.roles AS roles
  ON roles.id = user_roles.role_id
JOIN public.tenants AS tenants
  ON tenants.id = roles.tenant_id
WHERE tenants.tenant_code = 'KIT_MKT'
  AND tenants.deleted_at IS NULL
  AND roles.role_code = 'ADMIN'
UNION ALL
SELECT NULL::uuid, NULL::varchar, NULL::smallint, NULL::timestamptz,
       NULL::uuid, NULL::smallint
WHERE false;
```

User hiện ra là user có một dòng `user_roles` tới role `ADMIN` của tenant `KIT_MKT` còn sống. View không thêm điều kiện `users.tenant_id = tenants.id`.

### `preflight_ro.ledger_386`

Quản trị viên chạy `to_regclass('public.kit_schema_migrations')` lúc cấp quyền và chọn đúng một định nghĩa.

Có quan hệ: owner được `SELECT (filename)`. Cột `filename` chỉ đứng trong `WHERE` của `EXISTS`. Cột `applied_at` không được cấp và không được tham chiếu. `filename_recorded` là boolean.

```sql
CREATE VIEW preflight_ro.ledger_386
WITH (security_barrier = true, security_invoker = false) AS
SELECT TRUE AS ledger_relation_exists,
       EXISTS (
           SELECT 1
           FROM public.kit_schema_migrations AS kit_schema_migrations
           WHERE kit_schema_migrations.filename = '386_pack_content_famixa_canon_administer.sql'
       ) AS filename_recorded
UNION ALL
SELECT NULL::boolean, NULL::boolean
WHERE false;
```

Không có quan hệ: không `GRANT` trên ledger, và view không nhắc tới bảng đó.

```sql
CREATE VIEW preflight_ro.ledger_386
WITH (security_barrier = true, security_invoker = false) AS
SELECT FALSE AS ledger_relation_exists,
       NULL::boolean AS filename_recorded
UNION ALL
SELECT NULL::boolean, NULL::boolean
WHERE false;
```

`filename_recorded` trống hoặc false chỉ có nghĩa là ledger không ghi đúng tên file đó, hoặc ledger không có. Content runner trong repo chỉ chạy từng file SQL và không ghi `public.kit_schema_migrations`. File `386_pack_content_famixa_canon_administer.sql` cũng không tự ghi ledger. Kết quả view vì thế không được suy ra thành “migration 386 chưa chạy” hay “đã chạy”.

`filename_recorded = true` chỉ có nghĩa là ledger có đúng tên file. Xác nhận permission và grant bằng `canon_permission` và `canon_grants`. Ledger không thay hai view đó. Sự tồn tại của bảng ledger và nội dung của nó trên production chưa được xác minh.

## 3. Ma trận `SELECT` cột của `famixa_preflight_owner`

Chỉ `GRANT SELECT (cột, …)` không kèm `GRANT OPTION`. Không `GRANT SELECT` cả bảng. `has_table_privilege(..., 'SELECT')` của owner trên các bảng này vẫn false; quyền thật nằm ở `has_column_privilege`.

| Bảng | Cột được cấp | Xuất | Chỉ JOIN, WHERE hoặc EXISTS |
|---|---|---|---|
| `public.tenants` | `id` | `kit_mkt_tenants.id` | `JOIN` ở `kit_mkt_admin_roles`, `canon_grants`, `kit_mkt_admin_users` |
| `public.tenants` | `tenant_code` | `kit_mkt_tenants`, `canon_grants` | `WHERE = 'KIT_MKT'` ở `kit_mkt_tenants`, `kit_mkt_admin_roles`, `kit_mkt_admin_users` |
| `public.tenants` | `status` | `kit_mkt_tenants.status`, `tenant_status` của `kit_mkt_admin_roles` và `canon_grants` | Không |
| `public.tenants` | `deleted_at` | `kit_mkt_tenants`, `tenant_deleted_at` của `kit_mkt_admin_roles` và `canon_grants` | `WHERE IS NULL` chỉ ở `kit_mkt_admin_users` |
| `public.roles` | `id` | `role_id` của `kit_mkt_admin_roles`, `canon_grants`, `kit_mkt_admin_users` | `JOIN` với `role_permissions.role_id` và `user_roles.role_id` |
| `public.roles` | `tenant_id` | `kit_mkt_admin_roles`, `canon_grants` | `JOIN tenants.id` ở ba view có role, gồm `kit_mkt_admin_users` |
| `public.roles` | `role_code` | `canon_grants` | `WHERE = 'ADMIN'` ở `kit_mkt_admin_roles` và `kit_mkt_admin_users` |
| `public.roles` | `status` | `role_status` của ba view có role | Không |
| `public.permissions` | `id` | `canon_permission.id` | `JOIN` với `role_permissions.permission_id` |
| `public.permissions` | `permission_code` | `canon_permission` | `WHERE` của `canon_permission` và của `canon_grants` |
| `public.permissions` | `permission_name` | `canon_permission` | Không |
| `public.permissions` | `module_name` | `canon_permission` | Không |
| `public.role_permissions` | `id` | `canon_grants.grant_id` | Không |
| `public.role_permissions` | `role_id` | Không | `JOIN roles.id` |
| `public.role_permissions` | `permission_id` | Không | `JOIN permissions.id` trong `canon_grants` |
| `public.user_roles` | `user_id` | Không | `JOIN users.id` |
| `public.user_roles` | `role_id` | Không | `JOIN roles.id` |
| `public.users` | `id` | `kit_mkt_admin_users.user_id` | `JOIN user_roles.user_id` |
| `public.users` | `username` | `kit_mkt_admin_users` | Không |
| `public.users` | `status` | `user_status` | Không |
| `public.users` | `deleted_at` | `user_deleted_at` | Không |
| `public.kit_schema_migrations` | `filename` | Không | `WHERE` trong `EXISTS`, và chỉ khi `to_regclass` có quan hệ lúc cấp quyền |

Cột có trong source nhưng view không dùng, nên không cấp: `tenants.tenant_name`, `country_code`, `default_currency`, `timezone`, `settings`, `created_at`, `updated_at`; `roles.role_name`, `description`, `created_at`, `updated_at`; `permissions.description`, `created_at`; `role_permissions.created_at`; `user_roles.id`, `created_at`; `users.tenant_id`, `must_change_password`, `last_login_at`, `created_at`, `updated_at`; `kit_schema_migrations.applied_at`.

`tenants.organization_id` và `tenants.business_vertical` (`migrations/051_platform_multi_branch_i18n.sql`) cùng `tenants.rx_directory_discoverable` (`migrations/100_rx_prescriber_network.sql`) cũng không được cấp. Ba cột này không có dòng trong ma trận `GRANT` ở trên và không được thêm vào ma trận đó.

### Cột và bảng cấm

Source có các cột này. Cả owner và role truy vấn đều không được `SELECT` chúng. View không được tham chiếu chúng.

| Đối tượng | Căn cứ source |
|---|---|
| `public.users.password_hash` | `migrations/002_identity.sql` |
| `public.users.email` | `migrations/002_identity.sql` |
| `public.users.employee_id` | `migrations/002_identity.sql` |
| `public.users.kit_account_id` | `migrations/228_kit_accounts_memberships.sql` thêm cột này trong khối có thể bị bỏ qua nếu thiếu quyền owner. Nếu cột không có trên một database, sự vắng mặt thỏa lệnh cấm. Nếu cột có, `SELECT` của cả hai role phải false |
| `public.kit_accounts`, mọi cột, gồm `email`, `phone`, `password_hash` | `migrations/228_kit_accounts_memberships.sql`. Cấm cả bảng, không chỉ từng cột |
| `public.refresh_tokens.token_hash` | `migrations/002_identity.sql`. Không cấp quyền nào trên `public.refresh_tokens` |
| `public.staff_otp_challenges.code_hash` và `phone` | `migrations/183_people_auth_selfie_customer_feedback.sql`. Không cấp quyền nào trên bảng này |
| `public.employees.email` và `phone` | `migrations/002_identity.sql`. Không cấp quyền nào trên `public.employees`. `users.employee_id` bị cấm nên view không nối sang `employees` |

Role truy vấn không có `SELECT` bảng hay `SELECT` cột trên mọi bảng ở mục này, kể cả các cột owner được phép đọc.

## 4. Ma trận schema và view

| Role | Schema `preflight_ro` | Tám view | Bảng nền |
|---|---|---|---|
| `famixa_preflight_owner` | Owner, `CREATE`, `USAGE` | Owner | Đúng các cột ở mục 3. Không có quyền trên bảng cấm |
| `famixa_canon_preflight_ro` | `USAGE`; không có `CREATE` | Chỉ `SELECT`; không có `GRANT OPTION`. Không có `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES`, `TRIGGER`. `MAINTAIN` không được cấp; câu kiểm chỉ gọi quyền này khi `server_version_num >= 170000` | Không có quyền bảng hay cột trên `public` |

RLS, nếu đang bật trên bảng nền, áp lên owner vì owner không phải owner bảng nghiệp vụ và không có `BYPASSRLS`. View trả không dòng khi RLS chặn là trạng thái đóng. Không cấp `BYPASSRLS` để lấy dữ liệu. Source đã đọc không bật RLS trên các bảng identity này. Trạng thái RLS production chưa được xác minh. Owner bảng thì đi vòng RLS trừ khi có `FORCE ROW LEVEL SECURITY`; checklist vì thế xác nhận owner preflight không sở hữu các bảng đó.

`CONNECT` của `PUBLIC` trên database có thể vẫn còn. Quyền đó chưa cho đọc bảng. Mật khẩu và `pg_hba` cho `127.0.0.1/32` là cửa kết nối. Không thu hồi `CONNECT` của `PUBLIC` trong đợt này vì có thể làm đứt ứng dụng. Tên quan hệ trong `pg_class` vẫn có thể thấy qua catalog mặc định. Đó là tên đối tượng, không phải dòng nghiệp vụ.

`canon_grants` cố ý hiện mọi tenant đang giữ permission này. `kit_mkt_admin_users` hiện `username` của user gắn role `ADMIN` của tenant `KIT_MKT` còn sống.

## 5. Checklist sau cấp quyền

Chạy bằng chính `famixa_canon_preflight_ro`, chỉ `SELECT`. Checklist đạt không chứng minh một phiên sau đó còn giữ `statement_timeout` hay `default_transaction_read_only`, vì role đổi được hai tham số đó.

Mẫu SQL, kết quả mong đợi và điều kiện dừng cho các kiểm tra mà mục này mới nêu bằng lời nằm ở mục 8. Mục 8 không đổi tám view đã được R82 chấp thuận.

Boolean quyền `NULL`, câu lỗi, hoặc `permission denied` là `BLOCKED`. Không ghi `NULL` thành false. Một nhóm quyền đã false không thay cho quyền chưa quan sát. Trạng thái đóng, `DATA_ABSENT`, ledger hoặc manifest không chứng minh tenant, permission, grant hay user ADMIN tồn tại.

1. Trên `pg_roles`, cả `famixa_preflight_owner` và `famixa_canon_preflight_ro` có `rolsuper`, `rolbypassrls`, `rolcreatedb`, `rolcreaterole`, `rolreplication`, `rolinherit` đều false. `rolcanlogin` false với owner và true với role truy vấn. Câu cửa của Q1 phải đọc đủ bảy cờ này trên đúng hai dòng. Cờ true trái đặc tả, gồm `rolcanlogin` true ở owner hoặc false ở role truy vấn, là `STOP`. `NULL`, lỗi, thiếu dòng, hoặc không quan sát được một cờ là `BLOCKED`.
2. `pg_roles.rolconfig` của role truy vấn có `default_transaction_read_only=on`, `search_path=preflight_ro`, và `statement_timeout` hữu hạn khác 0. Trong phiên kiểm tra, `current_setting` của ba mục đó khớp `rolconfig`. Trong transaction đầu tiên, `current_setting('transaction_read_only')` là `on`. Hai GUC đọc/ghi thời gian không phải ranh giới chống ghi không thể vượt qua. Câu `rolconfig` và câu GUC phiên nằm ở Q1. Không đọc được `rolconfig` hoặc một GUC: `BLOCKED`. Ở role truy vấn, `rolconfig` null, thiếu một trong ba mục, `statement_timeout` là 0 hoặc không hữu hạn, hoặc GUC phiên không khớp mục tương ứng trong `rolconfig`: `STOP`.
3. `has_schema_privilege` của role truy vấn trên `preflight_ro`: `USAGE` true, `CREATE` false.
4. Với owner, `has_column_privilege` `SELECT` true đúng trên từng cột được cấp ở mục 3, và false trên từng cột cấm cũng như từng cột source-có nhưng không cấp. `has_column_privilege` `REFERENCES` false trên mọi cột đã quan sát của các bảng đó. `has_table_privilege` của owner cho bảy quyền `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES`, `TRIGGER` đều false trên các bảng nghiệp vụ đó. Khi Q8 đã có `server_version_num >= 170000`, `MAINTAIN` cũng false. PostgreSQL 15 và 16 không gọi `MAINTAIN`. Việc không gọi không phải một kết quả false. Phiên bản chưa là số nguyên, hoặc Q8 còn `BLOCKED`: phần `MAINTAIN` là `BLOCKED`, và bảy quyền false không làm điều này thành `PASS_SHAPE`. Boolean quyền `NULL` hoặc câu lỗi: `BLOCKED`. Không có quan hệ ledger thì không có `GRANT` filename. Có quan hệ thì owner chỉ true trên `SELECT` cột `filename`; các quyền bảng vẫn false.
5. Với role truy vấn, bảy quyền bảng `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES`, `TRIGGER` đều false trên `public.users`, `public.tenants`, `public.roles`, `public.permissions`, `public.role_permissions`, `public.user_roles`, và trên ledger nếu quan hệ đã quan sát. Nhánh `MAINTAIN` giống điều 4. `has_column_privilege` của role truy vấn false cho `SELECT` và cho `REFERENCES` trên mọi cột đã quan sát của các bảng đó, kể cả cột owner được phép đọc. Cả hai role có bảy quyền bảng false, và `SELECT` cột cùng `REFERENCES` cột false, trên `public.kit_accounts`, `public.refresh_tokens`, `public.staff_otp_challenges` và `public.employees` khi các quan hệ đó đã quan sát. Quan hệ không quan sát được không biến các quyền thành false. Nếu `users.kit_account_id` đã là `COLUMN_ABSENT` trên bảng `users` đang quan sát được, không gọi `has_column_privilege` cho cột đó. Nếu cột còn, `SELECT` và `REFERENCES` của cả hai role trên cột đó false. Boolean `NULL` hoặc lỗi: `BLOCKED`.
6. `GRANT` cột và `GRANT SELECT` view đều không có grant option.
7. Với từng view, role truy vấn chỉ có `SELECT` và không có `GRANT OPTION`. `INSERT`, `UPDATE`, `DELETE` và `TRUNCATE` false chưa đủ để đạt. `REFERENCES` và `TRIGGER` phải được quan sát và phải false. `MAINTAIN` chỉ được gọi khi `server_version_num >= 170000`, và khi đó phải false. Dưới `170000` với phiên bản đã là số nguyên: không gọi `MAINTAIN`. Phiên bản chưa là số nguyên hoặc Q8 còn `BLOCKED`: phần `MAINTAIN` là `BLOCKED` và điều 7 chưa thành `PASS_SHAPE`. Quyền hiện hữu trái đặc tả là `STOP`. Boolean `NULL` hoặc lỗi là `BLOCKED`. Hình `PASS_SHAPE` là Q6.
8. `pg_class.relowner` của tám view là `famixa_preflight_owner`. `reloptions` có `security_barrier=true` và `security_invoker=false`. Câu quan sát nằm cuối Q6. View không thấy: `NOT_OBSERVABLE`, kết luận điều này `BLOCKED`. `owner_name` null khi view đã thấy: `BLOCKED`. Owner khác: `STOP`. `reloptions` null hoặc thiếu một reloption bắt buộc khi view đã thấy: `STOP`. Câu lỗi: `BLOCKED`.
9. Đi `pg_auth_members` theo cả đường trực tiếp và gián tiếp. Không có đường giữa hai role preflight. Không có đường từ một trong hai role tới `kitplatform`, `postgres`, `pg_read_all_data`, `pg_write_all_data`, `pg_monitor` hay `pg_signal_backend`.
10. `pg_get_viewdef` của từng view không chứa `password_hash`, `email`, `employee_id`, `kit_account_id`, `token_hash`, `code_hash`, `phone`, `kit_accounts`, `refresh_tokens`, `staff_otp_challenges`, `employees`, `applied_at`, hay `users.tenant_id`. `db_identity` không có nhánh `UNION ALL … WHERE false`. Bảy view kia có `UNION ALL` và một nhánh `WHERE false`. Định nghĩa ledger hoặc chứa đúng tên file `386_pack_content_famixa_canon_administer.sql`, hoặc không nhắc `kit_schema_migrations`. Câu `pg_get_viewdef` nằm cuối Q6. Không có định nghĩa quan sát được: `BLOCKED`. Sự thiếu văn bản không chứng minh định nghĩa sạch. Chuỗi cấm có mặt, hoặc hình `UNION ALL` trái điều kiện trên: `STOP`. Câu lỗi, hoặc role truy vấn không có `EXECUTE` trên `pg_get_viewdef(oid)`: `BLOCKED`.
11. `SELECT` tám view thành công và trả đúng các cột đã khai. Không phát lệnh `INSERT`, kể cả trong transaction có `ROLLBACK`.
12. `pg_class.relowner` của `public.users`, `public.tenants`, `public.roles`, `public.permissions`, `public.role_permissions`, `public.user_roles`, `public.kit_accounts`, `public.refresh_tokens`, `public.staff_otp_challenges` và `public.employees` không phải `famixa_preflight_owner`. Nếu `public.kit_schema_migrations` tồn tại, owner preflight cũng không sở hữu quan hệ đó.
13. Role truy vấn có `rolconnlimit = 2`. `rolvaliduntil` có giá trị và còn hiệu lực tại thời điểm kiểm tra. Câu kiểm nằm ở Q1. Không đọc được một trong hai mục, hoặc thiếu dòng role: `BLOCKED`. `rolconnlimit` đã đọc và khác 2: `STOP`. `rolvaliduntil` đã đọc mà null, hoặc mốc thời gian không còn hiệu lực tại thời điểm kiểm tra: `STOP`. Không ghi giá trị thời hạn vào tài liệu này.
14. Ghi `server_version_num`. Máy chủ phải hỗ trợ cả `security_barrier` và `security_invoker`. `security_invoker` có từ PostgreSQL 15. Không chấp nhận được hai reloption này thì checklist không đạt.
15. Với từng cột còn tồn tại trong `public.tenants.organization_id`, `public.tenants.business_vertical` và `public.tenants.rx_directory_discoverable`, `has_column_privilege` `SELECT` và `REFERENCES` của cả `famixa_preflight_owner` và `famixa_canon_preflight_ro` phải là false. Sự vắng mặt chỉ là `COLUMN_ABSENT` sau khi bảng `tenants` đã quan sát được và catalog cột của bảng đó đã đọc được. Bảng hoặc catalog cột không quan sát được: `NOT_OBSERVABLE` hoặc `BLOCKED`, không phải cột vắng mặt, và không phải lý do cấp `SELECT`. Cột đã là `COLUMN_ABSENT` thì không có quyền cột để đọc và không gọi `has_column_privilege`. Kết quả true trên một trong hai role là `STOP`. Boolean `NULL` hoặc lỗi là `BLOCKED`. Dừng, và không thêm ba cột này vào ma trận `GRANT` ở mục 3.

## 6. Chưa xác minh được khi chưa có kết nối

Phiên bản PostgreSQL đang chạy, việc phiên bản đó hỗ trợ hai reloption, rule `pg_hba`, owner thực của bảng `public`, thuộc tính role, membership thực, RLS trên các bảng identity, quyền `PUBLIC` trên schema `public` và trên catalog, `EXECUTE` của `pg_get_constraintdef`, sự hiện diện của `users.kit_account_id`, bốn constraint, số dòng `KIT_MKT`, permission và grant, user ADMIN, và việc `public.kit_schema_migrations` có tồn tại hay không. Checklist ở mục 5 chưa được chạy.

Chưa chạy trên production: đọc `constraints_expected` theo đủ điều kiện đạt (`contype = 'u'`, `convalidated = true`, đúng bảng, đúng tên, đúng thứ tự cột). `actual_table` trống chưa được quan sát và không phải tiêu chí duy nhất cho constraint thiếu. Chưa chạy `has_column_privilege` của cả hai role trên `tenants.organization_id`, `tenants.business_vertical` và `tenants.rx_directory_discoverable`. Sự hiện diện của ba cột đó trên production cũng chưa được xác minh.

Mật khẩu, `VALID UNTIL` và giá trị cụ thể của `statement_timeout` không nằm trong tài liệu. Lối kết nối của quản trị viên cũng không nằm trong tài liệu này. Mẫu SQL ở mục 8 chưa được chạy trên production.

## 7. Quyết định Architect đã ghi tại R82

Các điểm dưới đây đã được Architect chấp thuận ở cấp đặc tả. Bản đầy đủ là `R82-ARCHITECT-DECISION-REGISTER.md`. Trạng thái chung: `ACCEPTED_AT_SPECIFICATION_LEVEL`. `PRODUCTION_EXECUTION_APPROVED = NO`.

| ID | Quyết định đã chốt ở cấp đặc tả |
|---|---|
| R82-ADR-01 | Đường nối `kit_mkt_admin_users` qua `user_roles` và `roles.tenant_id`. Không đọc `users.tenant_id`. |
| R82-ADR-02 | `role_permissions.role_id` và `role_permissions.permission_id` được `SELECT` để JOIN dù không xuất ra view. |
| R82-ADR-03 | Hai dạng `ledger_386`, chọn bằng `to_regclass` lúc thiết lập. Filename không chứng minh migration đã chạy hay chưa chạy. |
| R82-ADR-04 | `statement_timeout` và `default_transaction_read_only` là mặc định phiên, không phải ranh giới chống ghi. |
| R82-ADR-05 | Mật khẩu, `VALID UNTIL` và giá trị `statement_timeout` hữu hạn khác 0 đi qua kênh ngoài tài liệu. Register không chứa giá trị cụ thể. |
| R82-ADR-06 | Quyền catalog hiện có và `EXECUTE` của `pg_get_constraintdef` là phụ thuộc chưa xác minh. Thiếu quyền thì dừng, không nới `GRANT` catalog. |
| R82-ADR-07 | `tenants.organization_id`, `tenants.business_vertical` và `tenants.rx_directory_discoverable` nằm ngoài ma trận `GRANT`. |
| R82-ADR-08 | Constraint chỉ đạt khi đồng thời đủ `contype = 'u'`, `convalidated = true`, đúng bảng, đúng tên và đúng thứ tự cột. |
| R82-ADR-09 | Chấp thuận đặc tả không xác nhận production sẵn sàng. |

ADR-05 chỉ chấp thuận nguyên tắc quản lý secret. Giá trị mật khẩu, thời hạn và timeout chưa được chọn. Việc chọn các giá trị đó sau này không tự cấp quyền tạo role hoặc mở kết nối production.

Chấp thuận R82 không cho phép kết nối production, chạy SQL trên production, tạo role hoặc schema, tạo view, cấp `GRANT`, triển khai migration 386, hay đọc secret. Checklist mục 5 chưa chạy. Mọi hoạt động trên production cần phê duyệt riêng sau khi các điều kiện ở mục 6 được xác minh.

## 8. Mẫu truy vấn kiểm tra

Mục này bổ sung SQL cho các kiểm tra mà mục 5 nêu bằng lời. Tám câu `CREATE VIEW` ở mục 2 giữ nguyên. R82 giữ nguyên. `PRODUCTION_EXECUTION_APPROVED = NO`.

Các câu dưới đây là đặc tả. Chúng chưa được thực thi trên production, trên một bản sao, hay trên bất kỳ phiên nào trong lần soạn mục này. Một kết quả chỉ phát sinh khi một phiên được phê duyệt riêng chạy đúng câu đó. Lỗi `permission denied` có nghĩa là `BLOCKED`: ghi lại lỗi, và dừng câu đó. Không thêm `GRANT` catalog, `BYPASSRLS`, `SELECT` cả bảng, membership, superuser mới, hay quyền đọc `pg_hba` để đổi `BLOCKED` thành đạt.

Bốn cách đọc kết quả, dùng chung cho mọi câu:

| Nhãn | Ý nghĩa |
|---|---|
| `PASS_SHAPE` | Hình dạng quan sát được khớp điều kiện của câu đó. Đây là điều kiện đặc tả, chưa phải kết quả production. |
| `DATA_ABSENT` | Phiên đọc được đối tượng, và dòng hoặc cột không có trong phần đã đọc. |
| `BLOCKED` | Phiên không quan sát được, hoặc còn thiếu một sự kiện do quản trị viên giữ. Ghi lý do. |
| `STOP` | Đã quan sát một hình dạng trái với đặc tả đã chấp thuận. |

`NULL` trả về từ `has_table_privilege`, `has_column_privilege`, `has_schema_privilege`, `has_function_privilege`, `has_any_column_privilege` hoặc `pg_has_role` không phải true và không phải false. Với dạng truyền OID, `NULL` nghĩa là role, quan hệ hoặc cột không còn để kiểm. Dạng truyền tên có thể báo lỗi khi đối tượng không tồn tại; lỗi đó là `BLOCKED`, không ghi thành false. Thiếu role là lỗi hoặc không có dòng role, không phải quyền false.

Superuser làm các hàm quyền trả true trên mọi đối tượng. Câu nào dùng các hàm đó đều cần hai role có `rolsuper = false` trước khi đọc boolean. `rolsuper = true` là `STOP`; boolean quyền của role đó không phải bằng chứng grant.

Mỗi câu SQL dưới đây là một lệnh `SELECT`. Không có `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, DDL, `GRANT`, `REVOKE`, `SET ROLE` hay `SET SESSION AUTHORIZATION`. Tính chỉ đọc là tính của câu lệnh, không phải của GUC `default_transaction_read_only` (R82-ADR-04).

Phiên `ADMIN_SESSION` là phiên superuser sẵn có của quản trị viên, đúng vai trò `postgres` đã được mục 1 dành cho bước thiết lập. Kết quả phiên đó mang nhãn `ADMIN_SESSION`. Nó không thay kết quả checklist của `famixa_canon_preflight_ro`. Phiên chỉ đọc không nhận superuser và không được `SET ROLE` sang `postgres`.

Thứ tự khi một phiên được phê duyệt sau này: Q10, Q8, đầu dò quyền Q9, Q2, Q4, rồi sau khi có role thì Q3 và Q1, gồm bảy cờ role, `rolconfig`, `rolconnlimit` và `rolvaliduntil`. GUC phiên đọc trong phiên checklist. Sau khi cấp quyền cột thì Q5, Q6 và Q2 lần nữa. Sau khi có view thì `relowner`, `reloptions` và `pg_get_viewdef` của tám view, rồi Q7. Nội dung `pg_hba` chỉ thuộc `ADMIN_SESSION`.

### Q1. Membership và đường `SET ROLE`

- Role dự kiến: `famixa_canon_preflight_ro`, sau khi cả hai role tồn tại.
- Đối tượng đọc: `pg_catalog.pg_roles`, `pg_catalog.pg_auth_members`.
- Quyền tối thiểu: `SELECT` trên hai quan hệ đó và `EXECUTE` trên `pg_has_role(oid, oid, text)`. Đây là quyền catalog hiện có theo R82-ADR-06. Thiếu thì `BLOCKED`. Không `GRANT SELECT` trên `pg_auth_members`, không kết nạp `pg_monitor` hay `pg_read_all_settings`.
- Chỉ đọc: có.
- Thời điểm: sau khi tạo role, trước hoặc sau khi tạo view. Trước khi có cả hai role, chưa có membership để đo.

`pg_auth_members` là catalog của cả cụm. `NOINHERIT` không chặn `SET ROLE`. Từ PostgreSQL 16 (`server_version_num >= 160000`), một đường cho phép `SET ROLE` khi mọi cạnh trên đường đó có `set_option = true`. Trước PostgreSQL 16 không có cột `set_option`; mọi cạnh membership đều có thể dẫn tới `SET ROLE`. Chọn một trong hai câu đi đường theo Q8. Chạy câu có `set_option` trên máy chủ 15 làm câu lỗi; lỗi đó không phải “không có membership” và không được ghi thành false. Phiên bản chưa là số nguyên, hoặc Q8 còn `BLOCKED`: không chọn câu đi đường, kết quả Q1 là `BLOCKED`. Trên PostgreSQL 16 trở lên, bỏ câu có `set_option` thì đường `SET ROLE` chưa được kiểm; kết quả đường đó là `BLOCKED`.

Câu cửa, cả hai phiên bản. Bảy cờ role, `rolconnlimit`, `rolvaliduntil` và `rolconfig` đọc cùng một câu. Câu không gọi cột `pg_auth_members` của phiên bản cao hơn.

```sql
SELECT rolname,
       rolsuper,
       rolbypassrls,
       rolinherit,
       rolcreatedb,
       rolcreaterole,
       rolreplication,
       rolcanlogin,
       rolconnlimit,
       rolvaliduntil,
       rolconfig
FROM pg_catalog.pg_roles
WHERE rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
ORDER BY rolname;
```

`PASS_SHAPE` của bảy cờ chỉ đứng khi có đúng hai dòng, đủ hai tên `famixa_preflight_owner` và `famixa_canon_preflight_ro`, và đủ bảy cờ đã được đọc. Trên cả hai dòng, `rolsuper`, `rolbypassrls`, `rolinherit`, `rolcreatedb`, `rolcreaterole` và `rolreplication` đều false. `rolcanlogin` false trên dòng owner và true trên dòng role truy vấn. Thiếu một dòng, thiếu một tên, hoặc số dòng khác hai: `BLOCKED`, role chưa quan sát đủ. Câu lỗi: `BLOCKED`. Một cờ `NULL`: `BLOCKED`. Không ghi `NULL` thành false và không suy cờ còn lại từ ba cờ đã đọc.

Cờ trái đặc tả là `STOP`: một trong sáu cờ phải false lại true trên bất kỳ dòng nào; `rolcanlogin` true trên owner; `rolcanlogin` false trên role truy vấn.

`rolconnlimit` và `rolvaliduntil` là mục 5 điều 13, chỉ ràng buộc role truy vấn. Không đọc được hai mục đó, hoặc thiếu dòng role truy vấn: `BLOCKED`. `rolconnlimit` của role truy vấn đã đọc và khác 2: `STOP`. `rolvaliduntil` của role truy vấn đã đọc mà null, hoặc mốc thời gian không còn hiệu lực tại thời điểm kiểm tra: `STOP`. `NULL` của `rolvaliduntil` là giá trị catalog đã thấy, nghĩa là không có thời hạn; đó là hình trái R82-ADR-05, nên là `STOP`, khác với cờ boolean `NULL` chưa đọc được. Không ghi mốc thời hạn vào tài liệu này. Hai mục này của owner được ghi lại và không bị thêm ngưỡng.

`rolconfig` của role truy vấn phải chứa đúng các mục `default_transaction_read_only=on`, `search_path=preflight_ro`, và một mục `statement_timeout` có giá trị hữu hạn khác 0. Giá trị timeout cụ thể không được viết vào tài liệu này. `rolconfig` null, thiếu một trong ba mục, hoặc `statement_timeout` là 0 hay không hữu hạn: `STOP` khi dòng role đã đọc được. Câu lỗi hoặc không có dòng role: `BLOCKED`. `rolconfig` của owner được ghi lại; đặc tả không bắt owner chứa ba mục đó.

Câu GUC phiên chạy bằng `famixa_canon_preflight_ro` trong phiên checklist. `transaction_read_only` đọc trong transaction đầu tiên của phiên đó. Không kết nạp `pg_read_all_settings` để đọc các GUC này.

```sql
SELECT current_setting('default_transaction_read_only') AS default_transaction_read_only,
       current_setting('search_path') AS search_path,
       current_setting('statement_timeout') AS statement_timeout,
       current_setting('transaction_read_only') AS transaction_read_only;
```

Câu lỗi, hoặc một giá trị `NULL`: `BLOCKED` cho mục đó. `default_transaction_read_only`, `search_path` và `statement_timeout` phải khớp mục tương ứng trong `rolconfig` của role truy vấn. `transaction_read_only` trong transaction đầu tiên phải là `on`. Lệch một mục: `STOP`. Khớp ba mục không chứng minh một phiên sau còn giữ hai GUC user-set, và không phải ranh giới chống ghi (R82-ADR-04).

Đường đi ra, PostgreSQL 16 trở lên. Đường rỗng là hình dạng được chấp thuận. `path_can_set_role` là khả năng `SET ROLE` theo cả đường.

```sql
WITH RECURSIVE start_role AS (
    SELECT oid, rolname
    FROM pg_catalog.pg_roles
    WHERE rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
),
outgoing AS (
    SELECT s.rolname AS start_role,
           g.rolname AS granted_role,
           m.admin_option,
           m.inherit_option,
           m.set_option,
           m.set_option AS path_can_set_role,
           1 AS depth,
           ARRAY[s.oid, m.roleid] AS path_oids,
           s.rolname || ' -> ' || g.rolname AS path_names
    FROM start_role AS s
    JOIN pg_catalog.pg_auth_members AS m ON m.member = s.oid
    JOIN pg_catalog.pg_roles AS g ON g.oid = m.roleid
    UNION ALL
    SELECT o.start_role,
           g.rolname,
           m.admin_option,
           m.inherit_option,
           m.set_option,
           o.path_can_set_role AND m.set_option,
           o.depth + 1,
           o.path_oids || m.roleid,
           o.path_names || ' -> ' || g.rolname
    FROM outgoing AS o
    JOIN pg_catalog.pg_auth_members AS m
      ON m.member = o.path_oids[array_length(o.path_oids, 1)]
    JOIN pg_catalog.pg_roles AS g ON g.oid = m.roleid
    WHERE o.depth < 32
      AND m.roleid <> ALL (o.path_oids)
)
SELECT start_role, granted_role, admin_option, inherit_option, set_option,
       path_can_set_role, depth, path_names
FROM outgoing;
```

PostgreSQL 15, bỏ `inherit_option` và `set_option`. Mọi dòng đều là đường có thể `SET ROLE`, nên `path_can_set_role` là true.

```sql
WITH RECURSIVE start_role AS (
    SELECT oid, rolname
    FROM pg_catalog.pg_roles
    WHERE rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
),
outgoing AS (
    SELECT s.rolname AS start_role,
           g.rolname AS granted_role,
           m.admin_option,
           TRUE AS path_can_set_role,
           1 AS depth,
           ARRAY[s.oid, m.roleid] AS path_oids,
           s.rolname || ' -> ' || g.rolname AS path_names
    FROM start_role AS s
    JOIN pg_catalog.pg_auth_members AS m ON m.member = s.oid
    JOIN pg_catalog.pg_roles AS g ON g.oid = m.roleid
    UNION ALL
    SELECT o.start_role,
           g.rolname,
           m.admin_option,
           TRUE AS path_can_set_role,
           o.depth + 1,
           o.path_oids || m.roleid,
           o.path_names || ' -> ' || g.rolname
    FROM outgoing AS o
    JOIN pg_catalog.pg_auth_members AS m
      ON m.member = o.path_oids[array_length(o.path_oids, 1)]
    JOIN pg_catalog.pg_roles AS g ON g.oid = m.roleid
    WHERE o.depth < 32
      AND m.roleid <> ALL (o.path_oids)
)
SELECT start_role, granted_role, admin_option, path_can_set_role, depth, path_names
FROM outgoing;
```

Đường đi vào, PostgreSQL 16 trở lên.

```sql
WITH RECURSIVE start_role AS (
    SELECT oid, rolname
    FROM pg_catalog.pg_roles
    WHERE rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
),
incoming AS (
    SELECT target.rolname AS preflight_role,
           member_role.rolname AS member_name,
           m.admin_option,
           m.inherit_option,
           m.set_option,
           m.set_option AS path_can_set_role,
           1 AS depth,
           ARRAY[target.oid, m.member] AS path_oids,
           member_role.rolname || ' -> ' || target.rolname AS path_names
    FROM start_role AS target
    JOIN pg_catalog.pg_auth_members AS m ON m.roleid = target.oid
    JOIN pg_catalog.pg_roles AS member_role ON member_role.oid = m.member
    UNION ALL
    SELECT i.preflight_role,
           member_role.rolname,
           m.admin_option,
           m.inherit_option,
           m.set_option,
           i.path_can_set_role AND m.set_option,
           i.depth + 1,
           i.path_oids || m.member,
           member_role.rolname || ' -> ' || i.path_names
    FROM incoming AS i
    JOIN pg_catalog.pg_auth_members AS m
      ON m.roleid = i.path_oids[array_length(i.path_oids, 1)]
    JOIN pg_catalog.pg_roles AS member_role ON member_role.oid = m.member
    WHERE i.depth < 32
      AND m.member <> ALL (i.path_oids)
)
SELECT preflight_role, member_name, admin_option, inherit_option, set_option,
       path_can_set_role, depth, path_names
FROM incoming;
```

Đường đi vào trên PostgreSQL 15. Mọi cạnh đều có thể dẫn tới `SET ROLE`, nên `path_can_set_role` là true. Câu này không đọc `set_option`.

```sql
WITH RECURSIVE start_role AS (
    SELECT oid, rolname
    FROM pg_catalog.pg_roles
    WHERE rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
),
incoming AS (
    SELECT target.rolname AS preflight_role,
           member_role.rolname AS member_name,
           m.admin_option,
           TRUE AS path_can_set_role,
           1 AS depth,
           ARRAY[target.oid, m.member] AS path_oids,
           member_role.rolname || ' -> ' || target.rolname AS path_names
    FROM start_role AS target
    JOIN pg_catalog.pg_auth_members AS m ON m.roleid = target.oid
    JOIN pg_catalog.pg_roles AS member_role ON member_role.oid = m.member
    UNION ALL
    SELECT i.preflight_role,
           member_role.rolname,
           m.admin_option,
           TRUE AS path_can_set_role,
           i.depth + 1,
           i.path_oids || m.member,
           member_role.rolname || ' -> ' || i.path_names
    FROM incoming AS i
    JOIN pg_catalog.pg_auth_members AS m
      ON m.roleid = i.path_oids[array_length(i.path_oids, 1)]
    JOIN pg_catalog.pg_roles AS member_role ON member_role.oid = m.member
    WHERE i.depth < 32
      AND m.member <> ALL (i.path_oids)
)
SELECT preflight_role, member_name, admin_option, path_can_set_role, depth, path_names
FROM incoming;
```

Đối chiếu `pg_has_role` chạy sau câu đi đường của đúng phiên bản. Câu đi đường vẫn là phép duyệt membership theo cả hai chiều. `pg_has_role` không thay câu đi đường. Hướng duyệt và quy tắc chặn mọi đường membership giữ nguyên: tập membership được chấp thuận là rỗng.

PostgreSQL 15, khi Q8 đã có `server_version_num >= 150000` và `server_version_num < 160000`. Không gọi `pg_has_role(..., 'SET')`. Trên phiên bản này membership có thể dẫn tới `SET ROLE`, nên `pg_has_role(..., 'MEMBER')` là kiểm tra membership đó. `pg_has_role(..., 'USAGE')` là quyền dùng được ngay, không cần `SET ROLE`. Câu này không đọc `set_option`.

```sql
SELECT s.rolname AS start_role,
       expected.rolname AS target_role,
       t.oid IS NOT NULL AS target_present,
       CASE WHEN t.oid IS NULL THEN NULL
            ELSE pg_has_role(s.oid, t.oid, 'MEMBER') END AS is_member,
       CASE WHEN t.oid IS NULL THEN NULL
            ELSE pg_has_role(s.oid, t.oid, 'USAGE') END AS inherits_privileges
FROM pg_catalog.pg_roles AS s
CROSS JOIN (VALUES
    ('famixa_preflight_owner'),
    ('famixa_canon_preflight_ro'),
    ('kitplatform'),
    ('postgres'),
    ('pg_read_all_data'),
    ('pg_write_all_data'),
    ('pg_monitor'),
    ('pg_signal_backend')
) AS expected(rolname)
LEFT JOIN pg_catalog.pg_roles AS t ON t.rolname = expected.rolname
WHERE s.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
  AND s.rolname <> expected.rolname;
```

PostgreSQL 16 trở lên, khi Q8 đã có `server_version_num >= 160000`. `pg_has_role(..., 'MEMBER')` là membership, kể cả membership không kèm quyền đổi role. `pg_has_role(..., 'SET')` là quyền đổi role. Hai kết quả này khác nhau. Câu có `'SET'` không thay các câu đi đường: những câu đó vẫn đọc `set_option` trên từng cạnh. `pg_has_role(..., 'USAGE')` là quyền dùng được ngay. Thêm `WITH ADMIN OPTION` hoặc `WITH GRANT OPTION` vào `pg_has_role` kiểm tra quyền `ADMIN` trên membership; cách viết đó không đọc `set_option` và không phải câu dưới đây.

```sql
SELECT s.rolname AS start_role,
       expected.rolname AS target_role,
       t.oid IS NOT NULL AS target_present,
       CASE WHEN t.oid IS NULL THEN NULL
            ELSE pg_has_role(s.oid, t.oid, 'MEMBER') END AS is_member,
       CASE WHEN t.oid IS NULL THEN NULL
            ELSE pg_has_role(s.oid, t.oid, 'SET') END AS can_set_role,
       CASE WHEN t.oid IS NULL THEN NULL
            ELSE pg_has_role(s.oid, t.oid, 'USAGE') END AS inherits_privileges
FROM pg_catalog.pg_roles AS s
CROSS JOIN (VALUES
    ('famixa_preflight_owner'),
    ('famixa_canon_preflight_ro'),
    ('kitplatform'),
    ('postgres'),
    ('pg_read_all_data'),
    ('pg_write_all_data'),
    ('pg_monitor'),
    ('pg_signal_backend')
) AS expected(rolname)
LEFT JOIN pg_catalog.pg_roles AS t ON t.rolname = expected.rolname
WHERE s.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
  AND s.rolname <> expected.rolname;
```

Phiên bản chưa là số nguyên, Q8 còn `BLOCKED`, hoặc không chạy được câu của đúng phiên bản: không chạy câu còn lại, kết quả đối chiếu là `BLOCKED`. Gọi `'SET'` trên máy chủ 15 làm câu lỗi. Lỗi đó là `BLOCKED`. Lỗi hàm, lỗi quyền, thiếu `EXECUTE` trên `pg_has_role(oid, oid, text)`, và thiếu đối tượng đều là `BLOCKED`. `CASE` chỉ giữ `NULL` khi target không còn OID. `NULL` không phải false. Câu lỗi không được ghi thành false và không được ghi thành “không có membership”.

Không có dòng cho một role preflight ở vế `start_role`: `BLOCKED`, role không quan sát được. `target_present` false: boolean của dòng đó là `NULL`, không ghi thành false. `target_present` true mà một boolean của câu phiên bản đó là `NULL`: `BLOCKED`. Trên PostgreSQL 16 trở lên, `can_set_role` thuộc các boolean đó.

Kết quả mong đợi khi câu của đúng phiên bản chạy được: hai câu đi đường của phiên bản đó đều không có dòng. Một dòng đi ra hoặc đi vào, kể cả tới role không nằm trong danh sách tên, là `STOP`. Trên PostgreSQL 15, dòng đó là membership có thể dẫn tới `SET ROLE`. Trên PostgreSQL 16 trở lên, `path_can_set_role` false vẫn là membership: vẫn `STOP`. Có dòng ở `depth = 32`: `BLOCKED`, vì đường chưa được đi hết. `postgres` không có trong `pg_roles`: `BLOCKED`. `kitplatform` hoặc một predefined role không có trong `pg_roles`: `TARGET_ROLE_ABSENT`. Đi đường rỗng và target vắng mặt chỉ khớp khi cả hai quan sát đều chạy được. Một quan sát còn `BLOCKED`: kết quả là `BLOCKED`, không ghép thành khớp.

Với mỗi target còn tồn tại, PostgreSQL 15 cần `is_member` false và `inherits_privileges` false. `is_member` true là `STOP`. PostgreSQL 16 trở lên cần `is_member`, `can_set_role` và `inherits_privileges` đều false. `is_member` true là `STOP` dù `can_set_role` false. `can_set_role` true trong khi đi đường rỗng, hoặc đi đường có `path_can_set_role` true trong khi `can_set_role` false: `STOP`, hai quan sát không khớp. Lỗi quyền trên `pg_auth_members`: `BLOCKED`.

### Q2. Owner và RLS của bảng nền

- Role dự kiến: lần đầu là `ADMIN_SESSION`, trước khi tạo role preflight. Lần checklist là `famixa_canon_preflight_ro`.
- Đối tượng đọc: `pg_catalog.pg_namespace`, `pg_catalog.pg_class`, `pg_catalog.pg_roles`.
- Quyền tối thiểu: `SELECT` trên ba quan hệ đó. Không cần `SELECT` trên bảng nghiệp vụ. Thiếu `SELECT` catalog: `BLOCKED`, không `GRANT`.
- Chỉ đọc: có.
- Thời điểm: quan hệ, owner hiện tại và hai cờ RLS đọc được trước khi tạo role. Điều kiện “owner không phải role preflight” chỉ đọc sau khi role tồn tại.

`relrowsecurity` và `relforcerowsecurity` là cờ trên `pg_class`. `row_security_active(table)` trả lời cho `current_user` của phiên đang chạy. View chạy bằng `famixa_preflight_owner`, không bằng role truy vấn. Phiên chỉ đọc không `SET ROLE` sang owner để gọi hàm đó. Phiên superuser có `row_security_active` false; giá trị đó không chứng minh RLS đang tắt. Source migration không phải quan sát RLS production.

```sql
SELECT expected.relname,
       expected.required_relation,
       c.oid IS NOT NULL AS relation_visible,
       c.relkind,
       owner_role.rolname AS owner_name,
       c.relrowsecurity,
       c.relforcerowsecurity
FROM (VALUES
    ('users', TRUE),
    ('tenants', TRUE),
    ('roles', TRUE),
    ('permissions', TRUE),
    ('role_permissions', TRUE),
    ('user_roles', TRUE),
    ('kit_accounts', FALSE),
    ('refresh_tokens', FALSE),
    ('staff_otp_challenges', FALSE),
    ('employees', FALSE),
    ('kit_schema_migrations', FALSE)
) AS expected(relname, required_relation)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_roles AS owner_role ON owner_role.oid = c.relowner
ORDER BY expected.relname;
```

Câu lỗi: `BLOCKED`. Câu chạy và trả một dòng cho mỗi tên: catalog đã đọc được.

Với bảng bắt buộc, `relation_visible` false: `BLOCKED`, quan hệ cần cho view không thấy. Với bảng không bắt buộc, `relation_visible` false: `RELATION_ABSENT`. Sự vắng mặt không phải `has_table_privilege = false`. `relkind` khác `r` trên quan hệ đã thấy: `STOP`. Hai dòng cùng `relname`: `STOP`.

`PASS_SHAPE` của owner, chỉ sau khi role preflight tồn tại: mọi quan hệ đã thấy có `owner_name` khác `famixa_preflight_owner` và khác `famixa_canon_preflight_ro`. `owner_name` null trong khi `relation_visible` true: `BLOCKED`, tên owner không đọc được. Owner trùng một trong hai role preflight: `STOP`, kể cả khi `relforcerowsecurity` true. Trước khi tạo role, câu này là mốc `ADMIN_SESSION`: ghi owner và hai cờ, chưa phải dòng checklist.

`relrowsecurity` được ghi lại đúng boolean đã đọc. `true` nghĩa là RLS có thể làm view, vốn chạy bằng owner không phải owner bảng và không có `BYPASSRLS`, trả không dòng. Không dòng ở Q7 khi cờ này true là `NO_VISIBLE_ROWS`, không phải `DATA_ABSENT`. Không cấp `BYPASSRLS` và không đổi owner bảng. `relrowsecurity` false: RLS không áp lên bảng đó. `NULL` trên quan hệ đã thấy: `BLOCKED`.

### Q3. Quyền đọc catalog và `EXECUTE` của `pg_get_constraintdef`

- Role dự kiến: đo quyền của cả hai role; phiên checklist là `famixa_canon_preflight_ro`. Phiên `ADMIN_SESSION` có thể đo cùng câu trước khi role truy vấn đăng nhập, và kết quả mang nhãn `ADMIN_SESSION`.
- Đối tượng đọc: các quan hệ `pg_catalog` liệt kê trong câu, `pg_catalog.pg_proc`, `pg_catalog.pg_namespace`, và hàm `pg_get_constraintdef(oid)`.
- Quyền tối thiểu: `EXECUTE` trên `has_table_privilege(oid, oid, text)`, `has_schema_privilege(oid, oid, text)`, `has_function_privilege(oid, oid, text)`, `pg_get_function_identity_arguments(oid)`, cộng `SELECT` để phân giải OID. Người gọi không cần trở thành owner.
- Chỉ đọc: có.
- Thời điểm: `PUBLIC` và việc nhìn thấy hàm đo được trước khi tạo role. Quyền của từng role đo sau khi role tồn tại và trước `CREATE VIEW` của `constraints_expected`. View này không được tạo khi quyền owner chưa đủ.

Tên user `public` trong các hàm `has_*_privilege` là pseudo-role `PUBLIC`, theo định nghĩa hàm tra quyền của PostgreSQL. `EXECUTE` true là khả năng chạy hàm, gồm quyền cấp cho `PUBLIC`. Nó không nói gì về `GRANT OPTION`; mục đó là Q6. `proacl` null là ACL mặc định của hàm, không phải “không có EXECUTE”. Hàm `has_function_privilege` là kết quả hiệu lực.

Quan hệ view cần owner `SELECT`: `pg_namespace`, `pg_class`, `pg_constraint`, `pg_attribute`. Owner còn cần `USAGE` trên schema `public` và `pg_catalog`. Hàm view cần owner `EXECUTE`: đúng overload `pg_get_constraintdef(oid)`, không phải `pg_get_constraintdef(oid, boolean)`.

Quan hệ checklist cần role truy vấn `SELECT`: sáu quan hệ trên cộng `pg_roles`, `pg_auth_members`, `pg_proc`. Role truy vấn cần `EXECUTE` trên `pg_get_viewdef(oid)` để làm mục 5 điều 10, và trên các hàm `has_*_privilege`, `pg_has_role(oid, oid, text)`, `aclexplode(aclitem[])`, `pg_get_userbyid(oid)`, `pg_get_function_identity_arguments(oid)` mà các câu mục 8 gọi. Cả hai role, khi `rolsuper` false, phải có `SELECT` false trên `pg_catalog.pg_authid`.

```sql
SELECT r.rolname,
       t.relname,
       has_table_privilege(r.oid, t.oid, 'SELECT') AS can_select
FROM pg_catalog.pg_roles AS r
CROSS JOIN (
    SELECT c.oid, c.relname
    FROM pg_catalog.pg_class AS c
    JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
    WHERE n.nspname = 'pg_catalog'
      AND c.relname IN (
          'pg_namespace', 'pg_class', 'pg_constraint', 'pg_attribute',
          'pg_roles', 'pg_auth_members', 'pg_proc', 'pg_authid'
      )
) AS t
WHERE r.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
ORDER BY t.relname, r.rolname;
```

Ít hơn tám quan hệ trong nguồn con: `BLOCKED`, catalog không quan sát đủ, không ghi phần thiếu thành `can_select = false`. Không có dòng role: `BLOCKED`, role chưa có.

```sql
SELECT r.rolname,
       n.nspname,
       has_schema_privilege(r.oid, n.oid, 'USAGE') AS can_use
FROM pg_catalog.pg_roles AS r
JOIN pg_catalog.pg_namespace AS n
  ON n.nspname IN ('public', 'pg_catalog')
WHERE r.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro');
```

```sql
SELECT r.rolname,
       p.proname,
       pg_get_function_identity_arguments(p.oid) AS identity_args,
       has_function_privilege(r.oid, p.oid, 'EXECUTE') AS can_execute
FROM pg_catalog.pg_proc AS p
JOIN pg_catalog.pg_namespace AS n ON n.oid = p.pronamespace
JOIN pg_catalog.pg_roles AS r
  ON r.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
WHERE n.nspname = 'pg_catalog'
  AND (
      (p.proname = 'pg_get_constraintdef'
       AND pg_get_function_identity_arguments(p.oid) = 'oid')
      OR (p.proname = 'pg_get_viewdef'
          AND pg_get_function_identity_arguments(p.oid) = 'oid')
      OR (p.proname = 'has_table_privilege'
          AND pg_get_function_identity_arguments(p.oid) = 'oid, oid, text')
      OR (p.proname = 'has_column_privilege'
          AND pg_get_function_identity_arguments(p.oid) = 'oid, oid, smallint, text')
      OR (p.proname = 'has_schema_privilege'
          AND pg_get_function_identity_arguments(p.oid) = 'oid, oid, text')
      OR (p.proname = 'has_function_privilege'
          AND pg_get_function_identity_arguments(p.oid) = 'oid, oid, text')
      OR (p.proname = 'has_any_column_privilege'
          AND pg_get_function_identity_arguments(p.oid) = 'oid, oid, text')
      OR (p.proname = 'pg_has_role'
          AND pg_get_function_identity_arguments(p.oid) = 'oid, oid, text')
      OR (p.proname = 'aclexplode'
          AND pg_get_function_identity_arguments(p.oid) = 'aclitem[]')
      OR (p.proname = 'pg_get_userbyid'
          AND pg_get_function_identity_arguments(p.oid) = 'oid')
      OR (p.proname = 'pg_get_function_identity_arguments'
          AND pg_get_function_identity_arguments(p.oid) = 'oid')
  );
```

Chuỗi `identity_args` ở trên là danh sách đối số mà các mẫu mục 8 gọi. Chúng chưa được chạy trong lần soạn này. Nếu một overload đúng arity hiện diện với cách viết kiểu khác, dùng overload đó và ghi lại chuỗi thực. Nếu không có overload đúng arity: `BLOCKED`. Không lấy `pg_get_constraintdef(oid, boolean)` thay cho overload một đối số.

```sql
SELECT c.relname,
       has_table_privilege('public', c.oid, 'SELECT') AS public_select
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
WHERE n.nspname = 'pg_catalog'
  AND c.relname IN (
      'pg_namespace', 'pg_class', 'pg_constraint', 'pg_attribute',
      'pg_roles', 'pg_auth_members', 'pg_proc', 'pg_authid'
  )
ORDER BY c.relname;
```

`PASS_SHAPE` phần view: owner có `can_select` true trên `pg_namespace`, `pg_class`, `pg_constraint`, `pg_attribute`; `can_use` true trên `public` và `pg_catalog`; `can_execute` true trên `pg_get_constraintdef(oid)`. Một mục trong số đó false: `STOP` theo R82-ADR-06. Không cấp `GRANT` catalog và không cấp `EXECUTE`. Owner `can_select` true trong khi `public_select` false: quyền đã có từ grant riêng sẵn có; ghi nhận, không cấp thêm grant. `pg_authid` có `can_select` true hoặc `public_select` true trong khi `rolsuper` false: `STOP`.

Role truy vấn thiếu `SELECT` hoặc `EXECUTE` của một câu checklist: câu đó là `BLOCKED`. Không `GRANT` để checklist chạy được. Kết quả `ADMIN_SESSION` giữ nhãn của nó và không đổi câu checklist thành `PASS_SHAPE`. `can_execute` false của role truy vấn trên `pg_get_constraintdef(oid)` không chặn view, vì view chạy bằng owner; nó chỉ được ghi lại. `can_execute` false của role truy vấn trên `pg_get_viewdef(oid)` làm mục 5 điều 10 thành `BLOCKED`.

### Q4. `users.kit_account_id` và ba cột tenants của ADR-07

- Role dự kiến: phiên nào đọc được `pg_attribute`. Checklist ghi kết quả từ `famixa_canon_preflight_ro` khi role đó đọc được catalog. Trước khi có role, cùng câu thuộc `ADMIN_SESSION`.
- Đối tượng đọc: `pg_catalog.pg_namespace`, `pg_catalog.pg_class`, `pg_catalog.pg_attribute`. Không đọc giá trị nghiệp vụ của các cột này.
- Quyền tối thiểu: `SELECT` catalog. Thiếu: `BLOCKED`. Không `GRANT` cột để “thấy” sự tồn tại.
- Chỉ đọc: có.
- Thời điểm: trước khi tạo role và view. Quyền trên cột đã thấy được đo ở Q5, sau khi cấp quyền.

Chỉ diễn giải một cột khi Q2 thấy bảng chứa nó và catalog cột của bảng đó đọc được. Bảng chưa thấy: `NOT_OBSERVABLE` khi câu catalog chạy nhưng không thấy bảng, và `BLOCKED` khi câu lỗi, thiếu quyền catalog, hoặc Q2 còn `BLOCKED`. Với bảng bắt buộc, Q2 ghi kết quả checklist của quan hệ không thấy là `BLOCKED`; nhãn quan sát vẫn là `NOT_OBSERVABLE`. Hai nhãn đi cùng nhau. Không phải `RELATION_ABSENT` và không phải `COLUMN_ABSENT`. Thiếu bằng chứng catalog không phải bằng chứng cột không tồn tại.

```sql
SELECT expected.relname,
       expected.attname,
       c.oid IS NOT NULL AS table_visible,
       a.attrelid IS NOT NULL AND NOT a.attisdropped AS column_present,
       a.attisdropped
FROM (VALUES
    ('users', 'kit_account_id'),
    ('tenants', 'organization_id'),
    ('tenants', 'business_vertical'),
    ('tenants', 'rx_directory_discoverable')
) AS expected(relname, attname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_attribute AS a
  ON a.attrelid = c.oid
 AND a.attname = expected.attname
 AND a.attnum > 0
ORDER BY expected.relname, expected.attname;
```

Câu đếm cột còn sống của cùng bảng. `live_column_count` chỉ có giá trị khi bảng đã có OID. Không thấy bảng thì kết quả là `NULL`, không phải số 0.

```sql
SELECT expected.relname,
       c.oid IS NOT NULL AS table_visible,
       CASE
           WHEN c.oid IS NULL THEN NULL
           ELSE (
               SELECT COUNT(*)
               FROM pg_catalog.pg_attribute AS a
               WHERE a.attrelid = c.oid
                 AND a.attnum > 0
                 AND NOT a.attisdropped
           )
       END AS live_column_count
FROM (VALUES
    ('users'),
    ('tenants')
) AS expected(relname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
ORDER BY expected.relname;
```

Câu lỗi: `BLOCKED`. `table_visible` false: `NOT_OBSERVABLE`; với hai bảng bắt buộc này, kết luận checklist là `BLOCKED`. Không dùng `COLUMN_ABSENT`. `table_visible` true mà `live_column_count` là `NULL` hoặc 0: `BLOCKED`, catalog cột chưa được quan sát. Không lấy số 0 làm bằng chứng từng cột đã vắng.

`COLUMN_ABSENT` chỉ khi `table_visible` true, `live_column_count` lớn hơn 0, Q2 không còn `BLOCKED` cho bảng đó, và `column_present` false, kể cả khi `attisdropped` true. `column_present` true: `COLUMN_PRESENT`.

`users.kit_account_id` ở `COLUMN_ABSENT`: lệnh cấm được thỏa bởi sự vắng mặt đã quan sát. Không gọi `has_column_privilege` cho cột không có, và không cấp `SELECT`. Bảng `users` không quan sát được thì lệnh cấm chưa được xác nhận bằng sự vắng mặt; kết luận là `BLOCKED`. `COLUMN_PRESENT`: Q5 phải thấy `SELECT` và `REFERENCES` false ở cả hai role. Ba cột ADR-07 ở `COLUMN_ABSENT`: không có quyền cột để đọc; sự vắng mặt không đưa chúng vào ma trận `GRANT` và không làm điều 15 của mục 5 thất bại. `COLUMN_PRESENT`: Q5 phải thấy `SELECT` và `REFERENCES` false ở cả hai role; true là `STOP`. Boolean `NULL` hoặc lỗi hàm: `BLOCKED`.

### Q5. Quyền cột trên từng bảng và cột nhạy cảm

- Role dự kiến: `famixa_canon_preflight_ro`.
- Đối tượng đọc: `pg_catalog.pg_roles`, `pg_catalog.pg_namespace`, `pg_catalog.pg_class`, `pg_catalog.pg_attribute`. Hàm quyền nhận OID; câu không cần `SELECT` dữ liệu bảng `public`.
- Quyền tối thiểu: `SELECT` trên các catalog đó, `EXECUTE` trên `has_column_privilege(oid, oid, smallint, text)`, `has_table_privilege(oid, oid, text)` và `has_any_column_privilege(oid, oid, text)`. Thiếu: `BLOCKED`. Không cấp `SELECT` bảng hoặc cột để làm hàm trả true.
- Chỉ đọc: có.
- Thời điểm: sau khi role tồn tại và sau khi các `GRANT` cột ở mục 3 đã cấp. Trước thời điểm đó, cột lẽ ra được cấp mà đang false là `SETUP_INCOMPLETE`, tức `BLOCKED` đối với checklist, không phải lý do cấp rộng hơn ma trận.

`has_column_privilege(..., 'SELECT')` true khi role có `SELECT` cấp đúng cột hoặc có `SELECT` cả bảng. Nó không nói về `GRANT OPTION`. `PASS_SHAPE` của một cột được cấp là đồng thời: cột có mặt, owner có column `SELECT` true, owner có table `SELECT` false, role truy vấn có column `SELECT` false và table `SELECT` false. Table `SELECT` true cùng column `SELECT` true: `STOP`, grant rộng hơn ma trận cột.

Chỉ gọi hàm quyền khi role có trong `pg_roles` và cột có trong `pg_attribute` với `attnum > 0` và `attisdropped = false`. Cột liệt kê mà vắng trên bảng đã quan sát: xử lý theo bảng phân loại phía dưới, không ép hàm trả false. Bảng không quan sát được: không gọi hàm. Bảng bắt buộc không thấy giữ `NOT_OBSERVABLE`, và kết luận quyền là `BLOCKED`. Bảng không bắt buộc không thấy lấy `RELATION_ABSENT` từ Q2. Q2 còn `BLOCKED`: không đổi thành `RELATION_ABSENT` và không đổi thành `COLUMN_ABSENT`.

Cột owner phải true, role truy vấn phải false: `tenants.id`, `tenant_code`, `status`, `deleted_at`; `roles.id`, `tenant_id`, `role_code`, `status`; `permissions.id`, `permission_code`, `permission_name`, `module_name`; `role_permissions.id`, `role_id`, `permission_id`; `user_roles.user_id`, `role_id`; `users.id`, `username`, `status`, `deleted_at`. `kit_schema_migrations.filename` chỉ thuộc nhóm phải true khi Q2 thấy quan hệ. Quan hệ ledger vắng: không có grant `filename`, và không có giá trị false để ghi.

Cột cả hai role phải false khi cột còn: `tenants.tenant_name`, `country_code`, `default_currency`, `timezone`, `settings`, `created_at`, `updated_at`, `organization_id`, `business_vertical`, `rx_directory_discoverable`; `roles.role_name`, `description`, `created_at`, `updated_at`; `permissions.description`, `created_at`; `role_permissions.created_at`; `user_roles.id`, `created_at`; `users.tenant_id`, `must_change_password`, `last_login_at`, `created_at`, `updated_at`, `password_hash`, `email`, `employee_id`, `kit_account_id`; `kit_schema_migrations.applied_at` khi quan hệ ledger còn. Các cột tên của bảng cấm, khi bảng còn: mọi cột của `kit_accounts`, trong đó có `email`, `phone`, `password_hash`; mọi cột của `refresh_tokens`, trong đó có `token_hash`; mọi cột của `staff_otp_challenges`, trong đó có `code_hash` và `phone`; mọi cột của `employees`, trong đó có `email` và `phone`.

```sql
SELECT e.relname,
       e.attname,
       e.expectation,
       c.oid IS NOT NULL AS table_visible,
       a.attnum IS NOT NULL AS column_present,
       CASE
           WHEN owner_role.oid IS NULL OR a.attnum IS NULL THEN NULL
           ELSE has_column_privilege(owner_role.oid, c.oid, a.attnum, 'SELECT')
       END AS owner_select,
       CASE
           WHEN query_role.oid IS NULL OR a.attnum IS NULL THEN NULL
           ELSE has_column_privilege(query_role.oid, c.oid, a.attnum, 'SELECT')
       END AS query_select,
       CASE
           WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
           ELSE has_table_privilege(owner_role.oid, c.oid, 'SELECT')
       END AS owner_table_select,
       CASE
           WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
           ELSE has_table_privilege(query_role.oid, c.oid, 'SELECT')
       END AS query_table_select
FROM (VALUES
    ('tenants', 'id', 'grant'),
    ('tenants', 'tenant_code', 'grant'),
    ('tenants', 'status', 'grant'),
    ('tenants', 'deleted_at', 'grant'),
    ('tenants', 'tenant_name', 'deny'),
    ('tenants', 'country_code', 'deny'),
    ('tenants', 'default_currency', 'deny'),
    ('tenants', 'timezone', 'deny'),
    ('tenants', 'settings', 'deny'),
    ('tenants', 'created_at', 'deny'),
    ('tenants', 'updated_at', 'deny'),
    ('tenants', 'organization_id', 'deny'),
    ('tenants', 'business_vertical', 'deny'),
    ('tenants', 'rx_directory_discoverable', 'deny'),
    ('roles', 'id', 'grant'),
    ('roles', 'tenant_id', 'grant'),
    ('roles', 'role_code', 'grant'),
    ('roles', 'status', 'grant'),
    ('roles', 'role_name', 'deny'),
    ('roles', 'description', 'deny'),
    ('roles', 'created_at', 'deny'),
    ('roles', 'updated_at', 'deny'),
    ('permissions', 'id', 'grant'),
    ('permissions', 'permission_code', 'grant'),
    ('permissions', 'permission_name', 'grant'),
    ('permissions', 'module_name', 'grant'),
    ('permissions', 'description', 'deny'),
    ('permissions', 'created_at', 'deny'),
    ('role_permissions', 'id', 'grant'),
    ('role_permissions', 'role_id', 'grant'),
    ('role_permissions', 'permission_id', 'grant'),
    ('role_permissions', 'created_at', 'deny'),
    ('user_roles', 'user_id', 'grant'),
    ('user_roles', 'role_id', 'grant'),
    ('user_roles', 'id', 'deny'),
    ('user_roles', 'created_at', 'deny'),
    ('users', 'id', 'grant'),
    ('users', 'username', 'grant'),
    ('users', 'status', 'grant'),
    ('users', 'deleted_at', 'grant'),
    ('users', 'tenant_id', 'deny'),
    ('users', 'must_change_password', 'deny'),
    ('users', 'last_login_at', 'deny'),
    ('users', 'created_at', 'deny'),
    ('users', 'updated_at', 'deny'),
    ('users', 'password_hash', 'deny'),
    ('users', 'email', 'deny'),
    ('users', 'employee_id', 'deny'),
    ('users', 'kit_account_id', 'deny'),
    ('kit_schema_migrations', 'filename', 'grant_if_visible'),
    ('kit_schema_migrations', 'applied_at', 'deny'),
    ('kit_accounts', 'email', 'deny'),
    ('kit_accounts', 'phone', 'deny'),
    ('kit_accounts', 'password_hash', 'deny'),
    ('refresh_tokens', 'token_hash', 'deny'),
    ('staff_otp_challenges', 'code_hash', 'deny'),
    ('staff_otp_challenges', 'phone', 'deny'),
    ('employees', 'email', 'deny'),
    ('employees', 'phone', 'deny')
) AS e(relname, attname, expectation)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = e.relname
LEFT JOIN pg_catalog.pg_attribute AS a
  ON a.attrelid = c.oid
 AND a.attname = e.attname
 AND a.attnum > 0
 AND NOT a.attisdropped
LEFT JOIN pg_catalog.pg_roles AS owner_role
  ON owner_role.rolname = 'famixa_preflight_owner'
LEFT JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
ORDER BY e.relname, e.attname;
```

`COLUMN_ABSENT` chỉ được gán khi bảng chứa cột đã được xác nhận tồn tại và quan sát được: câu catalog của dòng đó không lỗi, `table_visible` true, Q2 không còn `BLOCKED` cho quan hệ đó, và câu đếm của Q4 có `live_column_count` lớn hơn 0 trên bảng đó. `column_present` false trong điều kiện đó, kể cả khi cột đã bị drop, mới là cột vắng mặt. Bảng không quan sát được thì giữ nguyên nguyên nhân: câu chạy nhưng `table_visible` false trên quan hệ bắt buộc là `NOT_OBSERVABLE`, và kết luận quyền của dòng đó là `BLOCKED`; câu lỗi, thiếu quyền catalog, catalog cột không đọc được, hoặc Q2 còn `BLOCKED` là `BLOCKED`. Quan hệ không bắt buộc không thấy là `RELATION_ABSENT` từ Q2. Thiếu bằng chứng catalog không phải bằng chứng cột không tồn tại, và không được ghi `COLUMN_ABSENT`.

Phân loại từng dòng:

- `expectation = 'grant'` và `table_visible` false: `NOT_OBSERVABLE` nếu quan hệ bắt buộc chưa thấy, kết luận dòng là `BLOCKED`. Không phải `COLUMN_ABSENT`.
- `expectation = 'grant'`, `table_visible` true và `column_present` false: `BLOCKED`. View không đúng hình đã chốt. Không đổi sang cột khác. Không ghi `COLUMN_ABSENT` cho cột thuộc ma trận grant.
- `expectation = 'grant'` và `column_present` true: `PASS_SHAPE` chỉ khi `owner_select` true, `owner_table_select` false, `query_select` false, `query_table_select` false. Boolean `NULL` hoặc lỗi hàm: `BLOCKED`. `owner_select` false: `STOP`. `query_select` true hoặc một table `SELECT` true: `STOP`.
- `kit_schema_migrations.filename` khi `table_visible` false: `RELATION_ABSENT` chỉ khi Q2 đã gán nhãn đó. Q2 còn `BLOCKED`: giữ `BLOCKED`. Không đo quyền và không ghi `COLUMN_ABSENT`. Khi `table_visible` true và `column_present` false: `BLOCKED`. Khi cột còn: cùng `PASS_SHAPE` của nhóm grant. Boolean `NULL` hoặc lỗi: `BLOCKED`.
- `users.kit_account_id` và ba cột ADR-07 chỉ là `COLUMN_ABSENT` khi bảng đã quan sát, `live_column_count` lớn hơn 0, và `column_present` false, như Q4. `table_visible` false: `NOT_OBSERVABLE`, kết luận checklist `BLOCKED`, không phải `COLUMN_ABSENT`. Khi cột còn: cả hai `SELECT` cột phải false và table `SELECT` phải false. True là `STOP`. Boolean `NULL`, lỗi hàm, hoặc không quan sát được quyền: `BLOCKED`.
- Cột `deny` khác, bảng đã quan sát mà cột không còn: `BLOCKED` cho riêng dòng quyền đó, vì đặc tả không quan sát được privilege false. Không ghi `COLUMN_ABSENT`. Không cấp `SELECT`. Ngoại lệ `COLUMN_ABSENT` đã chốt chỉ gồm `users.kit_account_id` và ba cột ADR-07, và chỉ trên bảng đã quan sát với catalog cột đọc được.
- Bảng cấm hoặc ledger khi `table_visible` false: `RELATION_ABSENT` chỉ khi Q2 đã gán nhãn đó. Q2 còn `BLOCKED`: giữ `BLOCKED`. Không ghi `query_select` null thành false và không ghi `COLUMN_ABSENT`.
- Cột `deny`, cột còn: `owner_select` và `query_select` đều false, hai table `SELECT` đều false. True là `STOP`. `NULL` trong khi cột còn và role còn, hoặc lỗi hàm: `BLOCKED`.

Câu quét mọi cột người dùng còn lại. Cột không có trong danh sách trên mà owner hoặc role truy vấn có `SELECT` true: `STOP`. Cùng cột đó với cả hai `SELECT` false: ghi `COLUMN_NOT_IN_SPEC`, không đưa vào ma trận `GRANT`.

```sql
SELECT c.relname,
       a.attname,
       has_column_privilege(owner_role.oid, c.oid, a.attnum, 'SELECT') AS owner_select,
       has_column_privilege(query_role.oid, c.oid, a.attnum, 'SELECT') AS query_select,
       has_column_privilege(owner_role.oid, c.oid, a.attnum, 'INSERT') AS owner_insert,
       has_column_privilege(query_role.oid, c.oid, a.attnum, 'INSERT') AS query_insert,
       has_column_privilege(owner_role.oid, c.oid, a.attnum, 'UPDATE') AS owner_update,
       has_column_privilege(query_role.oid, c.oid, a.attnum, 'UPDATE') AS query_update
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
JOIN pg_catalog.pg_attribute AS a ON a.attrelid = c.oid
JOIN pg_catalog.pg_roles AS owner_role
  ON owner_role.rolname = 'famixa_preflight_owner'
JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
WHERE n.nspname = 'public'
  AND c.relname IN (
      'users', 'tenants', 'roles', 'permissions', 'role_permissions',
      'user_roles', 'kit_accounts', 'refresh_tokens', 'staff_otp_challenges',
      'employees', 'kit_schema_migrations'
  )
  AND a.attnum > 0
  AND NOT a.attisdropped
ORDER BY c.relname, a.attnum;
```

`INSERT` hoặc `UPDATE` cột true ở bất kỳ role nào: `STOP`. Boolean `NULL` trên dòng cột đã có: `BLOCKED`. Câu lỗi: `BLOCKED`. Bảng bắt buộc không có dòng nào trong kết quả quét, dù Q2 thấy quan hệ: `BLOCKED`. Câu quét này không đọc `REFERENCES` cấp cột. Thiếu câu `REFERENCES` phía dưới: kết luận quyền đó là `BLOCKED`, không phải false.

Các câu `has_column_privilege` phía trên chỉ gọi hàm khi `a.attnum` không null. Cột không tồn tại: không gọi kiểm tra cột. `NULL` của nhánh đó không phải quyền false.

Câu `REFERENCES` cấp cột dùng cùng danh sách quan hệ với câu quyền bảng. Hàm chỉ chạy khi role còn OID, quan hệ còn OID và `attnum` còn. `REFERENCES` là quyền cột trên mọi phiên bản mà thiết kế này còn được xét. Câu này không chứa `TRIGGER` và không chứa `MAINTAIN`: hai quyền đó không được gọi ở cấp cột. `MAINTAIN` vẫn là câu bảng riêng, chỉ khi `server_version_num >= 170000`.

```sql
SELECT expected.relname,
       expected.required_relation,
       c.oid IS NOT NULL AS relation_visible,
       a.attname,
       a.attnum IS NOT NULL AS column_present,
       CASE
           WHEN owner_role.oid IS NULL OR c.oid IS NULL OR a.attnum IS NULL THEN NULL
           ELSE has_column_privilege(owner_role.oid, c.oid, a.attnum, 'REFERENCES')
       END AS owner_references,
       CASE
           WHEN query_role.oid IS NULL OR c.oid IS NULL OR a.attnum IS NULL THEN NULL
           ELSE has_column_privilege(query_role.oid, c.oid, a.attnum, 'REFERENCES')
       END AS query_references
FROM (VALUES
    ('users', TRUE),
    ('tenants', TRUE),
    ('roles', TRUE),
    ('permissions', TRUE),
    ('role_permissions', TRUE),
    ('user_roles', TRUE),
    ('kit_accounts', FALSE),
    ('refresh_tokens', FALSE),
    ('staff_otp_challenges', FALSE),
    ('employees', FALSE),
    ('kit_schema_migrations', FALSE)
) AS expected(relname, required_relation)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_attribute AS a
  ON a.attrelid = c.oid
 AND a.attnum > 0
 AND NOT a.attisdropped
LEFT JOIN pg_catalog.pg_roles AS owner_role
  ON owner_role.rolname = 'famixa_preflight_owner'
LEFT JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
ORDER BY expected.relname, a.attnum;
```

Câu lỗi, kể cả lỗi từ khóa hoặc `permission denied`: `BLOCKED`, không ghi thành false. Quan hệ bắt buộc không thấy: `NOT_OBSERVABLE`, kết luận dòng `BLOCKED`. Quan hệ không bắt buộc không thấy: `RELATION_ABSENT`. `NULL` trên các dòng đó không phải quyền false và không phải `COLUMN_ABSENT`. Q2 còn `BLOCKED`: không đổi quan hệ đó thành `RELATION_ABSENT`.

Quan hệ đã quan sát mà không có dòng cột nào: `BLOCKED`, catalog cột chưa được quan sát. Không suy ra từng cột là `COLUMN_ABSENT`. Trên dòng đã có `column_present` true, `owner_references` hoặc `query_references` true: `STOP`. Cả hai false là hình được chấp thuận của quyền `REFERENCES` trên dòng đó; hình đó chưa phải `PASS_SHAPE` của cả Q5. Thiếu OID của một role: `BLOCKED`. Boolean `NULL` trong khi cột, quan hệ và role còn OID: `BLOCKED`. `NULL` của nhánh `CASE` không phải quyền false. `REFERENCES` bảng false không thay một dòng cột chưa quan sát. `SELECT` cột đúng ma trận không làm câu này thành `PASS_SHAPE`.

Câu quyền bảng dưới đây kiểm tra từng quan hệ trong danh sách Q2. Mỗi quyền là một cột kết quả riêng: `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES`, `TRIGGER`, cho cả hai role. `has_any_column_privilege(..., 'SELECT')` cũng là một cột riêng cho từng role. Câu này không chứa từ khóa `MAINTAIN`. Hàm chỉ được gọi khi role còn OID và quan hệ còn OID. Quan hệ không có OID: các cột quyền là `NULL`, và hàm không chạy cho dòng đó.

```sql
SELECT expected.relname,
       expected.required_relation,
       c.oid IS NOT NULL AS relation_visible,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'SELECT') END AS owner_select,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'SELECT') END AS query_select,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'INSERT') END AS owner_insert,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'INSERT') END AS query_insert,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'UPDATE') END AS owner_update,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'UPDATE') END AS query_update,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'DELETE') END AS owner_delete,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'DELETE') END AS query_delete,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'TRUNCATE') END AS owner_truncate,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'TRUNCATE') END AS query_truncate,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'REFERENCES') END AS owner_references,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'REFERENCES') END AS query_references,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'TRIGGER') END AS owner_trigger,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'TRIGGER') END AS query_trigger,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_any_column_privilege(owner_role.oid, c.oid, 'SELECT') END AS owner_any_column_select,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_any_column_privilege(query_role.oid, c.oid, 'SELECT') END AS query_any_column_select
FROM (VALUES
    ('users', TRUE),
    ('tenants', TRUE),
    ('roles', TRUE),
    ('permissions', TRUE),
    ('role_permissions', TRUE),
    ('user_roles', TRUE),
    ('kit_accounts', FALSE),
    ('refresh_tokens', FALSE),
    ('staff_otp_challenges', FALSE),
    ('employees', FALSE),
    ('kit_schema_migrations', FALSE)
) AS expected(relname, required_relation)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_roles AS owner_role
  ON owner_role.rolname = 'famixa_preflight_owner'
LEFT JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
ORDER BY expected.relname;
```

Câu lỗi, `permission denied`, thiếu role, hoặc ít hơn mười một dòng: `BLOCKED`. Không cấp thêm quyền nào để checklist đạt. Hai dòng cùng `relname`: `STOP`, cùng quy tắc Q2.

`required_relation` true và `relation_visible` false: `NOT_OBSERVABLE`. Đây là quan hệ không quan sát được, không phải quan hệ không tồn tại. Các `NULL` quyền của dòng đó không phải false. Kết quả dòng đó là `BLOCKED`. `required_relation` false và `relation_visible` false: `RELATION_ABSENT`, lấy cùng phân loại từ Q2. Sự vắng mặt không phải “mọi quyền đã được kiểm và đều false”. Q2 còn `BLOCKED`: Q5 không đổi quan hệ đó thành `RELATION_ABSENT`.

`relation_visible` true: quan hệ đã được quan sát. Mười bốn cột quyền bảng và hai cột `has_any_column_privilege` của dòng đó phải là boolean. Một cột `NULL`: `BLOCKED`. Một quyền false không chứng minh các quyền còn lại đã được kiểm. Thiếu cột `TRUNCATE`, `REFERENCES`, `TRIGGER`, hoặc thiếu `has_any_column_privilege`, dù `SELECT`, `INSERT`, `UPDATE` và `DELETE` đang false: `BLOCKED`.

Trên quan hệ đã quan sát, bảy quyền bảng của cả hai role phải false. Một giá trị true: `STOP`. `query_any_column_select` true: `STOP`. `owner_any_column_select` true chỉ đúng trên bảng có ít nhất một cột trong ma trận grant, gồm `kit_schema_migrations` khi Q2 thấy quan hệ và grant `filename` đã được cấp. Trên `kit_accounts`, `refresh_tokens`, `staff_otp_challenges` và `employees`, `owner_any_column_select` phải false. True trên một bảng cấm: `STOP`. Sau bước grant, `owner_any_column_select` false trên bảng phải có grant cột: `STOP`. Trước bước đó: `SETUP_INCOMPLETE`, tức `BLOCKED` đối với checklist.

`MAINTAIN` nằm ở một câu riêng và chỉ chạy khi Q8 đã có `server_version_num >= 170000`. PostgreSQL 15 và 16 không gọi từ khóa này. Phiên bản chưa là số nguyên, hoặc Q8 còn `BLOCKED`: không gọi `MAINTAIN`; phần `MAINTAIN` là `BLOCKED`. Bảy quyền false không thay kết quả `MAINTAIN` và không làm Q5 thành `PASS_SHAPE` khi phần `MAINTAIN` còn `BLOCKED`.

```sql
SELECT expected.relname,
       expected.required_relation,
       c.oid IS NOT NULL AS relation_visible,
       CASE WHEN owner_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(owner_role.oid, c.oid, 'MAINTAIN') END AS owner_maintain,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'MAINTAIN') END AS query_maintain
FROM (VALUES
    ('users', TRUE),
    ('tenants', TRUE),
    ('roles', TRUE),
    ('permissions', TRUE),
    ('role_permissions', TRUE),
    ('user_roles', TRUE),
    ('kit_accounts', FALSE),
    ('refresh_tokens', FALSE),
    ('staff_otp_challenges', FALSE),
    ('employees', FALSE),
    ('kit_schema_migrations', FALSE)
) AS expected(relname, required_relation)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_roles AS owner_role
  ON owner_role.rolname = 'famixa_preflight_owner'
LEFT JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
ORDER BY expected.relname;
```

Trên máy chủ đã xác nhận `server_version_num >= 170000`, câu này bắt buộc. Câu lỗi, kể cả lỗi từ khóa: `BLOCKED`, không ghi thành false. Quan hệ đã quan sát: cả hai cột phải false. True: `STOP`. `NULL` trên quan hệ đã quan sát: `BLOCKED`. Quan hệ bắt buộc không thấy: `NOT_OBSERVABLE`, `BLOCKED`. Quan hệ không bắt buộc không thấy: `RELATION_ABSENT`; `NULL` không phải false. Dưới `170000` và phiên bản đã là số nguyên: không chạy câu này. Việc không gọi `MAINTAIN` trên PostgreSQL 15 và 16 không phải một kết quả false. Không cấp `MAINTAIN` hay bất kỳ quyền nào khác để checklist đạt.

### Q6. `GRANT OPTION` tách khỏi `has_*_privilege`

- Role dự kiến: `famixa_canon_preflight_ro`, sau các grant và sau tám view.
- Đối tượng đọc: `pg_attribute.attacl`, `pg_class.relacl`, `pg_namespace.nspacl`, `pg_roles`.
- Quyền tối thiểu: `SELECT` trên `pg_attribute`, `pg_class`, `pg_namespace`, `pg_roles`, và `EXECUTE` trên `aclexplode(aclitem[])`. Thiếu: `BLOCKED`. Không cấp quyền catalog để đọc ACL.
- Chỉ đọc: có.
- Thời điểm: sau khi cấp quyền và sau khi tạo view. Schema `preflight_ro` chưa có thì phần schema và view là `SETUP_INCOMPLETE`.

Quyền hiệu lực và grant option là hai quan sát khác nhau. `has_column_privilege`, `has_table_privilege` và `has_schema_privilege` với quyền thuần, ví dụ `'SELECT'` hoặc `'USAGE'`, trả lời “role có dùng được quyền đó không”. Kết quả true không chứng minh có `GRANT OPTION`. Kết quả false không chứng minh vắng `GRANT OPTION` và không mô tả cờ trong ACL. Dạng `'SELECT WITH GRANT OPTION'` hoặc `'USAGE WITH GRANT OPTION'` trả lời “role có thể cấp tiếp quyền đó không”. Câu trả lời true còn xảy ra khi role là owner hoặc superuser, dù ACL không có dấu grant option cho đúng grantee. Nguyên tắc này giữ nguyên: owner có thể cấp quyền trong những trường hợp `is_grantable` không phản ánh đầy đủ quyền thực tế của owner. Nguồn cờ grant trên một dòng ACL là `aclexplode`: cột `is_grantable`. Grantee `0` là `PUBLIC`. Không gọi `pg_get_userbyid(0)`. Một kết quả Q5 không điền vào chỗ ACL chưa quan sát được.

ACL `NULL` không có nghĩa là không có grant option. `attacl` null trên cột đã thấy nghĩa là không có ACL riêng của cột. `aclexplode` trên null không có dòng. Đó là `NO_COLUMN_ACL`. Nó không phải “đã xác minh không có grant option”, không phải “không có quyền”, và không phải `PASS_SHAPE`. `relacl` null trên quan hệ đã thấy là ACL bảng mặc định đang được quan sát dưới dạng null. Nó không phải “không có grant option”, và không được thay bằng một ACL suy diễn rồi coi là grant đã cấp. `nspacl` null trên schema đã thấy cũng vậy. Quyền hiệu lực vẫn đọc ở Q5. Grant option không được kết luận từ ACL null.

Quan hệ Q2 đã thấy, nhưng câu ACL lỗi, thiếu dòng của quan hệ đó, hoặc cờ null không đọc được: ACL không quan sát được. Kết quả là `BLOCKED` hoặc `SETUP_INCOMPLETE`. Không phải `PASS_SHAPE` và không phải “không có grant option”. Schema `preflight_ro` chưa có: phần schema và view là `SETUP_INCOMPLETE`. Không cấp quyền catalog để đọc ACL.

Ba câu dưới đây chỉ quan sát ACL có null hay không. `CASE` trả `NULL` khi quan hệ, cột hoặc schema không có OID. `NULL` đó nghĩa là chưa quan sát được ACL, không nghĩa là ACL null.

```sql
SELECT expected.relname,
       expected.required_relation,
       c.oid IS NOT NULL AS relation_visible,
       CASE WHEN c.oid IS NULL THEN NULL ELSE c.relacl IS NULL END AS relacl_is_null
FROM (VALUES
    ('users', TRUE),
    ('tenants', TRUE),
    ('roles', TRUE),
    ('permissions', TRUE),
    ('role_permissions', TRUE),
    ('user_roles', TRUE),
    ('kit_accounts', FALSE),
    ('refresh_tokens', FALSE),
    ('staff_otp_challenges', FALSE),
    ('employees', FALSE),
    ('kit_schema_migrations', FALSE)
) AS expected(relname, required_relation)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'public'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
ORDER BY expected.relname;
```

```sql
SELECT c.relname,
       a.attname,
       CASE WHEN a.attnum IS NULL THEN NULL ELSE a.attacl IS NULL END AS attacl_is_null
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
JOIN pg_catalog.pg_attribute AS a ON a.attrelid = c.oid
WHERE n.nspname = 'public'
  AND c.relname IN (
      'tenants', 'roles', 'permissions', 'role_permissions',
      'user_roles', 'users', 'kit_schema_migrations',
      'kit_accounts', 'refresh_tokens', 'staff_otp_challenges', 'employees'
  )
  AND a.attnum > 0
  AND NOT a.attisdropped
ORDER BY c.relname, a.attnum;
```

```sql
SELECT expected.nspname,
       n.oid IS NOT NULL AS schema_visible,
       CASE WHEN n.oid IS NULL THEN NULL ELSE n.nspacl IS NULL END AS nspacl_is_null
FROM (VALUES ('preflight_ro')) AS expected(nspname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = expected.nspname;
```

Câu lỗi: `BLOCKED` hoặc `SETUP_INCOMPLETE`. Quan hệ bắt buộc không thấy: `NOT_OBSERVABLE`, `BLOCKED` cho kết luận ACL. Quan hệ không bắt buộc không thấy: `RELATION_ABSENT`. `relacl_is_null` true trên quan hệ đã thấy: đã quan sát ACL null, không kết luận grant option. Quan hệ Q2 đã thấy mà không có dòng trong câu `relacl`: `BLOCKED`. Câu cột chỉ trả cột còn tồn tại. Quan hệ Q2 đã thấy mà không có dòng cột: `BLOCKED`, ACL cột không quan sát được. Không gọi `has_column_privilege` cho cột không có. `schema_visible` false: `SETUP_INCOMPLETE`. `nspacl_is_null` true: đã quan sát ACL null, không kết luận grant option.

`PASS_SHAPE` của một cột trong ma trận grant chỉ đứng khi câu quan sát đã thấy cột, `attacl_is_null` false, và `aclexplode` của chính ACL đó có một dòng `privilege_type = 'SELECT'`, grantee là `famixa_preflight_owner`, `is_grantable = false`, không có privilege khác cho cùng grantee. Một dòng `REFERENCES` cho cùng grantee là privilege khác: `STOP`. Q5 thấy `REFERENCES` cột true cũng là `STOP`, kể cả khi câu ACL không có dòng đó. `relacl` của cùng bảng, khi `relacl_is_null` false, không có dòng `SELECT` cho owner này. Role truy vấn không có dòng trong `attacl` hoặc `relacl` của các bảng `public` thuộc Q5 chỉ là một điều kiện bên trong hình đó, và chỉ khi ACL tương ứng đã quan sát được và không null. `PUBLIC` không có `SELECT` trên các bảng và cột đó. Thiếu dòng grantee bắt buộc không phải `PASS_SHAPE`. ACL null hoặc ACL không quan sát được không phải `PASS_SHAPE`.

`PASS_SHAPE` của view chỉ đứng khi đồng thời đủ các điều kiện sau. `relacl` của view đã quan sát được và không null. Grantee `famixa_canon_preflight_ro` có đúng một quyền `SELECT`, `is_grantable = false`, tức không có `GRANT OPTION`. Cùng grantee không có `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES` hay `TRIGGER`. `PUBLIC` không có dòng trên view. Câu quyền hiệu lực thấy `SELECT` true, sáu quyền `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `REFERENCES` và `TRIGGER` đều false, và `SELECT WITH GRANT OPTION` false. Khi Q8 đã có `server_version_num >= 170000`, câu `MAINTAIN` riêng thấy false, và ACL không có `MAINTAIN` cho role truy vấn hoặc cho `PUBLIC`. Bốn quyền `INSERT`, `UPDATE`, `DELETE` và `TRUNCATE` false không đủ để thành `PASS_SHAPE`. Thiếu một quan sát trong danh sách, boolean `NULL`, hoặc câu lỗi: `BLOCKED`. Quyền đã quan sát mà trái danh sách, hoặc `is_grantable` true trên dòng `SELECT` của role truy vấn: `STOP`.

PostgreSQL 15 và 16 không gọi `MAINTAIN`. Việc không gọi không phải false và không tự hoàn thành `PASS_SHAPE`; các điều kiện còn lại vẫn phải đứng. Phiên bản chưa là số nguyên, hoặc Q8 còn `BLOCKED`: không gọi `MAINTAIN`, phần `MAINTAIN` là `BLOCKED`, và view chưa thành `PASS_SHAPE`. Không gọi từ khóa đó trên các phiên bản chưa hỗ trợ để đổi lỗi thành false.

Thiếu dòng grantee bắt buộc không phải `PASS_SHAPE`. Dòng owner của chính view có `is_grantable` true là ACL owner mặc định, vì owner sở hữu view. Dòng đó không bị đọc thành “owner không có quyền cấp”. Nó cũng không thay điều kiện grant option của role truy vấn. View không thấy: `NOT_OBSERVABLE`, kết luận checklist `BLOCKED`.

`PASS_SHAPE` của schema `preflight_ro` chỉ đứng khi schema đã thấy, `nspacl_is_null` false, role truy vấn có `USAGE`, `is_grantable = false`, không có `CREATE`, và `PUBLIC` không có dòng. Thiếu dòng grantee bắt buộc không phải `PASS_SHAPE`. Dòng owner của schema là quyền sở hữu. `is_grantable` trên dòng đó không phải bằng chứng rằng owner hết quyền cấp.

Các câu `aclexplode` bên dưới lọc `IS NOT NULL` và lọc grantee. Không có dòng từ các câu đó không đủ để kết luận không có grant option. Kết quả rỗng có thể là ACL null, grantee không khớp bộ lọc, quan hệ không thấy, hoặc câu chưa quan sát được ACL. Không suy “không có grant option” chỉ từ kết quả rỗng.

```sql
SELECT c.relname,
       a.attname,
       x.grantee,
       CASE WHEN x.grantee = 0 THEN 'PUBLIC' ELSE grantee_role.rolname END AS grantee_name,
       x.privilege_type,
       x.is_grantable
FROM pg_catalog.pg_attribute AS a
JOIN pg_catalog.pg_class AS c ON c.oid = a.attrelid
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
CROSS JOIN LATERAL aclexplode(a.attacl) AS x
LEFT JOIN pg_catalog.pg_roles AS grantee_role ON grantee_role.oid = x.grantee
WHERE n.nspname = 'public'
  AND c.relname IN (
      'tenants', 'roles', 'permissions', 'role_permissions',
      'user_roles', 'users', 'kit_schema_migrations'
  )
  AND a.attnum > 0
  AND NOT a.attisdropped
  AND a.attacl IS NOT NULL
  AND (
      x.grantee = 0
      OR grantee_role.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
  )
ORDER BY c.relname, a.attname, grantee_name, x.privilege_type;
```

```sql
SELECT n.nspname,
       c.relname,
       x.grantee,
       CASE WHEN x.grantee = 0 THEN 'PUBLIC' ELSE grantee_role.rolname END AS grantee_name,
       x.privilege_type,
       x.is_grantable
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
CROSS JOIN LATERAL aclexplode(c.relacl) AS x
LEFT JOIN pg_catalog.pg_roles AS grantee_role ON grantee_role.oid = x.grantee
WHERE c.relacl IS NOT NULL
  AND (
      (n.nspname = 'public' AND c.relname IN (
          'tenants', 'roles', 'permissions', 'role_permissions',
          'user_roles', 'users', 'kit_schema_migrations',
          'kit_accounts', 'refresh_tokens', 'staff_otp_challenges', 'employees'
      ))
      OR (n.nspname = 'preflight_ro' AND c.relkind = 'v')
  )
  AND (
      x.grantee = 0
      OR grantee_role.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
  )
ORDER BY n.nspname, c.relname, grantee_name, x.privilege_type;
```

```sql
SELECT n.nspname,
       x.grantee,
       CASE WHEN x.grantee = 0 THEN 'PUBLIC' ELSE grantee_role.rolname END AS grantee_name,
       x.privilege_type,
       x.is_grantable
FROM pg_catalog.pg_namespace AS n
CROSS JOIN LATERAL aclexplode(n.nspacl) AS x
LEFT JOIN pg_catalog.pg_roles AS grantee_role ON grantee_role.oid = x.grantee
WHERE n.nspname = 'preflight_ro'
  AND n.nspacl IS NOT NULL
  AND (
      x.grantee = 0
      OR grantee_role.rolname IN ('famixa_preflight_owner', 'famixa_canon_preflight_ro')
  );
```

Đối chiếu khả năng cấp tiếp, chỉ sau khi `rolsuper` false và Q2 đã thấy role không sở hữu bảng nền:

```sql
SELECT c.relname,
       a.attname,
       has_column_privilege(owner_role.oid, c.oid, a.attnum, 'SELECT WITH GRANT OPTION') AS owner_can_grant_column
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
JOIN pg_catalog.pg_attribute AS a ON a.attrelid = c.oid
JOIN pg_catalog.pg_roles AS owner_role
  ON owner_role.rolname = 'famixa_preflight_owner'
WHERE n.nspname = 'public'
  AND c.relname IN (
      'tenants', 'roles', 'permissions', 'role_permissions',
      'user_roles', 'users', 'kit_schema_migrations'
  )
  AND a.attnum > 0
  AND NOT a.attisdropped;
```

```sql
SELECT c.relname,
       has_table_privilege(query_role.oid, c.oid, 'SELECT WITH GRANT OPTION') AS query_can_grant_view
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
WHERE n.nspname = 'preflight_ro'
  AND c.relkind = 'v';
```

```sql
SELECT has_schema_privilege(query_role.oid, n.oid, 'USAGE WITH GRANT OPTION') AS query_can_grant_usage
FROM pg_catalog.pg_namespace AS n
JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
WHERE n.nspname = 'preflight_ro';
```

Một cột grant mà ACL cột đã quan sát được, `attacl_is_null` false, và không có dòng `SELECT` cho owner: `STOP` nếu Q5 thấy owner column `SELECT` true, vì quyền đang đến từ nơi khác; `STOP` cả khi Q5 thấy false, vì grant cột chưa có trong ACL đã đọc. ACL null hoặc ACL không quan sát được: `BLOCKED` hoặc `SETUP_INCOMPLETE`, không đổi thành `STOP` của một kết quả rỗng và không đổi thành `PASS_SHAPE`. `is_grantable` true trên dòng grant của owner ở bảng nền, hoặc của role truy vấn ở view hay schema: `STOP`.

Hàm `WITH GRANT OPTION` true trong khi `is_grantable` false không bị gộp thành “không có grant option” và không phải `PASS_SHAPE`. Owner của đối tượng vẫn có thể cấp quyền khi `is_grantable` không phản ánh đầy đủ quyền thực tế của owner. Với role không sở hữu đối tượng, hàm true nghĩa là còn một đường cấp quyền ngoài cờ của dòng ACL đó; hình grant option của grant preflight trên đối tượng đó không phải `PASS_SHAPE`. Hàm false trong khi `is_grantable` true: `STOP`, hai quan sát không khớp. Không dùng một mình hàm `has_*_privilege` để kết luận hết grant option. Không dùng một mình `is_grantable` khi Q2 còn `BLOCKED`. Không dùng một mình kết quả ACL rỗng để kết luận không có grant option.

Câu quyền hiệu lực của tám view không chứa từ khóa `MAINTAIN`. `REFERENCES` và `TRIGGER` được gọi trên PostgreSQL 15 trở lên; không cần `server_version_num >= 170000` để gọi hai quyền đó. Câu `SELECT WITH GRANT OPTION` phía trên nối trong `JOIN`, nên view không thấy sẽ biến mất khỏi kết quả. Sự vắng mặt đó không phải quyền false. Câu dưới đây giữ đủ tám tên.

```sql
SELECT expected.relname,
       c.oid IS NOT NULL AS view_visible,
       c.relkind,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'SELECT') END AS query_select,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'INSERT') END AS query_insert,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'UPDATE') END AS query_update,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'DELETE') END AS query_delete,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'TRUNCATE') END AS query_truncate,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'REFERENCES') END AS query_references,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'TRIGGER') END AS query_trigger,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'SELECT WITH GRANT OPTION') END AS query_select_grant_option
FROM (VALUES
    ('db_identity'),
    ('constraints_expected'),
    ('kit_mkt_tenants'),
    ('kit_mkt_admin_roles'),
    ('canon_permission'),
    ('canon_grants'),
    ('kit_mkt_admin_users'),
    ('ledger_386')
) AS expected(relname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'preflight_ro'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
ORDER BY expected.relname;
```

Câu lỗi: `BLOCKED`, không ghi thành false. Ít hơn tám dòng: `BLOCKED`. `view_visible` false: `NOT_OBSERVABLE`, kết luận view đó `BLOCKED`. `relkind` khác `v` khi đã thấy quan hệ: `STOP`. Thiếu OID role truy vấn: `BLOCKED`. Boolean `NULL` khi view và role còn OID: `BLOCKED`. `NULL` không phải quyền false. `query_references` hoặc `query_trigger` true: `STOP`. `query_select_grant_option` true: `STOP`. `query_select` false: `STOP`. Bốn cột `query_insert`, `query_update`, `query_delete` và `query_truncate` false, trong khi `REFERENCES`, `TRIGGER` hoặc grant option còn `NULL` hay chưa đọc: không phải `PASS_SHAPE`.

Câu `MAINTAIN` của view chỉ chạy khi Q8 đã có `server_version_num >= 170000`. PostgreSQL 15 và 16 không chạy câu này. Phiên bản chưa là số nguyên, hoặc Q8 còn `BLOCKED`: không chạy; phần `MAINTAIN` là `BLOCKED`.

```sql
SELECT expected.relname,
       c.oid IS NOT NULL AS view_visible,
       CASE WHEN query_role.oid IS NULL OR c.oid IS NULL THEN NULL
            ELSE has_table_privilege(query_role.oid, c.oid, 'MAINTAIN') END AS query_maintain
FROM (VALUES
    ('db_identity'),
    ('constraints_expected'),
    ('kit_mkt_tenants'),
    ('kit_mkt_admin_roles'),
    ('canon_permission'),
    ('canon_grants'),
    ('kit_mkt_admin_users'),
    ('ledger_386')
) AS expected(relname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'preflight_ro'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_roles AS query_role
  ON query_role.rolname = 'famixa_canon_preflight_ro'
ORDER BY expected.relname;
```

Trên máy chủ đã xác nhận `server_version_num >= 170000`, câu này bắt buộc. Câu lỗi, kể cả lỗi từ khóa: `BLOCKED`, không ghi thành false. View đã thấy: `query_maintain` false. True: `STOP`. `NULL` khi view đã thấy: `BLOCKED`. View không thấy: `NOT_OBSERVABLE`, `BLOCKED`. Dưới `170000` và phiên bản đã là số nguyên: không chạy câu này. Việc không gọi `MAINTAIN` không phải một kết quả false. Sáu quyền kia false không thay phần `MAINTAIN` còn `BLOCKED`. Không cấp `MAINTAIN` để checklist đạt. Quyền sở hữu của owner trên view không được đọc thành grant `MAINTAIN` của role truy vấn.

Ba câu sau quan sát mục 5 điều 8 và điều 10. Chúng không đổi tám định nghĩa view đã được R82 chấp thuận.

```sql
SELECT expected.relname,
       c.oid IS NOT NULL AS view_visible,
       c.relkind,
       owner_role.rolname AS owner_name,
       c.reloptions
FROM (VALUES
    ('db_identity'),
    ('constraints_expected'),
    ('kit_mkt_tenants'),
    ('kit_mkt_admin_roles'),
    ('canon_permission'),
    ('canon_grants'),
    ('kit_mkt_admin_users'),
    ('ledger_386')
) AS expected(relname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'preflight_ro'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
LEFT JOIN pg_catalog.pg_roles AS owner_role ON owner_role.oid = c.relowner
ORDER BY expected.relname;
```

Câu lỗi hoặc ít hơn tám dòng: `BLOCKED`. `view_visible` false: `NOT_OBSERVABLE`, kết luận điều 8 `BLOCKED`. `relkind` khác `v` khi đã thấy: `STOP`. `owner_name` null khi view đã thấy: `BLOCKED`. `owner_name` khác `famixa_preflight_owner`: `STOP`. `reloptions` null, hoặc thiếu `security_barrier=true`, hoặc thiếu `security_invoker=false`, khi view đã thấy: `STOP`. Đây là giá trị catalog đã đọc, không phải boolean quyền `NULL`.

```sql
SELECT expected.relname,
       c.oid IS NOT NULL AS view_visible,
       c.relkind,
       CASE
           WHEN c.oid IS NULL OR c.relkind IS DISTINCT FROM 'v' THEN NULL
           ELSE pg_get_viewdef(c.oid)
       END AS view_definition
FROM (VALUES
    ('db_identity'),
    ('constraints_expected'),
    ('kit_mkt_tenants'),
    ('kit_mkt_admin_roles'),
    ('canon_permission'),
    ('canon_grants'),
    ('kit_mkt_admin_users'),
    ('ledger_386')
) AS expected(relname)
LEFT JOIN pg_catalog.pg_namespace AS n ON n.nspname = 'preflight_ro'
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relnamespace = n.oid
 AND c.relname = expected.relname
ORDER BY expected.relname;
```

Hàm chỉ được gọi khi quan hệ đã thấy và `relkind = 'v'`. Câu lỗi, thiếu `EXECUTE` trên `pg_get_viewdef(oid)`, hoặc ít hơn tám dòng: `BLOCKED`. `view_visible` false: `NOT_OBSERVABLE`, kết luận điều 10 `BLOCKED`. `view_definition` null khi view đã thấy: `BLOCKED`. Không có văn bản định nghĩa không chứng minh định nghĩa sạch, và không chứng minh định nghĩa thiếu một chuỗi cấm.

Với định nghĩa đã đọc: có `password_hash`, `email`, `employee_id`, `kit_account_id`, `token_hash`, `code_hash`, `phone`, `kit_accounts`, `refresh_tokens`, `staff_otp_challenges`, `employees`, `applied_at`, hoặc `users.tenant_id` là `STOP`. `db_identity` có nhánh `UNION ALL` cùng `WHERE false`: `STOP`. Một trong bảy view còn lại thiếu `UNION ALL` hoặc thiếu nhánh `WHERE false`: `STOP`. Định nghĩa `ledger_386` đạt hình đã chốt khi chứa đúng tên file `386_pack_content_famixa_canon_administer.sql`, hoặc khi không nhắc `kit_schema_migrations`. Nhắc `kit_schema_migrations` mà không chứa đúng tên file đó: `STOP`. Câu này không sửa view.

### Q7. Ngưỡng `KIT_MKT`, permission, grant và user ADMIN

- Role dự kiến: `famixa_canon_preflight_ro`.
- Đối tượng đọc: `preflight_ro.kit_mkt_tenants`, `preflight_ro.kit_mkt_admin_roles`, `preflight_ro.canon_permission`, `preflight_ro.canon_grants`, `preflight_ro.kit_mkt_admin_users`.
- Quyền tối thiểu: `USAGE` trên schema `preflight_ro` và `SELECT` trên năm view. Không đọc bảng nền bằng role này. Lỗi quyền trên bảng nền, nếu có ai nối thêm, là ranh giới quyền đúng đặc tả; không cấp `SELECT` bảng để đối chiếu.
- Chỉ đọc: có.
- Thời điểm: sau khi view tồn tại. Q2 của các bảng mà view đọc phải đã có kết quả. Q2 `BLOCKED` thì mọi kết luận “có” hoặc “không có” của Q7 là `BLOCKED`.

Nguồn migration, manifest và ledger không phải ngưỡng. `ledger_386.filename_recorded` không chứng minh migration đã chạy hay chưa chạy. Số dòng dưới đây không được so với file SQL trong repo. Checklist quyền ở Q5 có thể `PASS_SHAPE` trong khi một view dữ liệu là `DATA_ABSENT`. `DATA_ABSENT` không phải lỗi quyền.

Bảng RLS của từng view: `kit_mkt_tenants` đọc `tenants`; `kit_mkt_admin_roles` đọc `roles`, `tenants`; `canon_permission` đọc `permissions`; `canon_grants` đọc `role_permissions`, `permissions`, `roles`, `tenants`; `kit_mkt_admin_users` đọc `users`, `user_roles`, `roles`, `tenants`. “RLS tắt” nghĩa là Q2 thấy `relation_visible` true, `relkind = 'r'` và `relrowsecurity = false` trên mọi bảng trong nhóm. Một bảng `relrowsecurity` true, hoặc Q2 chưa đọc được bảng đó: nhóm ở trạng thái RLS không kết luận được sự vắng mặt.

```sql
SELECT id, tenant_code, status, deleted_at
FROM preflight_ro.kit_mkt_tenants;
```

```sql
SELECT role_id, tenant_id, role_status, tenant_status, tenant_deleted_at
FROM preflight_ro.kit_mkt_admin_roles;
```

```sql
SELECT id, permission_code, permission_name, module_name
FROM preflight_ro.canon_permission;
```

```sql
SELECT grant_id, tenant_id, tenant_code, tenant_status, tenant_deleted_at,
       role_id, role_code, role_status
FROM preflight_ro.canon_grants;
```

```sql
SELECT user_id, username, user_status, user_deleted_at, role_id, role_status
FROM preflight_ro.kit_mkt_admin_users;
```

Lỗi câu `SELECT`, gồm `permission denied`: `BLOCKED`. Không cấp `BYPASSRLS` và không cấp thêm cột. Không dòng do view lỗi không phải số đếm không.

Ngưỡng khi RLS tắt:

| View | 0 dòng | 1 dòng | Nhiều hơn 1 dòng |
|---|---|---|---|
| `kit_mkt_tenants` | `DATA_ABSENT` | `OBSERVED` | `STOP` |
| `canon_permission` | `DATA_ABSENT` | `OBSERVED` | `STOP` |
| `kit_mkt_admin_roles` | `DATA_ABSENT` | `OBSERVED` | `STOP` |
| `canon_grants` | `DATA_ABSENT` | `OBSERVED` | `OBSERVED`, không có trần |
| `kit_mkt_admin_users` | `DATA_ABSENT` | `OBSERVED` | `OBSERVED` |

`STOP` ở ba view đầu là vì unique constraint đã chốt trong R82-ADR-08 không cho hai `tenant_code`, hai `permission_code`, hoặc hai `role_code` ADMIN trong cùng tenant. Nếu `constraints_expected` chưa đạt điều kiện R82-ADR-08, số dòng lớn hơn một vẫn là `STOP` đối với kết luận “đúng một đối tượng”. `canon_grants` cố ý hiện mọi tenant. Tenant khác `KIT_MKT` không phải `STOP`. `grant_id` lặp: `STOP`. Dòng toàn null: `STOP`, vì nhánh `WHERE false` không được trả dòng.

`kit_mkt_tenants` một dòng với `deleted_at` không null: `OBSERVED_DELETED`. Đó không phải tenant còn sống. `kit_mkt_admin_users` phải không dòng khi đó, vì view lọc `tenants.deleted_at IS NULL`. Vẫn thấy user: `STOP`. Giá trị `status` được ghi lại; đặc tả không đặt mã status bắt buộc.

Kết luận “tenant `KIT_MKT` còn sống tồn tại” cần một dòng `kit_mkt_tenants`, `deleted_at` null, RLS tắt trên `tenants`. Kết luận “permission `content.canon.administer` tồn tại” cần đúng một dòng `canon_permission`, RLS tắt trên `permissions`. Kết luận “có grant” cần ít nhất một dòng `canon_grants`, RLS tắt trên cả bốn bảng của view đó; số dòng không chứng minh migration. Kết luận “có user ADMIN của `KIT_MKT` còn sống” cần ít nhất một dòng `kit_mkt_admin_users` với `user_id` không null, RLS tắt trên `users`, `user_roles`, `roles` và `tenants`. Không dòng ở trạng thái RLS tắt là `DATA_ABSENT`: không có dòng nhìn thấy, và checklist không bị thất bại chỉ vì tập user rỗng.

Khi RLS không kết luận được sự vắng mặt: không dòng là `NOT_OBSERVABLE`, nên kết luận tồn tại là `BLOCKED`. Có dòng là quan sát không đầy đủ, vì policy có thể đã giấu dòng khác; kết luận “đây là toàn bộ” là `BLOCKED`. Nhiều hơn một dòng ở ba view có trần ở bảng trên vẫn là `STOP`.

`kit_mkt_tenants` là `DATA_ABSENT` trong khi một view ADMIN có dòng, và RLS tắt trên mọi bảng liên quan: `STOP`, các quan sát không khớp.

### Q8. `server_version_num`

- Role dự kiến: bất kỳ phiên đã kết nối. Ghi nhãn phiên bằng Q10. Checklist lặp lại bằng `famixa_canon_preflight_ro`.
- Đối tượng đọc: GUC `server_version_num`. Không đọc bảng nghiệp vụ.
- Quyền tối thiểu: quyền đọc GUC này của một role thường. Lỗi quyền: `BLOCKED`. Không cấp `has_parameter_privilege` và không kết nạp `pg_read_all_settings`.
- Chỉ đọc: có.
- Thời điểm: trước khi tạo role và view. Đây là điều kiện để có được tiếp tục thiết kế view. `preflight_ro.db_identity.server_version` là chuỗi; nó không thay câu số này. Định nghĩa view đã chốt không bị sửa để thêm cột.

```sql
SELECT current_setting('server_version_num')::integer AS server_version_num;
```

Lỗi hoặc kết quả không phải số nguyên: `BLOCKED`. `server_version_num < 150000`: `STOP`. `security_invoker` có từ PostgreSQL 15, và view đã chốt cần reloption đó. Không bỏ reloption để đi tiếp. `server_version_num >= 150000`: điều kiện phiên bản cần thiết đã đủ để xét tiếp; nó chưa chứng minh `CREATE VIEW` sẽ chấp nhận reloption. Việc `CREATE VIEW` từ chối reloption vẫn xử lý theo mục 1: gỡ view `preflight_ro` vừa tạo trong lần thử đó, không tạo lại theo mặc định khác. Bước `CREATE VIEW` là thiết lập, không thuộc phiên chỉ đọc. `server_version_num >= 160000`: Q1 dùng câu đi đường có `set_option` và câu `pg_has_role(..., 'SET')`; Q10 mới gọi `system_user`. Từ `150000` đến dưới `160000`: Q1 dùng câu đi đường không có `set_option` và câu `pg_has_role(..., 'MEMBER')`, không gọi `'SET'`; Q10 không gọi `system_user`. Từ `170000`: Q5 và Q6 mới gọi quyền `MAINTAIN`. Dưới `170000`: Q5 và Q6 không gọi `MAINTAIN`. Phiên bản chưa là số nguyên: đối chiếu `pg_has_role` của Q1, phần `MAINTAIN` của Q5 và của Q6, và `system_user` của Q10 đều `BLOCKED`. Không gọi cú pháp của phiên bản cao hơn để đổi các mục đó thành false.

### Q9. `pg_hba` không cần đọc file hệ điều hành và không cần superuser mới

- Role dự kiến của đầu dò quyền: `famixa_canon_preflight_ro`.
- Role dự kiến của nội dung rule: `ADMIN_SESSION`, tức superuser sẵn có. Không tạo superuser, không `GRANT` role này cho một trong hai role preflight, không cấp `pg_read_all_settings`. Role `pg_read_all_settings` không phải cách đọc view này trong đặc tả, và không được kết nạp để mở view.
- Đối tượng đọc ở phiên chỉ đọc: dòng `pg_class` của `pg_catalog.pg_hba_file_rules`, nếu catalog đọc được, cộng `has_table_privilege`. Phiên chỉ đọc không `SELECT` từ view đó.
- Đối tượng đọc ở `ADMIN_SESSION`: `pg_catalog.pg_hba_file_rules`.
- Quyền tối thiểu của phiên chỉ đọc: `SELECT` trên `pg_class` nếu câu đầu dò chạy được. Không đủ: nội dung rule vẫn là việc của `ADMIN_SESSION`, và đầu dò là `BLOCKED`.
- Chỉ đọc: cả hai câu đều là `SELECT`. Câu nội dung không thuộc phiên chỉ đọc.
- Thời điểm: đầu dò sau khi role truy vấn đăng nhập. Nội dung file là bước `ADMIN_SESSION`. Có thể làm trước khi tạo view. Đọc được một dòng có địa chỉ `127.0.0.1/32` không biến dòng đó thành rule hiệu lực.

View `pg_hba_file_rules` mặc định chỉ superuser đọc được. View phản ánh nội dung file hiện tại mà tiến trình server đọc được, gồm file được `include` hoặc `include_dir` kéo vào. Nội dung đó không tự chứng minh cấu hình đã được nạp vào tiến trình đang chạy. Phiên chỉ đọc không đọc file trên đĩa, không gọi `pg_read_file`, không nhận `SELECT` trên view này, và không được mở rộng quyền hay cấp thêm quyền catalog để đọc file cấu hình.

```sql
SELECT c.oid IS NOT NULL AS hba_view_visible,
       CASE
           WHEN c.oid IS NULL OR query_role.oid IS NULL THEN NULL
           ELSE has_table_privilege(query_role.oid, c.oid, 'SELECT')
       END AS query_can_read_hba
FROM pg_catalog.pg_roles AS query_role
LEFT JOIN pg_catalog.pg_class AS c
  ON c.relname = 'pg_hba_file_rules'
 AND c.relnamespace = (SELECT oid FROM pg_catalog.pg_namespace WHERE nspname = 'pg_catalog')
WHERE query_role.rolname = 'famixa_canon_preflight_ro';
```

`PASS_SHAPE` của đầu dò: một dòng, `query_can_read_hba = false`. `true`: `STOP`, role chỉ đọc đang thấy cấu hình xác thực; không dùng khả năng đó để hoàn thành kiểm tra, và không giữ grant đó như một phần của thiết kế. `NULL` hoặc câu lỗi: `BLOCKED` cho đầu dò. Việc không đọc được view ở phiên chỉ đọc là ranh giới quyền mong muốn khi giá trị là false; nó không chứng minh nội dung `pg_hba` an toàn, và không thay câu `ADMIN_SESSION`.

`ADMIN_SESSION` xác nhận `rolsuper` của `session_user` bằng `pg_roles` trước. `rolsuper` false: `BLOCKED`, không nâng quyền. `rolsuper` true trên phiên sẵn có thì mới đọc:

```sql
SELECT line_number, type, database, user_name, address, netmask, auth_method, error
FROM pg_catalog.pg_hba_file_rules
ORDER BY line_number;
```

Câu này lỗi: `BLOCKED`. Không cấp `SELECT` trên `pg_hba_file_rules` cho role preflight, không tạo superuser mới, và không cấp thêm quyền catalog để đọc file cấu hình. Bất kỳ dòng nào có `error` không null: `STOP`, file có dòng server không nhận. `ORDER BY line_number` chỉ là thứ tự hiển thị của kết quả. Nó không phải thứ tự rule khớp.

`pg_hba_file_rules` không chỉ ra rule khớp đầu tiên của một kết nối, và không chứng minh nội dung file đang có hiệu lực trong tiến trình. Không suy ra rule khớp đầu tiên từ `line_number`, từ dòng có `address = 127.0.0.1`, hoặc từ một tập dòng đã lọc. Một dòng có địa chỉ `127.0.0.1/32` không vì thế mà là rule hiệu lực. Điều kiện SSL và GSS, loại kết nối, database, user, địa chỉ, thứ tự rule, `include`, `include_dir`, và các kiểu đối sánh liên quan có thể làm đổi rule nào khớp. Các kiểu đó gồm nhóm role, `all`, `sameuser`, `samerole`, `samehost`, `samenet` và hostname. Tập cột của câu trên không đủ để loại các kiểu đó.

Kết luận về rule `pg_hba` hiệu lực giữ `BLOCKED` khi chưa có bằng chứng đủ để xác định đồng thời cấu hình đã nạp vào tiến trình đang chạy và rule khớp đầu tiên. `PASS_SHAPE` của đầu dò không đổi kết luận đó. Không mở rộng quyền catalog và không thêm quyền đọc file, gồm `pg_read_file`, `SELECT` trên `pg_hba_file_rules` cho role preflight, hay `pg_read_all_settings`, để vượt qua điều kiện này. Q10 chưa xác nhận được `database_name`: không xét khớp database, kết luận rule hiệu lực vẫn `BLOCKED`. Không thêm dòng `pg_hba` trong đặc tả này.

Đăng nhập thành công của `famixa_canon_preflight_ro` không thay bằng chứng về rule hiệu lực và không chỉ ra rule nào. Đăng nhập thất bại không chỉ ra rule nào đã từ chối. `client_addr` là `127.0.0.1` cũng không thay bằng chứng đó. Cả ba quan sát đều không biến một dòng `127.0.0.1/32` thành rule hiệu lực và không thay `pg_hba_file_rules`.

### Q10. Danh tính phiên

- Role dự kiến: mọi phiên dùng trong preflight. Phiên checklist phải là `famixa_canon_preflight_ro`. Phiên thiết lập giữ nhãn `ADMIN_SESSION` khi `session_user` là superuser sẵn có.
- Đối tượng đọc: hàm danh tính phiên. Không đọc bảng nghiệp vụ và không đọc `api.env` hay `secrets.generated`.
- Quyền tối thiểu: quyền của một role đã kết nối. `current_database`, `inet_server_addr`, `inet_server_port`, `inet_client_addr`, `inet_client_port`, `session_user`, `current_user` và `pg_is_in_recovery` là hàm phiên. Lỗi một hàm: `BLOCKED` cho mục đó.
- Chỉ đọc: có.
- Thời điểm: trước mọi câu khác, và lặp lại ngay sau khi role chỉ đọc đăng nhập. Phần `db_identity` chỉ chạy sau khi view tồn tại. `system_user` chỉ gọi khi Q8 đã có `server_version_num >= 160000`. Ngưỡng PostgreSQL 15 không dùng cho hàm này.

```sql
SELECT current_database()::text AS database_name,
       inet_server_addr() AS server_addr,
       inet_server_port() AS server_port,
       inet_client_addr() AS client_addr,
       inet_client_port() AS client_port,
       session_user::text AS session_user_name,
       current_user::text AS current_user_name,
       pg_is_in_recovery() AS in_recovery;
```

Câu sau chỉ chạy khi Q8 đã có `server_version_num >= 160000`. Dưới `160000`, phiên bản chưa là số nguyên, hoặc Q8 còn `BLOCKED`: không gọi `system_user`. Kết luận “đã xác thực bằng mật khẩu” khi đó là `BLOCKED`.

```sql
SELECT system_user AS authenticated_identity;
```

Cách xác nhận: người đã mở phiên đối chiếu `database_name`, `server_addr`, `server_port` và `session_user_name` với đúng bốn tham số họ đã dùng để mở phiên. Bốn tham số đó nằm ngoài tài liệu này. Khớp cả bốn mục là `IDENTITY_CONFIRMED` cho đúng phiên đó. Lệch một mục: `STOP`. Không sửa tài liệu này để ghi tên database, host, port hay tài khoản vừa nhìn thấy.

`server_addr` null hoặc `server_port` null: kết nối không phải TCP. Giá trị null không phải địa chỉ `127.0.0.1` và không phải một port đã xác nhận. Kết luận “phiên đi qua `127.0.0.1/32`” là `BLOCKED`. `client_addr` null được đọc cùng cách. `client_addr` khác `127.0.0.1`: `STOP` đối với cửa loopback. `current_user_name` khác `session_user_name`: `SET ROLE` hoặc một hàm security definer đang đổi user hiệu lực; `STOP`.

Ở phiên checklist, cả `session_user_name` và `current_user_name` phải là `famixa_canon_preflight_ro`. Một tên khác nghĩa là phiên này không phải phiên checklist; kết quả dữ liệu của nó không được ghi thành kết quả mục 5. `in_recovery` true: phiên đang ở standby; mọi kết luận về primary là `BLOCKED`.

Kết luận “đã xác thực bằng mật khẩu” chỉ được xem xét khi câu `system_user` đã chạy trên `server_version_num >= 160000` và `authenticated_identity` bắt đầu bằng `md5:` hoặc `scram-sha-256:`. Hai tiền tố đó mới đủ điều kiện để giá trị được xem là bằng chứng về phương thức xác thực mật khẩu. `NULL`, chuỗi rỗng, và mọi tiền tố khác, gồm `peer:`, `trust`, `ident:`, `gss:`, `sspi:`, `cert:`, `pam:`, `ldap:` và `radius:`, đều là `BLOCKED` đối với kết luận đó. Câu `system_user` lỗi hoặc không được gọi: `BLOCKED`. Không suy ra phương thức xác thực từ `client_addr`, từ `server_addr`, hoặc từ việc đăng nhập thành công. Không ghi mật khẩu vào kết quả. `client_addr` là `127.0.0.1` không chứng minh phương thức xác thực và không chứng minh rule hiệu lực ở Q9.

Sau khi view tồn tại, `SELECT` `preflight_ro.db_identity` phải khớp `database_name`, `server_addr`, `server_port`, `current_user_name` và `in_recovery` của câu hàm trong cùng phiên. View không có `session_user` và không có địa chỉ client; hai mục đó vẫn lấy từ câu hàm. Lệch giữa view và hàm: `STOP`.

### Ranh giới của mục 8

Mục 8 không tạo role, schema, view, grant, policy hay secret. Không đổi tám view. Không đọc `api.env` hay `secrets.generated`. Không lấy file migration, manifest hoặc ledger làm bằng chứng một migration đã chạy trên production. Không điền tên database, host, port, tài khoản kết nối hay mật khẩu mà quản trị viên chưa xác nhận ở một phiên được phê duyệt. Các câu trên chưa được chạy. `PRODUCTION_EXECUTION_APPROVED = NO`.
