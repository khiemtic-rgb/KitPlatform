# NOVIXA Staff Enablement V1 — báo cáo hoàn thiện

Ngày: 2026-10-31.

Kết luận staging: **READY_FOR_CONTROLLED_PILOT**.

Kết luận phê duyệt phát hành production của binary cây đầy đủ `f79604e6…`: **BLOCKED**. Binary đó vẫn không được đưa lên production.

Kết luận artifact Staff tương thích production, dựng từ baseline `66afd2d` cộng phần Staff tối thiểu: **READY_FOR_PRODUCTION_RELEASE_APPROVAL**. Đây là phê duyệt cho gói artifact, không phải lệnh deploy. Không phải PRODUCTION READY. Không phải PRODUCTION DEPLOYED.

| Nhãn | Trạng thái |
|---|---|
| DATABASE STAGING VERIFIED | Có. Hành trình đã chạy trên `novixa_w5_staging`, rồi dữ liệu giả đã xóa |
| STAGING SITE VERIFIED | Có. `/people/tasks` đã bấm trên `http://w5-admin.157.10.53.182.sslip.io` |
| RELEASE READY | Có. Gói đêm 412 rồi 413 đã tách khỏi runner ban ngày. Chưa chạy trên production |
| PRODUCTION DEPLOYED | Không |

## Site đã kiểm

Staging site là unit `kit-platform-w5` trên VPS B `157.10.53.182`. API `127.0.0.1:5291`, public `w5-api.157.10.53.182.sslip.io` và `w5-admin.157.10.53.182.sslip.io`. Database `novixa_w5_staging`.

Cùng máy còn `kit-platform-api` (`127.0.0.1:5000`) và Second Brain (`127.0.0.1:5095`). Chỉ restart `kit-platform-w5`. Hai unit kia giữ `active`. Health sau triển khai và sau khi xóa tenant giả: W5 200, API 200, Second Brain 200.

Không đụng host production `103.200.23.229`. Không chạy migration production. Không sao chép dữ liệu Xuân Hòa.

## Artifact

| Bản | SHA256 `KitPlatform.Api.dll` | Vị trí |
|---|---|---|
| Staff Enablement đang chạy | `f79604e6c27ba12543a2388f9fe756c1e844399144f574ce1958fc68cc17d488` | `/var/www/kit-platform-w5/api` |
| W5 đã nghiệm thu, dùng để rollback | `cc4eb7e82ae927f2f612b68c1f0ea14950fa2646486346a4137f4a7eaf6fecf7` | `/var/backups/kit-platform-w5-api-cc4eb7e8` |

Admin trước khi đổi nằm ở `/var/backups/kit-platform-w5-admin-pre-staff`. File `appsettings*.json` trên server không bị ghi đè. Bundle admin đang phục vụ site staging trỏ `w5-api.157.10.53.182.sslip.io` (`index-DHHYt1fW.js`). Không copy bundle admin này lên production. Đêm production phải build admin với `VITE_API_BASE_URL=https://api.novixa.vn`.

Rollback site staging, nếu cần, chỉ trên unit W5: copy hai thư mục backup về `/var/www/kit-platform-w5/api` và `admin`, `chown www-data`, restart `kit-platform-w5`. Không down migration. Không restart Second Brain hay `kit-platform-api`.

## Đã bấm trên UI staging

Tenant giả `STAFF_UI_PROBE`, hai người, hai ca mở `STAFFUI-A` và `STAFFUI-B`. Không dùng nhân viên hay khách production. Đã xóa sau kiểm: 0 tenant probe, 0 tenant `NT_XUANHOA` / `DEMO_PHARMACY` / `KIT_MKT` trên database này.

Quản lý (`learning.read`, `learning.write`, `success.checklist`, `success.read`):

