# Giao việc: hoàn thiện KAP PHARMACY_V2 (Soát nhanh nhà thuốc)

Đọc `tools/kap-v2/README.md` trước. File đó ghi những gì đã làm, những gì còn lại và lý do.

## Bối cảnh ngắn

- Bộ câu hỏi mới `PHARMACY_V2` nằm trong `migrations/447_kap_pharmacy_v2.sql`. File này được **sinh tự động** từ `tools/kap-v2/build.py` và `gen_sql.py`.
- Đã sửa backend trong pack Survey:
  - `AssessmentScoringEngine`
  - `AssessmentRepository.GetRequiredCountsAsync` (câu rẽ nhánh `metadata.showIf`)
  - nhãn tag mới
  - `BenchmarkAggregateMinSampleSize = 30`
- Đã sửa frontend `client/assessment-web`:
  - dùng V2
  - link làm tiếp
  - bộ thay chữ
  - câu chữ trên trang
- Phần C# **chưa được biên dịch** vì máy viết code không có .NET SDK. Frontend đã qua `tsc` và `vite build`.

## Quy tắc khi làm

1. **Không sửa tay** `447_kap_pharmacy_v2.sql`. Muốn đổi câu hỏi hay luật thì sửa `tools/kap-v2/build.py`, rồi chạy `python build.py && python gen_sql.py` trong `tools/kap-v2` và copy SQL vào `migrations/`.
2. Giữ nguyên định dạng file gốc: CRLF và BOM. Ví dụ, `AssessmentScoringEngine.cs` có BOM, còn `AssessmentRepository.cs` thì không.
3. Không đổi dữ liệu hay hành vi của `PHARMACY_V1`. Bài nháp V1 cũ phải làm tiếp được.
4. Chỉ deploy ở **Việc 8**, và chỉ khi Việc 1, 2, 3 và 7 đã xong. Không deploy ở bước nào khác.
5. Không commit file chứa mật khẩu hay khóa, ví dụ `E:\Maychu_VPS\tk.txt` hay appsettings production.
6. Làm xong mỗi việc thì ghi kết quả vào mục "Nhật ký" cuối file này: đã làm gì, file nào, cách kiểm tra.

---

## Việc 1: Build backend và sửa lỗi biên dịch (làm trước)

- Chạy `dotnet build KitPlatform.slnx`. Nếu lỗi nằm ở 6 file đã sửa thì sửa cho build qua, giữ nguyên ý đồ:
  - `src/Packs/Survey/KitPlatform.Packs.Survey.Infrastructure/Assessment/AssessmentScoringEngine.cs`
  - `.../Assessment/AssessmentRepository.cs`
  - `.../Assessment/KapVietnameseLabels.cs`
  - `.../Assessment/KapAiNarrativeService.cs`
  - `.../Assessment/KapConsultingNarrativeBuilder.cs`
  - `src/Packs/Survey/KitPlatform.Packs.Survey.Application/AssessmentSettings.cs`
- Nếu có test project cho pack Survey, chạy `dotnet test` cho project đó.
- Kiểm tra `appsettings*.json` của API host xem có ghi đè `Assessment:BenchmarkAggregateMinSampleSize` không. Nếu có thì báo lại, **không tự sửa** file production.

**Xong khi:** build không lỗi, test (nếu có) qua.

## Việc 2: Chạy thử toàn bộ luồng V2 trên máy dev

1. Áp migration vào DB dev: `scripts\apply-447-kap-pharmacy-v2.ps1`.
2. Chạy API và `client/assessment-web` (`npm run dev`).
3. Đi hết một vòng:
   1. Tạo bài.
   2. Chọn P3 = "Hàng tồn, cận hạn, hết hàng". Phải hiện câu I7, tổng 31 câu.
   3. Trả lời hết.
   4. Bấm "Xem kết quả".
   5. Để lại thông tin.
   6. Xem báo cáo.
   7. Tải PDF loại `owner`.
