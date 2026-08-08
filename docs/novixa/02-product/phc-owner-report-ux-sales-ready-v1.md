# PHC — Báo cáo dễ hiểu cho chủ nhà thuốc (Sales-ready)

**Mã:** `NVX-PHC-UX-01` · **Version:** 1.0 · **Ngày:** 2026-08-08  
**Áp dụng cho:** Pharmacy Health Check đang chạy + Report V2  
**Đối tượng đọc:** Chủ nhà thuốc độc lập (không rành CNTT)  
**Mục tiêu phụ:** Sales dễ mở cuộc gọi 20 phút và đề xuất Pilot 30 ngày  

**Liên quan:**  
[gtm-001-pharmacy-health-check-sales-pilot-paid-analysis-v1.md](../04-gtm/gtm-001-pharmacy-health-check-sales-pilot-paid-analysis-v1.md) ·  
handoff website pause · PHC-V2 master spec (trao đổi)

---

## 1. Mục tiêu trải nghiệm

Chủ nhà thuốc sau 2–3 phút cầm báo cáo phải trả lời được:

1. Nhà thuốc mình **đang ổn chỗ nào?**  
2. Mình **đang mất cơ hội / rủi ro chỗ nào?** (nỗi đau)  
3. **Nên làm gì trước** trong 30 ngày?  
4. Nếu muốn nhờ ai đồng hành — **bước tiếp theo là gì?** (Pilot, không “mua phần mềm”)

Sales sau khi gửi báo cáo phải có sẵn:

- 3 câu mở thoại theo nỗi đau  
- 1 đề xuất Pilot cụ thể (1–2 mục tiêu)  
- Không cần giải thích radar/score kỹ thuật

---

## 2. Nguyên tắc viết & trình bày

| Làm | Không làm |
|-----|-----------|
| Tiếng Việt đời thường, câu ngắn | Thuật ngữ: FEFO, KPI matrix, capability, dimension, rule, score band |
| Nói “anh/chị”, “nhà thuốc”, “khách quay lại” | “Module”, “pipeline”, “digital maturity”, “data layer” |
| Nỗi đau = hậu quả kinh doanh | “Điểm Kho 2.4/4” làm tiêu đề chính |
| Tối đa 1 điểm tổng + 6 thanh đơn giản | Radar / scatter / ma trận Impact×Feasibility trên trang đầu |
| 3 mạnh + 3 khoảng trống + 1 việc làm trước | 27 trang đầy đủ trước khi thấy kết luận |
| Solution Fit sau chẩn đoán | CTA “Mua Novixa” ngay trang 1 |
| Pilot = kiểm chứng cải thiện nỗi đau | “Dùng thử phần mềm free” |

**Giọng văn chuẩn (thay từ):**

| Tránh | Dùng |
|-------|------|
| Nhà thuốc yếu / kém | Còn dư địa cải thiện / nên ưu tiên chuẩn hóa |
| Hệ thống số hóa thấp | Việc ghi chép và theo dõi còn phụ thuộc nhiều vào người |
| Thiếu data-driven | Chưa dùng hết dữ liệu bán hàng để chăm khách |
| Module Inventory | Quản lý hạn dùng / giảm hàng cận date |
| CRM / O2O | Nhắc khách mua lại / giữ khách quen |

---

## 3. Cấu trúc “gói đọc chính” (Owner Pack)

Chủ NT **chỉ cần đọc gói này**. Phần còn lại = phụ lục (kỹ thuật / sales nội bộ).

### 3.1 Trang Cover (½–1 trang)

- Tiêu đề: **Đánh giá nhanh nhà thuốc** (có thể phụ đề “Pharmacy Health Check”)  
- Tên nhà thuốc · ngày · mã phiếu  
- Một dòng: *“Báo cáo giúp anh/chị nhìn rõ điểm mạnh và việc nên ưu tiên — không phải tài liệu kỹ thuật.”*  
- Không logo “AI”, không claim doanh thu.

### 3.2 Trang “Kết quả tóm tắt” (bắt buộc — quan trọng nhất)

Bố cục tối giản:

