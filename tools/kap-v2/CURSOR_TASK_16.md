# CURSOR TASK 16 — Chỉ deploy (code đã viết xong)

Code đã được viết và build thử xong. **Không sửa code**, chỉ build, sao lưu và đưa lên.
Nếu build lỗi hoặc thấy file khác ngoài danh sách dưới đây bị đổi thì DỪNG và báo lại.

## D1. assessment-web (survey.novixa.vn) — giao diện bài làm + trang kết quả

Các file đã đổi (repo E:\KitPlatform):
- client/assessment-web/src/shared/survey/survey-logic.ts (thêm `expectedQuestionCount`)
- client/assessment-web/src/modules/survey/SurveyPage.tsx (bộ đếm "Câu x/31", nhóm PROFILE không hiện nhãn "Không tính điểm")
- client/assessment-web/src/modules/results/ResultsPage.tsx (gom 2 điểm nổi bật + nút "Xem ba việc nên làm trước" lên cạnh điểm tổng)
- client/assessment-web/src/styles/index.css (CSS `.results-next`)

Các bước:
1. `cd client/assessment-web && npm run build` — phải pass.
2. Trên VPS: sao lưu thư mục web hiện tại thành `/var/www/kit-platform/assessment-web-backup-<YYYYMMDD_HHMM>.tgz`.
3. rsync `dist/` lên đúng thư mục web assessment như lần deploy ngày 19/11.
4. **Không** đụng API, DLL, DB, appsettings.
5. Kiểm tra: mở https://survey.novixa.vn/start, làm vài câu → thanh trên hiện "Câu 1/31"; trang kết quả có thẻ "Xem ba việc nên làm trước" ngay dưới/cạnh điểm tổng.
6. Commit 4 file trên (chỉ 4 file này) với message `feat(assessment-web): results summary CTA + question counter`. **Không push** cho đến khi người dùng gõ "push".

## D2. novixa-site — trang /vi/health-check/

Các file đã đổi (worktree E:\novixa-site-release\novixa-site):
- src/pages/vi/health-check.astro
- src/styles/health-check-pharmacy.css

Nội dung: tiêu đề mới "…có đang để lọt tiền?" + dòng phụ; nút CTA lên ngay dưới mô tả; thanh nút cố định trên điện thoại; bỏ dòng "100+ nhà thuốc"; chuyển tiếp utm_*/ref sang link survey.

Các bước:
1. `git fetch origin` rồi tạo nhánh `health-check-v3` từ `origin/main`.
2. Chỉ đưa 2 file trên vào nhánh đó (copy từ worktree hiện tại nếu cần).
3. `npm run build` — phải pass. Kiểm tra `dist/vi/health-check/index.html` chứa "có đang để lọt tiền" và KHÔNG chứa "100+".
4. Commit: `feat(health-check): stronger hero, mobile sticky CTA, utm passthrough`.
5. **DỪNG**. Báo hash commit. Chỉ push khi người dùng gõ "push". Không ép push.

## Không làm
- Không commit tk.txt, .env, appsettings.Production.json.
- Không đụng các thay đổi chưa commit không thuộc KAP (YouTube, video trang chủ, Pharmacy sales, Content, Loyalty, Sales agent).
