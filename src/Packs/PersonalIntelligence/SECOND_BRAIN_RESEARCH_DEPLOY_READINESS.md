# Second Brain Research — sẵn sàng triển khai

Nhiệm vụ: `SECOND-BRAIN-RESEARCH-BLOCKER-RESOLUTION-001`  
Ngày: 2026-10-22  
Trạng thái: **NOT READY**

Không deploy production. Không chạy migration production. Không bật live trading. Không merge vào `main`.

API host trên nhánh Research đã build được. Chưa deploy được: admin production build vẫn lỗi TypeScript có sẵn ngoài Second Brain, publisher chưa nằm trong Git, và migration cùng quyền đọc chưa được phép áp dụng.

## 1. Blocker build đã xử lý

Lỗi `BrandNarrativeDna.cs` có trên `origin/main` `608bc054` trước khi thêm Research. Hai commit Research `cffb50f2` và `d2c28a3a` không đụng Content. Trước bản sửa, `git diff origin/main` của các file Content dưới đây là rỗng, và `dotnet build src/KitPlatform.Api` trên nhánh dừng ở `ContentBrandFitDto` không có `Territory` và `NarrativeFit`.

Ý nghĩa đã có trong mã đã commit, không phải hợp đồng mới:

- `BrandNarrativeDna.SeriesEntryBlock` trả `NarrativeFitRequired` khi territory trống. Territory `off-brand` hoặc `narrativeFit == false` là `OFF-BRAND`. Territory trống không bị coi là `core`.
- `ContentArticleSeriesService` đã đọc `Territory`, `TerritoryId`, `BrandAngle`.
- Migration `384_pack_content_brand_narrative_dna.sql` đã thêm `pack_content.brand.narrative_json`. `BrandRow` và hai câu SELECT chưa map cột này, trong khi series gọi `brand.NarrativeJson`.
- `NarrativeFitIntegrityTests` đã truyền `narrativeFit` rồi `territory`, và đã gọi `ContentQualityGate.NarrativeBoundary`. Project test platform không biên dịch được trước bản sửa này.

Bản sửa chỉ nối những chỗ đó:

- `ContentBrandFitDto` thêm bốn đối số tùy chọn ở cuối: `NarrativeFit`, `Territory`, `TerritoryId`, `BrandAngle`. Caller cũ vẫn biên dịch. Tên JSON là camelCase `narrativeFit`, không dùng tên `fit` đang nằm ở working tree chưa commit.
- `BrandRow.NarrativeJson` và hai SELECT `narrative_json`. Không đổi INSERT/UPDATE, nên lần lưu brand không ghi đè `narrative_json` bằng `{}`.
- `ContentQualityGate` thêm `NarrativeBoundary`. Territory trống không phải vi phạm. `off-brand` hoặc `narrativeFit == false` chặn duyệt và publish. Caller không truyền hai đối số mới giữ hành vi cũ.
- `ContentQualityRunner` và nhánh gate trong `ContentGenerateService` đọc territory của brand trên package. Không viết lại prompt.

Không xóa chỗ dùng thuộc tính, không bỏ test, không tắt kiểm tra. Không thêm `Confidence` hay `BoundaryWarnings`.

Bằng chứng sau bản sửa, trên `E:\KitPlatform-sb-research`:

- `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` — PASS, 0 errors.
- `dotnet test tests/KitPlatform.Platform.Tests/KitPlatform.Platform.Tests.csproj --filter NarrativeFitIntegrityTests` — PASS, 13. Test mới `Stored_brand_fit_keeps_territory_and_does_not_treat_a_missing_one_as_core` giữ `off-brand` khi JSON có territory, và `NarrativeFitRequired` khi JSON cũ không có territory.

Commit sửa: `96657e06a6bfe36bfc3ccfe6b55cdee4d2fadd50`.

## 2. Git

| Mục | Giá trị |
|---|---|
| Remote | `https://github.com/khiemtic-rgb/KitPlatform.git` |
| `origin/main` | `608bc054c9b6389190c4c214ac478b3397b110dc` |
| Nhánh | `second-brain-research-readiness` |
| Commit gói Research | `cffb50f20af27066362163ee41387d530abe8a6b` |
| Commit báo cáo gói | `d2c28a3a10073b44bb512299e1487716aa76470a` |
| Commit sửa Content | `96657e06a6bfe36bfc3ccfe6b55cdee4d2fadd50` |
| Working tree gốc | `E:\KitPlatform`, branch `recovery/production-source-20261021`, HEAD `8c59ceb4` |

Làm việc trong worktree `E:\KitPlatform-sb-research`. Không stage `Program.cs`, Famixa, `package.json`, auth, hay working tree bẩn. Không merge, không force-push.

