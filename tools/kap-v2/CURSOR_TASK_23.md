# CURSOR TASK 23 — Đưa phần chấm điểm PHARMACY_V2 vào main, rồi mới thay DLL (giao lúc 2026-11-20 08:20 +07)

## Tình hình lúc giao

- `origin/main` = `49ffc84b`: có PDF mới (Task 22), nhưng **chưa có** phần chấm điểm PHARMACY_V2 đang chạy trên VPS:
  - P3 là câu "điều lo nhất", không tính điểm;
  - nhóm toàn câu "không biết" không bị tính 0 điểm.
- Phần chấm điểm này chỉ nằm ở nhánh local `kap-v2-backend` (`1e68a1a7`), chưa push.
- Không thay DLL lúc này là đúng.

Quy định chung:
- Không ép push.
- Push chỉ khi người dùng gõ "push"; deploy chỉ khi người dùng gõ "deploy".
- Ghi giờ (+07) mỗi bước vào `tools/git-safety/LOG_<ngày>.md`.
- **Mọi việc làm trong worktree riêng** (`git worktree add E:\wt\...`). Không checkout, rebase hay stash trên cây làm việc chính `E:\KitPlatform`.
  - Lúc 23:44 ngày 19/11, file `client/assessment-web/src/shared/report/owner-report.ts` và `tools/kap-v2/owner-report.fixtures.json` trên cây chính đã bị đưa về bản cũ, mất phần Claude sửa lúc 23:35.
  - Không được để lặp lại.

## 1. Gộp `kap-v2-backend` với main

1. Worktree `E:\wt\kap-v2-merge` từ `origin/main` (49ffc84b), nhánh `kap-v2-merge_2026-11-20`.
2. `git cherry-pick 1e68a1a7`.
   - Có xung đột thì giữ **cả hai**: phần chấm điểm V2 của 1e68a1a7, và phần PDF hoặc `KapV2OwnerReportBuilder` của 49ffc84b. Ghi lại từng file xung đột và cách gộp.
   - Không chắc thì DỪNG hỏi.
3. Build Release `src/Packs/Survey`: 0 lỗi.
4. Kiểm tra chấm điểm không đổi. Chấm lại bằng code của nhánh này (test hoặc chạy lệnh tính, không ghi vào DB) cho 6 bài:
   - 5 bài `qa5_19_11`;
   - c321edfc.

   `overallPct` và `scorePct` từng nhóm phải **bằng đúng** số đang lưu trên production:

   | Bài | overallPct |
   |---|---|
   | 25ad3848 | 48.3 |
   | 1faa94d6 | 68.9 |
   | 448e10a7 | 36.7 |
   | c086b505 | 68.7 |
   | 227bc585 | 26.7 |
   | c321edfc | 49 (lấy từ DB) |

   Nhóm GROWTH của 25ad3848, 448e10a7, c086b505 phải vẫn là "chưa đủ dữ liệu". Lệch bất kỳ số nào thì DỪNG.
5. Test file mẫu PDF của Task 22 vẫn pass.
6. So mã băm: build lại nhánh này với `-p:SourceRevisionId=<mã của bản VPS>` không còn khớp với VPS là bình thường, vì đã thêm code PDF. Chỉ cần mục 4 khớp số.
7. Commit (cherry-pick đã tạo commit). Báo hash. Chờ "push" rồi fast-forward main.

## 2. Thay 2 DLL Survey trên VPS (chờ "deploy")

Chỉ làm sau mục 1, từ commit đã có trên origin/main:
1. Build Release từ worktree sạch tại đúng commit đó.
2. Sao lưu 2 DLL đang chạy: `/var/www/kit-platform/api-backup-<YYYYMMDD_HHMM>/`.
3. Thay đúng 2 DLL:
   - `KitPlatform.Packs.Survey.Application.dll`
   - `KitPlatform.Packs.Survey.Infrastructure.dll`

   Kiểm tra trước: các DLL phụ thuộc khác (`KitPlatform.Application.dll`, `KitPlatform.Infrastructure.dll`…) không đổi phiên bản. Nếu đổi thì DỪNG, vì đây là thay DLL riêng lẻ, không deploy cả API.
4. Khởi động lại dịch vụ API, giống ngày 19/11.
5. Ghi `/var/www/kit-platform/api/SURVEY_VERSION.txt` (commit, giờ, người làm).
6. Kiểm tra sau khi thay:
   - `GET /api/public/assessment/templates/PHARMACY_V2` trả 200.
   - Tạo PDF admin cho c321edfc và 25ad3848: 2–4 trang (bản admin thêm 1 trang), đủ chữ "phân tích", "ưu tiên", "tiền".
   - Điểm của 6 bài ở mục 1.4 trên trang admin không đổi.
   - Chạy `scripts/check-survey-sync.ps1`.
7. Có lỗi thì đưa 2 DLL sao lưu về ngay và báo lại.

## 3. Đưa lại phần Claude sửa lúc 23:35 (sau mục 2)

Bản đúng nằm ở **chỗ riêng, không phải cây chính**:
- `tools/kap-v2/pending/owner-report.ts`: thay cho `client/assessment-web/src/shared/report/owner-report.ts`.
- `tools/kap-v2/pending/owner-report.fixtures.json`: thay cho `tools/kap-v2/owner-report.fixtures.json`.

Nội dung thay đổi:
- Mục "Điều anh/chị lo nhất" nhắc lại câu trả lời của nhà thuốc ("Anh/chị cho biết khó nhất là…").
- Bảng `CONCERN_QUESTIONS`: việc gắn với câu trả lời rẽ nhánh được nhân hệ số 1.5 khi xếp.

Các bước, trong worktree mới `E:\wt\owner-report-concern` từ origin/main:
1. Chép 2 file pending vào đúng chỗ.
2. Sửa `KapV2OwnerReportBuilder.cs` cho khớp: thêm `CONCERN_QUESTIONS`, hệ số 1.5, các câu chữ mới. Chạy test file mẫu: phải khớp 100%.
3. `cd client/assessment-web && npx tsc --noEmit && npm run build`: pass. Build Release Survey: pass.
4. Commit: `fix(kap-v2): echo the owner's concern and rank matching actions first [<giờ> +07]`.
5. Chờ "push". Rồi chờ "deploy":
   - giao diện: như TASK 18 mục 2, ghi VERSION.txt;
   - DLL: như mục 2 ở trên.

   Web và PDF phải lên **cùng lúc**, để hai bên luôn nói giống nhau.
6. Kiểm tra: trang báo cáo c321edfc có câu "Anh/chị cho biết khó nhất là kiểm soát tiền và hàng", PDF admin của c321edfc có cùng câu đó.
7. Xong thì xóa thư mục `tools/kap-v2/pending/` trong commit tiếp theo.

## Báo lại

| Mục | Giờ xong (+07) | Hash | Kết quả kiểm tra | Đang chờ |
|---|---|---|---|---|
| 1 Gộp | | | điểm 6 bài khớp / lệch | "push" |
| 2 DLL | | | PDF, điểm, sync | — |
| 3 Lời mới | | | test mẫu, web và PDF cùng câu | "push" / "deploy" |
