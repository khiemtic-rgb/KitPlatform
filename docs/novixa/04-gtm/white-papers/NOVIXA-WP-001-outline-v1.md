# NOVIXA-WP-001 — Outline chi tiết (V1)

**Tên tài liệu:** Từ Quản lý đến Phát triển — Vì sao nhà thuốc cần một nền tảng mới sau POS?  
**Mã:** NOVIXA-WP-001 · **Loại:** White Paper phân tích ngành (không phải brochure bán hàng)  
**Phiên bản outline:** 1.0 · **Ngày:** 2026-08-05  
**Độ dài mục tiêu bản đầy đủ:** 40–60 trang (PDF in / web long-form)  
**Độc giả chính:** Chủ nhà thuốc GPP, dược sĩ chủ, quản lý chuỗi nhỏ (1–10 cửa)  
**Độc giả phụ:** Tư vấn chuyển đổi số ngành dược, đối tác triển khai  

**Bản đầy đủ (chuẩn):** [NOVIXA-WP-001-tu-quan-ly-den-phat-trien-v1.md](./NOVIXA-WP-001-tu-quan-ly-den-phat-trien-v1.md)  
**Tài liệu kèm:** [Executive Brief](./NOVIXA-WP-001-executive-brief-v1.md) (8–12 trang đọc nhanh) · PDF in: `print/NOVIXA-WP-001-Tu-Quan-ly-den-Phat-trien.pdf`  
**Neo định vị nội bộ:** [operational-positioning-v1.md](../../01-company/operational-positioning-v1.md) (NVX-CMP-05)  
**Series:** WP-001 → WP-003 → WP-004 → WP-002 → WP-005 (không ship cả bộ cùng lúc)

---

## 0. Mục tiêu biên tập & chỉ số thành công

### 0.1 Mục tiêu đọc (reader outcome)

Sau khi đọc, chủ nhà thuốc **tự kết luận**:

> “Mình đang dùng POS để **quản lý**. Mình chưa có nền tảng để **phát triển** nhà thuốc sau bán hàng.”

### 0.2 Không phải mục tiêu

- Chứng minh “Novixa thắng Sapo / KiotViet / …”
- Liệt kê feature theo kiểu catalog sản phẩm
- Hứa chăm sóc y khoa thay bác sĩ / AI kê đơn

### 0.3 Giọng văn

| Làm | Không làm |
|-----|-----------|
| Ghi nhận thành tựu POS ngành | Công kích / gọi tên để hạ đối thủ |
| Đặt câu hỏi mở cho chủ tự trả lời | Ép kết luận bằng số liệu không nguồn |
| Nói bằng vòng đời sau hóa đơn | Nói bằng jargon module nội bộ |
| Gắn Ready / Pilot / Định hướng khi nhắc năng lực | Đồng nhất mọi vòng lặp như đã vận hành toàn quốc |

### 0.4 Nhãn trạng thái năng lực (bắt buộc khi viết phần VI–IX)

| Nhãn | Ý nghĩa với độc giả |
|------|---------------------|
| **Ready** | Đang vận hành trên nền tảng Novixa hôm nay |
| **Pilot** | Đang thử / mở dần theo tenant |
| **Định hướng** | Tầm nhìn sản phẩm; chưa phải cam kết triển khai đồng loạt |

### 0.5 Anti-claims (nhắc ở lời nói đầu + phụ lục)

- Không thay bác sĩ · không khám · không điều trị  
- AI hỗ trợ quyết định vận hành — không tự kê đơn / chẩn đoán  
- Không thay phần mềm kế toán thuế đầy đủ (Phase 2)

---

## 1. Cấu trúc bản đầy đủ (13 chương + phụ lục · khung cũ ~10 phần)

