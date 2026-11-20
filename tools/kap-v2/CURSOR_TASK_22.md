# CURSOR TASK 22 — PDF do máy chủ tạo cho bài PHARMACY_V2: thay bản 27 trang bằng đúng nội dung báo cáo mới (giao lúc 2026-11-19 23:30 +07)

## Vì sao

Người dùng mở PDF máy chủ tạo cho bài thử c321edfc ("Báo cáo tư vấn chuyển đổi số bằng AI", 27 trang) và thấy khó hiểu. Cụ thể:
- Lời tư vấn doanh nghiệp: "AI nhận thấy…", "doanh nghiệp", "thiếu nền tảng quản trị", "số hóa", "Thắng nhanh", "Ưu tiên cải thiện theo phản hồi".
- Con số tự đặt ra: "sẵn sàng chuyển đổi số 49%", "phù hợp Novixa 57%", "khả năng thành công 72%".
- 3 thang điểm cùng lúc: 49/100, "Cấp 3 — Chuẩn hóa", "3,5/10", và "Vận hành 13%".
- Chữ lặp lỗi: "Thực hành tốt nhà thuốc (Thực hành tốt nhà thuốc (Thực hành tốt nhà thuốc (GPP)))".
- **Lỗi font làm mất chữ "ti":** "Phân ch" (phân tích), "ưu ên" (ưu tiên), "ền" (tiền), "ếp tục" (tiếp tục), "chi ết" (chi tiết). Lỗi này có ở mọi PDF máy chủ tạo, không riêng PHARMACY_V2.

Trang web báo cáo đã dùng bản mới. Logic và toàn bộ chữ nằm ở `client/assessment-web/src/shared/report/owner-report.ts`, đã chạy trên production từ 23:02. PDF máy chủ phải nói **đúng y như trang web**.

Quy định chung:
- Không ép push.
- Push chỉ khi người dùng gõ "push"; deploy chỉ khi người dùng gõ "deploy".
- Mỗi bước ghi giờ (+07) vào `tools/git-safety/LOG_<ngày>.md`.
- Làm trên nhánh mới từ origin/main: `feat/kap-v2-owner-pdf_2026-11-19`.

## 0. Đưa bản chữ mới nhất của owner-report.ts lên trước (giao diện)

Lúc 23:35, Claude đã sửa `client/assessment-web/src/shared/report/owner-report.ts`:
- mục "Điều anh/chị lo nhất" nhắc lại câu trả lời của nhà thuốc;
- việc gắn với điều lo nhất được xếp lên trước.

File mẫu `tools/kap-v2/owner-report.fixtures.json` đã được tạo lại theo bản này. Các bước:
1. Từ origin/main, commit `owner-report.ts` với message `fix(assessment-web): echo the owner's concern and rank matching actions first [2026-11-19 23:35 +07]`. Commit kèm file mẫu.
2. `npx tsc --noEmit && npm run build` phải pass.
3. Chờ người dùng gõ "push" rồi đưa lên main. Chờ người dùng gõ "deploy" rồi deploy giao diện như CURSOR_TASK_18 mục 2: sao lưu, đồng bộ dist, ghi VERSION.txt.

## 1. Chuyển owner-report sang C#

Tạo `src/Packs/Survey/KitPlatform.Packs.Survey.Infrastructure/Assessment/KapV2OwnerReportBuilder.cs`. Chuyển **nguyên văn** `owner-report.ts`:
- cùng chữ, từng ký tự;
- cùng cách xếp việc (hệ số 1.6 cho câu gắn điều lo nhất, 1.25 cho nhóm ưu tiên, 1.3 khi khoản mất tiền từ 20 triệu);
- cùng giới hạn 2 việc mỗi nhóm, cùng trường hợp nhà thuốc chỉ có một người đứng quầy.

Đầu vào:
- câu trả lời theo mã câu và mã phương án (lấy từ responses và template);
- điểm từng nhóm, `insufficientData`;
- `overallPct`.

Đầu ra: cùng cấu trúc với `OwnerReport`.

