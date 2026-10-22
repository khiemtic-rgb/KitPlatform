# Second Brain Research — sẵn sàng triển khai

Nhiệm vụ: `SECOND-BRAIN-RESEARCH-DEPLOY-READINESS-001`  
Ngày: 2026-10-22  
Trạng thái: **NOT READY**

Không deploy production. Không chạy migration production. Không bật live trading. Không merge vào `main`.

## 1. Việc đã làm

Research Publication API, UI Second Brain, test, migration của pack và tài liệu được đưa lên nhánh riêng xuất phát từ `origin/main` đã fetch, không đụng working tree bẩn của `E:\KitPlatform`.

`ExchangeBackfillOptions` nhận cấu hình `PersonalIntelligence:ExchangeBackfill`. Giá trị mặc định `WorkerEnabled=true` không đổi. Máy chủ phải đặt `false` cho đến khi có quyết định vận hành giao dịch riêng.

Publisher `publish_research_v1.py` vẫn ở `E:\Kit-Brain\second-brain-trading-source-inventory\research-lab-v1`. Thư mục đó không phải git repo. Không di chuyển, không tạo repo mới, không xóa dữ liệu nghiên cứu.

## 2. Git

| Mục | Giá trị |
|---|---|
| Remote | `https://github.com/khiemtic-rgb/KitPlatform.git` |
| `origin/main` sau fetch | `608bc054c9b6389190c4c214ac478b3397b110dc` |
| Nhánh gói | `second-brain-research-readiness` |
| Commit gói | `cffb50f20af27066362163ee41387d530abe8a6b` |
| Working tree gốc | `E:\KitPlatform`, branch `recovery/production-source-20261021`, HEAD `8c59ceb4` |
| Lệch với `origin/main` | 198 commit sau, 6 commit trước. Không merge. |

Working tree gốc vẫn giữ thay đổi ngoài phạm vi: TestPack, JWT query token, Famixa, `package.json`, auth contracts, vertical marketing. `Program.cs` ở cây gốc không được stage nguyên file.

## 3. File theo nhóm

Trong nhánh gói:

- API Research: pack `src/Packs/PersonalIntelligence`, bind trong `Program.cs`, hai `ProjectReference` trong `KitPlatform.Api.csproj`, `PlatformModuleCodes.PersonalIntelligence`, `CoreAuthority.cs`.
- UI: `client/admin/src/modules/second-brain`, route trong `router.tsx`, `registry.tsx`, `platform-feature-map.ts`, `vite.second-brain.config.ts`.
- Test: `tests/KitPlatform.PersonalIntelligence.Tests`.
- Vận hành local: `scripts/second-brain-trading-ui-start.ps1`, `scripts/second-brain-trading-ui-stop.ps1`.
- Migration nguồn, chưa chạy: `migrations/385`, `387`–`393`, manifest `deploy/ubuntu/migration-files.personal-intelligence.txt`. Manifest này không nằm trong `migration-files.prod.txt`.
- Publisher: không có trong repo.

## 4. Build và test

| Kiểm tra | Kết quả |
|---|---|
| `dotnet test` filter `ResearchPublicationTests\|SecondBrainV1Tests` trên cây gốc | PASS, 20 |
| Cùng filter trên nhánh từ `origin/main` | PASS, 20 |
| `dotnet build` pack test trên `origin/main` | PASS, 0 errors |
| `dotnet build src/KitPlatform.Api` trên cây gốc local | PASS, 0 errors |
| `dotnet build src/KitPlatform.Api` trên `origin/main` + Research | FAIL, 2 errors có sẵn trong Content: `BrandNarrativeDna.cs` gọi `Territory` và `NarrativeFit` nhưng `ContentBrandFitDto` không có hai thuộc tính đó. Không sửa Content trong nhiệm vụ này. |
| `python -m unittest tests.test_research_lab_v1 tests.test_paper_ops_v1 tests.test_publish_research_v1` | PASS, 14 |
| `npx tsx research-view.smoke.ts` và `trading-routes.smoke.ts` | PASS |
| `npm run build` admin trên `origin/main` | NOT RUN. Cây `origin/main` không có `node_modules` riêng. Build admin đã PASS trên cây local ở nhiệm vụ nghiệm thu trước. |
| Test PostgreSQL của pack | NOT RUN. Không chạy migration và không ghi database để phục vụ đóng gói. |

`Newtonsoft.Json` 10.0.3 (GHSA-5crp-9r3c-p9vr) là cảnh báo có sẵn của host và các pack khác trên `origin/main`. Pack Research không khai báo trực tiếp gói này.

## 5. Blocker