| Khối | Chương (bản MD hiện tại) | Vai trò |
|------|--------------------------|---------|
| Bìa · Lời nói đầu | — | Định khung “đối chiếu / phân tích ngành” |
| Thị trường & khoảng trống | 1–5 | Ghi nhận POS + mở bài toán sau bán |
| Từ hóa đơn → phát triển | 6–9 | Hóa đơn → việc chăm → Novixa mở rộng trên nền POS |
| **App & định vị gắn kết** | **10** | *Sổ sức khỏe số của gia đình* · nhà thuốc = người bạn đồng hành · nền tảng gắn kết sức khỏe |
| Chi phí cơ hội · ngày vận hành · sau một năm | 11–13 | Cơ hội bỏ lỡ · day-in-life · kết |
| Phụ lục A–E | — | Tự kiểm nhà thuốc · trạng thái · góc nhìn khách |
| **Phụ lục F–J** | — | Vì sao có App cho dân · giá trị người dùng · khách vs user · một ngày · lợi ích hai phía |

> **Chương 10** đặt sau Chương 9 (POS là nguồn dữ liệu) và trước chi phí cơ hội / ngày vận hành — cầu nối tự nhiên: *“29 ngày khách không đến cửa?”*  
> **Phụ lục F–J** đặt sau Phụ lục E (góc nhìn khách) — đào sâu phía người dân / gia đình mà không cắt mạch chương chính.

---

## 2. Outline chi tiết theo chương

### Bìa

- Tiêu đề chính / phụ đề  
- Mã NOVIXA-WP-001 · Năm xuất bản · “White Paper — Phân tích ngành”  
- Một câu neo: *Thị trường POS đã giải quyết bài toán quản lý bán hàng. Novixa được xây để giải quyết bài toán tiếp theo: phát triển nhà thuốc sau bán hàng.*

### Mục lục

Đủ đến cấp 2 (Phần → mục A/B/C). Không liệt kê hàng trăm tiểu mục trên mục lục in.

### Lời nói đầu (1–1,5 trang)

**Ý chính**

1. Nhà thuốc Việt Nam đã số hóa quầy — đó là tiến bộ thật.  
2. Sau số hóa, câu hỏi đổi từ “bán được chưa?” sang “sau bán thì sao?”.  
3. Tài liệu này **không phủ nhận** phần mềm quản lý hiện có; nó mô tả **lớp giá trị kế tiếp**.  
4. Cách đọc: ưu tiên Phần II–V nếu chỉ có 20 phút; Executive Brief nếu chỉ có 10 phút.

**Deliverable viết:** 600–900 chữ, không pitch giá.

---

### Cách đọc tài liệu này (0,5 trang)

| Hồ sơ độc giả | Đọc trước |
|---------------|-----------|
| Chủ đang hài lòng với POS hiện tại | II → V → X |
| Chủ đang đau doanh thu khách cũ | V → VII → IX |
| Chủ kỹ tính / so sánh hệ thống | I → IV → VIII + phụ lục nhãn trạng thái |
| Cố vấn / nội bộ Novixa | Cả bộ + Phụ lục A–C |

---

## PHẦN I — Thị trường POS đã giải quyết điều gì?  
*(~4–5 trang · Tone: ghi nhận)*

### I.A Bối cảnh 15–20 năm số hóa nhà thuốc

- Từ sổ tay / Excel → quầy điện tử  
- Áp lực GPP, lô, hạn dùng, hóa đơn  
- POS trở thành “hạ tầng mặc định” của nhà thuốc hiện đại

### I.B Những bài toán đã được giải quyết tốt (checklist ngành)

Liệt kê dạng ghi nhận (không gắn brand):

1. Quản lý bán hàng / thu ngân  
2. Quản lý tồn kho  
3. Theo dõi lô và hạn dùng  
4. Hỗ trợ nguyên tắc FEFO trong vận hành  
5. Báo cáo doanh thu theo ngày / tháng  
6. Quản lý nhân viên / ca cơ bản  
7. Kết nối hoặc chuẩn bị hóa đơn điện tử (mức độ tùy hệ thống)  
8. (Tuỳ chọn) Khuyến mãi, điểm thưởng cơ bản  

**Ghi chú biên tập:** Mỗi mục 3–5 câu “vì sao đây là bước tiến lớn”.

### I.C Nếu không có POS thì nhà thuốc mất gì?

