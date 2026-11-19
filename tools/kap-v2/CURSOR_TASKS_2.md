# Giao việc đợt 2: xếp báo cáo theo nỗi đau, rồi deploy kiểu thay đúng phần cần thay

File này **tách riêng** khỏi `CURSOR_HANDOFF.md`, vì file đó đã hai lần bị ghi đè bằng bản cũ. Quy tắc chung vẫn như mục "Quy tắc khi làm" của `CURSOR_HANDOFF.md`. Kết quả ghi vào mục Nhật ký **cuối file này**. Nếu editor báo file đã đổi trên đĩa thì tải lại trước khi ghi.

**Đã biết (19/11):** bộ DLL production trùng hash với `src/KitPlatform.Api/bin/Release/net10.0`, build lúc 2026-11-18 21:10. Bộ này chứa thay đổi chưa commit tính tới lúc đó, nên **không được** build lại từ commit `134d7834` để deploy.

## Việc 10: Xếp báo cáo theo nỗi đau của người làm

Đã chạy thử 5 vai trên dev (bài `0172d10b`, `a1247e7b`, `8b7bce1b`, `fdc99618`, `4f5ef31d`). Kết quả:

- Hai ô nhận xét sơ bộ luôn bị ước tính tiền chiếm chỗ. Vai "nhân viên làm không đều tay" (Vận hành 13/100) chỉ thấy hai con số tiền kho.
- Báo cáo đầy đủ xếp việc nên làm chỉ theo mức ảnh hưởng của câu, không theo nỗi đau ở câu P3.

Sửa như sau:

1. **`build.py`: thêm `PAIN_QUESTIONS`.** Mỗi nỗi đau gắn với danh sách câu cụ thể, không gắn cả nhóm:
   - `PAIN_CUSTOMER`: C2, C3, C4, C5
   - `PAIN_INVENTORY`: I1, I3, I4, I5, I6
   - `PAIN_STAFF`: O1, O2, O3, O4
   - `PAIN_NUMBERS`: B1, B2, B3, B4
   - `PAIN_COMPLIANCE`: O5, T4
   - `PAIN_GROWTH`: G1, G2, O1, O2
2. **`gen_sql.py`: sinh thêm hai luật cho mỗi cặp (nỗi đau, câu).**
   - Luật phát hiện `V2_PINS_<PAIN>_<Q>`:
     - expression `response.P3.option_code = '<PAIN>' AND response.<Q>.score <= 2`. Bộ đánh giá đã hỗ trợ `AND`.
     - payload giống hệt `V2_INS_<Q>`.
     - priority 98 nếu impact ≥ 1.1, ngược lại 97.
   - Luật việc nên làm `V2_PREC_<PAIN>_<Q>`:
     - cùng expression.
     - payload giống `V2_REC_<Q>`, nhưng payload `priority` = 1.
     - rule priority 95.
3. **Đổi priority của luật ước tính.** `V2_EST_EXPIRED_*` giữ 99. `V2_EST_STOCKOUT_*` hạ từ 98 xuống **96**. Kết quả: ô sơ bộ thứ nhất là ước tính hàng hết hạn (nếu có), ô thứ hai là phát hiện đúng nỗi đau.
4. **Backend: bỏ trùng theo tiêu đề.** Ở `AssessmentSubmissionService`, cả lúc `complete` (chọn 2 nhận xét) và lúc `capture-lead` (lưu tất cả), không lưu hai nhận xét cùng `title`, hay hai việc nên làm cùng `title`. Giữ bản có priority cao hơn.
5. **Nhóm thiếu dữ liệu.** Nhóm có từ 50% câu tính điểm trở lên được trả lời bằng phương án `score = null` ("Không biết", "Chưa gặp"…) thì không tính điểm, và không vào điểm tổng. Trang kết quả và báo cáo ghi "Chưa đủ dữ liệu" cho nhóm đó, không hiện 0/100.
6. **`StartPage.tsx`: sửa lỗi treo ở dev** do React StrictMode. Giữ `started` ref, nhưng bỏ kiểm tra `cancelled` trước `navigate`, để lần chạy đầu vẫn chuyển trang.
7. **Bộ thử hồi quy.**
   - Tạo `tools/kap-v2/qa_personas.json`, gồm 5 bộ đáp án theo mã câu và mã phương án. Lấy từ phần "5 vai" ngay dưới.
   - Tạo `tools/kap-v2/run_qa.py`. Script gọi API công khai trên dev theo thứ tự: tạo bài với `utm_source=internal_test`, PUT đáp án, `complete`, rồi in điểm 6 nhóm và 2 nhận xét sơ bộ.
   - Script **không** gọi `capture-lead`.