```
┌─────────────────────────────────────────────┐
│  Mức tổng: 58/100 · Đang trong giai đoạn    │
│  chuyển đổi vận hành                        │
│  [một thanh ngang đơn giản]                 │
├─────────────────────────────────────────────┤
│  ĐANG LÀM TỐT (tối đa 3)                    │
│  ✓ …                                        │
│  ✓ …                                        │
│  ✓ …                                        │
├─────────────────────────────────────────────┤
│  NỖI ĐAU / CƠ HỘI LỚN (tối đa 3)            │
│  01 … (1 câu hậu quả kinh doanh)            │
│  02 …                                       │
│  03 …                                       │
├─────────────────────────────────────────────┤
│  NÊN LÀM TRƯỚC (1 việc trong 30 ngày)       │
│  → …                                        │
├─────────────────────────────────────────────┤
│  Bước tiếp: Trao đổi 20 phút về kết quả     │
│  (không bán ngay trên trang này)            │
└─────────────────────────────────────────────┘
```

**Rule nội dung nỗi đau:** mỗi mục phải có dạng:

> **[Hiện tượng người thường hiểu]** → **[Hậu quả với tiền / thời gian / khách / rủi ro]**

Ví dụ đạt:

- “Có dữ liệu bán hàng nhưng chưa dùng để nhắc khách mua lại → dễ mất doanh thu từ khách quen.”  
- “Biết hạn dùng nhưng cảnh báo chưa chủ động → dễ cận date, vốn nằm im.”  
- “Hai cơ sở mà phải hỏi nhân viên mới biết tình hình → chủ khó yên tâm khi không đứng quầy.”

Ví dụ không đạt:

- “Category CUSTOMER = 2.1; Cross-rule CUST-001 fired.”

### 3.3 Trang “Sáu góc nhìn đơn giản” (tuỳ chọn ngắn)

6 thanh ngang = 6 category hiện tại (Kinh doanh, Khách hàng, Kho, Vận hành, Dữ liệu & CN, Phát triển).

Mỗi thanh kèm **một câu giải thích đời thường** (≤20 từ).  
Không biểu đồ tròn / radar trên gói Owner.

### 3.4 Trang “Việc làm trong 30 ngày”

Tối đa **3 việc**, mỗi việc:

| Mục | Ví dụ |
|-----|--------|
| Việc | Thiết lập danh sách khách cần chăm trong tuần |
| Ai làm | Chủ / quản lý ca |
| Xong khi nào | Trong 30 ngày |
| Biết hiệu quả khi | Có danh sách và đã gọi/nhắn ≥ N khách (baseline tuần 1) |

Không nhồi KPI ảo (“doanh thu +15%”).

### 3.5 Trang “Nếu muốn có người đồng hành” (Solution Fit nhẹ — Sales hinge)

Chỉ **sau** chẩn đoán:

> Dựa trên 3 nỗi đau trên, hướng đồng hành phù hợp thường là:  
> **Pilot 30 ngày — tập trung [1–2 mục tiêu đã chọn], không đổi hết hệ thống một lúc.**

3 hộp lựa chọn (tick mental, không bảng giá dày):

1. **An tâm vận hành & kho** (bán hàng thống nhất, hạn dùng, tồn)  
2. **Giữ khách & nhắc mua lại**  
3. **Chủ theo dõi từ xa / nhiều cơ sở**

Giá Pilot (Sales nói miệng hoặc trang phụ):

- 299.000đ / cơ sở / 30 ngày (vận hành)  
- 598.000đ nếu kèm lớp phát triển (App/chăm sóc)  
- “Phí Pilot được tính vào tháng đầu nếu tiếp tục.”

CTA đúng giọng:

- “Đặt lịch 20 phút để chọn 1–2 mục tiêu Pilot phù hợp nhà thuốc.”  
- Không: “Đăng ký gói ngay hôm nay — ưu đãi có hạn.”

---

## 4. Phụ lục (không đẩy lên đầu)

Giữ cho người cần đào sâu / auditor / sales kỹ thuật:

- Điểm chi tiết theo câu  
- Methodology / evidence / rule id  
- SWOT / risk / roadmap 90 ngày đầy đủ  
- Benchmark (chỉ khi có nguồn tin cậy; không “toàn quốc”)

