# CURSOR TASK 18 — Gỡ chỗ kẹt của mục B, C (tiếp CURSOR_TASK_17)

Quy định chung giữ nguyên như TASK 17:
- Không ép push.
- Không đụng các thay đổi chưa commit không thuộc khảo sát.
- Không commit tk.txt, .env, appsettings*.json.
- Gặp gì ngoài dự kiến thì DỪNG và báo lại.

## 1. Sửa main đang build hỏng ở assessment-web (repo E:\KitPlatform)

Hiện trạng: main (6a019742) có báo cáo mới, nhưng thiếu 5 file mà báo cáo mới cần. 5 file này chưa từng được commit, trong khi bản đang chạy trên survey.novixa.vn được build kèm chúng. Claude đã so 5 file trên máy với bản dùng để build thử lúc tối: giống hệt.

Các bước:
1. Từ `origin/main` (6a019742), tạo nhánh `fix/assessment-web-deps`.
2. Đưa đúng 5 file này từ cây làm việc vào nhánh, không đổi nội dung:
   - `client/assessment-web/src/shared/api/assessment.api.ts` (có `insufficientData`, `adoptSessionFromHash`, `buildResumeLink`, `isSessionError`)
   - `client/assessment-web/src/shared/score/score-display.tsx`
   - `client/assessment-web/src/shared/score/vn-normalize.ts`
   - `client/assessment-web/src/modules/landing/LandingPage.tsx`
   - `client/assessment-web/src/modules/landing/StartPage.tsx`
3. Trên nhánh này (cây làm việc sạch, đã cất mọi thay đổi khác), chạy `cd client/assessment-web && npx tsc --noEmit && npm run build`.
   - Nếu vẫn còn lỗi thiếu tên ở file nào khác trong `client/assessment-web`, thì đưa thêm file đó từ cây làm việc vào. Chỉ lấy file trong `client/assessment-web/src`, liệt kê lại từng file, rồi build lại.
   - Nếu lỗi nằm ngoài `client/assessment-web` thì DỪNG.
4. Commit: `fix(assessment-web): commit shared files the live survey already runs`.
5. Fast-forward main lên nhánh này rồi push. Người dùng đã gõ "push" cho mục D. Đây là phần còn thiếu của mục D nên được push luôn, nhưng chỉ khi build pass và chỉ có file trong `client/assessment-web`.
6. Trả lại cây làm việc như cũ (`git stash pop`). Báo lại hash commit và danh sách file.

## 2. Mục B: đưa assessment-web lên survey.novixa.vn (chờ người dùng gõ "deploy")

Làm như TASK 17 mục B, nhưng build từ commit ở bước 1:
1. Build.
2. Sao lưu `/var/www/kit-platform/assessment-web-backup-<YYYYMMDD_HHMM>.tgz`.
3. Đồng bộ `dist/`. Không đụng API, DLL, cơ sở dữ liệu, appsettings.
4. Kiểm tra sau khi lên:
   - File JS trên site có "In hoặc lưu PDF" và "Anh/chị đã làm xong bài".
   - Trang /start vẫn hiện "Câu 1/31" khi làm bài.

## 3. Mục C: Health Check (repo E:\novixa-site-release)

`origin/main` đã có thêm 4 commit trang chủ (4b66f1a7, 0e36ba20, 1c5f4343, b5f649f5), nên `health-check-v3` không còn fast-forward được.

Các bước:
1. `git fetch origin`. Tạo `health-check-v4` từ `origin/main`, rồi `git cherry-pick 7677995a`.
2. Nếu có xung đột thì DỪNG. Commit này chỉ đụng `novixa-site/src/pages/vi/health-check.astro` và `novixa-site/src/styles/health-check-pharmacy.css`, nên bình thường không xung đột.
3. Build novixa-site. Kiểm tra `dist/vi/health-check/index.html` có "có đang để lọt tiền" và không có "100+".
4. Kiểm tra commit chỉ gồm đúng 2 file trên.
5. Đẩy `health-check-v4` lên `main` (fast-forward, không ép). Người dùng đã gõ "push" cho mục C, nhưng nếu `origin/main` lại đổi lần nữa thì DỪNG.
6. Chờ Cloudflare Pages build xong. Kiểm tra https://novixa.vn/vi/health-check/ có "có đang để lọt tiền" và không còn "100+".

## 4. Commit phần khảo sát phía máy chủ còn nằm ngoài git (repo E:\KitPlatform, KHÔNG push)

API khảo sát trên máy chủ đang chạy bản build từ code chưa commit (Survey pack, migration 447). Cần lưu vào git để lần sau không build nhầm từ commit cũ.

Các bước:
1. Chạy `git status --short` cho các đường dẫn: `src/Packs/Survey`, `migrations/447_kap_pharmacy_v2.sql`, `scripts/apply-447-kap-pharmacy-v2.ps1`, `scripts/kap-v2-completion-time.sql`, `tools/kap-v2`. Gửi danh sách cho người dùng.
2. Bỏ qua `bin/`, `obj/`, `__pycache__/`.
3. Nếu thấy file thuộc khảo sát nằm ở chỗ khác (ví dụ `src/KitPlatform.Api` có đoạn khảo sát), thì liệt kê riêng và DỪNG hỏi. Không tự gom vào.
4. Tạo nhánh `kap-v2-backend` từ main mới nhất. Commit các file ở bước 1 với message `feat(survey): PHARMACY_V2 backend, migration 447 and tools (as deployed 2026-11-19)`.
5. Build `src/Packs/Survey` ở Release. Báo lại commit này build ra DLL có giống DLL đang chạy trên máy chủ không (so mã băm 2 DLL Survey với bản đã sao lưu ngày 19/11).
6. **Không push và không deploy.** Chờ người dùng.

## Mục E của TASK 17

Vẫn để sau.
