# SB-RESEARCH-OWNER-APPROVAL-REVIEW-001

Ngày review: 2026-10-22
Repository: `E:\KitPlatform-sb-research`
Remote: `https://github.com/khiemtic-rgb/KitPlatform.git`

Candidate status: **NOT READY FOR PRODUCTION**

Đây là hồ sơ review để chủ dự án quyết định. Mission không deploy, không chạy migration, không đổi quyền, không merge, không sửa code, và không chuyển mục `PENDING` thành `APPROVED`.

## A. Executive decision

Candidate `3d5d7b42f9ca98032c7aec6638797234650a5abd` trên `admin-build-blocker-001` đã được xác minh lại từ Git. `origin/admin-build-blocker-001` khớp HEAD. `origin/main` vẫn là `608bc054c9b6389190c4c214ac478b3397b110dc`. Worktree sạch trước khi thêm tài liệu này.

Candidate chưa đủ điều kiện chuẩn bị release. Các điều kiện chưa đáp ứng:

- Manifest Personal Intelligence áp tám file, gồm schema trading và exchange, không chỉ migration 390. Chưa có phê duyệt database đích.
- `personal_intelligence.research.read` không có trong SQL. Gán qua role hiện có sẽ không gắn được nếu dòng permission chưa tồn tại.
- Policy hiện tại cho ADMIN và `personal_intelligence.trading.run` đọc Research. `trading.run` còn mở API trading.
- `ExchangeBackfillOptions.WorkerEnabled` mặc định `true`. Chưa có owner xác nhận process đích sẽ đặt `false` trước khi chạy.
- Publication root chưa có owner và giá trị. Không có bản sao rollback nào được xác nhận trong mission này.
- PostgreSQL integration là `BLOCKED/NOT RUN`. `KIT_PI_TEST_CONNECTION` không được đặt. Test ghi dữ liệu và không có bước dọn.

Không dùng trạng thái `DEPLOY READY`.

## B. Decision register

### DEC-01 — Migration scope

- Vấn đề: Có cho phép dùng `deploy/ubuntu/migration-files.personal-intelligence.txt` trên database đích sau khi review dependency hay không.
- Bằng chứng: Manifest liệt kê đúng tám file, theo thứ tự `385`, `387`, `388`, `389`, `390`, `391`, `392`, `393`. File 390 chỉ `INSERT` một dòng `personal_intelligence` vào `platform_module_registry` khi chưa có. Manifest không nằm trong `migration-files.prod.txt`. Mission này không thêm nó vào đó.
- Lựa chọn:
  1. Không áp manifest. Gate module tiếp tục đóng vì registry chưa có mã này.
  2. Phê duyệt áp cả tám file, bằng runner manifest Personal Intelligence, trên một database đã được nêu tên và đã có dependency bên dưới. Không gắn file này vào `migration-files.prod.txt`.
  3. Yêu cầu một manifest chỉ có 390 ở một thay đổi sau. Candidate hiện tại không có manifest đó.
- Khuyến nghị: Chưa áp lên production. Nếu chủ dự án muốn mở gate module, phải phê duyệt cả manifest, vì runner áp cả danh sách chứ không áp riêng 390. Manifest tạo schema trading, bảng ciphertext kết nối sàn, job backfill, và hai permission exchange.
- Rủi ro nếu chưa quyết: Không quyết thì module không mở. Quyết nhầm là “chỉ chạy 390” sẽ kéo theo schema trading và exchange.
- Người phê duyệt: Chủ dự án, cùng người chịu trách nhiệm database đích.
- Trạng thái: `PENDING`

### DEC-02 — Research read permission

