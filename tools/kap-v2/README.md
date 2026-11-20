# KAP PHARMACY_V2 — "Soát nhanh nhà thuốc"

Nguồn duy nhất của bộ câu hỏi V2 là `build.py`. Muốn sửa câu chữ, phương án, điểm hoặc luật báo cáo thì sửa file này, rồi chạy:

```
cd tools/kap-v2
python build.py      # kiểm tra + sinh template.json, insights.json, review.md
python gen_sql.py    # sinh 447_kap_pharmacy_v2.sql -> copy vào migrations/
```

`gen_sql.py` dừng lại nếu có lỗi. Các lỗi được kiểm tra: trùng ID hoặc mã, câu tính điểm không đủ thang 1–4, câu rẽ nhánh trỏ sai, và chữ sẽ bị bộ thay chữ của frontend sửa.

| File | Dùng cho |
|---|---|
| `PHARMACY_V2.review.md` | Bản đọc để dược sĩ và sales duyệt câu chữ |
| `PHARMACY_V2.template.json` | Cùng cấu trúc với `GET /api/public/assessment/templates/PHARMACY_V2` |
| `PHARMACY_V2.insights.json` | Luật báo cáo dạng dữ liệu (nguồn để sinh `assessment_rule`) |

## Áp dụng

1. Migration: `scripts/apply-447-kap-pharmacy-v2.ps1 -ConnectionString "..."`. Migration này chạy lại được nhiều lần mà không lỗi. Nó cần 068 và 089 đã chạy trước. PHARMACY_V1 vẫn active để các bài nháp cũ làm tiếp được.
2. Build lại API và assessment-web, rồi deploy như bình thường (`deploy-production.ps1` → `deploy-kap-vps.ps1`).

## Đã thay đổi trong code

**Backend (pack Survey)**

- `AssessmentScoringEngine`:
  - Nhóm không có câu nào được chấm (nhóm PROFILE, hoặc toàn đáp án "Không biết") không còn bị tính 0 điểm vào điểm tổng.
  - `P3` được nhận là câu nỗi đau.
- `AssessmentRepository.GetRequiredCountsAsync`: câu rẽ nhánh (`metadata.showIf`) chỉ bắt buộc khi đang hiển thị. Thiếu sửa này thì bài V2 không bao giờ nộp được.
- Nhãn tiếng Việt cho các tag mới: `KapVietnameseLabels`, `KapAiNarrativeService`, `KapConsultingNarrativeBuilder`.
- `AssessmentSettings.BenchmarkAggregateMinSampleSize`: đổi từ 5 lên 30. Nếu appsettings đang ghi đè giá trị này thì cần sửa ở đó.

**Frontend (assessment-web)**

- Bài mới dùng PHARMACY_V2. Bài cũ mở theo `templateCode` của chính bài đó.
- Mã phiên lưu cả trong localStorage, nên mở tab mới vẫn làm tiếp được. "Lưu & thoát" đưa link làm tiếp (`/survey/{id}#t=...`). Khi không mở được bài thì hiện thông báo rõ ràng, thay vì lặng lẽ quay về trang chủ.
- Bỏ mã câu hỏi nội bộ ("Câu C1") khỏi giao diện. Các ô lựa chọn có tên đọc được cho trình đọc màn hình.
- Bộ thay chữ không còn viết hoa sai ("Tăng Doanh thu") và không còn sửa câu đã có dấu.
- Trang kết quả: dòng gợi ý dùng đúng thang nhãn (40/60/80).
- Bỏ các câu hứa "so sánh ngành", "bảo mật tuyệt đối", "gửi qua email". Code không gửi email nào.
- Ô đồng ý nhận tư vấn không còn đánh dấu sẵn.
- Trang đầu:
  - Bỏ "Cơ hội tăng doanh thu" và "Mức sẵn sàng cạnh tranh".
  - Hình mẫu ghi rõ là minh họa, nhãn điểm tính theo đúng thang.

## Còn cần quyết định / làm tiếp

- **Đồng ý nhận tư vấn:** `AssessmentLeadCapturedHandler` vẫn đẩy đầu mối vào CRM dù người làm không đánh dấu đồng ý. Việc có chặn hay không cần pháp chế và kinh doanh quyết định.
- **Duyệt chuyên môn:** câu O5, O6 và T4 (🩺 trong `review.md`) cần dược sĩ hoặc pháp chế duyệt. Phần "Nếu muốn làm tự động" trong luật báo cáo cần sales xác nhận từng tính năng đã có thật.
- **Root cause KB** (`assessment_root_cause_kb`) chưa có cho V2, nên phần "nguyên nhân gốc" trong báo cáo đầy đủ sẽ trống cho đến khi được viết.
- **Số phút trên trang đầu** (`DURATION_LABEL` trong `LandingPage.tsx`) cần cập nhật theo thời gian làm bài trung vị đo được từ bài thật.
- **Trang novixa.vn/vi/health-check/** nằm ngoài repo này, cần sửa theo cùng nội dung: 6 nhóm và bỏ các câu hứa chưa có căn cứ.

## Deploy

Chưa được deploy cả API từ main cho tới khi nhánh `integration-check` đạt đủ ba điều:

1. build solution Release và `client/admin` qua;
2. test qua;
3. **0 route thiếu** so với lõi đang chạy trên VPS (`134d7834`).

Trong thời gian đó chỉ thay riêng DLL Survey như đã làm.

Khi được phép deploy cả API, giữ các bước sau:

1. Chỉ build để deploy từ commit đã push lên origin/main. Không build từ cây làm việc có file chưa commit.
2. Trước khi deploy: chạy `scripts/check-survey-sync.ps1`. Có dòng LOCAL lệch thì commit và push trước.
3. Deploy xong: ghi VERSION.txt, rồi chạy lại `check-survey-sync.ps1`. Tất cả phải khớp.
4. Sửa nóng trực tiếp trên VPS là không được. Nếu bắt buộc, phải commit lại vào git ngay trong ngày.
5. Mỗi việc giao Cursor là một file `CURSOR_TASK_<số>.md` mới. Không sửa file cũ.

Lõi API trên VPS và mã Survey là hai việc khác nhau. Bản đang chạy: lõi `134d7834`, Survey `6df6a9ce` build trên lõi đó. Không thay `KitPlatform.Application.dll`, `KitPlatform.Infrastructure.dll`, `KitPlatform.Domain.dll` hay Pharmacy khi chỉ cập nhật Survey.
