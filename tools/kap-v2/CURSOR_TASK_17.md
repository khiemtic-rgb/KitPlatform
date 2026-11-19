# CURSOR TASK 17 — Việc còn lại sau khi viết lại báo cáo cho chủ nhà thuốc

Code giao diện đã viết và build thử xong. **Không sửa chữ hay giao diện trong các file dưới đây.**
Gặp lỗi build, hoặc thấy file khác ngoài danh sách bị đổi, thì DỪNG và báo lại.

Quy định chung:
- Chỉ đưa lên máy chủ khi người dùng gõ **deploy**.
- Chỉ push khi người dùng gõ **push**. Không bao giờ ép push (`--force`).
- Không commit tk.txt, .env, appsettings.Production.json.
- Không đụng các thay đổi chưa commit không thuộc khảo sát: YouTube, video trang chủ, Pharmacy sales, Content, Loyalty, Sales agent.

---

## A. Commit báo cáo mới (repo E:\KitPlatform)

Trên nhánh `work/luu-2026-11-16` (đang có commit cee4dc70), commit đúng 5 file này:

- `client/assessment-web/src/shared/report/owner-report.ts` (file mới: toàn bộ chữ và cách chọn việc của báo cáo)
- `client/assessment-web/src/modules/report/ReportPage.tsx` (bài PHARMACY_V2 dùng báo cáo mới, nút "In hoặc lưu PDF")
- `client/assessment-web/src/modules/results/ResultsPage.tsx` (2 điểm nổi bật dùng chung chữ với báo cáo, bỏ chữ "trưởng thành", "nhóm năng lực")
- `client/assessment-web/src/modules/results/UnlockPage.tsx` (danh sách quyền lợi khớp với báo cáo mới)
- `client/assessment-web/src/styles/index.css` (thêm khối CSS `.or-*` ở cuối file)

Các bước:
1. `cd client/assessment-web && npx tsc --noEmit && npm run build`. Cả hai phải pass.
2. `git add` đúng 5 file trên.
3. Commit với message `feat(assessment-web): owner report in plain pharmacy language`.
4. Báo lại hash commit.

## B. Đưa assessment-web lên survey.novixa.vn (chờ người dùng gõ "deploy")

Làm giống mục D1 của CURSOR_TASK_16:
1. Build bản ở bước A.
2. Trên máy chủ, sao lưu thư mục web hiện tại thành `/var/www/kit-platform/assessment-web-backup-<YYYYMMDD_HHMM>.tgz`.
3. Đồng bộ `dist/` lên đúng thư mục web assessment như lần trước.
4. **Không** đụng API, DLL, cơ sở dữ liệu, appsettings.

Kiểm tra sau khi lên:
- File JS đang chạy trên https://survey.novixa.vn có các chuỗi "In hoặc lưu PDF" và "Anh/chị đã làm xong bài".
- Mở https://survey.novixa.vn/results/c321edfc-5ff5-4adf-9b0a-0bc45c28eb9c thì thấy 2 điểm nổi bật mới. Ô đầu là "Hàng hết hạn: khoảng 25 triệu mỗi năm" hoặc một mục bắt đầu bằng "Anh/chị cho biết…".
- Phần cuối trang có tiêu đề "Điểm từng phần".

Báo lại tên file sao lưu.

## C. Trang Health Check (repo E:\novixa-site-release, chờ người dùng gõ "push")

Commit 7677995a trên nhánh `health-check-v3` đã sẵn sàng. Khi người dùng gõ "push":
1. `git fetch origin`. Kiểm tra `origin/main` vẫn là tổ tiên của `health-check-v3`. Nếu không phải thì DỪNG và báo lại.
2. Đẩy `health-check-v3` lên `main` (fast-forward, không ép).
3. Chờ Cloudflare Pages build xong. Kiểm tra https://novixa.vn/vi/health-check/ có câu "có đang để lọt tiền" và không còn "100+".

## D. Gộp phần khảo sát vào main (repo E:\KitPlatform, chờ người dùng gõ "push")