- Mở `/people/tasks`. Giao «Giao ca A» cho nhân viên, gắn ca A. Giao «Giao ca B» cho chính quản lý, gắn ca B. Cả hai hiện trong «Việc của đội», kèm tên ca và kho.
- Việc đã xong không mở lại. API đổi về `open` trả 400, kể cả khi người gọi là quản lý.
- `/people/evaluations` mở được trang chấm tháng.

Nhân viên (`learning.read`, `success.checklist`; 0 quyền `reports.*`):

- Chỉ thấy «Giao ca A». Không thấy «Giao ca B». Không có nút Hủy và không có form giao việc.
- Bắt đầu → đang làm. Ghi «Da kiem ke tu lanh» → xong. Danh sách mở trống. Bật việc đã xong thì thấy trạng thái Xong và bằng chứng, không còn nút đổi trạng thái.
- Hủy việc của mình trả 400. Mở lại trả 400. Danh sách API chỉ có việc của mình.
- `GET /api/reports/sales/revenue-by-period` trả 403. `GET` ca mở của learning trả 403.
- `/success/cockpit` chuyển về `/success/shift-checklist`, không hiện doanh thu.
- Menu không có Báo cáo. Có mục Cockpit chủ NT, nhưng đường đó không vào số liệu chủ nhà thuốc.

Checklist hai ca:

- Khi chưa chọn ca, trang báo phải chọn ca trước khi tick. Hai ca đều có trong danh sách.
- Ca A: bắt đầu checklist mở, tick «Kiểm tra két / quỹ đầu ca», còn 1/6.
- Ca B lúc đó vẫn «Chưa làm». Bắt đầu ca B thì 0/6 và không có ô đã tick.
- Quay lại ca A vẫn 1/6.

Learning và đánh giá:

- Nhân viên mở `/people/learn`. Chương trình `pharmacy_onboarding_l0_l2` đọc được, 8 module. Chưa ghi danh nên trang báo chờ quản lý giao lộ trình.
- `GET /api/learning/evaluations` của nhân viên trả 403. Trang `/people/evaluations` hiện «Trang này dành cho chủ nhà thuốc / quản lý».
- Đánh giá của chính mình (`/api/learning/me/evaluations`) trả 200. Quản lý gọi danh sách đánh giá trả 200 và thấy form chấm tháng 10/2026.

Test database trước đó vẫn đúng: `PharmacyOperationalCompletionTests` 17 passed; một test hành trình trên `novixa_w5_staging` passed rồi xóa tenant `z%`.

## Ghi chú không chặn pilot

Nhân viên thấy mã ca dạng UUID trên thẻ việc, vì danh sách ca mở chỉ dành cho `learning.write`. Quản lý thấy «STAFFUI-A · Kho A probe». Việc vẫn gắn đúng ca. Không sửa trong lượt này.

Staging có bảng bậc nghề và 0 bậc nền. Trang bậc trống là thiếu dữ liệu, không phải lỗi giao việc. Không tạo bậc cho Xuân Hòa.

## Release đêm

Không thêm 412 và 413 vào `deploy/ubuntu/migration-files.prod.txt`. Runner ban ngày đọc manifest đó khi không có `MIGRATION_MANIFEST`, nên hai file không tự chạy lúc cửa hàng mở.

Gói riêng:

- Manifest: `deploy/ubuntu/migration-files.staff-enablement-v1.txt`
- Cửa: `deploy/ubuntu/run-staff-enablement-v1-night.sh`
- Thứ tự: `412_success_checklist_pos_shift.sql` rồi `413_learning_operational_task.sql`
- Không gồm 414–417. Không dùng `migration-files.pharmacy-ops-v2.txt` cho riêng Staff.

412 thêm `sales_shift_id`, index duy nhất theo ca khi có ca, và index legacy cho checklist cũ chưa có ca. Index một checklist mỗi ngày bị bỏ. Dòng cũ giữ nguyên. 413 tạo `pack_learning.operational_task` với người nhận bắt buộc, ca không bắt buộc, trạng thái `open` / `in_progress` / `done` / `cancelled`. Cả hai dùng `IF NOT EXISTS`. Không có down migration. Chạy lại an toàn nếu file dừng giữa chừng sau khi đã tạo object.

