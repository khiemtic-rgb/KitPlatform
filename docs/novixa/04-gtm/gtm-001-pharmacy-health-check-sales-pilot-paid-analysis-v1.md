# GTM-001 — Pharmacy Health Check → Sales → Pilot → Paid

**Mã đề xuất:** `NVX-GTM-001` · **Phiên bản phân tích:** 1.0 · **Ngày:** 2026-08-08  
**Nguồn:** Trao đổi thiết kế GTM + PHC-012 (anti-bias) + PHC-013 (Master Rulebook)  
**Liên quan:** [handoff-website-pause-pharmacy-health-check-2026-08-08.md](./handoff-website-pause-pharmacy-health-check-2026-08-08.md) · [assessment-engine-api-v1.md](../03-solution/assessment-engine-api-v1.md) · [sales-playbook-v1.md](../04-gtm/sales-playbook-v1.md)

---

## 1. Kết luận ngắn

Bộ funnel bạn mô tả **đúng ưu tiên cạnh tranh lúc này** — mạnh hơn việc tiếp tục stack module.  
Kỹ thuật **đã có ~70% cửa vào** (landing → survey → lead unlock → PDF + rule engine `PHARMACY_V1`).  
Phần **chưa có và đang chặn GTM** là Sales Kit + Pilot commercial playbook + CRM pipeline — không phải “chưa có Health Check”.

| Lớp | Trạng thái repo | Gap so với trao đổi |
|-----|-----------------|---------------------|
| Landing CRO | `novixa-site` `/vi/health-check/` | Copy còn “30 câu / 7 phút” — lệch với bản 20 câu Sales |
| Survey product | `assessment-web` + `/api/public/assessment` | **30–31 câu** `PHARMACY_V1`, không phải 20 câu GTM-001 |
| Rule / PDF / unlock | `AssessmentRuleEngine` + intelligence | Chưa đóng băng theo PHC-012/013 numbering |
| Sales scripts / objection / Zalo | Generic `sales-playbook-v1` | **Thiếu** NVX-SALES-001…009 |
| Pilot 30 ngày có phí → Paid | Founding terms rải rác | **Thiếu** playbook Pilot 299/598 + review Day 30 |
| CRM pipeline Lead→Paid→Lost | Không có PHC CRM | **Thiếu** trạng thái + reason codes |
| Docs NVX-PHC / NVX-SALES | Không tồn tại | Toàn bộ series chưa vào repo |

**Quyết định sản phẩm cần chốt sớm (1 trong 3):**

1. **A — Sales Kit first (khuyến nghị ngay):** Giữ `PHARMACY_V1` 30–31Q làm Diagnostic Product; viết Sales Kit map sang pain → demo → Pilot. Không block bằng redesign câu hỏi.  
2. **B — 20Q Product cut:** Tạo template mới `PHARMACY_HC_SALES_V1` (20Q) + Rulebook V1 riêng; song song hoặc thay landing CTA.  
3. **C — Phase cả hai:** Pack Sales Kit trên engine hiện tại (tuần 1–2) → đóng Rulebook + optional 20Q (tuần 3–6).

Khuyến nghị vận hành: **C**, với Pack A không chờ Pack B.

---

## 2. Đánh giá chất lượng thiết kế (trao đổi)

### Điểm mạnh (nên giữ nguyên triết lý)

1. **Diagnostic trước sản phẩm** — “không xin dùng thử trước” đúng ICP nhà thuốc độc lập.  
2. **Pilot có phí + có cam kết hai chiều** — tránh freeload, tăng tín hiệu mua.  
3. **Demo theo nỗi đau** — khớp playbook hiện có, cụ thể hóa bằng Health Check.  
4. **Maturity ≠ Lead Score** (PHC-012) — tách Opportunity / Readiness / Fit: tránh bán nhầm nhà thuốc “điểm thấp nhưng không sẵn sàng” và bỏ lỡ nhà thuốc “điểm cao nhưng Opportunity lớn”.  
5. **Rule-based + AI chỉ viết** — deterministic, audit được, phù hợp sau này hàng nghìn báo cáo.  
6. **Anti-bias App/digital** — diễn đạt trung lập Q14–16: bắt buộc nếu muốn PHC là sản phẩm chẩn đoán thật.  
7. **Lost reasons** — biến 50 Health Check thành dữ liệu roadmap (hiếm và rất đáng làm sớm).

### Điểm cần siết trước khi scale