- Tốc độ phục vụ  
- Kiểm soát thất thoát  
- Minh bạch doanh thu  
- Khả năng mở thêm cửa

### I.D Kết luận phần I (1 đoạn khóa)

> POS không phải “công nghệ cũ”. POS là nền tảng giao dịch mà nhà thuốc hiện đại **không thể thiếu**.

**Không có:** câu “nhưng họ thiếu…”, “tuy nhiên đối thủ…”.

---

## PHẦN II — Sau khi POS hoàn thành nhiệm vụ  
*(~4–5 trang · Tone: mở câu hỏi)*

### II.A POS kết thúc ở đâu trong hành trình khách?

Sơ đồ chữ:

```
Vào quầy → Tư vấn / chọn thuốc → Thanh toán → Hóa đơn → Rời cửa
                                                      ↑
                                              Điểm kết thúc phổ biến
```

### II.B Câu hỏi mở sau khi khách rời cửa

Viết thành loạt câu hỏi chủ tự trả lời (không ép số % nếu chưa có nguồn):

- Khách uống thuốc đúng liệu trình không?  
- Khi nào họ sắp hết thuốc?  
- Họ còn tin nhà thuốc mình không?  
- Lần mua tiếp theo sẽ ở đâu?  
- Gia đình họ còn nhu cầu chăm sóc nào?

### II.C “Mô hình hóa đơn kết thúc” vs “mô hình hóa đơn bắt đầu”

| | Hóa đơn kết thúc | Hóa đơn bắt đầu |
|--|------------------|-----------------|
| Mục tiêu | Ghi nhận giao dịch | Mở vòng chăm sóc & quay lại |
| Dữ liệu sau bán | Ít hoặc không dùng | Được kích hoạt theo thời gian |
| Vai trò chủ | Xem doanh thu ngày | Xem việc cần làm với khách |

### II.D Kết luận phần II

> Khi POS hoàn thành nhiệm vụ bán hàng, một nhiệm vụ mới xuất hiện — không phải nhiệm vụ của “thu ngân”, mà của **phát triển nhà thuốc**.

---

## PHẦN III — Một hóa đơn có thể tạo ra điều gì?  
*(~5–6 trang · Tone: mở rộng ý nghĩa dữ liệu)*

### III.A Giải phẫu một đơn thuốc / hóa đơn nhà thuốc

Lớp thông tin tiềm năng (mỗi lớp 1 đoạn + ví dụ đời thường):

1. Danh tính liên hệ (họ tên, SĐT — khi khách đồng ý)  
2. Sản phẩm / hoạt chất  
3. Số lượng, đơn vị  
4. Liều dùng / hướng dẫn (khi ghi nhận được)  
5. Số ngày dùng ước tính → **ngày có thể hết thuốc**  
6. Ngữ cảnh sức khỏe lặp lại (mãn tính, theo mùa…)  
7. Quan hệ gia đình (khi khách đăng ký app / hồ sơ)  
8. Lịch sử mua theo thời gian  

### III.B Từ dữ liệu đơn lẻ → lớp vận hành

Luồng narrative (không trình bày như roadmap kỹ thuật nặng):

```
Hóa đơn
  → Timeline khách
    → Việc cần chăm sóc (nhắc / tái mua / theo dõi)
      → App / kênh liên hệ
        → CRM chủ động
          → Chiến dịch có mục tiêu
            → Dashboard chủ
              → (Định hướng) Connect hệ sinh thái chăm sóc
```

### III.C Ý chính khóa

> Một hóa đơn không chỉ là chứng từ thanh toán.  
> Một hóa đơn có thể là **điểm khởi đầu của một hệ sinh thái giá trị** — nếu nhà thuốc chọn kích hoạt lớp sau bán hàng.

### III.D Hộp “Ranh giới đạo đức & pháp lý” (0,5 trang)

- Đồng ý liên hệ / bảo vệ dữ liệu cá nhân  
- Không suy diễn chẩn đoán từ lịch sử mua  
- AI / tự động hóa chỉ hỗ trợ vận hành chăm sóc hợp lệ  

---