Script từ chối chạy nếu thiếu `STAFF_NIGHT_RELEASE=approved`. Database có tên `novixa_prod`, `kitplatform_test` hoặc `pharmacore` còn cần `CONFIRM_PRODUCTION_DATABASE=yes`. Script chưa được nối vào `scripts/run-incremental-migrations-prod.ps1` và chưa chạy.

Đêm production, chỉ sau phê duyệt riêng, nhà thuốc đóng cửa, không còn ca `status = 1`:

1. `pg_dump -Fc` `novixa_prod` ra file mode 600 ngoài thư mục app. Restore thử vào database tên mới, đếm tenant, đơn và sản phẩm, rồi drop database thử. Giữ dump. Cách làm cùng gói W5 trong `NOVIXA_W5_PRODUCTION_READINESS_V1.md`.
2. Nếu cửa sổ đã gồm 411–417 đã duyệt, 412 và 413 đi trong gói đó. Nếu chỉ Staff, chạy đúng script ở trên. Không chạy 414–417 chỉ vì Staff.
3. Deploy API đúng hash `f79604e6…`. Không ghi đè `appsettings` của server. Build admin production trỏ `https://api.novixa.vn`. Không dùng bundle `index-DHHYt1fW.js`.
4. Restart đúng service production đã được duyệt. Không restart Second Brain trong gói này.
5. Sau đó: một quản lý thấy việc của đội, một nhân viên không thấy việc người khác và không thấy doanh thu, hai ca mở thì checklist bắt chọn ca, số đơn và sản phẩm không lệch.

Dừng nếu backup không restore được, một trong hai file lỗi, hash copy lên khác `f79604e6…`, hoặc số đơn và sản phẩm lệch.

Rollback production: giữ dump, đưa lại binary cũ đã lưu hash, không chạy down migration. Cột và bảng mới không bắt buộc với binary cũ. Không xóa checklist đã có.

## Pilot tại Xuân Hòa

Chỉ mở sau bước phát hành đêm ở trên. Tài khoản và dữ liệu là của nhà thuốc, không phải tenant probe.

Nhân viên: `/people`, `/people/learn`, `/people/tasks`, `/success/shift-checklist`, `/people/recognize`.

Quản lý có `learning.write`: `/people`, `/people/tasks`, checklist theo ca, `/people/enrollments`, `/people/evaluations`, `/people/recognize`, `/people/grow`.

Việc thuộc ca chỉ khi người giao đánh dấu và chọn ca đang mở. Nhân viên không tự hủy. Việc không tự sinh khi mở ca. Việc xong không mở lại. Trang việc không hiện doanh thu cửa hàng. Nhân viên không có `reports.read`.

Nếu màn bậc nghề trống, đó là chưa có bậc trong dữ liệu. Không tự tạo bậc trong nhiệm vụ này.

## Việc còn lại trước đêm

- Phê duyệt riêng cho cửa sổ đêm. Lượt này không triển khai production.
- Tách hoặc chấp nhận bằng văn bản việc binary Staff cũng đổi đường đọc Copilot. Chi tiết ở mục phê duyệt bên dưới.
- Build admin production và ghi hash. Bundle staging không được đưa lên `api.novixa.vn`.
- Chốt tài khoản nào giữ `learning.write` nếu hiện chưa có. Không cấp quyền trong lượt này.

## Phê duyệt phát hành production — 2026-10-31

Trạng thái: **BLOCKED**. Không kết nối production, không chạy migration, không deploy.

### Kiểm kê artifact

