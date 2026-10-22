# Second Brain Research — sẵn sàng triển khai

Nhiệm vụ: `SECOND-BRAIN-RESEARCH-RELEASE-CANDIDATE-001`  
Ngày: 2026-10-22  
Trạng thái: **NOT READY**

Không deploy production. Không chạy migration production. Không bật live trading. Không merge vào `main`.

Ứng viên review là nhánh `admin-build-blocker-001`. API và admin build trên nhánh đó đã PASS. Chưa deploy được: publisher chưa được pin trong Git, migration 390 chưa được duyệt, quyền `personal_intelligence.research.read` chưa được seed, và `WorkerEnabled` vẫn mặc định bật.

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
| `origin/main` | `608bc054c9b6389190c4c214ac478b3397b110dc` — không đổi sau khi push ứng viên |
| `origin/second-brain-research-readiness` | `a0374a05fd614e8e524fc304cc44aee11bdf3e57` |
| Ứng viên release | `admin-build-blocker-001` |
| Commit gói Research (API) | `cffb50f20af27066362163ee41387d530abe8a6b` |
| Commit báo cáo gói | `d2c28a3a10073b44bb512299e1487716aa76470a` |
| Commit sửa Content | `96657e06a6bfe36bfc3ccfe6b55cdee4d2fadd50` |
| Commit sửa admin | `c57d8d7e3b44a263b79fe8215cac88b3d5ad8635` |
| Commit bằng chứng admin | `c75b4d49076cd35559f51349f4360ac7ba0965e4` |
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
| `npm run build` trong `client/admin` (`tsc --noEmit && vite build`) trên nhánh `admin-build-blocker-001` | PASS. Trước khi sửa, `tsc` dừng với 89 errors, không có error trong `src/modules/second-brain`. Sau commit `c57d8d7e` thì `tsc` 0 errors và `vite build` xong. Strict mode, `noUnusedLocals`, `noUnusedParameters` vẫn bật. |
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

Hai phương án pin publisher. Chưa thực hiện. Không chuyển thư mục lab, không tạo repository, không đưa script vào KitPlatform khi chưa được duyệt.

Phương án A — repository riêng cho `research-lab-v1`:

- Ghi commit publisher cạnh commit KitPlatform lúc deploy.
- Ưu: lab, journal, snapshot và ledger không vào KitPlatform. API và script pin độc lập. Rollback publisher là checkout commit cũ rồi publish lại.
- Nhược: cần một remote mới và một người phụ trách. Hai nơi phải cùng có mặt trong lần deploy. Submodule chưa được duyệt.

Phương án B — chỉ vendor `publish_research_v1.py` vào KitPlatform sau khi được duyệt:

- Pin SHA-256 `D1F3EF3D066F70B167848FC9AA5245107B9ED7347E29C6C4F5E9CAE3AE333361`, xác nhận lại 2026-10-22. Thư mục lab vẫn không phải git repo.
- Ưu: một commit deploy chứa đúng script mà API đọc. Dữ liệu nghiên cứu ở ngoài Git.
- Nhược: bản trong KitPlatform có thể lệch bản trong lab nếu lab vẫn là nguồn sửa. Không được commit artifact, journal, hay credential cùng script.

Cho đến khi có lựa chọn đó, pin vận hành là SHA-256 của script ở trên cộng manifest của từng publication. File thiếu hoặc JSON hỏng dừng trước bước đổi thư mục. Rollback là bản sao publication operator giữ ngoài slot `.previous`, hoặc publish lại bốn artifact của lần trước.

## 5. Quyền và cấu hình

Policy `PersonalIntelligenceResearchRead` không đổi: ADMIN, `personal_intelligence.research.read`, hoặc `personal_intelligence.trading.run`. `trading.approve`, `exchange.read` và `exchange.connect` một mình không đủ.

Migration `390_platform_module_personal_intelligence.sql` chỉ thêm `personal_intelligence` vào `platform_module_registry`. Không seed `personal_intelligence.research.read`. File nằm trong `deploy/ubuntu/migration-files.personal-intelligence.txt` cùng 385 và 387–393. Không nằm trong `migration-files.prod.txt`. Không chạy.

