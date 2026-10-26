# SECOND-BRAIN-V1-RUNTIME-HARDENING — EXECUTION REPORT

Ngày ghi: 2026-10-17. Trading Module V1 chưa hoàn thành. PASS chỉ gắn với kiểm thử đã chạy.

## 1. Nguyên nhân blocker workspace

FACT. `WorkspaceResolutionMiddleware` chạy sau authentication. Với user đã xác thực, nó gọi `WorkspaceResolver`, và câu SQL đọc `kit_workspace.workspace_workspace`. Database kiểm thử lúc đó chưa có bảng này, nên request có permission trả HTTP 500 trước controller.

FACT. Bảng được tạo bởi migration 073. 073 phụ thuộc 072. 072 seed `kit_core.core_language` từ `public.platform_locales`. Bảng locale đó được tạo bởi migration 051. 051 còn sửa `customer_accounts`, `products` và `product_prices`, nên không áp được khi các bảng đó chưa có.

## 2. Migration đã áp trên database hiện có

FACT. Không tạo database mới và không xóa `kit_pi_validation`. Thứ tự bổ sung, sau khi các bảng đích đã có:

1. `003_catalog.sql` — `products`, `product_units`, `product_prices`.
2. `004_inventory.sql` — `warehouses`, `inventory_batches`.
3. `005_procurement.sql` — `suppliers` và chứng từ mua.
4. `006_sales.sql` — `customers`.
5. `007_customer_app.sql` — `customer_accounts`.
6. `051_platform_multi_branch_i18n.sql` — `platform_locales`, `platform_module_registry`, và backfill settings cho tenant chưa có `settings.platform`.
7. `072_kit_core_iam_tenant.sql` — hoàn tất.
8. `073_kit_org_workspace.sql` — tạo `kit_workspace.workspace_workspace` và backfill một workspace mặc định cho mỗi tenant.

Không áp 386. Không áp lại 385, 387, 388, 389. Không sửa các file đó. `pg_policies` trên `pack_personal_intelligence` vẫn 0. RLS vẫn tắt.

051 không ghi đè `PI_A`: tenant này đã có `settings.platform`, nên `enabled_modules` vẫn là `["personal_intelligence"]`. `PI_B` nhận danh sách pharmacy mặc định và không có `personal_intelligence`.

## 3. Database

FACT. Vẫn `127.0.0.1:5433`, database `kit_pi_validation`. Không dùng `kitplatform` hay `kitplatform_test`. Không ghi credential.

## 4. HTTP đi qua middleware tới controller

FACT. Process `KitPlatform.Api` riêng, build ra thư mục output riêng, listen `127.0.0.1` trên cổng trống. Connection trỏ database trên. `pg_stat_activity` thấy session của process đó. JWT chỉ ở biến môi trường của process kiểm thử.

FACT. Request có quyền và module bật trả `TradingLoopResult`. PostgreSQL có decision đúng `decision_id`, execution `PAPER`, trade `source = FIXTURE`, evidence gắn context của decision, và một event `DecisionMade`. Response này chỉ được controller tạo sau middleware workspace. Process kiểm thử đã dừng. Thư mục output đã xóa. Process đang phục vụ việc khác là pid 11404 và không bị dừng.

## 5. Authentication, authorization, module, tenant

FACT. Qua process Kestrel đó:

- không có token → 401
- user không có permission `personal_intelligence.trading.run` → 403
- tenant `PI_B` không bật module → 403, câu `Module is not enabled for this tenant.`
- tenant `PI_B` được bật module tạm thời nhưng body chọn tenant `PI_A` → 403, message chứa `cannot switch`, decision của thesis đó = 0
- tenant `PI_A` có module và quyền → request được xử lý

Gate module trên host này là `TenantPlatformSettingsService` thật, vì host đăng ký service đó và test không gắn reader giả.

FACT. `platform_module_registry` đã có từ 051, với 12 mã seed của file đó. `personal_intelligence` có trong `PlatformModuleCodes` nhưng chưa có trong seed 051. Request gate không đọc registry; nó đọc `settings.platform.enabled_modules`. Đường admin `UpdateAsync` lọc theo registry nên chưa chấp nhận mã này. NOT VERIFIED cho thao tác bật module qua API admin.

## 6. Persistence, risk, paper, live, idempotency

FACT. Cùng process Kestrel và PostgreSQL:

- paper chỉ khi risk cho phép và có `ApprovedBy`; mode lưu là `PAPER`
- autonomy `recommend_only`
- policy lưu `proposed`; response không có event `PolicyApproved`
- veto notional: `riskAllowed = false`, `paperExecuted = false`, execution của decision đó = 0
- `Live = true` → HTTP 400, decision của thesis đó = 0
- cùng `OperationKey` và cùng payload → cùng `decisionId`, đúng một decision và một event `DecisionMade`
- cùng khóa, đổi thesis → HTTP 409, decision cũ vẫn là một dòng
- khóa mới → `decisionId` khác

## 7. Build và test

FACT. `dotnet build` API ra thư mục riêng: 0 Error. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests` với kết nối `kit_pi_validation` cổng 5433: Passed 33, Failed 0, Skipped 0. Trong đó có bài HTTP Kestrel ở trên và các bài test host trước đó.

NOT VERIFIED. Export sàn, PnL thật, fill live. Bài catalog `Platform_module_codes_include_all_asbuilt_catalog_entries` không chạy vì build của project đó đụng DLL pid 11404 đang khóa. Source catalog và mảng expected đều có `personal_intelligence`.

## 8. Git

FACT. Branch `main`, HEAD `8c59ceb4f3be537898106ec058ea547406f722e5`. Không stage, commit, reset, clean, stash. Hash `ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`. Working tree Famixa và NOVIXA vẫn còn. Không sửa middleware và không tạo bảng workspace giả.

## 9. Phần còn lại

`pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot` và `funding_payment` vẫn chỉ có schema.

Bước tiếp theo có giá trị nhất là thêm `personal_intelligence` vào `platform_module_registry` bằng migration tiến, để đường admin bật module dùng đúng registry hiện hành. Không cần việc đó để request trading đã xác thực đi qua host.