| Artifact | SHA256 | Ghi chú |
|---|---|---|
| `artifacts/staff-enablement-v1/api-upload/KitPlatform.Api.dll` | `f79604e6c27ba12543a2388f9fe756c1e844399144f574ce1958fc68cc17d488` | Khớp bản đang chạy trên staging |
| `migrations/412_success_checklist_pos_shift.sql` | `769da094c9671abd2ed45633bdeb197fc2bb4902c778d7f72bf972f46e984d2a` | Trong manifest Staff |
| `migrations/413_learning_operational_task.sql` | `1fa3ae84c70cdfe66cc5f38d78f70932429a62c57995d221c63cca8dd52b9ea9` | Trong manifest Staff |
| Admin staging `index-DHHYt1fW.js` | `02a7e5da796fc9fd4ac27fa5039c56affc470574fb00256380b4e56b0f269548` | Trỏ `w5-api.157.10.53.182.sslip.io`. Không dùng cho production |
| Admin production | Chưa có file build | Chưa có hash |
| Rollback W5 | `cc4eb7e82ae927f2f612b68c1f0ea14950fa2646486346a4137f4a7eaf6fecf7` | `/var/backups/kit-platform-w5-api-cc4eb7e8` trên VPS B. Đây là rollback của site staging, không phải binary production hiện tại |

`client/admin/.env.production` đặt `VITE_API_BASE_URL=https://api.novixa.vn`. `resolveApiOrigin()` dùng biến này trước. Khi biến trống, host `admin.novixa.vn`, `app.novixa.vn`, `pos.novixa.vn` mới fallback về `https://api.novixa.vn`. Bundle staging đã gán biến lúc build nên không fallback.

`migration-files.prod.txt` dừng ở `371_inventory_stock_review.sql`. Không có 411–417. `scripts/run-incremental-migrations-prod.ps1` chỉ upload manifest đó. `migration-files.pharmacy-ops-v2.txt` có 411–415 và ghi không chạy trên `novixa_prod`. Script Staff không gọi file đó.

### Migration 412 rồi 413

412 phụ thuộc bảng checklist (132) và `sales_shifts` (015). Thêm `sales_shift_id`, giữ dòng cũ bằng index khi cột này null, bỏ index một checklist mỗi ngày. 413 phụ thuộc `pack_learning`, `employees`, `branches`, `sales_shifts`. Tạo `operational_task`, người nhận bắt buộc, ca không bắt buộc. Cả hai `IF NOT EXISTS`. Không có down migration. Không phụ thuộc 414–417.

Thứ tự trong manifest là 412 rồi 413. Script `run-staff-enablement-v1-night.sh` chỉ chạy khi `STAFF_NIGHT_RELEASE=approved`, và từ chối database tên production nếu thiếu `CONFIRM_PRODUCTION_DATABASE=yes`. Chưa chạy.

### Vì sao BLOCKED

`KitPlatform.Api.dll` `f79604e6…` là publish cả cây hiện tại, không phải binary chỉ có Staff. `GetKnowledgeRulesAsync` đọc `pharmacy_knowledge_rule_version`, bảng tạo ở migration 416. 416 không nằm trong gói Staff và không nằm trong manifest production. Nếu bảng chưa có, hàm bắt lỗi thiếu bảng và trả danh sách rule rỗng. Copilot trên đường gợi ý đó hết rule cho đến khi có 416. Màn duyệt và xuất bản tri thức gọi cùng bảng mà không có nhánh bắt lỗi này.

Đưa DLL này lên production cùng chỉ 412 và 413 sẽ đổi hành vi W5 dù không chạy 414–417. Gói migration thì tách đúng. Binary thì không tách được. Không sửa code W5 trong lượt chuẩn bị này.

Admin production chưa được build, nên chưa có hash để đối chiếu trước khi copy.

### Checklist đêm — chỉ sau khi hết BLOCKED và có phê duyệt riêng

Chưa thực hiện các mục này.