## 3. Build và test trên nhánh công việc

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build src/KitPlatform.Api/KitPlatform.Api.csproj` | PASS, 0 errors. Cảnh báo NU1903 `Newtonsoft.Json` 10.0.3 (GHSA-5crp-9r3c-p9vr) có sẵn trên host, không phải gói do Research khai báo. |
| `dotnet test` filter `NarrativeFitIntegrityTests` | PASS, 13 |
| `dotnet test` filter `ResearchPublicationTests\|SecondBrainV1Tests` | PASS, 20 |
| `python -m unittest tests.test_research_lab_v1 tests.test_paper_ops_v1 tests.test_publish_research_v1` tại `research-lab-v1` | PASS, 14 |
| `npx tsx src/modules/second-brain/research-view.smoke.ts` và `trading-routes.smoke.ts` | PASS |
| `npm run build` trong `client/admin` (`tsc --noEmit && vite build`) | FAIL. `tsc` dừng với 89 errors. Không có error trong `src/modules/second-brain`. Các file lỗi khớp `origin/main` (diff rỗng). Phần lớn nằm ở Famixa và kit-video; còn `GoodsReceiptListPage.tsx`, `AdjustmentListPage.tsx`, `PosConsultationModal.tsx`. Không sửa trong nhiệm vụ này vì đó là mặt Content và Pharmacy ngoài phạm vi Research. |
| Test PostgreSQL của Personal Intelligence | NOT RUN. Test đòi `KIT_PI_TEST_CONNECTION`, từ chối cổng 5432, và ghi vào database đích. Không có database cô lập đã xác minh. Không dùng production và không dùng Postgres đang nghe 5432. |

## 4. Publisher

Nguồn vẫn là `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1`. Thư mục không phải git repo. Không di chuyển, không tạo repository, không xóa dữ liệu nguồn.

SHA-256 của `publish_research_v1.py` tại thời điểm kiểm tra: `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361`.

Cách tạo publication từ bốn artifact:

```
python publish_research_v1.py --destination <thư mục publication> --lab <LAB_RESULT.json> --status <PAPER_OPS_STATUS.json> --report <PAPER_RESEARCH_REPORT.json> --health <PAPER_OPS_HEALTH.json>
```

`--published-at` là tùy chọn. Schema `research-publication/v1`. Bốn object: lab, status, report, health. Script đọc và parse JSON trước khi đổi thư mục đích. File thiếu hoặc JSON hỏng in `PUBLISH_FAILED` và trả mã 1, không gọi bước đổi chỗ. Status bỏ mọi key có `api_key`. Report bỏ `frozen_lab_result`. `publicationId` là `pub-` cộng 16 ký tự hex của SHA-256 bốn hash. Manifest ghi bốn hash đó.

Đổi thư mục là staging rồi hai lần rename, không phải một syscall. Script xóa bản `.previous` sau khi publish thành công, nên rollback nội dung phải dựa trên một bản sao operator giữ riêng, hoặc publish lại đúng bốn artifact của lần trước. Không sửa file nguồn để vượt validation.

Quyền: process API chỉ cần đọc thư mục publication. Không đặt thư mục đó làm web root. Không trỏ vào journal, cache, hay snapshot. Không ghi path máy local vào `appsettings` dùng chung.

Phương án quản lý phiên bản, chưa thực hiện, cần người phụ trách chọn một:

1. Đưa `research-lab-v1` vào một git repository riêng và ghi commit đó cạnh commit KitPlatform lúc deploy.
2. Chỉ đưa `publish_research_v1.py` vào KitPlatform sau khi được duyệt, pin bằng SHA-256 ở trên. Dữ liệu lab, journal, snapshot và ledger ở ngoài repo.

Cho đến khi có lựa chọn đó, pin vận hành là SHA-256 của script ở trên cộng manifest của từng publication.

## 5. Quyền và cấu hình

Policy `PersonalIntelligenceResearchRead` không đổi: ADMIN, `personal_intelligence.research.read`, hoặc `personal_intelligence.trading.run`. `trading.approve`, `exchange.read` và `exchange.connect` một mình không đủ.

Migration `390_platform_module_personal_intelligence.sql` chỉ thêm `personal_intelligence` vào `platform_module_registry`. Không seed `personal_intelligence.research.read`. File nằm trong `deploy/ubuntu/migration-files.personal-intelligence.txt` cùng 385 và 387–393. Không nằm trong `migration-files.prod.txt`. Không chạy.

Cách cấp quyền đọc tối thiểu, chưa thực hiện: sau khi module được bật cho đúng tenant, gán `personal_intelligence.research.read` cho role chỉ cần đọc, qua cách gán permission tenant hiện có. Không gán `personal_intelligence.trading.run` cho người chỉ đọc, vì claim đó còn mở đường chạy trading. Không đổi policy. Không mở thêm quyền.

`PersonalIntelligence__ResearchPublication__Root` được bind từ `PersonalIntelligence:ResearchPublication` trong `Program.cs` của nhánh. Root trống làm API trả `MISSING`. Không ghi giá trị vào Git.

`ExchangeBackfillOptions.WorkerEnabled` vẫn mặc định `true`. Section `PersonalIntelligence:ExchangeBackfill` đã được bind. Lần triển khai Research bắt buộc đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false` trên process API. Không đổi mặc định trong mã và không đặt credential sàn để vượt blocker.

