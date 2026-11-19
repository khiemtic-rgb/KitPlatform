# CURSOR TASK 19 — Đồng bộ 3 nơi: máy chủ (VPS), git (origin/main), máy local

Làm **sau khi xong CURSOR_TASK_18** (mục 1, 3, 4 và mục 2 deploy).

Mục tiêu: phần khảo sát và trang Health Check giống hệt nhau ở 3 nơi, để không còn lần nào build hoặc deploy từ chỗ thiếu code rồi mất phần đã sửa.

Quy định chung giữ nguyên:
- Không ép push.
- Không đụng thay đổi ngoài khảo sát.
- Không commit tk.txt, .env, appsettings*.json.
- Push cần người dùng gõ "push". Deploy cần người dùng gõ "deploy".

## 1. Đưa phần máy chủ của khảo sát vào main

Nhánh `kap-v2-backend` (TASK 18 mục 4) gồm Survey pack, migration 447, `scripts/apply-447-*`, `scripts/kap-v2-completion-time.sql`, `tools/kap-v2`.
1. Rebase lên `origin/main` mới nhất. Build `src/Packs/Survey` ở Release.
2. So mã băm 2 DLL (`KitPlatform.Packs.Survey.Application.dll`, `KitPlatform.Packs.Survey.Infrastructure.dll`) với 2 DLL đang chạy trên VPS.
   - Giống nhau: báo người dùng, chờ gõ "push" rồi fast-forward main.
   - Khác nhau: DỪNG. Báo khác ở đâu (so danh sách file nguồn với bản build ngày 18/11), không push, không deploy.

## 2. Bảng đối chiếu 3 nơi

Lập bảng và gửi người dùng. Mỗi dòng là một thành phần; cột VPS / GIT (origin/main) / LOCAL ghi hash hoặc "khớp" / "lệch":

| Thành phần | Cách so |
|---|---|
| assessment-web (survey.novixa.vn) | Build từ origin/main. So danh sách file trong `dist/assets` và mã băm `index.html` với thư mục web trên VPS. |
| API khảo sát (2 DLL Survey) | Mã băm DLL trên VPS so với DLL build từ origin/main. |
| Cơ sở dữ liệu | Production có template `PHARMACY_V2` đang active, đúng số câu hỏi (36) và số luật (109) như trong `migrations/447_kap_pharmacy_v2.sql`. File 447 có trong origin/main. |
| Trang Health Check (novixa.vn) | origin/main có commit Health Check. Trang thật có "có đang để lọt tiền". |
| LOCAL E:\KitPlatform | `git status` không còn file chưa commit trong `client/assessment-web`, `src/Packs/Survey`, `migrations/447*`, `scripts/*447*`, `scripts/kap-v2-*`, `tools/kap-v2`. Nhánh local trỏ đúng origin/main. |
| LOCAL E:\novixa-site-release | Worktree đã về origin/main mới nhất. Không còn file Health Check chưa commit. |

Dòng nào lệch thì ghi rõ lệch gì. Chỉ sửa khi đó là phần khảo sát hoặc Health Check. Phần khác thì báo lại.

## 3. Ghi dấu phiên bản trên VPS

Mỗi lần deploy phần khảo sát, ghi một file trên VPS:
- `/var/www/kit-platform/assessment-web/VERSION.txt`, có nội dung:
  - commit: <hash origin/main>
  - built: <ngày giờ>
  - by: <người hoặc công cụ>
- `/var/www/kit-platform/api/SURVEY_VERSION.txt`: cùng nội dung, cho 2 DLL Survey.

Lần này ghi luôn cho bản vừa deploy, nếu bảng ở mục 2 khớp.

## 4. Script kiểm tra đồng bộ

Tạo `scripts/check-survey-sync.ps1`. Chạy là in ra bảng ở mục 2: VPS / GIT / LOCAL, khớp hay lệch, với hash cụ thể. Script chỉ đọc, không sửa gì. Commit script này cùng nhánh ở mục 1.

## 5. Quy tắc từ nay (ghi vào `tools/kap-v2/README.md`, mục "Deploy")

1. Chỉ build để deploy từ commit **đã push lên origin/main**. Không build từ cây làm việc có file chưa commit.
2. Trước khi deploy: chạy `check-survey-sync.ps1`. Có dòng LOCAL lệch thì commit và push trước.
3. Deploy xong: ghi VERSION.txt, rồi chạy lại `check-survey-sync.ps1`. Tất cả phải "khớp".
4. Sửa nóng trực tiếp trên VPS là không được. Nếu bắt buộc, phải commit lại vào git ngay trong ngày.
5. Mỗi việc giao Cursor là một file `CURSOR_TASK_<số>.md` mới. Không sửa file cũ.

## Báo lại cho người dùng

- Bảng đối chiếu ở mục 2, trước và sau khi làm.
- Hash commit cuối của main ở cả 2 repo.
- Những gì còn lệch mà nằm ngoài phần khảo sát (chỉ liệt kê, không sửa).