1. Cả hai ca bán của nhà thuốc đã đóng (`sales_shifts.status` khác 1).
2. Không có đơn mới trong ít nhất 2 giờ.
3. `pg_dump -Fc` `novixa_prod`, mode 600, ngoài thư mục app. Restore vào database tên mới, đếm tenant, đơn, sản phẩm, checklist. Drop database thử. Giữ dump.
4. Xác nhận `pack_learning` và `success_shift_checklist_run` đã có. Xác nhận 412 và 413 chưa có trong `kit_schema_migrations`.
5. Đối chiếu SHA256 file 412 và 413 với bảng trên trước khi chạy.
6. Chạy `STAFF_NIGHT_RELEASE=approved` và `CONFIRM_PRODUCTION_DATABASE=yes` với `run-staff-enablement-v1-night.sh`. Không chạy `migration-files.pharmacy-ops-v2.txt`. Không chạy 414–417.
7. Build admin với `VITE_API_BASE_URL=https://api.novixa.vn`. Ghi SHA256 file `index-*.js`. Xác nhận bundle không chứa host `w5-api`.
8. Copy API của artifact tương thích production, đối chiếu DLL với `b916eacc…`. Không copy `f79604e6…`. Không ghi đè `appsettings` của server.
9. Restart đúng service production đã được duyệt, sau khi migration xong. Không restart Second Brain.
10. Health API 200. Một quản lý thấy việc của đội. Một nhân viên không thấy việc người khác và không thấy doanh thu. Hai ca mở thì checklist bắt chọn ca. Số đơn và sản phẩm không lệch so với lần đếm trước dump.

Dừng nếu dump không restore được, hash lệch, một trong hai file lỗi, hoặc còn ca đang mở.

### Rollback — không chạy down migration

1. Giữ dump. Không xóa checklist cũ và không drop `operational_task` trên production.
2. Đưa lại binary API đã lưu từ trước cửa sổ đêm. Hash đó phải được ghi lại trước khi copy bản mới. Hash W5 `cc4eb7e8…` là bản staging, không dùng làm binary production nếu production đang là bản khác.
3. Đưa lại admin production cũ.
4. Restart đúng service đã đổi. Không restart Second Brain.
5. Health 200. Mở một checklist cũ và một đơn đã có để xác nhận còn đọc được.
6. Cột `sales_shift_id` và bảng `operational_task` có thể để lại. Binary cũ không cần chúng.

### Pilot có kiểm soát — chỉ sau đêm thành công

Một quản lý có `learning.write` và một nhân viên không có `reports.read`, trên tenant Xuân Hòa, trong một ca thật đã thống nhất trước.

- Quản lý giao một việc có chọn ca đang mở, và một việc không gắn ca. Thấy cả hai trong việc của đội.
- Nhân viên chỉ thấy việc của mình, bắt đầu, ghi bằng chứng, xong. Không hủy được. Việc xong không mở lại.
- Hai ca mở thì checklist bắt chọn ca. Tick một mục ở ca này không hiện đã tick ở ca kia.
- Nhân viên mở được học bài. Trang chấm tháng của nhân viên bị chặn. Quản lý mở được form đánh giá.
- Nhân viên không mở được báo cáo doanh thu và không ở lại Cockpit chủ.
- Cuối ca: quản lý ghi số việc giao, số việc xong, checklist nào lệch, và câu chữ nhân viên không hiểu. Không cấp thêm quyền trong lúc pilot.

Chấp nhận pilot khi năm việc trên đúng trên tài khoản Xuân Hòa. UUID trên thẻ việc của nhân viên và trang bậc trống không làm hỏng các việc đó, nhưng phải được ghi vào phản hồi.

### Chưa kiểm

- Schema production, số ca đang mở, và hai giờ không có đơn. Không được suy từ staging.
- Hash binary đang chạy trên host production. Bản rollback production chưa được lưu trong gói này.
- Ai đang có `learning.write` tại Xuân Hòa.
- Bậc nghề trên production có dòng hay không.
- Admin production chưa build.
- Hành vi Copilot sau khi cài DLL `f79604e6…` lên database chưa có bảng của migration 416.

## Artifact tương thích production — 2026-10-31

Trạng thái: **READY_FOR_PRODUCTION_RELEASE_APPROVAL**.