| Rủi ro | Mô tả | Cách xử lý đề xuất |
|--------|--------|---------------------|
| **Hai bộ câu hỏi** | GTM-001 = 20Q (5×4); engine hiện tại = 30–31Q; PHC-013 map Q08–Q20 lệch số | Chốt **một canonical map** `Sales20 ↔ Engine31` hoặc tách template; cấm số liệu lẫn trong report |
| **Hai thang maturity** | GTM: 0–39 / 40–59 / 60–79 / 80–100; PHC-013: 0–20…81–100 | Một bảng band chính thức trong Rulebook V1 |
| **Category weight lệch** | GTM mỗi nhóm 20 điểm; PHC-013 Customer/Care/App = 15 | Khóa weight trong SCORE-xxx; Sales Kit chỉ quote band chính thức |
| **Thiên vị nếu App nặng** | PHC-012 đúng khi giữ digital ≤15% | Không tăng trọng số App vì “world-class product” |
| **Sales “nịnh” vs Diagnostic** | Landing mạnh CTA PDF dễ thành lead form | Giữ unlock sau preview + báo cáo vẫn hữu ích nếu không mua |
| **Pilot scope** | 30 ngày “đủ gói” dễ quá tải | Tuần 4 chỉ **1–2 use case phát triển** (đã đúng trong trao đổi — biến thành rule cứng) |
| **CRM tool** | Pipeline giấy/Excel sẽ vỡ sau ~20 lead | V1: bảng đơn giản (Notion/Sheet chuẩn) → V1.1 entity `phc_lead` nếu cần |

### Alignment giá

Giữ nguyên định vị thương mại đã nêu:

| Gói | Giá | Nội dung tối thiểu (Sales speak) |
|-----|-----|----------------------------------|
| Vận hành | 299.000đ / cơ sở / tháng | POS, kho, lô/HSD, FEFO, KH, báo cáo, cloud |
| Phát triển | +299.000đ | CRM/care, loyalty, App, refill, O2O, Growth Desk |
| Pilot 30 ngày | 299.000đ (Ops) hoặc 598.000đ (Ops+Dev) | Phí Pilot **cộng vào tháng 1** nếu tiếp tục |

Không mở contest giá với POS rẻ — objection handling tách **price vs value** như PHẦN XVII.

---

## 3. Funnel mục tiêu (khóa operational)

```
50 NT mục tiêu → 30 tiếp cận → 15 Health Check → 10 Demo → 5 Pilot → 2–3 Paid
```

Pipeline trạng thái (một trạng thái / NT):

`Lead → Contacted → Interested → HealthCheck → Assessed → Demo → PilotProposed → Pilot → Paid | Lost(+reason)`

Lost reasons bắt buộc (enum): nhu cầu / hài lòng PM hiện tại / giá / sợ chuyển đổi / chưa thấy khác biệt / thiếu thời gian / thiếu nhân sự / chưa đúng thời điểm / thiếu tính năng / khác.

---

## 4. Nội dung cần xây — map sang 9 Sales Kit + PHC pack

### Pack A — Sales Kit (làm ngay, không chờ code Rulebook mới)

| Mã | Tài liệu | Nguồn trong trao đổi | Deliverable |
|----|----------|----------------------|-------------|
| NVX-SALES-001 | Bộ 20 câu (hoặc map 20↔31) | PHẦN III | Markdown + sheet scoring |
| NVX-SALES-002 | Scoring & band | PHẦN IV + PHC-013 §4 | Một bảng band chuẩn |
| NVX-SALES-003 | Report template khách | PHẦN VI | DOCX/MD + field map PDF hiện có |
| NVX-SALES-004 | Call script | PHẦN VII | 30s open + FAQ bán hàng |
| NVX-SALES-005 | Zalo script | PHẦN VIII | 3 tin + stop rule |
| NVX-SALES-006 | Demo playbook | PHẦN X | Pain → màn hình Novixa |
| NVX-SALES-007 | Pilot 30-day | PHẦN XI–XIV | Scope, giá, cam kết 2 chiều, timeline |
| NVX-SALES-008 | Day-30 review → Paid | PHẦN XV–XVI | Chỉ số + script chốt |
| NVX-SALES-009 | CRM & KPI tuần | PHẦN XVIII–XX | Sheet + enum Lost |

### Pack B — Diagnostic Engine (đóng băng trước khi sửa lớn production)

| Mã | Nội dung | Ghi chú |
|----|----------|---------|
| NVX-PHC-012 | Validation & anti-bias | Đã có trong trao đổi — **chốt thành doc repo** |
| NVX-PHC-013 | Master Rulebook V1.0 | 7 tầng; CROSS/OPP/FIT/PILOT/DEP/CONF |
| NVX-PHC-014 | Data model | Chỉ khi cần persist PHC sales/sales CRM riêng; assessment hiện tại đã có submission/score/insight |

**Không** viết thêm PHC-001…011 trong code trước khi Pack A chạy được với lead thật.

### Pack C — Product cut (tuỳ chọn sau 10–15 HC thật)

- Template `PHARMACY_HC_SALES_V1` 20 câu **hoặc** chế độ “Sales interview 20” offline trên cùng Rulebook.  
- Landing: CTA rõ “20–30 phút đánh giá” — thống nhất số câu.  
- Evidence confidence trên report (Medium/High) theo PHC-012.

---

## 5. Mapping 20 câu Sales ↔ năng lực Novixa (cho Demo)

Dùng khi chốt demo (không phụ thuộc số câu engine):

