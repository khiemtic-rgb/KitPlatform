# NOVIXA-CORE-REFACTOR-VERIFY-02

Xác minh bản sửa tương thích Core Engine trên checkout đang có. Không sửa mã, không restore, không commit, không push, không cherry-pick, không xóa checkout. Không mở Content Take Proxy. Không truy cập VPS, production API, database, hay tenant `NT_XUANHOA`.

Ngày chạy: 2026-10-13. Base SHA yêu cầu: `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be`.

## Kết quả

| Gate | Kết quả |
|---|---|
| Checkout chứa đúng sáu file | **Có**, tại `E:\KitPlatform`. Không có worktree Core Engine riêng. |
| 1. Diff sáu file | **PASS** cho nội dung sáu file. `git diff --check` toàn working tree **FAIL** ở hai file ngoài phạm vi. |
| 2. Ba lệnh build Release `--no-restore` | **PASS**. 0 error. Có warning baseline. |
| 3. Test filter `FullyQualifiedName~CoreEngineTests` | **Không chạy test nào.** Exit code 0 không phải bằng chứng PASS. |
| 4. Bảo toàn | HEAD không đổi. Diff sáu file không đổi. Không commit. |

Chờ Architect. Nhiệm vụ này không xử lý filter test và không dọn working tree.

## Checkout

| Mục | Giá trị |
|---|---|
| Đường dẫn | `E:\KitPlatform` |
| Nhánh | `main` |
| `git rev-parse HEAD` | `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` |
| Khớp base SHA | Có |

`git worktree list` không có checkout Core Engine riêng. Hai checkout bị cấm không được mở và không được sửa:

- `E:\KitPlatform-wt-content-take-proxy-isolated-verify-01`
- `E:\KitPlatform-wt-novixa-fix-clean-01`

Sáu file dưới đây đang `M` trong working tree của `E:\KitPlatform`. Không tạo bản sửa mới.

Working tree không chỉ gồm sáu file. Trước và sau các gate, `git diff --stat` toàn cây vẫn là **171 files changed, 7465 insertions(+), 2308 deletions(-)**. Sau các gate và trước file báo cáo này, `git status --short` có **472** dòng. Build Release vì vậy biên dịch working tree hiện có, gồm cả file dirty ngoài sáu file Core Engine.

## Gate 1 — Diff sáu file

`git diff --stat` của đúng sáu path:

```
 src/KitPlatform.Api/Controllers/System/WorkflowController.cs       |  2 +-
 src/KitPlatform.Infrastructure/CustomerApp/CustomerDraftOrderService.cs |  9 ++--
 src/KitPlatform.Infrastructure/Dashboard/DashboardService.cs        | 15 +++----
 src/Packs/Pharmacy/.../Inventory/BatchResolver.cs                   |  5 +--
 src/Packs/Pharmacy/.../Procurement/ProcurementRepository.cs         | 10 ++---
 tests/KitPlatform.Application.Tests/CoreEngineTests.cs              | 51 +++++++++++++++++-----
 6 files changed, 56 insertions(+), 36 deletions(-)
```

Stat này giống nhau trước build và sau test.

| Kiểm tra | Kết quả |
|---|---|
| `IPermissionEngine`, `IInventoryEngine`, `IPricingEngine` trong sáu file | Không còn. |
| `IWorkflowEngine` trong `WorkflowController` | Còn. Using đổi từ `KitPlatform.Application.Core.Engines` sang `KitPlatform.Packs.Pharmacy.Workflow`. Interface đó nằm ở namespace Pharmacy. |
| File ngoài sáu path trong diff sáu file | Không. |
| `git diff --check` trên sáu path | Exit **0**. |
| `git diff --check` toàn working tree | Exit **2**. |

Hai lỗi whitespace nằm ngoài sáu file. Không sửa.

| File | Lỗi |
|---|---|
| `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md:14` | trailing whitespace |
| `src/KitPlatform.Api/Controllers/Pharmacy/SalesController.cs:119` | trailing whitespace |

Không có file patch đã duyệt tách khỏi working tree này. Đối chiếu dùng diff sáu file tại chỗ và các type đang có trong cây.

| File | Thay cho engine cũ |
|---|---|
| `WorkflowController.cs` | `KitPlatform.Packs.Pharmacy.Workflow.IWorkflowEngine` |
| `CustomerDraftOrderService.cs` | Bỏ `IPermissionEngine`. `SalesDiscountPolicy.FromPermissions(_user.Permissions, _user.IsInRole("ADMIN"))` |
| `DashboardService.cs` | Bỏ `IPermissionEngine`. `ICurrentUserAccessor`: `IsInRole("ADMIN")` và `Permissions.Contains` cho `reports.read`, `reports.write`, `reports.export` |
| `BatchResolver.cs` | `IInventoryEngine` đổi thành `IBatchResolver` |
| `ProcurementRepository.cs` | Bỏ `IPricingEngine`. `ProcurementPricing.PriceReceipt` |
| `CoreEngineTests.cs` | `PricingEngineTests` đổi thành `PharmacyPricingTests`, gọi `SalesPricing` và `ProcurementPricing`. Các class khác trong file giữ nguyên |

`SalesDiscountPolicy.FromPermissions`, `IBatchResolver`, `ProcurementPricing.PriceReceipt`, `SalesPricing.PriceOrder`, `SalesPricing.ValidateDiscounts`, và `SalesPricing.ComputeLineRefundAmount` có trong cây Pharmacy hiện tại. Ba build ở Gate 2 biên dịch được các lời gọi này.

## Gate 2 — Build

Cả bốn `obj/project.assets.json` của Infrastructure, Pharmacy Infrastructure, Api, và Application.Tests đã có sẵn. Không chạy `dotnet restore`.

### 1. Infrastructure