- Vấn đề: Chốt tenant, entitlement module, và role được đọc Research. Không cấp `personal_intelligence.trading.run` chỉ để lấy quyền đọc.
- Bằng chứng: Không có câu `INSERT` nào cho `personal_intelligence.research.read` trong `migrations/`. `391` chỉ seed `personal_intelligence.exchange.connect` và `personal_intelligence.exchange.read`. `personal_intelligence.trading.run` và `personal_intelligence.trading.approve` cũng không có trong SQL migration; test PostgreSQL tự chèn hai mã trading. `UpdateRolePermissionsAsync` chỉ gắn `permission_code` đã có trong `public.permissions`. Mã chưa có sẽ không được gắn, và hàm vẫn commit.
- Module không được bật bởi migration 390. Bật thật sự là `settings.platform.enabled_modules` trên tenant. Mã phải có trong registry (`status` mặc định `1`) và trong `allowed_modules`. Trần `allowed_modules` đổi qua `UpdateTenantEntitlementAsync`, có kiểm provisioning key khi hệ thống đã có tenant. `WorkspacePackProvisioner` không có nhánh `personal_intelligence`; đường cập nhật entitlement vẫn gọi `kit_provision_pack_workspace` cho `novixa_pharmacy` như mọi lần cập nhật module.
- UI: `TradingLayout` cho vào khi là ADMIN, `trading.run`, `exchange.connect`, hoặc `exchange.read`. `research.read` không nằm trong danh sách đó. Research panel nằm sau layout này.
- Lựa chọn:
  1. Chốt tenant, thêm dòng permission `research.read` bằng quy trình dữ liệu được duyệt riêng, gán cho role chỉ đọc, và chấp nhận rằng UI admin hiện không mở cho riêng quyền đó.
  2. Dùng tài khoản ADMIN của tenant đã bật module cho lần đọc đầu. ADMIN cũng qua được policy trading.
  3. Cấp `trading.run` cho người chỉ cần xem. Không khuyến nghị.
- Khuyến nghị: Chọn phương án 1 hoặc 2. Không cấp `trading.run` để lấy quyền đọc. Việc thêm dòng permission và bật module là thay đổi dữ liệu, ngoài mission này.
- Rủi ro nếu chưa quyết: API trả 403. Gán mã chưa tồn tại trông như thành công nhưng claim không có trên token.
- Người phê duyệt: Chủ dự án và chủ tenant được đọc.
- Trạng thái: `PENDING`

### DEC-03 — Effective authorization

- Vấn đề: Chấp nhận hay yêu cầu xử lý riêng việc ADMIN và `personal_intelligence.trading.run` mở quyền đọc Research theo policy hiện tại.
- Bằng chứng: Policy `PersonalIntelligenceResearchRead` cho phép role `ADMIN`, claim `personal_intelligence.research.read`, hoặc claim `personal_intelligence.trading.run`. `TradingDeskController` dùng policy `PersonalIntelligenceTradingRun`, nên `trading.run` không phải quyền chỉ đọc. `trading.approve`, `exchange.read`, và `exchange.connect` không có trong assertion của policy Research. Test `Approve_or_exchange_permissions_cannot_read` kỳ vọng 403 cho ba mã đó. Filter module không có nhánh bỏ qua cho ADMIN. Test module tắt dùng client `research.read`, không dùng client ADMIN; cùng filter áp cho mọi caller.
- Lựa chọn:
  1. Giữ policy. Không cấp `trading.run` cho người chỉ đọc. Chấp nhận ADMIN của tenant đã bật module đọc được Research và cũng gọi được API trading.
  2. Yêu cầu một thay đổi policy sau này để tách ADMIN hoặc `trading.run` khỏi Research. Candidate này không chứa thay đổi đó.
- Khuyến nghị: Chấp nhận policy hiện tại cho vòng này và không đổi code. Không dùng quyền trading rộng để thay quyền đọc.
- Rủi ro nếu chưa quyết: Người được cấp `trading.run` để “xem Research” sẽ có quyền gọi API sổ và vòng trading khi module đang bật.
- Người phê duyệt: Chủ dự án.
- Trạng thái: `PENDING`

### DEC-04 — Runtime configuration

- Vấn đề: Chốt owner đặt publication root và tắt ExchangeBackfill worker trước khi chạy process API mới.
- Bằng chứng: `Program.cs` bind `PersonalIntelligence:ResearchPublication` và `PersonalIntelligence:ExchangeBackfill`. Root trống hoặc thư mục không tồn tại trả `MISSING`. `WorkerEnabled` mặc định `true`; worker chỉ thoát sớm khi giá trị đã là `false` lúc process chạy. Ba file `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` không có section Personal Intelligence và không có path máy local. `src/KitPlatform.Api` không gọi `UseStaticFiles`. Publisher xóa `.previous` sau khi publish thành công.
- Lựa chọn:
  1. Chỉ định owner vận hành. Trước process mới: `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`, root trỏ tới một bản publication đã giữ riêng, process chỉ có quyền đọc thư mục đó, không đặt exchange credential.
  2. Hoãn chạy process mới cho đến khi owner và bản sao rollback được ghi nhận.
