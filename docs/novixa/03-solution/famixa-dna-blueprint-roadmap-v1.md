# Famixa DNA · Mục tiêu · Lộ trình đầu · Phân quyền 4 gói

**Mã:** KIT-PRD-FO-DNA-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-07-29 · **Trạng thái:** Constitution + Wave A shipped (schema + DNA card) · align code packaging  
**Phụ thuộc:** [adaptive-family-engine-v1.md](./adaptive-family-engine-v1.md) · [family-commercial-packaging-v1.md](./family-commercial-packaging-v1.md) · `FamilyPlanCapabilityMatrix`

> **SoT capability:** code trong `FamilyCapabilityContracts.cs`. Doc này là Hiến pháp sản phẩm + lộ trình; nếu lệch matrix, ưu tiên code rồi cập nhật doc.

---

## 1. DNA của Famixa

### 1.1 Câu gốc

> **Every Family is Unique. Every Family Can Grow.**  
> Không có một công thức nuôi dạy đúng cho mọi nhà.

> **Quan tâm không phải nuông chiều.**  
> Quan tâm là hiện diện + tiêu chuẩn vừa sức + bằng chứng nhỏ — đúng nhà bạn.  
> (Growth Balance: tránh tự ti · thiếu phấn đấu · dễ hư — xem [`famixa-growth-balance-v1.md`](./famixa-growth-balance-v1.md))

AI **không** nói: *“Gia đình nên làm thế này.”*  
AI nói: *“Với nhà bạn, trong hoàn cảnh hiện tại, đây có thể là bước tiếp theo phù hợp.”*

### 1.2 Ba lớp kiến trúc (bản sắc)

| Lớp | Vai trò | Không phải |
|-----|---------|------------|
| **Family Growth Blueprint™** | Hiểu gia đình (8 lớp, sparse OK) | Form Settings 8 tầng |
| **Growth Engine** | Đề xuất bước nhỏ, gắn Blueprint | Tự sửa ngầm |
| **Fami** | Giọng đồng hành / Brief / Coach copy | Chatbot LLM tự do |

**Vận hành hàng ngày (AFE):** Con đề xuất · AI tóm tắt · Bố mẹ 👍/👎 · Mode 1 chạm · Wallet thỏa thuận.

### 1.3 Nguyên tắc bất biến (Hiến pháp)

1. **Cá nhân hóa theo nhà** — không công thức chung.  
2. **AI chuẩn bị ~95% · phụ huynh quyết ~5%** — mỗi quyết định 1–2 chạm.  
3. **Không tự mutate** Routine / Wallet / Mode — luôn Inbox.  
4. **Growth Zone ≠ bảng điểm xếp hạng** — chỉ “đang mạnh / cần chăm”.  
5. **Không đo máy / MDM** — Screen Time = thỏa thuận trong nhà.  
6. **Setup ≤ 5 phút · dùng ≤ 1 phút/ngày** trên điện thoại.  
7. **Blueprint sparse** — thiếu data thì hỏi 1 câu, không mở Settings.  
8. **Giá trị theo outcome phụ huynh** — không theo số task.

### 1.4 Family DNA (snapshot UI)

Sau onboarding / hydrate, app hiện **DNA card** (4 dòng), không phải dashboard cấu hình:

```text
Stage     · Teen Family / Primary School / …
Values    · Respect · Learning · Responsibility
Focus     · Confidence · Self-discipline
Next step · “Hôm nay thử hỏi con học được gì — thay vì điểm.”
```

Đây là **Digital Twin nhẹ** của nhà — mọi Coach / Proposal / Brief đọc cùng nguồn.

---

## 2. Mục tiêu đạt được

### 2.1 Mục tiêu sản phẩm (12–18 tháng)

