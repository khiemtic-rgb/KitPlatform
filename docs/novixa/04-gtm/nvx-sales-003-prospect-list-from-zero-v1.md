# NVX-SALES-003 — Lấy danh sách nhà thuốc (từ 0 → ~50)

**Version:** 1.0 · **Khu vực mặc định:** Thái Nguyên (đổi được)  
**Nguyên tắc:** Chưa có list ≠ dừng GTM. **50 NT là mục tiêu tuần**, không phải điều kiện bắt đầu CRM.

**Sau khi có SĐT / Zalo:** chuyển sang [009 CRM](./nvx-sales-009-phc-crm-pipeline-v1.md)  
**File research:** [nvx-sales-003-prospect-research-template.csv](./nvx-sales-003-prospect-research-template.csv)

---

## 1. Hai bảng tách bạch

| Bảng | Khi nào | Mục đích |
|------|---------|----------|
| **Research** (003) | Chỉ biết tên / địa chỉ / Maps | Tìm & lọc ICP |
| **CRM** (009) | Đã có cách liên hệ (SĐT/Zalo) hoặc đã Contacted | Chạy pipeline bán |

Không nhét 50 dòng “chưa biết gì” vào CRM rồi gọi mù.

---

## 2. ICP nhanh (giữ / bỏ)

**Giữ ưu tiên**
- Nhà thuốc GPP độc lập hoặc chuỗi nhỏ (1–3 cửa)
- Trong địa bàn đang làm (vd. TP Thái Nguyên, Sông Công, Phổ Yên…)
- Có cửa hàng thật (Maps có địa chỉ / giờ mở)

**Bỏ / để sau**
- Chuỗi quốc gia lớn (Long Châu, An Khang…) trừ khi có intro
- Quầy thuốc / tủ thuốc quá nhỏ, không rõ GPP
- Trùng bản ghi (cùng địa chỉ)

---

## 3. Nguồn lấy list (hợp pháp, public)

Làm **theo thứ tự** — mỗi nguồn 15–30 phút/ngày là đủ:

1. **Google Maps** — tìm `"nhà thuốc"`, `"nhà thuốc GPP"`, lọc theo khu vực  
   - Copy: tên, địa chỉ, link Maps, SĐT nếu hiện công khai trên listing  
2. **Giới thiệu** — hỏi chủ NT đã quen / bạn dược sĩ (nguồn `referral` tốt nhất)  
3. **Hội / hiệp hội địa phương** — danh sách sự kiện, group Zalo ngành (xin phép trước khi spam)  
4. **Landing Health Check** — lead inbound (`source=web`) — vào thẳng CRM  
5. **Không** mua file “50.000 SĐT” không rõ nguồn — rủi ro pháp lý + spam chết kênh

> Chỉ lưu SĐT/Zalo **đã công khai trên listing doanh nghiệp** hoặc do chủ tự cho. Không scrape app nội bộ / trang đăng nhập.

---

## 4. Nhịp độ đề xuất (không cần list đủ mới gọi)

| Ngày | Việc | Output |
|------|------|--------|
| D1–D2 | Research Maps: ~15–20 NT | Điền Research CSV |
| D3 | Lọc ICP + bổ sung SĐT public | 8–12 dòng “có phone” |
| D4 | Chuyển sang CRM `Lead` + gửi Zalo (005) | 5–10 Contacted |
| D5+ | Vừa research vừa follow | Mục tiêu tuần: +10 research, +5 Contacted |

**Mục tiêu tháng 1:** ~50 trên Research **hoặc** ~30 đã Contacted — không bắt buộc 50 sẵn ngày 1.

---

## 5. Flow Research → CRM

1. Research: `research_status = found`  
2. Có SĐT public / chủ cho: `research_status = phone_ok`  
3. Copy dòng sang CRM 009 với `status=Lead`, `source=maps|referral|…`  
4. Gửi tin Zalo mở đầu (005) → CRM `Contacted`  
5. Nếu không thấy SĐT sau 2 lần kiểm Maps: `research_status = no_phone` — giữ để hỏi referral sau, **không** tạo CRM giả

---

## 6. Cột Research (tóm tắt)

`prospect_id` · `pharmacy_name` · `address` · `district` · `maps_url` · `phone_public` · `icp_fit` (`yes`/`no`/`maybe`) · `research_status` (`found` / `phone_ok` / `no_phone` / `skipped`) · `moved_to_crm` (`yes`/`no`) · `notes`

---

## 7. Khi nào mới “đủ để chạy tuần”

Không cần 50. Bắt đầu tuần sales khi có **≥5** lead CRM có SĐT và đã Contacted / có lịch HC.

Research tiếp song song — đừng chờ list hoàn hảo.