- Khuyến nghị: Phương án 2 cho đến khi phương án 1 được ghi tên người và đường kiểm tra. Không ghi path vào appsettings dùng chung. Không dùng `.previous` làm bản rollback.
- Rủi ro nếu chưa quyết: Process mới có thể chạy worker backfill. Root sai trả `MISSING` hoặc đọc nhầm cây. Không có bản sao riêng thì không còn cây publication trước sau một lần publish thành công.
- Người phê duyệt: Chủ vận hành process API. Tên cụ thể: `BLOCKED — OWNER INPUT REQUIRED`.
- Trạng thái: `PENDING`

### DEC-05 — Isolated integration test

- Vấn đề: Ai cung cấp database cô lập và điều kiện để chạy PostgreSQL integration tests.
- Bằng chứng: `PiValidationDatabase` ném lỗi nếu `KIT_PI_TEST_CONNECTION` trống. Biến này không được đặt trong môi trường review. Test từ chối database tên `kitplatform`, `kitplatform_test`, `pharmacore`, `postgres`, từ chối cổng `5432`, và chỉ nhận database tên `kit_pi_validation`. `InitializeAsync` ghi tenant, user, person, permission, role. `DisposeAsync` không xóa các dòng đó. Test không có denylist hostname. Mission không chạy test và không mở database.
- Lựa chọn:
  1. Một owner cung cấp Postgres cô lập, chứng minh không phải production, đặt biến môi trường, chạy test, lưu kết quả cùng commit candidate, và dọn dữ liệu test vì code không dọn.
  2. Chấp nhận rủi ro `BLOCKED/NOT RUN` cho vòng Research read. Đường đọc publication không dùng database. Persistence trading vẫn chưa được kiểm trên SQL trong vòng này.
- Khuyến nghị: Giữ `BLOCKED/NOT RUN` cho đến khi có database và người cung cấp. Không dùng database production hoặc Postgres đang nghe cổng 5432.
- Rủi ro nếu chưa quyết: Không có bằng chứng SQL cho ledger, loop, và exchange. Điều đó không đổi kết luận Research read còn các gate khác.
- Người phê duyệt: Chủ dự án và người cung cấp database cô lập. Tên người cung cấp: `BLOCKED — OWNER INPUT REQUIRED`.
- Trạng thái: `BLOCKED`

### DEC-06 — Content column the build fix reads

- Vấn đề: API candidate đọc `pack_content.brand.narrative_json`. File `migrations/384_pack_content_brand_narrative_dna.sql` có trên `origin/main`, không phải file mới của nhánh. Comment trong file nói manifest Content, nhưng `deploy/ubuntu/migration-files.content.txt` không liệt kê `383` hoặc `384` và kết thúc ở `367`. File còn `UPDATE` narrative của brand `kittech`, `novixa`, `famixa`.
- Bằng chứng: `ContentRepository` trong commit `96657e06` select `narrative_json`. Manifest Content và manifest prod đều không có tên file 384. Mission không truy vấn database đích.
- Lựa chọn:
  1. Owner xác nhận database Content đích đã có cột này trước khi chạy API candidate.
  2. Nếu cột chưa có, dừng deploy API cho đến khi có quyết định migration riêng. Không áp 384 trong mission này. Áp file đó còn ghi đè JSON narrative của ba brand.
- Khuyến nghị: Xác nhận cột trên database đích. Không suy ra là đã áp chỉ vì file nằm trong Git.
- Rủi ro nếu chưa quyết: Query brand có thể lỗi khi cột vắng. Áp nhầm 384 sẽ ghi dữ liệu brand.
- Người phê duyệt: Chủ database Content.
- Trạng thái: `BLOCKED`

## C. Release gate

| Gate | Trạng thái |
|---|---|
| Candidate provenance đã xác minh | Đạt trong mission này |
| Build và test evidence đủ, nhất quán | Chưa. Kết quả PASS cũ là `REPORTED`. PostgreSQL là `BLOCKED/NOT RUN` |
| Publisher version-control và hash | Đạt. File nằm ở `tools/research-publication/publish_research_v1.py`. SHA-256 tính lại trong mission này khớp file lịch sử |
| Migration được review và phê duyệt | Review xong. Phê duyệt `PENDING` / `BLOCKED` |
| Tenant, entitlement, quyền đọc đã quyết | `PENDING` |
| Worker và publication root có owner và cách xác minh | `PENDING`. Owner chưa được chỉ định |
| Test `BLOCKED` được giải quyết hoặc được chấp nhận rủi ro | `BLOCKED`. Chưa có chấp nhận rủi ro |
| Kế hoạch rollback ứng dụng và publication | Chưa có owner cho bản sao publication. Rollback ứng dụng là trả artifact API và admin về bản trước; chưa được lập cho một lần deploy cụ thể |
| Approval riêng cho hành động production | Chưa có. Mission này không cấp approval đó |