**PDF mặc định gửi khách = Owner Pack (≈ 4–6 trang).**  
Full report / appendix = link “Xem bản đầy đủ” hoặc gửi khi khách hỏi.

---

## 5. Mapping nỗi đau → câu sales → Pilot (để chốt)

| Nỗi đau trên báo cáo (Owner) | Câu mở cuộc gọi | Demo ngắn | Pilot gợi ý |
|------------------------------|-----------------|-----------|-------------|
| Không biết khách nào sắp quay lại | “Anh/chị đang mất doanh thu từ khách quen vì chưa nhắc đúng lúc.” | Growth / lịch sử KH | Customer & Care 30 ngày |
| Cận date / vốn nằm im | “Phần kho đang ăn vốn im lặng.” | HSD / cảnh báo / FEFO (giải thích bằng lời: xuất gần hết hạn trước) | Inventory 30 ngày |
| Không nắm tình hình khi vắng mặt | “Anh/chị phải hỏi nhân viên mới yên tâm.” | Dashboard / báo cáo ngày | Foundation / Ops |
| Chăm khách dựa vào nhớ nhân viên | “Quy trình đang nằm trong đầu người, khó giao ca.” | SOP bán + danh sách chăm | Foundation + Care |
| Muốn App nhưng nền khách yếu | Không nhảy App | — | Foundation trước (dependency) |

**Sales không được** bắt đầu bằng menu module.  
**Sales phải** bắt đầu bằng đúng 1 trong 3 nỗi đau trên trang “Kết quả tóm tắt”.

---

## 6. Quy tắc kỹ thuật (để eng không phá UX)

1. **Presentation ≠ raw score:** có thể hiện 0–100; raw 1–4 giữ trong DB.  
2. **DiagnosticResult** phải có field dành cho Owner:
   - `ownerSummary` (3 strengths, 3 pains với `businessConsequence`)  
   - `oneThingFirst` (P1 action đời thường)  
   - `pilotHinge` (1–2 pilot goals bằng tiếng thường)  
3. Chart cho phép trên Owner Pack: **chỉ thanh ngang / số lớn**.  
4. AI narrative chỉ viết `ownerSummary` từ object đã chốt — không bịa %.  
5. Feature flag: `reportVariant = owner_v1 | full_v1` — mặc định gửi `owner_v1`.

---

## 7. Tiêu chí chấp nhận (UX)

Chủ NT đọc trang “Kết quả tóm tắt” trong **≤ 3 phút** và:

- [ ] Nói lại được ≥ 2 nỗi đau bằng lời của họ  
- [ ] Không hỏi “cái biểu đồ này nghĩa là gì?”  
- [ ] Không cảm thấy bị “chém phần mềm”  
- [ ] Sales đặt được lịch 20 phút trong ≥ 40% trường hợp sau khi gửi báo cáo (mục tiêu vận hành)

Pilot close:

- [ ] Đề xuất Pilot gắn đúng 1–2 nỗi đau trên báo cáo  
- [ ] Có giá + thời hạn 30 ngày + cam kết 2 chiều  
- [ ] Không “dùng thử vô thời hạn”

---

## 8. Việc không làm trong gói Owner

- Radar, ma trận Impact×Feasibility, bảng rule id  
- So sánh “trung bình ngành” khi chưa có cohort đủ  
- Claim “tăng X% doanh thu”  
- Đẩy App làm P1 khi nền khách/chăm sóc yếu  
- Dài > 8 trang trong email/Zalo gửi lần đầu

---

## 9. Thứ tự triển khai gợi ý

1. ✅ **Copy/ template Owner Pack** — xem thêm mẫu Minh An + sales checklist trong `docs/novixa/04-gtm/`  
2. ✅ **PDF `kind=owner` + OwnerPack trên API/UI** (presentation layer; không đổi scoring)  
3. Map pain từ rule cụ thể sau audit sâu (tiếp)  
4. Sales Kit đầy đủ 001–009  
5. Report V2 full/appendix

---

*Spec này không thay PHARMACY_V1. Nó định nghĩa lớp trình bày + ngôn ngữ + hinge Pilot để chủ NT hiểu và sales chốt được.*
