# SECOND-BRAIN-V1 — MODULE REGISTRY & ACTIVATION — EXECUTION REPORT

Ngày ghi: 2026-10-17. Trading Module V1 chưa hoàn thành. PASS chỉ gắn với kiểm thử đã chạy.

## 1. Migration và manifest

FACT. Số migration trống tiếp theo là `390`. File mới: `migrations/390_platform_module_personal_intelligence.sql`. Không sửa 051, 385, 386, 387, 388, 389.

FACT. Câu lệnh dùng đúng cột của `platform_module_registry`: `module_code`, `module_name`, `description`, `verticals`, `sort_order`. `status` để mặc định 1. Seed dùng `WHERE NOT EXISTS`, cùng kiểu các đăng ký pack sau 051. Chạy lại trên `kit_pi_validation` trả `INSERT 0 0`. Một dòng `personal_intelligence` / `Personal Intelligence` / vertical `hybrid` / `sort_order` 210.

FACT. Phân loại: đây là dòng catalog của pack `personal_intelligence` trong bảng registry dùng chung, cùng kiểu `323` (KIT Sales) và `294` (Local OS). Manifest nhận file là `deploy/ubuntu/migration-files.personal-intelligence.txt`. Không đưa vào `migration-files.prod.txt`, Family OS, Content, hay Local OS. Không áp file này lên database dùng chung hoặc production.

FACT. `UpdateTenantEntitlementAsync` và `TenantPlatformSettingsService.UpdateAsync` đều gọi `kit_provision_pack_workspace` cho gói `novixa_pharmacy` sau khi ghi settings. Hàm đó chưa có trên database kiểm thử. Đã chạy `079_kit_pack_registry_workspace_party_backfill.sql` trên `kit_pi_validation` vì package `novixa_pharmacy` và các bảng workspace/subscription đã có từ 072/073. Hàm được tạo và vòng provision pharmacy chạy xong. Câu `INSERT INTO kit_common.party_party` dừng với `relation does not exist`. Phần party không nằm trên đường bật module. Không tạo schema party để vượt câu đó.

## 2. Registry và catalog

FACT. Sau 390, registry có đúng một dòng `personal_intelligence`, `status = 1`. Mã trùng `PlatformModuleCodes.PersonalIntelligence`.

FACT. `Platform_module_codes_include_all_asbuilt_catalog_entries` chạy bằng output biệt lập `UseArtifactsOutput` / `ArtifactsPath` dưới `.dev`. Passed 1, Failed 0. DLL của pid 11404 không bị ghi đè. Bài này so mảng C# với danh sách expected, trong đó có `personal_intelligence`.

FACT. Registry trên `kit_pi_validation` không bằng toàn bộ `PlatformModuleCodes.All`. Nhiều mã pack khác chưa có migration tương ứng trên database này. Lệch đã đóng là mã `personal_intelligence`.

## 3. Admin activation

FACT. Đường thêm một module chưa nằm trong `allowed_modules` là `PUT /api/platform/tenants/{id}/entitlement`. `PlatformTenantService.UpdateTenantEntitlementAsync` lọc mã qua `platform_module_registry`. Tenant ADMIN `PUT /api/system/tenant-platform` chỉ bật tắt trong trần đã cấp; không dùng đường đó để thêm mã mới.

FACT. Process Kestrel riêng, `127.0.0.1`, database `kit_pi_validation`. Khóa thiết lập chỉ ở biến môi trường của process đó. Không có header `X-Platform-Key` → HTTP 401, settings không đổi.

FACT. `PUT` hợp lệ cho tenant `PI_B`, danh sách module hiện có cộng `personal_intelligence` và mã `not_a_real_module`. Response `enabledModules` có `personal_intelligence` và không có mã giả. `GET /api/system/tenant-platform` bằng danh tính tenant `PI_B` đọc lại cùng mã. Sau đó `PUT` với danh sách cũ, không còn `personal_intelligence`. Response không còn mã này.

FACT. `TenantPlatformSettingsService` đọc `tenants.settings` mỗi lần gate. Không có cache module. Cache 30 giây chỉ áp cho role/permission trong `DbPermissionsClaimsTransformation`. Request ngay sau khi tắt module bị từ chối.

FACT. Cuối bài test, settings và `business_vertical` của `PI_B` được trả về JSON đã lưu. `PI_A` vẫn chỉ có `personal_intelligence`. Không đổi tenant ngoài hai tenant tổng hợp này.

## 4. Module gate qua HTTP

FACT. Trước khi bật: `POST /api/personal-intelligence/trading/loops` của `PI_B` → 403, câu `Module is not enabled for this tenant.`

FACT. Sau khi entitlement bật module: cùng loại request, user có quyền `personal_intelligence.trading.run`, trả kết quả vòng fixture. PostgreSQL có decision của tenant `PI_B` và execution `mode = PAPER`.

FACT. Sau khi entitlement tắt module: request mới → 403 cùng câu module. Decision của thesis đó = 0.

## 5. Trading sau khi bật

FACT. Response có `executionMode = PAPER` và `autonomy = recommend_only`. Dòng paper gắn `decision_id` và `tenant_id` của `PI_B`. Request đi qua host có `WorkspaceResolutionMiddleware`; response chỉ được controller tạo sau gate module.

## 6. Build và test

FACT. `dotnet build` API ra thư mục riêng: 0 Error. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests` với `kit_pi_validation` cổng 5433: Passed 34, Failed 0, Skipped 0. Catalog test ở trên: Passed 1. Process kiểm thử đã dừng. Thư mục output tạm đã xóa. pid 11404 vẫn chạy.

## 7. Git

FACT. Branch `main`, HEAD `8c59ceb4f3be537898106ec058ea547406f722e5`. Không stage, commit, reset, clean, stash. Hash `ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`. Không sửa middleware, Core isolation, hay RLS. `pg_policies` trên `pack_personal_intelligence` vẫn 0.

## 8. Hạn chế

`pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot` và `funding_payment` vẫn chỉ có schema. Chưa có export sàn. `WorkspacePackProvisioner` không gọi provision pack riêng cho `personal_intelligence`; bật module không tạo workspace pack mới.

NOT VERIFIED. Đường tenant ADMIN `PUT /api/system/tenant-platform` sau khi trần đã chứa mã. Phần party của migration 079. Áp 390 lên môi trường khác database kiểm thử này.

Bước tiếp theo có giá trị nhất vẫn là một export giao dịch thật, đã gắn nhãn nguồn, trước khi coi kết quả trading là dữ liệu vận hành. Việc registry không còn chặn admin activation.