Không merge, không gắn manifest vào `migration-files.prod.txt`, không chạy migration, và không deploy từ packet này.

## Workstream A — Git provenance

Xác minh 2026-10-22, lệnh chỉ đọc. `git ls-remote` không cập nhật ref local.

| Mục | Giá trị |
|---|---|
| Root | `E:/KitPlatform-sb-research` |
| Branch | `admin-build-blocker-001` |
| HEAD | `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| Upstream local | Không cấu hình |
| Remote HEAD cùng tên | `origin/admin-build-blocker-001` = `3d5d7b42f9ca98032c7aec6638797234650a5abd` |
| `origin/main` | `608bc054c9b6389190c4c214ac478b3397b110dc` |
| Merge-base | `608bc054c9b6389190c4c214ac478b3397b110dc` |
| Ahead / behind `origin/main` | 8 / 0 |
| Worktree trước tài liệu này | Sạch |
| Diff `origin/main...HEAD` | 169 files, +21722 / −107 |

Candidate đúng commit đã nêu. Remote branch khớp. `origin/main` không đổi so với baseline. Nhánh local tên `main` trong repo này là `8c59ceb4`, ahead 6 và behind 198 so với `origin/main`. Worktree recovery `E:/KitPlatform` gắn với ref đó. Review không đọc ghi và không sửa worktree đó.

| Commit | Mục đích từ diff | Nhóm file | Kết luận scope |
|---|---|---|---|
| `cffb50f20af27066362163ee41387d530abe8a6b` | Đưa pack Personal Intelligence vào nhánh | Research API, reader, UI Research; đồng thời Trading, exchange, migration 385–393, host DI, worker | Research và trading nằm cùng một commit. Không phải gói chỉ đọc Research |
| `d2c28a3a10073b44bb512299e1487716aa76470a` | Sửa readiness doc | `SECOND_BRAIN_RESEARCH_DEPLOY_READINESS.md` | Documentation |
| `96657e06a6bfe36bfc3ccfe6b55cdee4d2fadd50` | Nối DTO và SELECT `narrative_json`, thêm `NarrativeBoundary` | 6 file Content, `NarrativeFitIntegrityTests` | Build fix API. Phụ thuộc cột 384, xem DEC-06 |
| `a0374a05fd614e8e524fc304cc44aee11bdf3e57` | Sửa readiness doc | cùng file readiness | Documentation |
| `c57d8d7e3b44a263b79fe8215cac88b3d5ad8635` | Làm `tsc` của admin biên dịch | 25 file Famixa/content, `AdjustmentListPage.tsx`, `GoodsReceiptListPage.tsx`, `PosConsultationModal.tsx` | Build fix admin. Đụng bề mặt kho, nhập hàng, POS |
| `c75b4d49076cd35559f51349f4360ac7ba0965e4` | Sửa readiness doc | cùng file readiness | Documentation |
| `54401aa65ad20ee73481c047000b5ad57b40a559` | Sửa readiness doc | cùng file readiness | Documentation |
| `3d5d7b42f9ca98032c7aec6638797234650a5abd` | Đưa publisher vào Git và thêm test | `tools/research-publication/*`, hai doc Research | Publisher tooling và documentation |

Phân loại file trên toàn diff:

- Research implementation: controller, DTO, `FileResearchPublicationReader`, policy Research, UI `research-view` / `ResearchPanel` / `research.api.ts`, và phần host bind publication.
- Trading và exchange, đi cùng commit gói: controller trading, connector, worker, ledger, UI sổ và sàn, script UI, migration 385 và 387–393 ngoài phần registry của 390.
- Build fix: Content `96657e06`; admin `c57d8d7e`. Trong admin, nút kiểm kê gọi `openCreate()` không kèm mouse event; hạn GRN thiếu thành chuỗi rỗng; timer POS đổi kiểu ref. Đây là sửa biên dịch, có một ép kiểu rỗng ở hạn dùng.
- Publisher tooling: `tools/research-publication/`.
- Documentation và evidence: các báo cáo trong `src/Packs/PersonalIntelligence/` và readiness doc.
- Unrelated product change: không thấy file ngoài bốn nhóm trên. Phần trading không phải sửa nhầm, nhưng rộng hơn đường đọc Research.

`origin/second-brain-research-readiness` local là `a0374a05`. Không được dùng ref đó làm candidate.

## Workstream B — Migration và quyền

### Migration 390

`migrations/390_platform_module_personal_intelligence.sql` chỉ chèn một dòng registry khi `module_code = personal_intelligence` chưa có. Cột khớp `CREATE TABLE` ở `051_platform_multi_branch_i18n.sql`: `module_code`, `module_name`, `description`, `verticals`, `sort_order`. `status` không được ghi và default là `1`, nên `ListModulesAsync` có thể thấy dòng này. File không seed permission, không sửa `tenants.settings`, và không bật module cho tenant nào.

### Manifest và tác động

`deploy/ubuntu/migration-files.personal-intelligence.txt` không nằm trong `migration-files.prod.txt`. Runner incremental chỉ áp file trong manifest được chọn. Áp manifest Personal Intelligence không chạy lại manifest Pharmacy, Content, Family OS, hay Local OS. Nó vẫn ghi vào bảng dùng chung:

- `platform_module_registry`: một dòng ở 390.
- `public.permissions`: `personal_intelligence.exchange.connect` và `personal_intelligence.exchange.read` ở 391.

Nó cũng tạo `pack_personal_intelligence`, ledger trading, vòng loop, receipt operation, bảng kết nối có ciphertext, sync execution, và job backfill. `393` còn `UPDATE` các dòng `trade` có `external_trade_id LIKE 'BYBIT:%'`.

### Dependency đọc được từ SQL

Các dependency sau được đọc từ `REFERENCES`, hàm, và `ALTER`. Mission không chạy SQL.

| File | Phụ thuộc đã thấy | Việc file làm |
|---|---|---|
| 385 | `public.users`, `public.tenants`, `kit_uuid_v7()`, `kit_bump_row_version()` | Tạo schema và ledger |
| 387 | Bảng `person` của 385 | Tạo `pi_events` và các bảng loop, gồm `pi_outcome`, `pi_reflection` |
| 388 | Bảng do 387 tạo | Trigger append-only và FK |
| 389 | Schema từ 385. `CREATE TABLE` không có `IF NOT EXISTS` | Tạo `pi_operation` |
| 390 | `platform_module_registry` từ 051 | Một dòng catalog |
| 391 | `person` và `public.permissions` | Kết nối sàn và hai permission exchange |
| 392 | Bảng `trade` của 385 | Cho phép `source_type = EXCHANGE` và bảng sync |
| 393 | Bảng `trade` | Cột market và bảng backfill; `UPDATE` dòng BYBIT |

`002_identity.sql`, `051_platform_multi_branch_i18n.sql`, và `071_kit_schemas_foundation.sql` có trong `migration-files.prod.txt`. Đó là nơi tạo `permissions`, registry, và hai hàm. Một database chưa áp các file đó sẽ không nhận manifest này. Thứ tự trong manifest là bằng chứng thứ tự áp. Không có đồ thị dependency nào khác trong runner.

Không có file `386_*.sql`. Comment của 387, 388, và 389 nhắc 386. Các bảng mà 388 sửa được tạo trong 387. Không có câu SQL nào tham chiếu object của một file 386. Khoảng số này chưa được giải thích bằng một artifact khác trong repo.

`389` không idempotent nếu chạy lại ngoài sổ `kit_schema_migrations`. Chưa xác minh database đích đã có một phần schema này hay chưa.

### Ma trận authorization

Mã trong code là `personal_intelligence.trading.approve`, `personal_intelligence.exchange.read`, và `personal_intelligence.exchange.connect`. Không có mã ngắn `trading.approve` trong policy Research.

| Chủ thể/claim | Được phép đọc Research API? | Căn cứ | Hành động cần thiết |
|---|---|---|---|
| ADMIN | Có, khi đã đăng nhập và module đang bật cho tenant. Role lấy từ `ClaimTypes.Role` | Policy dòng 58–65. JWT gán role ở `JwtTokenService`. Test `Trading_run_and_research_read_and_admin_can_read`. Filter module không loại ADMIN | Chủ dự án chấp nhận ở DEC-03. Vẫn phải bật module |
| `personal_intelligence.research.read` | Có ở API khi claim khớp và module bật. Không seed SQL. UI layout không nhận riêng quyền này | Policy; test cùng fact trên; `UpdateRolePermissionsAsync`; `TradingLayout` dòng 33–36 | Quyết tenant, seed permission, và role ở DEC-02. Không suy ra là gán role sẽ tự tạo dòng permission |
| `personal_intelligence.trading.run` | Có ở Research API. Cùng claim qua policy `PersonalIntelligenceTradingRun` của sổ trading | Policy dùng hằng `Permission`. `TradingDeskController` gắn policy Run | Không cấp cho người chỉ đọc |
| `personal_intelligence.trading.approve` một mình | Không | Không có trong assertion Research. Test `Approve_or_exchange_permissions_cannot_read` kỳ vọng 403 | Không dùng làm quyền đọc |
| `personal_intelligence.exchange.read` một mình | Không ở API. UI vẫn cho vào layout | Test 403. `TradingLayout` có mã này | Không suy ra là vào được màn hình thì đọc được publication |
| `personal_intelligence.exchange.connect` một mình | Không ở API. UI vẫn cho vào layout. Policy exchange-read có nhận connect; policy Research không nhận | Test 403. Policy ExchangeRead dòng 51–56 khác policy Research | Không dùng làm quyền đọc |

Rủi ro quyền rộng: một người chỉ cần xem Research vẫn đọc được nếu họ có `trading.run` hoặc role ADMIN. Đó là điều kiện trong policy, không phải một dòng role SQL gắn sẵn. `trading.approve` và hai quyền exchange không mở đường đọc đó.

## Workstream C — Cấu hình vận hành

Không đọc secret, không đọc biến môi trường production, không sửa default.

| Setting | Expected value/condition | Source of truth | Environment owner | Verification evidence | Status |
|---|---|---|---|---|---|
| `PersonalIntelligence__ResearchPublication__Root` | Thư mục publication có `manifest.json` và bốn object. Chuỗi trống hoặc thư mục không tồn tại làm reader trả `MISSING` | `ResearchPublicationOptions.SectionName`; `Program.cs` bind; `FileResearchPublicationReader` dòng 30–36 | `BLOCKED — OWNER INPUT REQUIRED` | Không có giá trị trong ba appsettings. Chưa có đường dẫn đích | `PENDING` |
| Layout publication | `manifest.json`; `lab/LAB_RESULT.json`; `paper/PAPER_OPS_STATUS.json`; `paper/PAPER_RESEARCH_REPORT.json`; `paper/PAPER_OPS_HEALTH.json`; schema `research-publication/v1`; publisher `research-lab-v1` | Tuple `OBJECTS` của publisher và mảng `Objects` của reader | Cùng owner publication | Hai nguồn trong Git khớp tên file và tên hash | Verified in source |
| Quyền thư mục của API | Process chỉ cần đọc và đi xuyên thư mục. Reader dùng `Exists` và `ReadAllBytes`. Lỗi quyền thành `UNREADABLE` | `FileResearchPublicationReader` | Owner process API | Không có lời gọi ghi trong reader | Verified in source. Quyền OS đích chưa kiểm |
| `PersonalIntelligence__ExchangeBackfill__WorkerEnabled` | `false` trước khi start process mới của release này | Default `true` ở `ExchangeBackfillOptions`. Worker thoát khi false. Bind ở `Program.cs` | `BLOCKED — OWNER INPUT REQUIRED` | Default code đã đọc. Process đích không được đọc | `PENDING` |
| Exchange credential | Không cần cho Research read. Không đặt `PersonalIntelligence__ExchangeCredentialKey` hoặc `PersonalIntelligence__ExchangeCredentialKeyId` cho lần chạy này | Research controller chỉ phụ thuộc reader. Key ring chỉ được gọi ở đường exchange | Cùng owner process | Đường code Research không gọi `RequireCurrent` khi worker tắt | `PENDING` xác nhận trên process đích |
| Publication không phải web root | Không mount thư mục publication thành static root | `src/KitPlatform.Api` không có `UseStaticFiles` | Owner deploy | Tìm trong project API | Verified in source. Cấu hình web server ngoài repo chưa kiểm |
| Appsettings dùng chung | Không ghi path máy local | Ba appsettings của `KitPlatform.Api` | Không đặt giá trị trong Git | Grep không thấy Personal Intelligence hay `Kit-Brain` | Verified |
| Rollback publication | Giữ một bản sao thư mục trước khi publish. Sau publish thành công, script xóa `.previous` | `publish_research_v1.py` xóa previous sau `staging.replace`; README cùng quy tắc | `BLOCKED — OWNER INPUT REQUIRED` | Code và README. Chưa thấy bản sao vận hành | `PENDING` |

Root trống trả `MISSING` cùng khi thiếu manifest hoặc thiếu một object bắt buộc. `FRESH` chỉ khi health là `OK`. Hash lệch trả `INCONSISTENT`. Những hợp đồng này nằm trong reader và test nguồn; kết quả chạy test là `REPORTED`, không chạy lại trong mission này.

## Workstream D — Evidence

Không chạy lại build hoặc test. Không có thay đổi code so với candidate trước tài liệu này. Đếm test trong nguồn chỉ để đối chiếu số lượng đã báo cáo, không biến `REPORTED` thành `PASS`.

| Kiểm tra | Số đã báo | Đối chiếu nguồn | Trạng thái |
|---|---|---|---|
| Publisher unittest | 7 PASS | 7 method trong `tools/research-publication/tests/test_publish_research_v1.py` | `REPORTED` |
| Lab/Paper Ops suite | 14 PASS | Báo cáo ghi chạy ở thư mục lab ngoài Git. Thư mục `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1` còn trên máy. Không chạy lại | `REPORTED` |
| `ResearchPublicationTests` + `SecondBrainV1Tests` | 20 PASS | 14 fact Research và 6 fact SecondBrain. Sáu fact sau là luật trading/paper trong bộ nhớ, không phải publication | `REPORTED` |
| API build | PASS | Chỉ có trong readiness doc mục 12 | `REPORTED` |
| Admin TypeScript/Vite build | PASS | Chỉ có trong readiness doc | `REPORTED` |
| `research-view.smoke.ts` và `trading-routes.smoke.ts` | PASS | Hai file smoke có trong Git. Không chạy lại | `REPORTED` |
| PostgreSQL integration | BLOCKED/NOT RUN | `KIT_PI_TEST_CONNECTION` không đặt. Test từ chối DB dùng chung theo tên và cổng, rồi ghi dữ liệu và không dọn | `BLOCKED` / `NOT RUN` |

Hash publisher, tính lại trong mission này: `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361`. File lịch sử cùng path trong README có cùng hash. Đó là xác minh byte, không phải kết quả unittest mới.

Điều kiện tối thiểu để mở khóa PostgreSQL integration:

1. Database cô lập, được chứng minh không phải production. Test chỉ kiểm tên `kit_pi_validation`, cấm vài tên và cấm cổng 5432. Nó không chứng minh hostname.
2. `KIT_PI_TEST_CONNECTION` trỏ đúng database đó. Không ghi giá trị vào Git hoặc vào báo cáo.
3. Owner chấp nhận test sẽ ghi tenant, user, person, permission, và role, rồi tự dọn vì `DisposeAsync` không xóa.
4. Kết quả chạy được lưu cùng commit candidate. Chưa có kết quả nào để lưu.

Trước release, nếu chủ dự án không chấp nhận `NOT RUN`, cần chạy đúng bộ đã báo cáo trên cùng commit: publisher unittest, filter `ResearchPublicationTests|SecondBrainV1Tests`, `dotnet build` API, và `npm run build` admin. Smoke Research và trading routes nằm trong bằng chứng UI đã báo cáo. Không chạy migration để làm các test đó xanh.

## Việc chưa xác minh

- Database đích đã áp 002, 051, 071 hay chưa. Chỉ thấy tên file trong manifest prod.
- Database Content đã có `narrative_json` hay chưa.
- Process production hiện có đặt worker và publication root hay không. Không đọc môi trường production.
- Web server ngoài API có phục vụ thư mục publication hay không.
- Vì sao số 386 được nhắc trong comment mà không có file.
- Kết quả PASS của build và test. Chúng là báo cáo trước, không chạy lại.

## Git của mission này

Chỉ thêm file này. Không commit và không push.