4. Kiểm tra từng điểm:
   - Nộp bài được, không bị báo thiếu câu bắt buộc.
   - Kết quả có 6 nhóm (Khách hàng, Vận hành, Kho, Kinh doanh, Dữ liệu & Công nghệ, Phát triển) và **không có** nhóm "Bắt đầu".
   - Hai dòng nhận xét sơ bộ ưu tiên ước tính tiền, khi I2 ≥ 1 triệu hoặc I3 ≥ 2 lần/tuần.
   - Không còn chữ "Doanh thu" viết hoa giữa câu. Không còn "so sánh ngành", "bảo mật tuyệt đối", "gửi qua email".
   - PDF tạo được, không lỗi. V2 chưa có root cause KB và benchmark cohort, nên các phần đó phải ẩn hoặc trống, không được văng lỗi.
   - Bấm "Lưu & thoát", copy link, mở ở cửa sổ ẩn danh. Phải làm tiếp đúng bài đó.
   - Mở một bài V1 làm dở (nếu DB dev có). Phải hiện bộ câu V1.
5. Lỗi nào có nguyên nhân ở backend hoặc PDF generator, như null reference hay giả định đủ 29 câu V1, thì sửa trong code. Không né bằng cách sửa dữ liệu.

**Xong khi:** đi hết luồng không lỗi, có ghi chú hoặc ảnh chụp từng bước trong Nhật ký.

## Việc 3: Trang novixa.vn/vi/health-check/ (thư mục `novixa-site`)

- Tìm trang Health Check trong `novixa-site` và sửa cho khớp với bài khảo sát thật:
  - **6 nhóm**: Khách hàng, Vận hành, Kho, Kinh doanh, Dữ liệu & Công nghệ, Phát triển. Bỏ "Nhân sự".
  - "31 câu, khoảng 7–10 phút".
  - Bỏ "tăng lợi nhuận bền vững", "cơ hội tăng doanh thu", "so sánh với chuẩn ngành", "mức sẵn sàng cạnh tranh".
  - Ghi rõ trình tự: làm bài không cần đăng ký → xem kết quả sơ bộ ngay → để lại số điện thoại để xem báo cáo đầy đủ → nếu đồng ý thì có cuộc gọi 10–15 phút.
  - Hình mẫu 62/100 phải ghi "Hình minh họa, không phải kết quả thật", và nhãn đúng thang là "Khá tốt" (thang: dưới 40 Cần cải thiện, 40–59 Cơ bản, 60–79 Khá tốt, từ 80 Xuất sắc).
- Dùng câu chữ của `client/assessment-web/src/modules/landing/LandingPage.tsx` làm chuẩn để hai trang nói giống nhau.

**Xong khi:** trang build được, không còn câu nào trong danh sách bỏ ở trên.

## Việc 4: Đồng bộ kịch bản bán và agent

- Tìm trong repo, nhất là pack Sales (`src/Packs/Sales`, migration `pack_kit_sales_*`, pain library, agent phases), các chỗ mô tả Health Check: "20–30 phút", "báo cáo ngắn", 4 mảng đánh giá.
- Sửa thành:
  - Bài tự làm: 31 câu, khoảng 7–10 phút, 6 nhóm.
  - Cuộc gọi sau báo cáo: 10–15 phút.
- **Giữ nguyên** các điều cấm: không cam kết % doanh thu, không dùng thử vô thời hạn, không ép App.
- Nếu nội dung nằm trong migration đã chạy thì viết migration mới (số tiếp theo sau số lớn nhất hiện có). Không sửa migration cũ.

**Xong khi:** `grep -ri "20–30 phút\|20-30 phút"` không còn kết quả liên quan Health Check.

## Việc 5: Root cause KB cho V2 (bản nháp, chờ duyệt)

- V1 có `assessment_root_cause_kb` trong `migrations/090_kap_pharmacy_intelligence_seed.sql`. V2 chưa có.
- Viết thêm vào `build.py` một bảng nguyên nhân gốc cho khoảng 8 câu có `impact` cao nhất: C3, I1, I3, B1, O1, O5, I4, C2. Cho `gen_sql.py` sinh thêm phần INSERT vào `assessment_root_cause_kb`. Theo đúng cột và cách viết `trigger_expression` của 090.
- Đánh dấu rõ trong `PHARMACY_V2.review.md` đây là bản nháp cần dược sĩ duyệt.

**Xong khi:** migration sinh lại chạy được và chạy lại lần hai không lỗi trên DB dev, báo cáo V2 có phần nguyên nhân gốc.

## Việc 6: Câu SQL đo thời gian làm bài thật