| Mục tiêu | Dấu hiệu đạt |
|----------|--------------|
| Famixa = AI Family Growth Companion | Phụ huynh cảm giác “AI hiểu nhà mình”, không phải app checklist |
| Blueprint là nền | Mọi đề xuất có `because` từ values / stage / resources |
| Peace trước Features | Giảm nhắc, tăng tự giác con; ROP / Letter có chứng cứ |
| Mobile-first | Không cần desktop Admin cho việc hàng ngày |
| 4 gói rõ khác biệt | Free thử nhịp · Plus theo dõi · Pro đồng hành · AI+ sâu |

### 2.2 Mục tiêu đo được (KPI)

| KPI | Mục tiêu sớm |
|-----|----------------|
| Onboarding hoàn tất | ≤ 5 phút (p50) |
| Quyết định Inbox / ngày | ≤ 5 lần; 1–2 chạm / lần |
| Tỷ lệ proposal được 👍 | ≥ 60% sau tuần 2 |
| Trial → Pro (30 ngày) | Theo dõi conversion; trial = quyền Pro |
| “Nhẹ tay hơn” | Nudge giảm so với baseline tuần 1 (narrative + số) |

### 2.3 Non-goals (giai đoạn đầu)

- Free-form LLM chat / gọi điện AI  
- MDM / VPN / chặn URL / đo Screen Time thiết bị  
- Bảng điểm so sánh nhà khác  
- Marketplace / hoa hồng  
- Form Blueprint đầy đủ 8 lớp lúc setup  

---

## 3. Lộ trình tối ưu giai đoạn đầu

Ưu tiên: **cảm giác DNA thật** + **Inbox/AFE ổn** + **Pro khác Free rõ** — trước khi đào sâu L3–L5.

### Sóng A — Nền & tin tưởng (0–6 tuần)

| Hạng mục | Việc | Outcome |
|----------|------|---------|
| A1 | DNA card từ onboarding (L1 tối thiểu + L6 values 3–5 + L8 1–2 goals) | “AI hiểu nhà mình” |
| A2 | Siết AFE: Inbox, day_mission, Mode, duyệt sao, WhoAreYou sạch chrome giả | App dùng được mỗi ngày |
| A3 | Capability gate đúng matrix (teaser Free/Plus, không silent fail) | Thấy khác biệt gói |
| A4 | Schema `family_blueprint` sparse JSON + GET/PUT | SoT kỹ thuật Blueprint |

**Không làm:** Growth Zone đầy đủ, Stage Timeline, AI+ deep playbook mở rộng.

### Sóng B — Đồng hành Pro (6–14 tuần)

| Hạng mục | Việc | Outcome |
|----------|------|---------|
| B1 | Coach / Brief / Proposal copy đọc Blueprint (values + goals) | Mỗi lời khuyên “vì nhà bạn…” |
| B2 | Screen Wallet + negotiate (Pro) | Thỏa thuận phút, không đo máy |
| B3 | Growth Report / ROP + Monthly Letter + Replay (Pro) | Chứng cứ 30 ngày |
| B4 | Growth Zone v1 — 4–6 trục narrative (không xếp hạng) | Thay dashboard điểm |

### Sóng C — Anticipation & AI+ (14–24 tuần)

| Hạng mục | Việc | Outcome |
|----------|------|---------|
| C1 | L2 Stage + Timeline (chuẩn bị lớp 1 / dậy thì / thi…) | Đề xuất trước 30–90 ngày |
| C2 | L3/L4/L5 inferred + 1–2 câu xác nhận | Twin nhẹ, không form dài |
| C3 | AI+ Deep Playbook / adaptive scan mở rộng | Gói cao khác Pro rõ |
| C4 | Blueprint ↔ famixa.vn story (marketing) | Web kể đúng DNA, app giữ DNA |

### Nguyên tắc tối ưu đầu

1. **Một nguồn sự thật:** Blueprint + capabilities — không nhân đôi KPI local.  
2. **Trial = Pro** — để phụ huynh thấy Peace trước khi trả tiền.  
3. **Hero SKU = Pro (199k)** — Plus là cầu thang, AI+ là upsell sâu.  
4. **Mỗi sóng ship được cảm nhận trên điện thoại** trong ≤ 1 phút dùng/ngày.