**Xong khi:** chạy `run_qa.py` trên dev cho kết quả đúng như sau.

| Vai | Ô sơ bộ thứ nhất | Ô sơ bộ thứ hai |
|---|---|---|
| Vai 1 | Ước tính hàng hết hạn | Phát hiện của câu I1 |
| Vai 2 | Phát hiện C3 | Phát hiện C2 |
| Vai 3 | Ước tính hàng hết hạn | Phát hiện của câu O1, O2, O3 hoặc O4 |
| Vai 4 | Phát hiện B2 | Phát hiện B4 |
| Vai 5 | Phát hiện O5 | — |

Ngoài ra, vai 5 có nhóm Phát triển và Công nghệ hiện "Chưa đủ dữ liệu", không phải 0. Migration 447 sinh lại phải chạy hai lần trên dev không lỗi. `dotnet build` và `npm run build` đều qua.

**5 vai** (mã câu = mã phương án; câu không ghi là không có trong vai):

- **Vai 1:** P2=SITES_1, P3=PAIN_INVENTORY, P5=AOV_80_150, C1=REG_0_2, C2=OPT2, C3=OPT1, C4=OPT1, C5=OPT1, I1=OPT1, I2=EXP_1_3M, I3=OPT2, I4=OPT2, I5=OPT2, I6=OPT2, I7=NEAR_NONE, O1=OPT3, O2=OPT3, O3=OPT2, O4=OPT3, O5=OPT3, B1=OPT2, B2=OPT2, B3=OPT2, B4=OPT2, T1=OPT3, T2=OPT2, T3=OPT2, T4=OPT3, G1=OPT2, G2=UNKNOWN, G5=PRI_STOCK
- **Vai 2:** P2=SITES_1, P3=PAIN_CUSTOMER, P5=AOV_80_150, C1=REG_6_8, C2=OPT2, C3=OPT2, C4=OPT2, C5=OPT2, C6=WHY_FORGET, I1=OPT2, I2=EXP_LT1M, I3=OPT3, I4=OPT2, I5=OPT3, I6=OPT2, O1=OPT2, O2=OPT2, O3=OPT2, O4=OPT3, O5=OPT2, B1=OPT2, B2=OPT1, B3=UNKNOWN, B4=OPT1, T1=OPT1, T2=OPT2, T3=OPT1, T4=OPT2, G1=OPT1, G2=UNKNOWN, G5=PRI_RETENTION
- **Vai 3:** P2=SITES_2_3, P3=PAIN_STAFF, P5=AOV_150_300, C1=REG_3_5, C2=OPT3, C3=OPT3, C4=OPT2, C5=OPT3, I1=OPT3, I2=EXP_1_3M, I3=OPT3, I4=OPT3, I5=OPT3, I6=OPT3, O1=OPT2, O2=OPT1, O3=OPT1, O4=OPT1, O5=OPT2, O7=STAFF_CONTROL, B1=OPT3, B2=OPT2, B3=OPT2, B4=OPT2, T1=OPT3, T2=OPT3, T3=OPT3, T4=OPT3, G1=OPT3, G2=OPT2, G5=PRI_STAFF
- **Vai 4:** P2=SITES_1, P3=PAIN_NUMBERS, P5=AOV_GT300, C1=REG_3_5, C2=OPT4, C3=OPT3, C4=OPT3, C5=OPT3, I1=OPT4, I2=EXP_0, I3=OPT4, I4=OPT3, I5=OPT4, I6=OPT3, O1=OPT3, O2=OPT3, O3=OPT3, O4=OPT4, O5=OPT3, B1=OPT3, B2=OPT1, B3=OPT2, B4=OPT2, B5=NUM_MARGIN, T1=OPT4, T2=OPT4, T3=OPT3, T4=OPT4, G1=OPT2, G2=OPT3, G5=PRI_PROFIT
- **Vai 5:** P2=SITES_1, P3=PAIN_COMPLIANCE, P5=UNKNOWN, C1=UNKNOWN, C2=OPT2, C3=OPT1, C4=UNKNOWN, C5=OPT1, I1=UNKNOWN, I2=UNKNOWN, I3=UNKNOWN, I4=OPT1, I5=OPT2, I6=OPT1, O1=OPT1, O2=UNKNOWN, O3=UNKNOWN, O4=OPT2, O5=OPT2, O6=INSP_RECORDS, B1=OPT1, B2=OPT1, B3=UNKNOWN, B4=OPT1, T1=OPT1, T2=OPT1, T3=OPT1, T4=UNKNOWN, G1=OPT1, G2=UNKNOWN, G5=PRI_COMPLIANCE