Nhánh `work/luu-2026-11-16` đang chứa phần khảo sát đã chạy trên site: cee4dc70 và commit ở bước A.
1. Liệt kê các commit có trên nhánh này mà chưa có trên `main`, kèm danh sách file của từng commit. Gửi người dùng xem.
2. Chỉ đưa vào main các commit thuộc khảo sát (KAP / assessment-web / Survey pack / migration 447 / tools/kap-v2). Nếu có commit lẫn file ngoài khảo sát thì DỪNG và hỏi.
3. Không đưa vào main bất kỳ thay đổi chưa commit nào trong thư mục làm việc.
4. Chỉ push khi người dùng gõ "push".

## E. (Làm sau, không gấp) Báo cáo cũ phía máy chủ cho người quản trị

Nhà thuốc không còn thấy phần này, vì bài PHARMACY_V2 đã dùng báo cáo mới ở giao diện. Nhưng trang quản trị và bản PDF do máy chủ tạo vẫn sai. Chỉ làm khi người dùng đồng ý.

Lỗi hiện tại:
1. `AssessmentAnalysisPipeline.ExtractRecommendations` gán `Priority = m.Rule.Priority` (72–95, số **lớn** là quan trọng). Nhưng `KapActionPlanBuilder` lại xếp `OrderBy(r => r.Priority)`, coi số **nhỏ** là quan trọng. Hậu quả: mục "Việc làm trong 30 ngày" lấy 3 việc ít quan trọng nhất, gắn nhãn "Thấp" và "90–180 ngày". Danh sách cũng không lọc trùng theo tiêu đề.
2. Với bài PHARMACY_V2, `KapOwnerPackBuilder` lấy "Điểm cần cải thiện" từ mục "Ưu tiên cải thiện theo phản hồi" và "Thắng nhanh: …". Hai mục này lấy từ nhóm điểm khá nhất, nên một nhóm vừa là điểm mạnh vừa là điểm cần cải thiện. Dòng "Trong 30 ngày tới, ưu tiên xử lý: Ưu tiên cải thiện theo phản hồi." vô nghĩa.

Hướng sửa:
- Chỉ áp dụng cho PHARMACY_V2. Trước khi sửa, kiểm tra thang priority của luật bài V1 để không làm hỏng V1.
- Lọc trùng khuyến nghị theo tiêu đề, giữ priority cao nhất. Xếp giảm dần.
- Gắn nhãn theo luật V2: ≥95 thì "Cao, 30 ngày"; 85–94 thì "Trung bình, 30–60 ngày"; dưới 85 thì "60–90 ngày".
- "Điểm cần cải thiện" của V2 lấy 3 nhóm điểm thấp nhất, bỏ các mục "Ưu tiên cải thiện theo phản hồi" và "Thắng nhanh".

Kiểm tra: dùng 5 bài thử đợt `qa5_19_11` (utm_campaign), dựng lại báo cáo quản trị. "Việc làm trong 30 ngày" phải ra:

| Nhà thuốc | Việc đứng đầu |
|---|---|
| 25ad3848 | Ghi lại mỗi lần khách hỏi mà không có thuốc |
| 1faa94d6 | Viết 10 câu nhân viên hay hỏi hoặc Viết ra 3 quy trình chính |
| 227bc585 | Tính lãi tháng hoặc Xếp 20 thuốc theo tiền lãi |

Phần này đổi DLL, nên đưa lên máy chủ phải theo cách thay từng DLL như ngày 19/11: so mã băm trước, sao lưu trước. Chỉ làm khi người dùng gõ "deploy".

---

Ghi chú: các bài thử (5 bài `qa5_19_11`, c321edfc, b0072f95, 3f07a82b) **không cần xóa**. Phần so sánh giữa các nhà thuốc chỉ đếm bài ở trạng thái `lead_captured` hoặc `report_ready`. Khi xem số liệu thì lọc bỏ `utm_source = 'internal_test'`.