Cách cấp quyền đọc tối thiểu, chưa thực hiện: sau khi module được bật cho đúng tenant, gán `personal_intelligence.research.read` cho role chỉ cần đọc, qua cách gán permission tenant hiện có. Không gán `personal_intelligence.trading.run` cho người chỉ đọc, vì claim đó còn mở đường chạy trading. Không đổi policy. Không mở thêm quyền.

`PersonalIntelligence__ResearchPublication__Root` được bind từ `PersonalIntelligence:ResearchPublication` trong `Program.cs` của nhánh. Root trống làm API trả `MISSING`. Không ghi giá trị vào Git.

`ExchangeBackfillOptions.WorkerEnabled` vẫn mặc định `true`. Section `PersonalIntelligence:ExchangeBackfill` đã được bind. Lần triển khai Research bắt buộc đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false` trên process API. Không đổi mặc định trong mã và không đặt credential sàn để vượt blocker.

## 6. Việc còn cần phê duyệt

1. Admin build PASS ở `c57d8d7e` trên `admin-build-blocker-001`. Nhánh đã có trên origin. `origin/main` `608bc054` vẫn còn 89 lỗi TypeScript cho đến khi pull request được duyệt và gộp.
2. Chủ publisher: chọn repository riêng hoặc vendoring script đã duyệt. Chưa được phép chuyển thư mục lab.
3. Chủ migration: cho phép áp `deploy/ubuntu/migration-files.personal-intelligence.txt` lên database đích đã review. Không thêm manifest này vào `migration-files.prod.txt` khi chưa có quyết định đó.
4. Chủ quyền: bật module `personal_intelligence` cho đúng tenant và gán `personal_intelligence.research.read` cho người chỉ đọc.
5. Vận hành: đặt `WorkerEnabled=false` và root publication trước khi chạy process API mới. Không bật kết nối sàn.

## 7. Checklist khi được phép triển khai

Chỉ làm sau khi `npm run build` admin PASS trên commit được deploy, publisher đã được pin, và người phụ trách đã duyệt migration cùng quyền.

1. Xác nhận commit deploy build được `src/KitPlatform.Api` và `npm run build` admin.
2. Gộp `admin-build-blocker-001` bằng pull request. Nhánh này đã chứa Research, bản sửa Content `96657e06` và bản sửa admin `c57d8d7e`. Không force-push. Không gộp `recovery/production-source-20261021`.
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

Ứng viên review là `admin-build-blocker-001`. Commit API `cffb50f20af27066362163ee41387d530abe8a6b`. Commit Content `96657e06a6bfe36bfc3ccfe6b55cdee4d2fadd50`. Commit admin `c57d8d7e3b44a263b79fe8215cac88b3d5ad8635`. `origin/main` vẫn `608bc054c9b6389190c4c214ac478b3397b110dc`.

Chưa deploy được vì publisher chưa được pin, migration 390 và quyền `personal_intelligence.research.read` chưa được phép áp dụng, và mặc định worker backfill vẫn bật. Phải tắt bằng cấu hình môi trường. Không gọi trạng thái này là `DEPLOY-READY`.

## 9. Xác nhận

Nhiệm vụ này không deploy production, không restart dịch vụ production, không chạy migration production, không đổi dữ liệu production, không gửi lệnh sàn, và không sửa snapshot, ledger, journal hay checkpoint.

## 10. Admin build — `KITPLATFORM-ADMIN-BUILD-BLOCKER-001`

Nhánh `admin-build-blocker-001` được cắt từ `second-brain-research-readiness` `a0374a05`. Không sửa `main`. Không đổi `tsconfig`, không thêm `any` hoặc `@ts-ignore`, không loại module khỏi build.

89 lỗi trên base thuộc các nhóm sau. Bản sửa bám hợp đồng đã có trong cùng commit:

- Type thiếu field mà caller đã ghi: `mixPrefs`, tab `voice`, `noAverage`, `acceptedTake.url` có thể trống.
- `useRef` của React 19 cần giá trị khởi tạo.
- API trả `null` trong khi helper chỉ nhận `undefined`: ngày lot, tuổi nhân vật, `missingTypes`.
- `apiErrorMessage` bắt buộc chuỗi fallback. Nút tạo phiếu kiểm kê không được nhận mouse event làm preset kho.
- `finalSource` trả về đúng union `FAL | RUNWAY | RUNWAY_TTS`, không phải `string`.
- `shotAction` nằm trên run, không trên shot. Territory era `planned` là trạng thái thật, smoke character create đã yêu cầu.
- Khóa nhận diện shot dùng `master_sha256`, `dna_sha256`, `prp_sha256`. Tên camelCase không có trên spec nên trước đây không so được hash.
- Biến và import không đọc bị gỡ vì `noUnusedLocals`. `insertShot` vẫn sắp theo `displayOrder`; tham số `afterDisplay` không đổi hành vi.

Rủi ro còn lại: so sánh hash nhận diện giờ nhìn đúng field snake_case, nên một spec đổi hash sẽ bị coi là đổi nhận diện. Smoke production shot vẫn PASS. `insertShot` chưa dùng `afterDisplay` để chèn vị trí.

## 11. Ứng viên release — `SECOND-BRAIN-RESEARCH-RELEASE-CANDIDATE-001`

Xác minh lại trước khi push. Remote `https://github.com/khiemtic-rgb/KitPlatform.git`. Worktree `E:\KitPlatform-sb-research` sạch trên `admin-build-blocker-001` tại `c75b4d49`. Lịch sử phía trên `origin/main`: `cffb50f2`, `d2c28a3a`, `96657e06`, `a0374a05`, `c57d8d7e`, `c75b4d49`. `c57d8d7e` là `fix(admin): make the production TypeScript build compile`, 28 file admin. Push thường, không force: `origin/admin-build-blocker-001` nhận `c75b4d49076cd35559f51349f4360ac7ba0965e4`. Sau push, `origin/main` vẫn `608bc054` và `origin/second-brain-research-readiness` vẫn `a0374a05`.