- Viết `scripts/kap-v2-completion-time.sql` cho PHARMACY_V2, lấy từ `assessment_submission`:
  - thời gian làm bài trung vị và p75 (`completed_at - started_at`), bỏ các bài dài hơn 2 giờ
  - tỷ lệ làm xong
  - câu hay bị bỏ ngang: câu cuối cùng có trả lời của các bài còn ở trạng thái draft
- Có kết quả thật thì cập nhật `DURATION_LABEL` trong `LandingPage.tsx` và trang ở Việc 3.

**Xong khi:** có file SQL chạy được trên DB dev.

## Việc 7: Mã theo dõi không được làm hỏng phễu (làm trước khi deploy)

Hiện nay `AssessmentSubmissionService` ném lỗi 400 "Mã đối tác không hợp lệ" khi `partnerCode` không tồn tại hoặc bị khóa. Hậu quả: link `?ref=` gõ sai, hoặc mã gắn cho quảng cáo, khiến người làm **không bắt đầu được bài**.

- **Backend:** mã đối tác không hợp lệ thì vẫn tạo bài với `source = 'public_web'`, bỏ `partnerCode`, và ghi mã gốc vào `metadata.invalidPartnerCode` để còn tra. Có log cảnh báo. Mã hợp lệ thì giữ nguyên hành vi hiện tại.
- **Theo dõi nguồn quảng cáo:**
  - Frontend đọc `utm_source`, `utm_medium`, `utm_campaign`, `utm_content` từ URL trang đầu và `/start`, lưu giống cách lưu `kap_partner_ref`, rồi gửi kèm khi tạo bài.
  - Backend lưu các trường này vào `assessment_submission.metadata.utm` (cột jsonb đã có). Không cần đổi schema.
  - Giới hạn độ dài mỗi trường (ví dụ 100 ký tự) và chỉ nhận chữ, số, `_` và `-`.
- **Câu SQL báo cáo theo nguồn:** thêm vào `scripts/kap-v2-completion-time.sql` (Việc 6) một phần đếm theo `utm_campaign` / `utm_content`:
  - bắt đầu
  - làm xong
  - để lại số

**Xong khi:**
- mở `/?ref=SAIMA` vẫn làm bài được;
- mở `/?utm_campaign=test&utm_content=can_han` thì bài tạo ra có `metadata.utm` đúng;
- `dotnet build` và `npm run build` đều qua.

## Việc 8: Deploy (chỉ làm khi Việc 1, 2, 3, 7 đã xong)

1. Ghi vào Nhật ký: commit hoặc thay đổi nào sẽ được deploy.
2. Backup DB production trước khi chạy migration: dùng `pg_dump` bảng `assessment_*`, hoặc dùng cách backup đang dùng cho VPS.
3. Chạy `scripts\apply-447-kap-pharmacy-v2.ps1` với connection string production.
4. Chạy `scripts\deploy-production.ps1`, rồi `scripts\deploy-kap-vps.ps1`.
5. Deploy trang `novixa-site` đã sửa ở Việc 3, theo cách deploy hiện có của site đó.
6. Kiểm tra trên production:
   - `https://survey.novixa.vn/` → bắt đầu bài → câu đầu là câu P2 (số cơ sở). Tổng 31 câu sau khi chọn P3.
   - `GET https://survey.novixa.vn/api/public/assessment/templates/PHARMACY_V2` trả 200.
   - Trang `novixa.vn/vi/health-check/` hiện nội dung mới.
7. Xóa hai bài test trên production (nếu không cần giữ để thống kê):
   - `3f07a82b-0f37-4469-b9be-6cd08fce25b8`: bài V1 được tạo khi kiểm tra trước đó.
   - Các bài Cursor tạo khi kiểm tra.
8. Ghi vào Nhật ký: **"ĐÃ DEPLOY"**, kèm giờ deploy và kết quả từng bước kiểm tra.

**Không tự sửa** `appsettings.Production.json` (ngưỡng `BenchmarkAggregateMinSampleSize` = 5). Chờ người phụ trách quyết định.

## Việc 9: Deploy bản sạch chỉ có KAP V2 (thay cho Việc 8)

Người phụ trách đã duyệt cách này (19/11, 16:40). Trong cây làm việc chính có thay đổi chưa commit ngoài KAP: Pharmacy sales, Content, Loyalty, Sales agent. Chưa rõ chúng của ai, nên **không động vào chúng**: không commit, không stash, không revert.