Nguồn dựng nằm trong `artifacts/staff-enablement-v1/prod-compatible`. Cây làm việc chính không bị checkout, reset, stash, stage, commit hay push. Không kết nối production. Không chạy migration production. Không deploy. Không restart service. Không cấp quyền.

### Baseline

Binary API đang chạy trên production, đã đo trước đó, là `KitPlatform.Api.dll` SHA256 `7bbf208b5edbbfc85e2d47aeefb0549b5af1b8ff4280eafa0c54813595906279`. File local `C:\Users\Admin\AppData\Local\Temp\novixa-rc-66afd2d9-publish\KitPlatform.Api.dll` cùng hash, và cùng hash đó có trong `novixa-rc-66afd2d9-publish.sha256.txt`.

Commit nguồn đã ghi cho lần publish đó là `66afd2d96512eaf8c0a6f533f2a571616b2781f6` (`fix(novixa): fail closed without a tenant…`, 2026-10-16). `src/` của snapshot RC khớp commit này sau khi Git chuẩn hóa xuống dòng. Không có file `src/` thừa. Mười file ngoài `src/` trong snapshot lệch commit (trang tồn kho, menu báo cáo, và năm file test). Chúng không vào DLL API. Artifact mới được archive từ commit, không lấy các lệch đó.

Commit này không phải tổ tiên của HEAD hiện tại. Vì vậy không checkout lên cây đang bẩn.

### File Staff đưa vào baseline

Giữ nguyên consultation, `ConsultationSafetyRules`, `SalesRepository` (INSERT `sales_returns` không có `client_return_id`), và `SuccessAuthorizationExtensions`. Hash blob của các file đó khớp `66afd2d`. DLL publish không chứa chuỗi `pharmacy_knowledge_rule_version` hay `client_return_id`. `KitPlatform.Infrastructure.dll` có `operational_task`.

Đưa vào:

- `OperationalTaskRules.cs`, `OperationalTaskService.cs`, `LearningOperationalTasksController.cs`
- một dòng `AddScoped<OperationalTaskService>` trong `DependencyInjection.cs`
- `ShiftChecklistShiftSelection.cs`, DTO, interface, repository, service checklist theo `sales_shift_id`
- `ShiftChecklistController`: thêm `salesShiftId`, vẫn policy `SuccessPolicies.Checklist`
- `LearningRepository`: đếm checklist theo `checked_by_user_id` (cột đã có từ migration 132)
- Admin: `/people/tasks`, API client, subnav, trang checklist chọn ca, chuỗi `success.json`
- `412_success_checklist_pos_shift.sql`, `413_learning_operational_task.sql`

Không đưa `ChecklistMutate` và `useCanMutateShiftChecklist`. Policy production vẫn cho `success.read` và `sales.write` tick checklist. Trang checklist đặt `canMutate` đúng với policy đó, không sửa `usePermission.ts`. Không đưa route tri thức, Second Brain, hay SQL `client_return_id`. Không đưa migration 411 và 414–417.

### Hash

| Artifact | SHA256 |
|---|---|
| `prod-compatible-publish/KitPlatform.Api.dll` | `b916eaccf1fb02628946bf61cb06234a400935635c2ee10d93b3b59721f0069e` |
| Admin `dist/assets/index-CaoP-gXP.js` | `9467280450441863647133a3828e1a0090e9e5dcf63857525ed020fad29bb83c` |
| `412_success_checklist_pos_shift.sql` | `769da094c9671abd2ed45633bdeb197fc2bb4902c778d7f72bf972f46e984d2a` |
| `413_learning_operational_task.sql` | `1fa3ae84c70cdfe66cc5f38d78f70932429a62c57995d221c63cca8dd52b9ea9` |

Build admin xóa `VITE_API_BASE_URL` của shell (shell đang trỏ host w5) rồi dùng `.env.production` = `https://api.novixa.vn`. JS sinh ra có `https://api.novixa.vn`. Không file JS nào chứa `w5-api.157.10.53.182.sslip.io`.