Review `client/admin` trên `c57d8d7e`, không sửa mã:

- `insertShot` sắp theo `displayOrder` rồi đánh lại `SH01-NN`. `void afterDisplay` chỉ làm tham số hết cảnh báo unused. Caller duy nhất là `kit-video-phase03.smoke.ts`: truyền `afterDisplay = 1` và `displayOrder: 1.5`, rồi kiểm tra giữ `shotId` và đánh lại mã hiển thị. Không có assertion nào yêu cầu vị trí theo `afterDisplay`. Không phải regression được chứng minh.
- `identityMutated` so `master_sha256`, `dna_sha256`, `prp_sha256`. Đó là field thật trên `ProductionShotSpec`. Tên camelCase trước đây không có trên spec nên hash không được so. Smoke nhận diện vẫn đổi `face` / `faceReference`. Không có test nào fail vì một hash hợp lệ bị coi là đổi nhận diện.
- `useRef` nhận giá trị khởi tạo `undefined` hoặc `null`, cùng trạng thái ban đầu. Ngày hết hạn GRN rỗng thành `''` đúng contract form. Nút tạo phiếu kiểm kê gọi `openCreate()` nên không đưa mouse event vào preset. Các smoke liên quan PASS.

Kiểm tra lại trên worktree, 2026-10-22:

- `npm run build` — PASS. `tsc --noEmit && vite build`, 3807 modules, khoảng 16s. Cảnh báo CSS `font-size: 22px` và chunk lớn hơn 500 kB có từ trước. Exit 0.
- `research-view.smoke.ts` — PASS, `research view ok`.
- `trading-routes.smoke.ts` — PASS, `trading routes ok`.
- `kit-video-phase03.smoke.ts` — PASS, tests 01–15.
- `kit-video-production-shot.smoke.ts` — PASS, 23 tests.
- `kit-video-production-reference-pack.smoke.ts` — PASS, 32 tests.
- `content-famixa-character-create.smoke.ts` — PASS.

Không có lỗi hành vi tái hiện được. Không có commit sửa mã ở nhiệm vụ này. PostgreSQL test của Personal Intelligence không chạy: chúng cần `KIT_PI_TEST_CONNECTION`, từ chối cổng 5432, và ghi vào database đích.

Blocker còn lại, chưa được nhiệm vụ này quyết:

- Publisher chưa pin. Hai phương án ở mục 4. Script vẫn ở `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1`, không phải git repo.
- Migration `390` chưa được duyệt và không nằm trong `migration-files.prod.txt`.
- `personal_intelligence.research.read` chưa được seed. Không dùng `trading.run` thay cho quyền đọc.
- Production phải đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`. Mặc định trong mã vẫn `true`.
- `origin/main` chưa chứa Research, Content fix, hay admin fix.