## Việc 13: Deploy kiểu thay đúng phần cần thay (chỉ KAP V2)

1. **Giữ bản production làm gốc.**
   - Copy nguyên `src/KitPlatform.Api/bin/Release/net10.0` (bản 18/11 21:10, trùng production) sang `E:\kap-release\baseline\`.
   - Kiểm tra lại SHA256 khớp với hash VPS đã đo.
   - **Không build Release vào thư mục cũ** trước khi copy xong.
2. **Xác định assembly bị thay đổi bởi KAP.** Liệt kê mọi file đã sửa cho KAP (Việc 1–7 và 10), ghi từng file thuộc project nào.
   - Mong đợi chỉ có `KitPlatform.Packs.Survey.Application` và `KitPlatform.Packs.Survey.Infrastructure`.
   - Nếu có file trong project khác (ví dụ `KitPlatform.Api`, `KitPlatform.Infrastructure`) thì **dừng và báo**, vì DLL của project đó chứa cả code khác.
3. **Kiểm tra trong hai project Survey không có thay đổi ngoài KAP.** Xem file sửa sau 2026-11-18 21:10 trong hai project đó, và `git diff` của chúng. Có thay đổi không thuộc KAP thì dừng và báo.
4. **Build và ghép.**
   - Build Release hai project Survey (cùng cấu hình như `deploy-production.ps1`) ra thư mục riêng.
   - Copy `baseline` thành `E:\kap-release\candidate\`, rồi thay đúng hai DLL Survey (và `.pdb` nếu có).
   - Ghi hash trước và sau vào Nhật ký.
5. **Thử bộ ghép trên dev.**
   - Chạy API từ thư mục `candidate`, trỏ DB dev đã có migration 447. Assessment-web dev trỏ vào API đó.
   - Chạy `run_qa.py`, kết quả phải đúng bảng ở Việc 10.
   - Đăng nhập admin, mở trang bán hàng tenant demo: không lỗi.
   - Xem log API: không có `MissingMethodException`, `TypeLoadException` hay `FileLoadException`.
6. **Dừng và hỏi người phụ trách.** Ghi vào Nhật ký: danh sách file sẽ thay trên VPS, kết quả bước 5, cách quay lại. Rồi **dừng chờ người phụ trách trả lời "deploy" trong chat**.
7. **Khi được duyệt:**
   1. Backup DB production.
   2. Chạy `apply-447-kap-pharmacy-v2.ps1` lên production.
   3. Trên VPS, copy hai DLL Survey hiện tại sang `/var/www/kit-platform/api-backup-<giờ>/`.
   4. Upload hai DLL mới.
   5. Sửa đúng dòng `BenchmarkAggregateMinSampleSize` thành 30 trong `appsettings.Production.json` trên VPS.
   6. Restart `kit-platform-api`.
   7. Deploy `client/assessment-web` (build Release, rsync vào `/var/www/kit-platform/assessment-web/`).
   8. Deploy `novixa-site`.
   9. Kiểm tra production:
      - Bài V2 có đủ 31 câu.
      - `/?ref=SAIMA` vẫn làm bài được.
      - Trang health-check hiện nội dung mới.
      - Admin vẫn đăng nhập được.
      - Trang bán hàng của tenant demo vẫn mở được.
   10. Ghi **"ĐÃ DEPLOY"**.
8. **Quay lại nếu lỗi:**
   1. Copy hai DLL từ thư mục backup về.
   2. Restart service.
   3. Đặt lại assessment-web từ bản cũ, nhớ lưu bản cũ trước khi rsync.
   4. Migration 447 không cần gỡ, vì nó chỉ thêm bộ câu hỏi V2, V1 vẫn chạy.

Không đụng `KitPlatform.Packs.PersonalIntelligence.ResearchHost.dll` thừa trên VPS.

## Nhật ký

| Việc | Trạng thái | Ghi chú |
|---|---|---|
| 10 | | |
| 13 | | |