1. **Liệt kê việc dở.** Chạy `git status --porcelain` trong `E:\KitPlatform`. Ghi vào Nhật ký một bảng gồm: file, nhóm (KAP / ngoài KAP), mô tả một dòng thay đổi. Mục đích là để người phụ trách biết đó là việc của ai.
2. **Tìm commit đang chạy trên production.** Dựa vào tag release, log deploy (`.tmp-deploy-*.log`) hoặc version API trên VPS (`/api/health`).
   - Không xác định chắc chắn được thì **dừng**, ghi lại các khả năng tìm được, chờ người phụ trách chọn.
   - Ghi commit tìm được vào Nhật ký.
3. **Tạo worktree.** `git worktree add E:\KitPlatform-kap-release <commit production> -b kap-v2-release`.
4. **Đưa vào chỉ những file thuộc KAP V2:**
   - Lấy danh sách file đã sửa ở Việc 1–7, bao gồm cả các file Cursor tự sửa thêm (`SurveyPage.tsx`, `AssessmentAnalysisPipeline.cs`, `KapOwnerPackBuilder.cs`, các file của Việc 7), `migrations/447_kap_pharmacy_v2.sql`, `scripts/apply-447-kap-pharmacy-v2.ps1`, `scripts/kap-v2-completion-time.sql`, `tools/kap-v2/*`, `appsettings.Production.json` (chỉ dòng ngưỡng 30) và các file `novixa-site` của Việc 3.
   - Với mỗi file, xem diff giữa commit production và cây làm việc. Nếu file có cả thay đổi ngoài KAP thì **chỉ chuyển phần của KAP**.
   - Ghi danh sách file vào Nhật ký.
5. **Build và kiểm tra trong worktree.**
   - `dotnet build`.
   - `npm run build` cho `client/assessment-web`.
   - Build `novixa-site`.
   - Chạy lại nhanh luồng ở Việc 2 trên DB dev từ worktree: làm bài V2 31 câu, nộp được, xem báo cáo, tải PDF.
6. **Commit trong worktree.** Commit lên nhánh `kap-v2-release`. Không merge vào nhánh chính, không push.
7. **Deploy từ worktree**, làm đúng các bước 2–7 của Việc 8:
   1. backup DB production;
   2. chạy migration 447;
   3. chạy `deploy-production.ps1` rồi `deploy-kap-vps.ps1` từ thư mục worktree;
   4. deploy `novixa-site`;
   5. kiểm tra trên production;
   6. xóa các bài test.
8. **Sau deploy, kiểm tra thêm phần nhà thuốc** để chắc phần ngoài KAP không bị ảnh hưởng: đăng nhập admin, mở trang bán hàng của một tenant demo.
9. **Ghi "ĐÃ DEPLOY"** vào dòng Việc 9 của Nhật ký, kèm commit, giờ deploy và kết quả từng bước kiểm tra.

Gặp lỗi ở bất kỳ bước production nào (2–5 của Việc 8) thì dừng, ghi lại, không thử tiếp bằng cách khác.

## Không làm, chỉ báo lại

- **Đồng ý nhận tư vấn:** `src/Packs/Survey/KitPlatform.Packs.Survey.Infrastructure/Events/AssessmentLeadCapturedHandler.cs` vẫn đẩy đầu mối vào CRM dù `consent_marketing = false`. Chỉ ghi lại chính xác luồng dữ liệu: field nào sang bảng nào, có lưu trạng thái đồng ý không. Việc có chặn hay không sẽ do pháp chế và kinh doanh quyết định.
- **Câu cần duyệt chuyên môn:** câu O5, O6 và T4 (đánh dấu 🩺 trong `review.md`) chờ dược sĩ duyệt. Không tự sửa câu chữ chuyên môn.

---

## Nhật ký