## PHẦN IV — Lớp sau bán hàng: POS giao dịch và nền tảng phát triển  
*(~5–6 trang · Tone: phân tầng, không tuyệt đối hóa)*

### IV.A Cách đặt vấn đề đúng

**Không viết:** “Tất cả POS đều không có…”  
**Viết:** “Các năng lực dưới đây là **lớp mở rộng giá trị sau bán hàng** — nơi nhiều nhà thuốc vẫn phụ thuộc sổ tay, Excel rời, hoặc chưa vận hành thành hệ.”

### IV.B Bảng phân tầng (dùng bảng này thay bảng ✓/✗ cạnh tranh)

| Năng lực sau bán hàng | Thường đủ ở lớp POS giao dịch | Lớp phát triển Novixa hướng tới |
|----------------------|-------------------------------|----------------------------------|
| Lưu hóa đơn / doanh thu | Phổ biến | Ready (cùng lớp giao dịch) |
| Quản lý kho / lô / FEFO | Phổ biến ở POS chuyên biệt NT | Ready |
| Hồ sơ khách gắn giao dịch | Thường có cơ bản | Ready — kích hoạt sâu hơn sau bán |
| App khách đồng hành | Không phải mặc định | Ready / Pilot theo gói |
| Nhắc lịch trình dùng / tái mua | Hiếm khi thành vòng kín | Ready / Pilot |
| Hàng đợi việc chăm sóc cho chủ/quầy | Hiếm | Pilot → Ready (Care Queue) |
| CRM / chiến dịch có mục tiêu | Thường rời hoặc thủ công | Ready / Pilot |
| AI hỗ trợ ưu tiên việc cần làm | Mới bắt đầu trên thị trường | Pilot / Định hướng (anti-claim) |
| Hồ sơ gia đình | Hiếm | Pilot / Định hướng |
| Kết nối phòng khám / chuyển tuyến | Hiếm, cục bộ | Định hướng (Connect) |
| Theo dõi hành trình chăm sóc dài hạn | Hiếm thành hệ | Định hướng có đo được |

### IV.C Giải thích từng hàng “lớp phát triển” (1/2–1 trang mỗi nhóm)

Nhóm A — Giao dịch & kho (nền)  
Nhóm B — Quan hệ khách sau bán  
Nhóm C — Vận hành chăm sóc theo thời gian  
Nhóm D — Hệ sinh thái (Connect)

### IV.D Kết luận phần IV

> Câu hỏi không còn là “POS nhà mình thiếu nút nào?”.  
> Câu hỏi là: “Sau bán hàng, nhà thuốc mình đang vận hành **lớp nào**?”

---

## PHẦN V — Chủ nhà thuốc đang mất gì?  
*(~4–5 trang · Tone: kinh doanh, không đổ lỗi POS)*

### V.A Bốn câu hỏi sau 30 ngày (kịch bản 100 khách/ngày)

Với giả định minh họa (ghi rõ *minh họa*, không khẳng định thống kê quốc gia):

1. Bao nhiêu người quay lại đúng nhà thuốc mình?  
2. Bao nhiêu người có thể đã bỏ liệu trình?  
3. Bao nhiêu người sắp hết thuốc trong 7 ngày tới?  
4. Bao nhiêu doanh thu tháng này đến từ khách đã từng mua?

### V.B Chi phí của “không biết”

- Doanh thu tái mua bị đẩy sang kênh khác  
- Nhân viên giỏi tư vấn nhưng không có việc rõ sau ca  
- Marketing làm theo cảm tính  
- Chủ nhìn doanh thu ngày mà không nhìn “sức khỏe quan hệ khách”

### V.C Đây không phải lỗi của POS

Đoạn bắt buộc giữ nguyên tinh thần:

> Khoảng trống này **không phải lỗi** của phần mềm POS.  
> Đó **không phải nhiệm vụ cốt lõi** mà POS được thiết kế để giải quyết.  
> POS được sinh ra để **quản lý giao dịch**.  
> Phát triển sau bán hàng là **bài toán lớp tiếp theo**.

