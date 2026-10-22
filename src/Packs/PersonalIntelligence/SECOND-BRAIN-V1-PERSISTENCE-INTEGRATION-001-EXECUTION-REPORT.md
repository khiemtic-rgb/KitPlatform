# SECOND-BRAIN-V1-PERSISTENCE-INTEGRATION-001 — EXECUTION REPORT

Ngày ghi: 2026-10-17. FACT là kết quả đã chạy hoặc diff đã đọc. INFERENCE là suy từ cấu trúc code. NOT VERIFIED là chưa có bằng chứng chạy. PASS chỉ dùng cho kiểm thử đã chạy.

## 1. Phần nào đã có đường lưu

FACT. Các bản ghi đang dùng trong vòng V1 có câu lệnh Dapper ghi và đọc: Context, Evidence, Decision, Action, Outcome, Reflection, Experience, Policy. Mỗi lần ghi đi cùng một dòng `pack_personal_intelligence.pi_events` trong cùng một transaction Npgsql. Không có câu lệnh ghi `platform_events`.

FACT. Paper fill ghi `trading_order` và `trading_execution` với mode `PAPER` trong cùng transaction với action và event. Veto và Live không tạo dòng execution. Import trade dùng `ON CONFLICT DO NOTHING`, rồi đọc lại đúng tenant nếu dòng đã tồn tại.

NOT VERIFIED. Chưa có phiên PostgreSQL nào thực thi các câu lệnh này. Vì vậy chưa thể gọi là đã persist trên database.

Các bảng có trong schema nhưng chưa có repository, nên chưa được persist: `pi_knowledge`, `pi_memory`, `pi_evaluation`, `pi_agent`, `market_snapshot`, `funding_payment`.

## 2. Repository, DI, host

- `IIntelligenceJournal` / `IntelligenceJournal`: ghi và đọc vòng canonical, lọc bằng `ITenantContext.TenantId`. Tenant trên bản ghi khác tenant hiện tại thì từ chối trước khi mở connection.
- `IPaperExecutionStore` / `PaperExecutionStore`: gọi Risk Engine và Demo execution, rồi mới ghi. Live bị từ chối trước khi ghi.
- `TradeRepository`: đọc theo `tenant_id` của context. `GetByExternalId` có thêm điều kiện tenant. Ghi lặp không còn `DO UPDATE`.
- `AddPersonalIntelligencePack` đăng ký journal, paper store và các repository ledger cũ.
- Host: thêm một `using`, một `AddPersonalIntelligencePack()` và một `ProjectReference`. Dòng TestPack, `ProductionTenantActionFilter` và phần bỏ `access_token` trên query vẫn còn trong diff.

## 3. File và migration

| File | Lý do |
|---|---|
| `migrations/388_pack_personal_intelligence_loop_integrity_v1.sql` | Trigger append-only cho `pi_events`, event type `EvidenceRecorded`, khóa ngoại composite cho outcome/reflection và order/execution. Không sửa 385, 386, 387. |
| `deploy/ubuntu/migration-files.personal-intelligence.txt` | Thêm 388. Không đụng manifest prod, content, Famixa. |
| `Infrastructure/Intelligence/IntelligenceSql.cs`, `IntelligenceJournal.cs` | SQL và transaction của vòng canonical. |
| `Infrastructure/Trading/PaperExecutionSql.cs`, `PaperExecutionStore.cs` | Ghi paper, không ghi Live. |
| `Application/Intelligence/IIntelligenceJournal.cs`, `TenantScope.cs` | Hợp đồng và kiểm tenant. |
| `Application/Trading/PaperExecutionStore.cs` | Kế hoạch ghi: veto và Live không có execution row. |
| `Trading/TradeRepository.cs` | Tenant match và idempotency không ghi đè dòng cũ. |
| `PersonalIntelligencePackDependencyInjection.cs` | Đăng ký dịch vụ mới. |
| `Program.cs`, `KitPlatform.Api.csproj` | Patch cục bộ, giữ diff Famixa/TestPack. |
| `tests/KitPlatform.PersonalIntelligence.Tests/*` | 12 kiểm thử. |

## 4. Build và test

FACT. `dotnet test tests/KitPlatform.PersonalIntelligence.Tests`: Passed 12, Failed 0. Lệnh này biên dịch Application, Infrastructure và project test từ source hiện tại.