Đêm sau này đối chiếu API với `b916eacc…`, không đối chiếu với `f79604e6…`.

### Database thử

Cluster Postgres riêng, cổng 5433, thư mục `artifacts/staff-enablement-v1/pg-staff-v1`, đã stop. Database `novixa_staff_v1_compat`. Không đụng `kitplatform`, `kitplatform_test`, `pharmacore`, `postgres`, hay production.

Manifest là bản trong commit `66afd2d`: 221 file, dừng ở `371_inventory_stock_review.sql`, không gồm 230 và 231 (hai dòng đó chỉ có trên cây làm việc sau baseline). Ledger thử có 214 dòng. Có `371`, `412`, `413`. Không có `411`, `414`, `415`, `416`, `417`.

Áp manifest trên database trống không sạch hoàn toàn. File lỗi, không chặn bảng Staff / bán / tư vấn:

- `060_family_members_status_align.sql` — cột `is_active` không có. `098` vẫn backfill tên file này vào ledger.
- `090_kap_pharmacy_intelligence_seed.sql`, `102_kap_pharmacy_v1_1_telemed.sql` — lệch khóa ngoại assessment.
- `155_learning_os_schema.sql` — `GRANT` cho role `kitplatform` không tồn tại trên cluster thử. Schema `pack_learning` đã tạo trước lệnh đó.
- `158_learning_credentials_gates_l3_l5.sql` — cùng role.
- `163`, `165`, `168`, `178`, `179`, `180` — seed quiz lệch khóa ngoại module.

Sau đó chỉ áp 412 rồi 413. `sales_returns` không có cột `client_return_id`. Không có `pharmacy_knowledge_rule_version` hay `pharmacy_knowledge_adoption`. `permissions` không có `pharmacy.knowledge.approve` hay `platform.knowledge.publish` trước và sau bài test.

### Kết quả bài test

Tenant thử `STAFFV1` trên database trên. API `http://127.0.0.1:5295` từ publish mới, rồi đã tắt.

| Việc | Kết quả |
|---|---|
| Quản lý giao việc đúng nhân viên và đúng ca | 200. Ca A cho nhân viên, ca B cho chính quản lý |
| Nhân viên chỉ thấy việc của mình | Chỉ `Giao ca A` |
| Nhân viên đổi trạng thái và ghi bằng chứng | `in_progress`, bằng chứng `Da kiem ke tu lanh`, rồi `done` |
| Việc xong không mở lại | Nhân viên và quản lý đều 400 |
| Nhân viên không hủy việc | Việc của người khác 404. Việc của mình đã xong: 400 |
| Không token | 401 trên danh sách việc và trên gợi ý tư vấn |
| Nhân viên gọi open-shifts | 403 |
| Checklist cũ còn nguyên sau 412 | Dòng legacy, `sales_shift_id` null, mục vẫn tick |
| Hai ca cùng ngày | Không chọn ca thì 400. Hai run khác nhau. Tick ca A = 1, ca B = 0 |
| Gợi ý tư vấn | Sốt trả Paracetamol, reason từ `pharmacy_knowledge_rule`, không rơi câu thiếu rule |
| Luật an toàn trong bộ nhớ | Đau ngực: `blocked=true`, `refer_medical`, không có sản phẩm. File `ConsultationSafetyRules.cs` khớp commit |
| Tạo phiếu trả | 201, `SR-000001`, không cần cột `client_return_id` |
| Nhân viên không có `sales.write` gọi trả hàng | 403 |
| Route duyệt / xuất bản tri thức | 404 với và không có token. Baseline không có các controller đó |
| Audit | Hai `assign` và hai `status` trên `learning.operational_task` |

Worker Family và Content của binary baseline ghi lỗi thiếu `pack_family.parent_push_subscription` và `pack_content.work_job` vì các bảng đó không thuộc manifest production đã áp. Request Staff, bán và tư vấn vẫn trả đúng. Không thêm fallback nuốt lỗi bảng tri thức.