---

## 4. Phân quyền theo 4 gói sản phẩm

### 4.1 Tầng thương mại

| Gói | Tier | Plan codes (chính) | Giá (tham chiếu) | Max trẻ | Tên outcome |
|-----|------|--------------------|-----------------|---------|-------------|
| **Trải nghiệm** | Free | `free` / hết trial | 0đ | 1 | Trải nghiệm Famixa |
| **Growth** | Plus | `plus_month` / `plus_year` (`starter_month` legacy) | 99k / 990k | 2 | Family Growth Plan |
| **Peace** | Pro | `family_pro_month` / `_year` | 199k / 1.99tr | ∞ | Family Peace Plan |
| **Đồng hành sâu** | AI+ | `family_ai_plus_month` / `_year` | 399k / 3.99tr | ∞ | Đồng hành AI chuyên sâu |

**Trial 30 ngày = quyền Pro** (để sống trải nghiệm Peace trước paywall).

### 4.2 Capability matrix (SoT)

| Capability | Free | Plus | Pro | AI+ | Ý nghĩa với DNA |
|------------|:----:|:----:|:---:|:---:|-----------------|
| `core_routine` | ✓ | ✓ | ✓ | ✓ | Day flow, sao, thành viên cơ bản |
| `weekly_insight` | ✓ | ✓ | ✓ | ✓ | Insight tuần nhẹ |
| `timeline` | | ✓ | ✓ | ✓ | Kỷ niệm / timeline nhà |
| `behavior_twin` | | ✓ | ✓ | ✓ | Tín hiệu hành vi (Twin nhẹ) |
| `ai_suggest` | | ✓ | ✓ | ✓ | Đề xuất thích nghi → Inbox |
| `behavior_coach` | | | ✓ | ✓ | Coach hành vi / nhẹ tay |
| `parenting_coach` | | | ✓ | ✓ | Parent Brief / Coach theo DNA |
| `growth_report` | | | ✓ | ✓ | ROP / Growth Report |
| `screen_negotiate` | | | ✓ | ✓ | Wallet phút thỏa thuận |
| `monthly_letter` | | | ✓ | ✓ | Thư tháng |
| `family_replay` | | | ✓ | ✓ | Replay khoảnh khắc |
| `parent_success_checkin` | | | ✓ | ✓ | Check-in thành công phụ huynh |
| `ai_plus_deep` | | | | ✓ | Deep Playbook + scan sâu |

### 4.3 Blueprint & Growth Zone theo gói (lớp sản phẩm)

| Bề mặt DNA | Free | Plus | Pro | AI+ |
|------------|------|------|-----|-----|
| DNA card (Stage/Values/Focus/Next) | Teaser / bản rút gọn | ✓ đầy đủ hydrate | ✓ + gắn Coach | ✓ + deep refs |
| Onboarding → L1/L6/L8 | ✓ tối thiểu | ✓ | ✓ | ✓ |
| Growth Engine đọc Blueprint | Chỉ insight tuần | + AI suggest | + Coach/Brief/Wallet | + Deep playbook |
| Growth Zone narrative | Khóa / teaser | Teaser 2 trục | ✓ đầy đủ | ✓ + dự báo Stage |
| Stage Timeline (chuẩn bị giai đoạn) | — | Teaser | ✓ | ✓ giàu hơn |
| Child trait L3 | — | 1–2 tín hiệu | ✓ | ✓ inferred sâu |

### 4.4 Khác biệt cảm nhận (1 câu / gói)