| Việc | Trạng thái | Ghi chú |
|---|---|---|
| 1 | Xong | `dotnet build KitPlatform.slnx`: 0 error, 54 warning (cảnh báo cũ, không nằm ở 6 file cần sửa). Lần đầu fail MSB3027 vì `KitPlatform.Api` pid 6660 khóa DLL; đã `scripts\stop-api-dev.ps1` rồi build lại, không sửa code. Không có test project riêng cho pack Survey (`tests/` không có project Survey). Báo lại, không sửa: `src/KitPlatform.Api/appsettings.Production.json` dòng Assessment `BenchmarkAggregateMinSampleSize` = 5, ghi đè default 30 trong `AssessmentSettings.cs`. `appsettings.Development.json` không ghi đè. |
| 2 | Xong | Migration 447 chạy trên DB dev. API `localhost:5290`, assessment-web `localhost:5176`. Sửa `SurveyPage.tsx`: lọc câu rẽ nhánh theo cả bộ câu hỏi. Chọn P3 "Hàng tồn, cận hạn, hết hàng" thì có I7, tổng 31/31, nộp được. Sửa pipeline để nhóm PROFILE không vào báo cáo với điểm 0. PDF owner tạo được. Bài V1 nháp vẫn mở câu V1. |
| 3 | Xong | Sửa `novixa-site/src/pages/vi/health-check.astro`: 6 nhóm, 31 câu, khoảng 7–10 phút, bỏ các câu hứa, hình 62/100 là minh họa và nhãn Khá tốt. Sửa thẻ nhà thuốc ở `danh-gia/index.astro`. `npx astro build` xong. |
| 4 | Xong | Kịch bản Sales và tài liệu đang trích câu cũ đổi thành bài tự làm 31 câu, khoảng 7–10 phút, 6 nhóm; cuộc gọi sau báo cáo 10–15 phút nếu đồng ý. |
| 5 | Xong | 8 nguyên nhân gốc sinh từ `build.py`. Migration 447 chạy hai lần trên DB dev. Báo cáo bài thử hiện phần nguyên nhân gốc. O5 giữ nguyên câu phát hiện. Bản nháp trong `PHARMACY_V2.review.md`. |
| 6 | Xong | `scripts/kap-v2-completion-time.sql` chạy trên DB dev. Một bài thử 6,1 phút, không đổi câu "khoảng 7–10 phút". |
| 7 | Xong | Mã đối tác không hợp lệ không còn chặn bài: `source=public_web`, mã gốc vào `metadata.invalidPartnerCode`, có log cảnh báo. UTM lưu `metadata.utm`, chỉ chữ số `_` `-`, tối đa 100 ký tự. `appsettings.Production.json`: `BenchmarkAggregateMinSampleSize` 5 → 30 (đã duyệt). `dotnet build` 0 error. `npm run build` assessment-web qua. Mở `/?ref=SAIMA` vào được câu P2, bài `df5eda8f-83b4-4e62-8862-c6278936349e`, source `public_web`, `invalidPartnerCode=SAIMA`. Mở `/?utm_campaign=test&utm_content=can_han` tạo bài `adb974ab-6c16-451f-bc6c-6e3fee9f7d51` với `utm` đúng hai trường. SQL nguồn chạy được: campaign `test` / content `can_han`, 1 bắt đầu, 0 xong, 0 để lại số. |
| 9 | | |
| 8 | Dừng | Không deploy. `scripts/deploy-kap-vps.ps1` publish cả API rồi rsync đè `/var/www/kit-platform/api` và restart service dùng chung. Cây làm việc còn thay đổi chưa commit ngoài KAP: Pharmacy sales (`SalesController.cs`, `SalesService.cs`), Content (topic scout, autopilot), Loyalty, Sales agent. Đẩy bản này sẽ đụng code nhà thuốc đang chạy, kể cả tenant `NT_XUANHOA`. Chưa backup DB production, chưa chạy migration 447 trên production, chưa deploy `novixa-site`. |
| 10 | Xong | Hash SHA256 48 file `*.dll` trong `src/KitPlatform.Api/bin/Release/net10.0` (bản build 2026-11-18 21:10, sát giờ DLL trên VPS). 27 DLL dự án ghi giờ 2026-11-18 21h, còn lại là thư viện NuGet cũ hơn. Commit cuối của git là `134d7834` lúc 2026-11-16 11:00, nên bộ DLL này không phải bản sạch của một commit. File handoff không có mục Việc 10; làm theo yêu cầu so hash DLL. |
| 11 | Xong | Chỉ đọc trên VPS: `sha256sum *.dll` trong `/var/www/kit-platform/api`. 49 file. Không sửa, không restart. |
| 12 | Dừng | Không phải mọi DLL giống nhau. 48 file có ở cả hai bên thì hash trùng hết. VPS thừa `KitPlatform.Packs.PersonalIntelligence.ResearchHost.dll` (80896 byte, giờ sửa 2026-11-16 12:43 +07), máy local không có file này trong thư mục Release. Không tạo worktree, không deploy. |