| Nhóm Sales | Pain điển hình | Demo Novixa |
|------------|----------------|-------------|
| Vận hành | Không thấy từ xa / nhân viên lệch quy trình | Dashboard / multi-branch / POS thống nhất |
| Kho | Cận date / không FEFO / chậm bán | Tồn lô–HSD / cảnh báo / FEFO |
| Khách hàng | Có hóa đơn nhưng không biết khách | Customer profile / lịch sử |
| Chăm sóc & phát triển | Chờ khách tự quay lại | Growth Desk / Smart refill / care list |
| Kênh số & App | Zalo rời rạc, không hành trình | Customer App / O2O / đặt thuốc |

Cross-rule ưu tiên nói chuyện với chủ (từ PHC-013):  
`CUST-01` dữ liệu nhưng chưa khai thác · `CARE-01` sau bán yếu · `INV-01/02` có lô nhưng thiếu cảnh báo/FEFO · **không** đề xuất App Pilot nếu thiếu Customer+Care foundation (`DEP` / Foundation Gate).

---

## 6. Lộ trình triển khai đề xuất

### Wave 0 — Freeze (0.5 ngày)

- [ ] Commit quyết định A/B/C (khuyến nghị C).  
- [ ] Đóng bản này + PHC-012/013 vào `docs/novixa/04-gtm/` + `02-product/`.  
- [ ] Cấm “Pilot miễn phí vô hạn” trong mọi script.

### Wave 1 — Sales Kit operable (3–5 ngày)

- [ ] Viết đủ NVX-SALES-001…009 (markdown trước, PDF sau).  
- [x] Sheet CRM 50 NT Thái Nguyên (ICP nhóm A) — [009](./nvx-sales-009-phc-crm-pipeline-v1.md) + CSV template.  
- [ ] Checklist buổi HC 25 phút (5+15+5).  
- [ ] Map pain → màn hình demo (SALES-006) trên tenant DEMO / Xuân Hòa.  
- [ ] Align landing copy (số phút/câu) — không redesign lớn.

### Wave 2 — Field validation (2–3 tuần)

- [ ] Chạy ≥15 Health Check (có thể dùng survey online **hoặc** 20 câu offline).  
- [ ] Ghi Lost reason 100%.  
- [ ] Soát Rulebook: chỗ nào báo cáo “sai cảm giác” → sửa **rule**, không sửa câu trả lời.  
- [ ] Mục tiêu cứng: ≥5 Demo · ≥2 Pilot đề xuất.

### Wave 3 — Engine alignment (song song sau Wave 1)

- [ ] Export Rulebook CROSS/OPP/FIT thành bảng MD (PHC-013) + đối chiếu `AssessmentRuleEngine` / seed DB.  
- [ ] Thêm CONFIDENCE + Foundation Gate App vào report language.  
- [ ] Chỉ khi Wave 2 chứng minh 20Q đủ: seed template 20Q mới — **không** phá lịch sử `PHARMACY_V1`.

### Wave 4 — Pilot commercial close

- [ ] Hợp đồng/Điều khoản Pilot 30 ngày (299 / 598).  
- [ ] Day-30 review template + chuyển Paid script.  
- [ ] KPI tuần: Contactor→HC→Demo→Pilot→Paid.

---

## 7. Tiêu chí thành công (8 tiêu chí PHC + 4 tiêu chí GTM)

**PHC (diagnostic):** dễ trả lời · dễ hiểu · trung lập · công bằng quy mô nhỏ · same input→same output · opportunity truy ngược được · mỗi Top có recommendation · chuyển được sang Pilot.

**GTM (commercial):**  
1. Có ≥1 Pilot trả phí từ thị trường độc lập ngoài Xuân Hòa.  
2. Conversion Demo→Pilot ≥40% trên sample ≥5.  
3. Lost reasons đủ để quyết định 1 ưu tiên roadmap trong 30 ngày.  
4. Không một sales call nào bắt đầu bằng “xin dùng thử phần mềm”.

---

## 8. Việc *không* làm trong phase này

- Web storefront P1/P2 mới (đã tạm dừng).  
- Mở Marketplace / Clinic / Academy GTM.  
- Để AI tự chấm điểm / tự bịa insight.  
- Tăng trọng số App để “bán App”.  
- Pilot App trước khi có Customer Data + Care process tối thiểu.

---

## 9. Việc làm tiếp theo ngay (nếu approve)

1. Tạo thư mục `docs/novixa/04-gtm/pharmacy-health-check/` với skeleton 9 file SALES + copy PHC-012/013 từ trao đổi (chỉnh nhất quán số câu/band).  
2. Điền SALES-004/005/007 trước (dùng được trên điện thoại ngày mai).  
3. Sheet CRM 50 NT + enum pipeline.  
4. Session sau: đối chiếu Rulebook với `AssessmentRuleEngine` seed thực tế.

---

*Tài liệu phân tích triển khai — không thay code production cho đến khi Wave 0 được chốt.*