| Gói | Phụ huynh cảm giác |
|-----|---------------------|
| **Free** | “Nhà có nhịp ngày — Famixa ghi nhận.” |
| **Plus** | “Nhà đang lớn lên — tôi thấy tín hiệu & đề xuất.” |
| **Pro** | “Famixa đồng hành — bớt nhắc, có chứng cứ & thỏa thuận.” |
| **AI+** | “Có playbook tuần & nhìn trước giai đoạn — như mentor riêng.” |

### 4.5 Paywall / UX

- Free → nhấn mạnh **Pro** (hero), không ép Plus.  
- Plus → upsell **Pro** (Coach / Letter / Wallet).  
- Pro → upsell **AI+** (Deep Playbook / scan mở rộng).  
- Free/Plus: **teaser có copy**, không blank / silent 403.  
- Admin: chặn thêm trẻ theo `maxChildren` trước khi API lỗi.

---

## 5. Ánh xạ Blueprint 8 lớp → khi nào mở

| Lớp | Nội dung | Sóng mở | Gói tối thiểu để “đầy đủ” |
|-----|----------|---------|---------------------------|
| L1 Profile | Cấu trúc nhà | A | Free (cơ bản) |
| L2 Stage | Giai đoạn phát triển | C | Pro (Timeline) |
| L3 Child | Trait / learning / **selfCalibration** | A+ (CAL-01) → C sâu | Free capture / Pro Coach |
| L4 Context | Môi trường / lịch / digital / **school bubble** | A+ (CAL-01) → C | Free capture |
| L5 Style | Parenting style | C | Pro (Coach cân bằng) |
| L6 Values | 3–5 giá trị | A | Free hydrate / Pro Coach |
| L7 Resources | Time / learning / outdoor… | B | Pro (gợi ý theo nguồn lực) |
| L8 Goals | Mục tiêu nhà | A | Free lưu / Pro ưu tiên đề xuất |

---

## 6. Chốt một dòng

**DNA:** Hiểu nhà bạn → đề xuất bước phù hợp → phụ huynh 👍.  
**Mục tiêu:** Peace & tăng trưởng có chứng cứ, không checklist.  
**Đầu:** DNA card + AFE ổn + Pro rõ khác Free.  
**Gói:** Free thử nhịp · Plus theo dõi · Pro đồng hành · AI+ sâu — matrix capability là SoT.

---

## 7. Việc tiếp theo (khi implement)

1. Epic `family-growth-blueprint-v1` — schema + DNA card (Sóng A4/A1).  
   **Done (Wave A):** mig `249_pack_family_blueprint.sql` · `GET/PUT …/blueprint` · `GET …/blueprint/dna` · `POST …/blueprint/hydrate` · DNA card trên Parent Home · hydrate khi lưu onboarding.
2. Gate UI theo `capabilities` đã có trên `GET …/subscription`.  
3. Không merge Blueprint vào `migration-files.prod.txt` (Pharmacy) — chỉ Family OS manifest.
4. **Website / pricing:** đọc [`famixa-package-website-memo-v1.md`](./famixa-package-website-memo-v1.md) trước khi sửa `famixa-site`.
5. **Self-calibration (bubble → cú sốc tự tin):** SoT [`famixa-self-calibration-playbook-v1.md`](./famixa-self-calibration-playbook-v1.md) · code `FamilySelfCalibration` · checklist nâng cấp §7 trong playbook.
6. **Growth Balance (quan tâm có phương pháp · tránh tự ti / thiếu phấn đấu / dễ hư):** SoT [`famixa-growth-balance-v1.md`](./famixa-growth-balance-v1.md) · code `FamilyGrowthBalance`.
7. **Wave B — Blueprint-first (2 tuần):** SoT [`famixa-wave-b-blueprint-first-2w-v1.md`](./famixa-wave-b-blueprint-first-2w-v1.md) · gap matrix [`famixa-blueprint-domain-gap-matrix-v1.md`](./famixa-blueprint-domain-gap-matrix-v1.md) · B1: Coach / Brief / CoachInsight đọc DNA (`blueprint-context.ts`, `FamilyCoachInsightService`).