### V.D Kết luận phần V

Liệt kê 5 “tài sản đang để ngoài sổ” theo ngôn ngữ chủ: khách cũ, liệu trình, thời điểm tái mua, uy tín tư vấn, dữ liệu chiến dịch.

---

## PHẦN VI — Novixa được sinh ra để giải quyết khoảng trống đó  
*(~6–8 trang · Chương xương sống)*

### VI.A Định vị một câu

> Novixa giúp nhà thuốc **vận hành chuẩn để chăm sóc khách bền vững** — trên cùng một nền dữ liệu từ quầy bán đến vòng sau bán hàng.

(Neo NVX-CMP-05 GTM one-liner.)

### VI.B Vòng lặp “Hóa đơn bắt đầu”

Sơ đồ trung tâm tài liệu:

```
POS (sinh dữ liệu giao dịch)
  → Hồ sơ & timeline khách
    → Việc chăm sóc / nhắc / tái mua
      → App & kênh liên hệ
        → CRM & chiến dịch có mục tiêu
          → Dashboard & ưu tiên việc của chủ
            → (Định hướng) Connect hệ sinh thái chăm sóc
```

Mỗi mũi tên = 1 mục viết: **việc gì xảy ra · ai dùng · giá trị · nhãn Ready/Pilot/Định hướng**.

### VI.C Ba tầng narrative + tháp giá trị App

| Tầng | Tên | Ý nghĩa với chủ |
|------|-----|-----------------|
| 1 | Smart Pharmacy | Vận hành quầy–kho–mua hàng chuẩn |
| 2 | Smart Care | Vòng chăm sóc sau bán đo được |
| 3 | Community Health | Tầm nhìn dài: nhà thuốc phục vụ sức khỏe cộng đồng tốt hơn — **không thay bệnh viện** |

**Chương 10 (bản đầy đủ)** đào sâu Big Idea:

> **Sổ sức khỏe số của mỗi gia đình** — nhà thuốc là **người bạn đồng hành sức khỏe của gia đình**.

```
Người dân / gia đình gắn bó hơn
        ▲
Sổ sức khỏe số · đồng hành hằng ngày
        ▲
Hồ sơ theo từng thành viên
        ▲
Nhà thuốc đồng hành của gia đình
        ▲
POS + Quản lý vận hành
```

Gọi trong ấn phẩm Việt: **nền tảng gắn kết sức khỏe** *(Health Engagement Platform)*. Không gọi chung chung “App sức khỏe”.  
**Phụ lục F–J** bổ sung Q&A / bảng giá trị / day-in-life phía người dân.

### VI.D “Operating moments” trong ngày của chủ (thay vì list module)

Ví dụ khung ngày:

- Sáng: việc chăm sóc / khách sắp hết thuốc  
- Trong ca: bán đúng lô–HSD, ghi nhận khách khi phù hợp  
- Cuối ngày: biết doanh thu **và** biết việc còn mở với khách  
- Cuối tuần: chiến dịch / nhóm khách cần chăm  
- Cuối tháng: tỷ trọng khách quay lại, hiệu quả chăm sóc

### VI.E Hộp anti-claim ngắn

Nhắc lại ranh giới y khoa & AI.

---

## PHẦN VII — Giá trị kinh doanh nhìn từ ghế chủ  
*(~4–5 trang · Tone: outcome, không “có AI/CRM”)*

### VII.A Năm câu chủ muốn trả lời được

1. Hôm nay **ai** cần được chăm sóc?  
2. **Ai** sắp đến lúc mua lại?  
3. Doanh thu đang đến từ khách mới hay khách cũ?  
4. Chiến dịch / lần chăm nào có hiệu quả?  
5. Nhân sự nào duy trì chất lượng tư vấn–chăm sóc tốt?

### VII.B Từ câu hỏi → chỉ số gợi ý (không bịa số quốc gia)