## 6. Việc còn cần phê duyệt

1. Chủ Content và admin: 89 lỗi TypeScript chặn `npm run build` trên chính `origin/main`. Second Brain không có trong danh sách lỗi.
2. Chủ publisher: chọn repository riêng hoặc vendoring script đã duyệt. Chưa được phép chuyển thư mục lab.
3. Chủ migration: cho phép áp `deploy/ubuntu/migration-files.personal-intelligence.txt` lên database đích đã review. Không thêm manifest này vào `migration-files.prod.txt` khi chưa có quyết định đó.
4. Chủ quyền: bật module `personal_intelligence` cho đúng tenant và gán `personal_intelligence.research.read` cho người chỉ đọc.
5. Vận hành: đặt `WorkerEnabled=false` và root publication trước khi chạy process API mới. Không bật kết nối sàn.

## 7. Checklist khi được phép triển khai

Chỉ làm sau khi `npm run build` admin PASS trên commit được deploy, publisher đã được pin, và người phụ trách đã duyệt migration cùng quyền.

1. Xác nhận commit deploy build được `src/KitPlatform.Api` và `npm run build` admin.
2. Gộp `second-brain-research-readiness` bằng pull request. Không force-push.
3. Đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`. Không đặt `PersonalIntelligence__ExchangeCredentialKey` cho lần đọc này.
4. Giữ một bản sao publication đang phục vụ. Chạy publisher ra thư mục mới. Kiểm tra `manifest.json` schema `research-publication/v1` và bốn hash.
5. Đặt `PersonalIntelligence__ResearchPublication__Root` vào thư mục đó.
6. Áp dụng `deploy/ubuntu/migration-files.personal-intelligence.txt` bằng quy trình đã review. Không trộn Pharmacy, Family OS, Content, hay Local OS.
7. Bật module cho đúng tenant. Cấp `personal_intelligence.research.read`. Không cấp quyền gửi lệnh.
8. Deploy API và admin từ commit đã gộp. Không restart PostgreSQL ngoài quy trình deploy hiện có.

Smoke sau deploy:

- `GET /api/personal-intelligence/research/publication` không token trả 401.
- Cùng path với user có quyền đọc trả 200, `schemaVersion` `research-publication/v1`, publication id và hash khớp manifest.
- Detail experiment giữ `EXPERIMENT_NOT_APPROVED`, `INSUFFICIENT_EVIDENCE`, `NOT_A_PERFORMANCE_CLAIM`.
- `FRESH` chỉ khi health nguồn là `OK`.
- `/second-brain/overview` hiện khối Research tách khỏi sổ giao dịch. Số sổ giao dịch không đổi vì số liệu research.
- Response không có path máy, API key, journal, cache, hay field `frozen_lab_result`.

Rollback:

- Trả API và admin về artifact trước lần deploy.
- Trỏ `PersonalIntelligence__ResearchPublication__Root` về bản sao publication trước đó, hoặc để trống. Root trống trả `MISSING` và không xóa thư mục trên đĩa.
- Giữ `WorkerEnabled=false`.
- Không xóa schema `pack_personal_intelligence`, journal, snapshot, ledger, hay artifact nguồn.
- Nếu lịch Paper Ops đã được bật riêng, dừng bằng `systemctl stop paper-ops.timer` hoặc `schtasks /End /TN SecondBrainPaperOpsServer`. Không đụng task local `SecondBrainPaperOpsV1`.

## 8. Kết luận

`NOT READY`.

Nguyên nhân build API đã xác định và đã sửa có test. Pack Research, publisher, lab, Paper Ops và UI smoke đều PASS. Admin không tạo được artifact vì 89 lỗi TypeScript có sẵn ngoài Second Brain. Publisher chưa có phiên bản trong Git. Migration 390 và quyền `personal_intelligence.research.read` chưa được phép áp dụng. Mặc định worker backfill vẫn bật và phải tắt bằng cấu hình môi trường.

## 9. Xác nhận

Nhiệm vụ này không deploy production, không restart dịch vụ production, không chạy migration production, không đổi dữ liệu production, không gửi lệnh sàn, và không sửa snapshot, ledger, journal hay checkpoint.