1. API host trên `origin/main` không build vì lỗi Content ở trên. Chưa đủ điều kiện tạo artifact triển khai từ đúng base.
2. Publisher không được quản lý bằng Git. Không có remote để đồng bộ mã `research-lab-v1`.
3. Module `personal_intelligence` chưa có trên production cho đến khi người phụ trách chạy manifest `migration-files.personal-intelligence.txt`. Không chạy trong nhiệm vụ này. Không thêm manifest này vào `migration-files.prod.txt`.
4. Quyền `personal_intelligence.research.read` không được seed bởi migration 390. ADMIN hoặc `personal_intelligence.trading.run` đọc được Research. Không đổi chính sách phê duyệt.
5. `ExchangeBackfillWorker` mặc định bật. Production phải đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false` trước khi chạy process API mới.
6. Nhánh local `recovery/production-source-20261021` lệch 198/6 so với `origin/main`. Không merge.

## 6. Cấu hình máy chủ

Không ghi giá trị bí mật.

| Biến | Việc cần |
|---|---|
| `PersonalIntelligence__ResearchPublication__Root` | Thư mục publication đã có `manifest.json` schema `research-publication/v1`. Để trống thì API trả `MISSING`. Không trỏ vào journal, cache, hay snapshot. |
| `PersonalIntelligence__ExchangeBackfill__WorkerEnabled` | `false` cho lần triển khai Research này. |
| `PersonalIntelligence__ExchangeCredentialKey` | Chỉ khi bật kết nối sàn. Không đặt cho lần đọc Research. Không commit khóa. |
| JWT và connection string | Dùng cấu hình API hiện có. Không thêm secret vào repo. |

Publisher, trên máy có mã `research-lab-v1` và bốn file nguồn đã hoàn tất:

```
python publish_research_v1.py --destination <thư mục publication> --lab <LAB_RESULT.json> --status <PAPER_OPS_STATUS.json> --report <PAPER_RESEARCH_REPORT.json> --health <PAPER_OPS_HEALTH.json>
```

Lệnh thất bại trước khi đổi thư mục đích nếu nguồn thiếu hoặc JSON hỏng. Không sửa file nguồn để vượt validation.

Paper Ops là lịch nghiên cứu riêng, mô tả trong `PAPER_OPS_SERVER_RUNBOOK.md` của kho lab. Không gửi lệnh sàn. Không đặt `BINANCE_API_KEY` hay `BINANCE_API_SECRET` vào môi trường của lịch đó.

UI admin gọi `/api/personal-intelligence/research` qua proxy sẵn có. Không có nút chạy experiment, phê duyệt, hay gửi lệnh trong khối Research.

## 7. Checklist khi được phép triển khai

Chỉ làm sau khi hết blocker ở mục 5 và có người phụ trách cho migration.

1. Xác nhận `origin/main` build được `src/KitPlatform.Api` trước khi gộp nhánh Research.
2. Gộp `second-brain-research-readiness` bằng pull request. Không force-push.
3. Đặt `PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false`.
4. Chạy publisher ra một thư mục trên máy chủ. Kiểm tra `manifest.json` có `research-publication/v1` và bốn hash.
5. Đặt `PersonalIntelligence__ResearchPublication__Root` vào thư mục đó.
6. Áp dụng `deploy/ubuntu/migration-files.personal-intelligence.txt` bằng quy trình migration đã review, không trộn vào manifest Pharmacy, Family OS, Content, hay Local OS.
7. Bật module `personal_intelligence` cho đúng tenant. Cấp `personal_intelligence.research.read` cho người cần đọc. Không cấp quyền gửi lệnh.
8. Deploy API và admin từ commit đã gộp. Không restart PostgreSQL ngoài quy trình deploy hiện có.

Smoke sau deploy:

- `GET /api/personal-intelligence/research/publication` không token trả 401.
- Cùng path với user có quyền trả 200, `schemaVersion` `research-publication/v1`, publication id và hash khớp manifest.
- Ba experiment id lấy từ publication. Detail giữ `EXPERIMENT_NOT_APPROVED`, `INSUFFICIENT_EVIDENCE`, `NOT_A_PERFORMANCE_CLAIM`.
- Paper status và report giữ `sampleWarning` nguồn. `FRESH` chỉ khi health nguồn là `OK`.
- `/second-brain/overview` hiện khối Research tách khỏi sổ giao dịch. Số sổ giao dịch không tăng vì số liệu research.
- Response không có path, API key, journal, cache, hay field `frozen_lab_result`.

Rollback:

- Trả API và admin về artifact trước lần deploy.
- Xóa hoặc để trống `PersonalIntelligence__ResearchPublication__Root`. Route biến mất khi artifact cũ không có controller. Root trống trả `MISSING`, không xóa publication trên đĩa.
- Giữ `WorkerEnabled=false`.
- Không xóa schema `pack_personal_intelligence`, journal, snapshot, ledger, hay thư mục publication trong rollback của lần đọc này.
- Dừng lịch Paper Ops bằng `systemctl stop paper-ops.timer` hoặc `schtasks /End /TN SecondBrainPaperOpsServer` nếu lịch đó đã được bật riêng. Không đụng task local `SecondBrainPaperOpsV1`.

## 8. Kết luận

`NOT READY`.

Mã Research đã được đóng trên nhánh từ `origin/main` và test pack PASS. Chưa deploy được vì API `origin/main` đang lỗi build Content, publisher chưa có Git, và migration module chưa được phép chạy.

## 9. Xác nhận

Nhiệm vụ này không deploy production, không restart dịch vụ production, không chạy migration production, không đổi dữ liệu production, không gửi lệnh sàn.