| Câu hỏi chủ | Chỉ số theo dõi (khi triển khai) |
|-------------|----------------------------------|
| Ai cần chăm | Số việc chăm mở / quá hạn |
| Ai sắp mua lại | Cohort sắp hết liệu trình |
| Khách cũ | % doanh thu từ khách quay lại |
| Chiến dịch | Tỷ lệ phản hồi / đơn phát sinh |
| Nhân sự | Chuẩn ca / ngoại lệ / hoàn thành checklist |

### VII.C Case minh họa dạng “một nhà thuốc” (fiction có kiểm soát)

1 kịch bản 1 cửa GPP · trước/sau 90 ngày — **không** gắn ROI % cứng nếu chưa có case study thật.  
Khi có founding case: thay bằng số đo được (WP-003 sẽ đào sâu).

---

## PHẦN VIII — Vì sao Novixa vẫn cần POS?  
*(~3–4 trang · Tone: kiến trúc giá trị)*

### VIII.A Ngộ nhận cần gỡ

- “Phát triển = chỉ cần app / CRM bên ngoài”  
- “POS chỉ để in bill”

### VIII.B POS là nơi sinh dữ liệu tin cậy

Luồng:

```
Đơn bán đúng (SP, lô, SL, KH)
  → Timeline có thật
    → Việc chăm sóc có căn cứ
      → Reminder / CRM / báo cáo có ý nghĩa
```

Không có lớp giao dịch chuẩn → lớp phát triển trở thành **đoán mò**.

### VIII.C Một nền tảng, một sự thật dữ liệu

- Tránh Excel / app rời lệch tồn và lệch khách  
- Chủ và nhân viên nhìn cùng một nguồn  
- Chăm sóc sau bán không tách khỏi tồn–bán–trả hàng

### VIII.D Kết luận phần VIII

> POS không phải “phần cũ cần thay”.  
> POS là **động cơ dữ liệu**. Novixa tổ chức động cơ đó thành năng lực phát triển.

**→ Bản đầy đủ tiếp bằng Chương 10:** App sức khỏe & vì sao không chỉ xây phần mềm cho nhà thuốc (sau khi đã hiểu POS = nguồn dữ liệu tin cậy).

---

## PHẦN IX — Lợi ích khi chuyển đổi theo thời gian  
*(~4–5 trang · Tone: lộ trình thực tế)*

### IX.A Nguyên tắc

Không hứa “bật AI là tăng doanh thu ngay”.  
Mô tả **năng lực xuất hiện theo thời gian** khi nhà thuốc vận hành có kỷ luật dữ liệu.

### IX.B Lộ trình tham chiếu (chỉnh theo onboarding thực tế trước khi publish)

| Mốc | Chủ nhìn thấy gì | Điều kiện thành công |
|-----|------------------|----------------------|
| **0–30 ngày** | Vận hành quầy–kho ổn; dashboard giao dịch rõ | Migrate danh mục/tồn; training ca |
| **30–60 ngày** | Khách bắt đầu có điểm chạm app / hồ sơ | Quy trình hỏi–ghi SĐT hợp lệ |
| **60–90 ngày** | Danh sách việc chăm / khách sắp quay lại có vận hành | Nhân sự dùng hàng ngày, không chỉ “cài app” |
| **90–180 ngày** | Đủ chuỗi dữ liệu để nhìn lại hiệu quả chăm sóc & ưu tiên | Kỷ luật dữ liệu + review tuần của chủ |

Ghi chú: số ngày là **khung tham chiếu**, không SLA pháp lý.

### IX.C Việc chủ cần làm (không chỉ “đổi phần mềm”)

- Chuẩn hóa dữ liệu khách  
- Thói quen quầy ghi nhận đúng  
- Review việc chăm sóc định kỳ  
- Chọn 1–2 vòng lặp đo được trước (vd. tái mua nhóm thuốc mãn tính)

---

## PHẦN X — Đây không phải cuộc đua POS  
*(~2–3 trang · Kết)*

### X.A Tái khẳng định

- Thị trường POS đã làm tốt bài quản lý bán hàng.  
- Bài tiếp theo là phát triển sau bán hàng.

### X.B Câu kết chuẩn (giữ nguyên tinh thần)