FACT. `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj -o .dev/sb-api-verify`: 0 Error. Thư mục output đã xóa sau khi build. Build thẳng vào `bin` của API thất bại vì process `KitPlatform.Api` (pid 22936) đang khóa DLL. Process đó không bị dừng.

Cảnh báo có sẵn, không phát sinh từ pack này: NU1903 Newtonsoft.Json 10.0.3, CS4014 trong `CustomerPushRepository.cs`.

## 5. Tenant, transaction, event, idempotency

| Kiểm tra | Loại | Kết quả |
|---|---|---|
| Tenant khác bị từ chối trước khi mở connection | Unit | PASS |
| Policy `approved` bị từ chối trước khi mở connection | Unit | PASS |
| SQL vòng lặp có `tenant_id`, ghi `pi_events`, không có `platform_events`, event insert không có `UPDATE` | Kiểm tra tĩnh câu SQL | PASS |
| Veto và Live không tạo execution row; paper dùng `PAPER` | Unit | PASS |
| DI resolve journal, paper store, trade repository | Unit, ServiceCollection | PASS |
| Import CSV lặp không nhận dòng trùng | Unit fixture | PASS, từ bộ test trước |
| Câu insert trade là `DO NOTHING` | Đọc source | PASS về hình dạng SQL |
| Đọc/ghi thật không thấy tenant khác | PostgreSQL | NOT VERIFIED |
| Rollback khi lệnh thứ hai lỗi | PostgreSQL | NOT VERIFIED. Code bọc hai lệnh trong một transaction và rollback khi có exception. Chưa chạy database để chứng minh không còn dòng dở. |
| Unique index chặn trùng trên DB | PostgreSQL | NOT VERIFIED |

## 6. IPermissionEngine

FACT. Trên commit `8c59ceb4`, `DashboardService` và `CustomerDraftOrderService` còn tham chiếu `IPermissionEngine`. Working tree hiện tại đã bỏ tham chiếu đó trước nhiệm vụ này.

`DashboardService` dùng `ICurrentUserAccessor`: `IsInRole("ADMIN")` và `Permissions` chứa `reports.read`, `reports.write`, `reports.export`.

`CustomerDraftOrderService` dùng `SalesDiscountPolicy.FromPermissions`, cùng cách với `SalesService`.

FACT. `dotnet test` biên dịch được `KitPlatform.Infrastructure` với hai file đó. Không tạo lại `IPermissionEngine`. Không sửa hai file shared trong nhiệm vụ này.

## 7. Migration

FACT. 385 và 387 không bị sửa. 386 vẫn untracked và không bị sửa. 388 là file mới, chưa áp dụng.

Không chạy SQL. Database trong cấu hình API là database local dùng chung tên `kitplatform`. Không có Docker để tạo database thử nghiệm riêng. Database integration chưa được xác minh.

388 giữ ADR-008: không có RLS.

## 8. Trạng thái E:\KitPlatform

FACT. Branch `main`, HEAD `8c59ceb4`. Không checkout, stash, stage, commit. Hash `ContentFamixaStudioView.tsx` vẫn `bd1fbb224a421221a43b3fc11988873746754bfa`.

Diff host còn các thay đổi TestPack và content filter đã có từ trước, cộng hai dòng Personal Intelligence. `DashboardService.cs` vẫn modified đúng diff sẵn có.

## 9. Xong, chưa xong, blocker

Đã xong: đường Dapper cho vòng đang dùng, paper store, siết tenant trên trade repository, DI, host compile được, 12 test.

Chưa xong: xác minh PostgreSQL cho isolation, rollback và unique index. Repository cho knowledge, memory, evaluation, agent, market snapshot, funding. API đang chạy chưa được restart, nên process hiện tại chưa load pack mới.

Blocker còn lại: chưa có database thử nghiệm riêng, có thể hủy, tách khỏi `kitplatform` local. Không áp migration khi chưa có môi trường đó.

## 10. Bước tiếp theo

Tạo một Postgres dùng riêng, áp 385 rồi 387 rồi 388, và chạy một integration test: ghi decision cùng event, cố tình làm lệnh event lỗi để xác nhận rollback, rồi đọc bằng tenant khác và xác nhận không thấy dòng. Sau khi test đó PASS mới restart API để nạp pack.