**Kiểm tra bằng file mẫu:** `tools/kap-v2/owner-report.fixtures.json` có 5 nhà thuốc thử, gồm đầu vào và kết quả mong đợi do chính `owner-report.ts` tạo ra.
- Viết test C# đọc file này, so **từng trường, từng chữ**.
- Phải khớp 100%. Không khớp thì sửa C#, **không sửa file mẫu và không sửa owner-report.ts**.

## 2. PDF mới cho PHARMACY_V2

Khi template là `PHARMACY_V2`, cả `GetReportPdfAsync` và `GetReportPdfForAdminAsync` (mọi `kind`) đều trả PDF mới, 2–4 trang A4. Các phần theo đúng thứ tự trang web:
1. Tên nhà thuốc, ngày làm bài, điểm `xx/100` và nhãn (Cần cải thiện / Cơ bản / Khá tốt / Xuất sắc, cùng mốc 40/60/80 như web), câu mở đầu.
2. Nhà thuốc đang mất tiền ở đâu (nếu có).
3. Điều anh/chị lo nhất (nếu có).
4. 3 việc nên làm trước. Mỗi việc có:
   - câu trả lời của nhà thuốc;
   - lý do;
   - cách làm;
   - ai làm, mất bao lâu, sau 30 ngày xem lại gì;
   - dòng phần mềm.
5. Anh/chị đang làm tốt.
6. Nên bắt đầu để ý (nếu có).
7. Điểm từng phần: thang 100, "Chưa áp dụng" hoặc "Chưa đủ câu trả lời để chấm" như web.
8. Muốn có người cùng làm? Số 0984.660.399.

Không có trong PDF mới:
- chữ "AI", "chuyển đổi số", "doanh nghiệp", "số hóa", "pilot", "module", "thắng nhanh", "Cấp 1–5", thang /10;
- các con số "sẵn sàng", "phù hợp Novixa", "khả năng thành công", so sánh ngành;
- mục lục, ma trận, SWOT, KPI.

Bản PDF admin được thêm 1 trang cuối "Dành cho người tư vấn": tên, số điện thoại, nguồn utm, có đồng ý gọi hay không, các câu trả lời rẽ nhánh (C6, I7, O6, O7, B5, G3). Bản cho nhà thuốc không có trang này.

Bài V1 và các template khác: giữ nguyên PDF cũ.

## 3. Sửa lỗi font mất chữ "ti"

Tìm nguyên nhân: font Lato đi kèm, ghép chữ "ti" (ligature) hoặc bộ chữ bị cắt. Sửa cho mọi PDF, bằng cách tắt ghép chữ hoặc dùng font có đủ dấu tiếng Việt (ví dụ Be Vietnam Pro hay Noto Sans, giấy phép OFL).

Kiểm tra: trích chữ từ PDF bằng `pdftotext` phải thấy đủ các chữ:
- "phân tích"
- "ưu tiên"
- "tiền"
- "tiếp tục"
- "chi tiết"
- "Thực hành tốt nhà thuốc"

## 4. Kiểm tra

1. Test ở mục 1 pass.
2. Tạo PDF cho 5 bài thử đợt `qa5_19_11` và bài c321edfc qua đường admin. Với mỗi PDF:
   - 2–4 trang (bản admin thêm 1 trang);
   - không chứa các chữ bị cấm ở mục 2;
   - đủ chữ ở mục 3;
   - câu mở đầu và tên 3 việc trùng với trang web.
3. Gửi người dùng PDF của c321edfc và của nhà thuốc A (25ad3848) để xem.
4. Build Release Survey pack: 0 lỗi.

## 5. Đưa lên

- Commit trên nhánh `feat/kap-v2-owner-pdf_2026-11-19`, message có giờ.
- Chờ người dùng gõ "push" rồi đưa vào main.
- Chờ người dùng gõ "deploy" rồi thay đúng 2 DLL Survey trên VPS:
  - so mã băm và sao lưu trước, như ngày 19/11;
  - ghi `/var/www/kit-platform/api/SURVEY_VERSION.txt`;
  - chạy `scripts/check-survey-sync.ps1` (Task 19).
- Chỉ deploy từ commit đã có trên origin/main.

Mục E của CURSOR_TASK_17 được thay bằng task này: báo cáo admin của PHARMACY_V2 cũng dùng `KapV2OwnerReportBuilder`, không sửa riêng `KapActionPlanBuilder` nữa.