> POS giúp nhà thuốc bán được một đơn hàng.  
> Novixa giúp một đơn hàng tạo ra nhiều đơn hàng tiếp theo — thông qua vận hành chăm sóc có dữ liệu.

### X.C Lời mời đọc tiếp (không CTA giá)

- Executive Brief (cùng mã)  
- NOVIXA-WP-003 — Kinh tế của khách hàng quay lại *(khi xuất bản)*  
- NOVIXA-WP-004 — Vì sao mỗi đơn thuốc là một tài sản dữ liệu  

### X.D Câu hỏi để lại cho độc giả (nửa trang)

1 câu: *Nhà thuốc mình đang dừng ở quản lý giao dịch — hay đã bắt đầu phát triển sau bán hàng?*

---

## 3. Phụ lục (bản đầy đủ)

### Phụ lục A — Bảng nhãn Ready / Pilot / Định hướng (chi tiết năng lực)

Cập nhật theo [module-catalog](../../02-product/module-catalog-v1.md) + `PHASE_SCOPE.md` trước mỗi lần xuất bản.

### Phụ lục B — Thuật ngữ

POS giao dịch · Lớp sau bán hàng · Care Queue · O2O · FEFO · Cohort · Connect…

### Phụ lục C — Nguồn & giới hạn phương pháp

- Phân tích định tính ngành + kiến trúc sản phẩm Novixa  
- Minh họa số chỉ mang tính cấu trúc tư duy  
- Case định lượng chuyển sang WP-003 khi có dữ liệu pilot

### Phụ lục D — Cấm / được phép nói (cho Sales & Content)

| Được | Không được |
|------|------------|
| “Sau POS là bài toán phát triển” | “Novixa hơn [tên đối thủ] vì…” |
| “POS hoàn thành nhiệm vụ quản lý” | “Tất cả POS đều không có CRM/App” |
| Nhãn Ready/Pilot/Định hướng | Hứa Connect toàn quốc / AI kê đơn |

### Phụ lục E — Bản đồ series White Paper

| Mã | Tên | Vai trò |
|----|-----|---------|
| WP-001 | Từ Quản lý đến Phát triển | Đổi khung tư duy *(tài liệu này)* |
| WP-002 | Nhà thuốc trong kỷ nguyên AI | AI đổi vận hành (sau khi có use-case đo được) |
| WP-003 | Kinh tế của khách hàng quay lại | Chứng minh tiền (CRM, App, refill) |
| WP-004 | Mỗi đơn thuốc là tài sản dữ liệu | Vai trò POS trong hệ sinh thái |
| WP-005 | Healthcare Platform | Tầm nhìn Người dân – Nhà thuốc – Phòng khám |

---

## 4. Kế hoạch sản xuất bản đầy đủ

| Bước | Việc | Owner gợi ý |
|------|------|-------------|
| 1 | Chốt outline + Executive Brief | Leadership / GTM |
| 2 | Draft Phần I–V (không product-heavy) | Content + GTM |
| 3 | Draft Phần VI–IX + gắn nhãn trạng thái với Product | Product review bắt buộc |
| 4 | Anti-claim & pháp lý dữ liệu cá nhân | Leadership + Legal nhẹ |
| 5 | Design PDF / web long-form | Brand |
| 6 | Sales script khóa theo Phụ lục D | Sales lead |
| 7 | Xuất bản WP-001 + Brief; trì hoãn WP-002/005 | GTM |

**Ước lượng trang viết draft:** ~12.000–18.000 chữ (VI).

---

## Changelog

| Version | Ngày | Thay đổi |
|---------|------|----------|
| 1.0 | 2026-08-05 | Outline chi tiết 10 phần + phụ lục + plan sản xuất |
| 1.1 | 2026-08-05 | Bản đầy đủ = 13 chương; chèn Chương 10 App / nền tảng gắn kết sức khỏe |
| 1.2 | 2026-08-05 | Big Idea Sổ sức khỏe số; Phụ lục F–J sau E |

---

*Owner: Leadership / GTM · Reviewer: Product (nhãn trạng thái) · Sync với NVX-CMP-05*