Lệnh: `dotnet build src/KitPlatform.Infrastructure/KitPlatform.Infrastructure.csproj -c Release --no-restore --verbosity minimal`

| Mục | Giá trị |
|---|---|
| Start | 2026-10-13 17:24:55.240 +07 |
| End | 2026-10-13 17:25:02.325 +07 |
| `Time Elapsed` | 00:00:06.85 |
| Exit | **0** |
| Error | 0 |
| Warning | 2 |

Warning:

- `NU1903` — `Newtonsoft.Json` 10.0.3, GHSA-5crp-9r3c-p9vr, trên `KitPlatform.Infrastructure.csproj`
- `CS4014` — `src/KitPlatform.Infrastructure/CustomerApp/CustomerPushRepository.cs(136,9)`, call không được await. File này không thuộc sáu file.

### 2. Pharmacy Infrastructure

Lệnh: `dotnet build src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/KitPlatform.Packs.Pharmacy.Infrastructure.csproj -c Release --no-restore --verbosity minimal`

| Mục | Giá trị |
|---|---|
| Start | 2026-10-13 17:25:23.601 +07 |
| End | 2026-10-13 17:25:27.427 +07 |
| `Time Elapsed` | 00:00:03.60 |
| Exit | **0** |
| Error | 0 |
| Warning | 2 |

Cả hai là `NU1903` cho `Newtonsoft.Json` 10.0.3, trên `KitPlatform.Packs.Pharmacy.Infrastructure.csproj` và `KitPlatform.Infrastructure.csproj`. Không có warning compiler.

### 3. Api

Lệnh: `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj -c Release --no-restore --verbosity minimal`

| Mục | Giá trị |
|---|---|
| Start | 2026-10-13 17:25:36.471 +07 |
| End | 2026-10-13 17:25:51.401 +07 |
| `Time Elapsed` | 00:00:14.70 |
| Exit | **0** |
| Error | 0 |
| Warning | **49** |

Bản ghi console của lệnh gate bị cắt giữa, nên không giữ đủ 49 dòng warning. Phần nhìn thấy gồm `NU1903` trên 12 project (`KitPlatform.Api`, Pharmacy, Infrastructure, Clinic, Survey, Connect, FamilyOs, Sales, Content, Care, LocalOs, TestPack), `CS8604` tại `ProductionReadinessAuthority.cs` dòng 290 và 400, `CS8604` tại `KapAiNarrativeService.cs` dòng 181–185, nhiều `CS0618` tại `KapChartImageGenerator.cs`, và `CS8602` tại `Program.cs` dòng 337, 366, 407, 465.

Một lần `--no-incremental` sau gate, không phải lệnh gate, báo **39** warning và 0 error, `Time Elapsed` 00:00:14.42. 39 dòng unique gồm 1 `NU1903` (`KitPlatform.Api.csproj`), 1 `CS4014` (`CustomerPushRepository.cs:136`), 4 `CS8602` (`Program.cs`), 7 `CS8604` (2 Content + 5 Survey), và 26 `CS0618` (`KapChartImageGenerator.cs`). Số 39 không thay cho số 49 của lệnh gate.

Không có warning compiler nào trong các dòng đã thấy nằm trên sáu file Core Engine.

## Gate 3 — Test

Lệnh đúng như yêu cầu:

`dotnet test tests/KitPlatform.Application.Tests/KitPlatform.Application.Tests.csproj -c Release --filter FullyQualifiedName~CoreEngineTests --no-restore --verbosity minimal`

| Mục | Giá trị |
|---|---|
| Start | 2026-10-13 17:27:14.668 +07 |
| End | 2026-10-13 17:27:21.723 +07 |
| Wall clock | 7 giây |
| Exit | **0** |
| Test thực sự chạy | **0** |
| PASS | 0 |
| FAIL | 0 |
| SKIP | 0 |

Runner in:

`No test matches the given testcase filter FullyQualifiedName~CoreEngineTests`

Exit 0 và việc build `KitPlatform.Application.Tests.dll` không phải kết quả test PASS.

File `tests/KitPlatform.Application.Tests/CoreEngineTests.cs` có 10 `[Fact]`. Fully qualified name của chúng không chứa chuỗi `CoreEngineTests`. Bốn class là `DrugKnowledgeRulesTests` (2), `PharmacyPricingTests` (4), `InventoryEngineFefoTests` (2), và `AiOrchestratorTests` (2). Không chạy lại với filter khác.

Lệnh test in `NU1903` khi biên dịch dependency. Không có dòng tổng warning của MSBuild và không có lỗi test.

## Gate 4 — Bảo toàn

| Mục | Trước gate | Sau test, trước báo cáo này |
|---|---|---|
| HEAD | `0d8568ae6e57003ae0f5e2813f1a35fd000ba6be` | Cùng SHA |
| Diff toàn cây | 171 files, +7465 / -2308 | Cùng số |
| Diff sáu file | 6 files, +56 / -36 | Cùng số |

Không commit, không push, không cherry-pick, không xóa checkout, không restore, không sửa source.

## Blocker baseline còn mở

- Working tree có 171 path dirty. Build không phải compile của một cây chỉ chứa sáu file.
- `git diff --check` toàn cây FAIL vì trailing whitespace ở `docs/KIT-PLATFORM-ADR-04-DECISION-V1.md` và `SalesController.cs`.
- `NU1903` cho `Newtonsoft.Json` 10.0.3 còn trên các project đã build.
- `CS4014` ở `CustomerPushRepository.cs`, các warning Survey/Content/`Program.cs` ở build Api. Không thuộc sáu file và không được sửa.
- Filter đã chỉ định không chọn 10 test trong `CoreEngineTests.cs`. Chưa có số PASS/FAIL của các test đó.
